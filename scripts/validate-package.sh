#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export MISTLIB_REPO_ROOT="$repo_root"
cd "$repo_root/Packages/com.tik-choco.mistlib"

version="$(node -p "require('./package.json').version")"
test "$(node -p "require('./package.json').name")" = "com.tik-choco.mistlib"
grep -Fq "public const string Package = \"$version\";" Runtime/API/MistVersion.cs
grep -Fq "public const string Engine = \"$version\";" Runtime/API/MistVersion.cs

sha256sum -c <<'CHECKSUMS'
2798382717869d1c37b175856e456f90d6f432f9c1730e4eff81346c2ca865af  Runtime/Plugins/Linux/x86_64/libmistlib.so
db7033cbc74b2ba3d87afab70a67fc746930406da2ceff475e71803664e9246d  Runtime/Plugins/Windows/x86_64/mistlib.dll
124f8c7964ac1ae6db9aa5c8589372853efd3fbf58b912499e8329911f1251f9  Runtime/Plugins/macOS/ARM64/libmistlib.dylib
CHECKSUMS

required_symbols=(
  get_version
  get_build_info
  init
  init_with_config
  join_room
  leave_room_id
  leave_all_rooms
  register_event_callback_v2
  register_log_callback
  update_position
  update_position_in_room
  send_message
  send_message_in_room
  set_config
  get_config
  get_stats
  storage_add
  storage_add_at
  storage_get
)

linux_symbols="$(nm -D --defined-only Runtime/Plugins/Linux/x86_64/libmistlib.so)"
windows_symbols="$(objdump -p Runtime/Plugins/Windows/x86_64/mistlib.dll)"
macos_strings="$(strings Runtime/Plugins/macOS/ARM64/libmistlib.dylib)"
for symbol in "${required_symbols[@]}"; do
  grep -Eq " T ${symbol}$" <<<"$linux_symbols" || {
    echo "missing Linux native symbol: $symbol" >&2
    exit 1
  }
  grep -Eq "[[:space:]]${symbol}$" <<<"$windows_symbols" || {
    echo "missing Windows native symbol: $symbol" >&2
    exit 1
  }
  # GNU strings merges the very short Mach-O `_init` symbol with an adjacent
  # printable byte. Every longer entry point remains independently verifiable.
  if [ "$symbol" != "init" ]; then
    grep -Eq "^_${symbol}$" <<<"$macos_strings" || {
      echo "missing macOS native symbol: $symbol" >&2
      exit 1
    }
  fi
done

while IFS= read -r asset; do
  test -f "$asset.meta" || {
    echo "missing Unity metadata: $asset.meta" >&2
    exit 1
  }
done < <(find Runtime Tests LICENSES -type f ! -name '*.meta' | sort)

while IFS= read -r directory; do
  test -f "$directory.meta" || {
    echo "missing Unity folder metadata: $directory.meta" >&2
    exit 1
  }
done < <(find Runtime Tests LICENSES -mindepth 1 -type d | sort)

test -f Runtime.meta
test -f Tests.meta
test -f LICENSES.meta

node - <<'NODE'
const fs = require('fs');
const path = require('path');

function visit(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const item = path.join(directory, entry.name);
    if (entry.isDirectory()) visit(item);
    else if (entry.name.endsWith('.asmdef')) JSON.parse(fs.readFileSync(item, 'utf8'));
  }
}

const manifest = JSON.parse(fs.readFileSync('package.json', 'utf8'));
for (const sample of manifest.samples ?? []) {
  if (!fs.existsSync(sample.path)) {
    console.error(`declared sample is missing: ${sample.path}`);
    process.exit(1);
  }
}
visit('Runtime');
visit('Tests');
visit('Samples~');

for (const item of manifest.files) {
  if (!fs.existsSync(item)) throw new Error(`declared package file is missing: ${item}`);
}
for (const item of ['package.json', ...fs.readdirSync('.').filter(name => name.endsWith('.md'))]) {
  if (!fs.existsSync(item + '.meta')) throw new Error(`missing Unity metadata: ${item}.meta`);
}

function checkMarkdown(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.name === '.git' || entry.name === 'node_modules' || entry.name === 'dist') continue;
    const item = path.join(directory, entry.name);
    if (entry.isDirectory()) checkMarkdown(item);
    else if (entry.name.endsWith('.md')) {
      const body = fs.readFileSync(item, 'utf8');
      for (const match of body.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)) {
        const target = match[1].split('#')[0];
        if (!target || /^[a-z]+:/i.test(target)) continue;
        if (!fs.existsSync(path.resolve(path.dirname(item), decodeURIComponent(target)))) {
          throw new Error(`broken local Markdown link in ${item}: ${target}`);
        }
      }
    }
  }
}
// Repository documentation remains outside the UPM package.
checkMarkdown(process.env.MISTLIB_REPO_ROOT);

// Git and disk installs use the directory directly, without npm's files filter.
const allowedRoots = new Set(['package.json', ...manifest.files]);
for (const item of fs.readdirSync('.')) {
  if (!allowedRoots.has(item)) throw new Error(`unexpected file in UPM package: ${item}`);
}
NODE

guid_list="$(grep -Rh '^guid: ' ./*.meta Runtime Tests LICENSES | awk '{print $2}')"
test "$(wc -l <<<"$guid_list")" -eq "$(sort -u <<<"$guid_list" | wc -l)"
grep -Eqv '^[0-9a-f]{32}$' <<<"$guid_list" && {
  echo "invalid Unity GUID" >&2
  exit 1
}

pack_listing="$(npm pack --dry-run --ignore-scripts --json)"
PACK_LISTING="$pack_listing" node - <<'NODE'
const entries = new Set(JSON.parse(process.env.PACK_LISTING)[0].files.map(item => item.path));
for (const name of ['CHANGELOG.md', 'LICENSE.md', 'Third Party Notices.md', 'package.json']) {
  for (const item of [name, name + '.meta']) {
    if (!entries.has(item)) throw new Error(`required file omitted from tarball: ${item}`);
  }
}
for (const item of entries) {
  if (/(^|\/)\.(git|github|env)(\/|\.|$)/.test(item) || item.startsWith('scripts/') ||
      (/^README(?:\.[a-z]+)?\.md(?:\.meta)?$/.test(item))) {
    throw new Error(`development or private file included in tarball: ${item}`);
  }
}
NODE
echo "UPM package $version is structurally valid"
