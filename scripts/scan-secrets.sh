#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_root="$repo_root/Packages/com.tik-choco.mistlib"
temp_root="$(mktemp -d)"
trap 'rm -rf "$temp_root"' EXIT

# Pin both the tool version and the digest of its official Linux x86_64 release.
curl --fail --location --silent --show-error \
  https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_linux_x64.tar.gz \
  --output "$temp_root/gitleaks.tar.gz"
(cd "$temp_root" && sha256sum -c <<'CHECKSUM'
551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb  gitleaks.tar.gz
CHECKSUM
)
tar -xzf "$temp_root/gitleaks.tar.gz" -C "$temp_root" gitleaks

# Directory mode audits the current files. Git history is intentionally excluded.
"$temp_root/gitleaks" dir "$repo_root" --redact=100 --no-banner

# Gitleaks skips binary files, so also audit their printable strings.
mkdir "$temp_root/binary-strings"
while IFS= read -r -d '' binary; do
  strings -a "$binary" > "$temp_root/binary-strings/$(basename "$binary").txt"
done < <(find "$package_root/Runtime/Plugins" -type f \
  \( -name '*.dll' -o -name '*.so' -o -name '*.dylib' \) -print0)
"$temp_root/gitleaks" dir "$temp_root/binary-strings" --redact=100 --no-banner
