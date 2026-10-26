#!/usr/bin/env bash

set -euo pipefail

# shellcheck source=scripts/release-common.sh
source "$(dirname "${BASH_SOURCE[0]}")/release-common.sh"

version="$(package_version)"
engine="$(sed -n 's/.*public const string Engine = "\([^"]*\)";.*/\1/p' "$package_root/Runtime/API/MistVersion.cs")"
local_tag="$(git -c versionsort.suffix=- tag --list 'v*' --sort=-version:refname | sed -n '1p')"
echo "Package: $version"
echo "Bundled engine: $engine"
echo "Latest local tag: ${local_tag:-none}"

if remote_tags="$(git -c versionsort.suffix=- ls-remote --tags --refs --sort=-version:refname origin 'v*')"; then
  remote_tag="$(awk 'NR == 1 { sub("refs/tags/", "", $2); print $2 }' <<<"$remote_tags")"
  echo "Latest origin tag: ${remote_tag:-none}"
else
  echo "Latest origin tag: unavailable" >&2
  exit 1
fi

latest=""
if command -v gh >/dev/null 2>&1; then
  latest="$(gh release view -R tik-choco-lab/mistlib --json tagName --jq .tagName 2>/dev/null)" || latest=""
fi
if [[ -z "$latest" ]]; then
  latest="$(curl --fail --location --silent --show-error --connect-timeout 15 --max-time 60 \
    https://api.github.com/repos/tik-choco-lab/mistlib/releases/latest \
    | node -e 'let s=""; process.stdin.on("data", d => s += d); process.stdin.on("end", () => { const r=JSON.parse(s); if (!r.tag_name) process.exit(1); console.log(r.tag_name); });')" \
    || die "could not read the latest published MistLib release"
fi
echo "Latest published MistLib release: $latest"
require_version "${latest#v}"
require_version "$engine"

# Compare SemVer precedence, including prereleases and ignoring build metadata.
node - "$engine" "${latest#v}" <<'NODE'
function parts(v) {
  const [core, pre] = v.split('+')[0].split(/-(.*)/s);
  return [core.split('.').map(BigInt), pre?.split('.')];
}
function compare(a, b) {
  const [ac, ap] = parts(a), [bc, bp] = parts(b);
  for (let i = 0; i < 3; i++) if (ac[i] !== bc[i]) return ac[i] < bc[i] ? -1 : 1;
  if (!ap || !bp) return ap ? -1 : bp ? 1 : 0;
  for (let i = 0; i < Math.max(ap.length, bp.length); i++) {
    if (ap[i] === undefined) return -1;
    if (bp[i] === undefined) return 1;
    if (ap[i] === bp[i]) continue;
    const an = /^\d+$/.test(ap[i]), bn = /^\d+$/.test(bp[i]);
    if (an && bn) return BigInt(ap[i]) < BigInt(bp[i]) ? -1 : 1;
    if (an !== bn) return an ? -1 : 1;
    return ap[i] < bp[i] ? -1 : 1;
  }
  return 0;
}
console.log(`Bundled engine behind: ${compare(process.argv[2], process.argv[3]) < 0 ? 'yes' : 'no'}`);
NODE
