#!/usr/bin/env bash

set -euo pipefail

# shellcheck source=scripts/release-common.sh
source "$(dirname "${BASH_SOURCE[0]}")/release-common.sh"

[[ $# -ge 1 && $# -le 2 ]] || die "usage: $0 <version> [--allow-dirty]"
version="$1"
allow_dirty=0
if [[ $# -eq 2 ]]; then
  [[ "$2" == --allow-dirty ]] || die "unknown argument: $2"
  allow_dirty=1
fi
require_version "$version"

if [[ "$allow_dirty" -eq 0 ]]; then
  while IFS= read -r -d '' file; do
    case "$file" in
      "$package_root/package.json"|"$package_root/Runtime/API/MistVersion.cs"|\
      "$package_root/Runtime/Plugins/Linux/x86_64/libmistlib.so"|\
      "$package_root/Runtime/Plugins/Windows/x86_64/mistlib.dll"|\
      "$package_root/Runtime/Plugins/macOS/ARM64/libmistlib.dylib"|\
      "$package_root/CHANGELOG.md"|"$package_root/LICENSE.md"|\
      "$package_root/Third Party Notices.md"|LICENSE.md|README.md|README.ja.md|README.zh.md|\
      "$package_root/Samples~/BasicRoom/README.md"|"$package_root/Samples~/CapsuleSync/README.md"|\
      scripts/validate-package.sh) ;;
      *) die "unrelated uncommitted change: $file (commit it first or pass --allow-dirty)" ;;
    esac
  done < <(git diff --no-renames --name-only -z HEAD; git ls-files --others --exclude-standard -z)
fi

current="$(package_version)"
engine="$(sed -n 's/.*public const string Engine = "\([^"]*\)";.*/\1/p' "$package_root/Runtime/API/MistVersion.cs")"
require_version "$current"
require_version "$engine"
if [[ "$current" != "$version" || "$engine" != "$version" ]]; then
  ./scripts/fetch-engine.sh "$version"
else
  echo "Already at $version; keeping the bundled binaries."
fi

node - "$version" "$current" "$engine" "$package_root" <<'NODE'
const fs = require('fs');
const crypto = require('crypto');
const [version, current, engine, root] = process.argv.slice(2);
const read = file => fs.readFileSync(file, 'utf8');
const write = (file, body) => { if (read(file) !== body) fs.writeFileSync(file, body); };
const manifestPath = `${root}/package.json`;
const manifest = JSON.parse(read(manifestPath));
manifest.version = version;
manifest.description = manifest.description.replace(/MistLib engine [^ .]+(?:\.[^ .]+)*/,
  `MistLib engine ${version}`);
write(manifestPath, JSON.stringify(manifest, null, 2) + '\n');

const constants = `${root}/Runtime/API/MistVersion.cs`;
let declarations = read(constants);
for (const field of ['Package', 'Engine']) {
  const pattern = new RegExp(`(public const string ${field} = ")[^"]+(";)`);
  if (!pattern.test(declarations)) throw new Error(`missing MistVersion.${field}`);
  declarations = declarations.replace(pattern, (_, prefix, suffix) => `${prefix}${version}${suffix}`);
}
write(constants, declarations);

const validator = 'scripts/validate-package.sh';
let checks = read(validator);
for (const binary of ['Linux/x86_64/libmistlib.so', 'Windows/x86_64/mistlib.dll', 'macOS/ARM64/libmistlib.dylib']) {
  const relative = `Runtime/Plugins/${binary}`;
  const digest = crypto.createHash('sha256').update(fs.readFileSync(`${root}/${relative}`)).digest('hex');
  const lines = checks.split('\n');
  const index = lines.findIndex(line => /^[a-f0-9]{64}  /.test(line) && line.endsWith(`  ${relative}`));
  if (index < 0) throw new Error(`missing checksum: ${relative}`);
  lines[index] = `${digest}  ${relative}`;
  checks = lines.join('\n');
}
write(validator, checks);

const documents = ['LICENSE.md', `${root}/LICENSE.md`, `${root}/Third Party Notices.md`,
  'README.md', 'README.ja.md', 'README.zh.md',
  `${root}/Samples~/BasicRoom/README.md`, `${root}/Samples~/CapsuleSync/README.md`];
const escape = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const oldVersions = [...new Set([current, engine])].map(escape).join('|');
const versionMentions = new RegExp(`(?:${oldVersions})(?![0-9])`, 'g');
for (const file of documents) {
  let body = read(file).replace(versionMentions, () => version);
  for (const old of new Set([current, engine])) {
    if (old === version) continue;
    body = body.replaceAll(`events-in-engine-${old.replaceAll('.', '')}`,
      `events-in-engine-${version.replaceAll('.', '').toLowerCase().replace(/[^\w-]/g, '')}`);
  }
  write(file, body);
}

const changelog = `${root}/CHANGELOG.md`;
let body = read(changelog);
if (!/^## Unreleased\s*$/m.test(body)) throw new Error('CHANGELOG is missing ## Unreleased');
if (!body.split('\n').some(line => line.startsWith(`## [${version}] - `))) {
  const date = new Date().toISOString().slice(0, 10);
  body = body.replace(/^## Unreleased[^\S\n]*\n/m, `## Unreleased\n\n## [${version}] - ${date}\n`);
}
write(changelog, body);
NODE

just check
echo "Review the changes, then run: guru s -f \"chore(release): v$version\""
