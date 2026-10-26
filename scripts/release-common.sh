#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
package_root="Packages/com.tik-choco.mistlib"

die() { echo "error: $*" >&2; exit 1; }

require_version() {
  node - "$1" <<'NODE' || die "invalid semantic version: $1 (use X.Y.Z without a v prefix)"
const version = process.argv[2];
const number = '(0|[1-9][0-9]*)';
const identifier = '(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)';
const pattern = new RegExp(`^${number}\\.${number}\\.${number}(-${identifier}(\\.${identifier})*)?(\\+[0-9A-Za-z-]+(\\.[0-9A-Za-z-]+)*)?$`);
process.exit(pattern.test(version) && !/\s/.test(version) ? 0 : 1);
NODE
}

package_version() {
  node -p "require('./$package_root/package.json').version"
}
