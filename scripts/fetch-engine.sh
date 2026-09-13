#!/usr/bin/env bash

set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <engine-version-without-v-prefix>" >&2
  exit 2
fi

version="$1"
case "$version" in
  [0-9]*.[0-9]*.[0-9]*) ;;
  *) echo "invalid semantic version: $version" >&2; exit 2 ;;
esac

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_root="$repo_root/Packages/com.tik-choco.mistlib"
temp_root="$(mktemp -d)"
trap 'rm -rf "$temp_root"' EXIT

base_url="https://github.com/tik-choco-lab/mistlib/releases/download/v${version}"

fetch() {
  local target="$1"
  local binary="$2"
  local destination="$3"
  local archive="mistlib-native-${target}-v${version}.zip"

  curl --fail --location --silent --show-error "$base_url/$archive" --output "$temp_root/$archive"
  unzip -j -q "$temp_root/$archive" "$binary" -d "$temp_root/$target"
  cp "$temp_root/$target/$binary" "$package_root/$destination"
  sha256sum "$temp_root/$archive" "$package_root/$destination"
}

fetch linux-x86_64 libmistlib.so Runtime/Plugins/Linux/x86_64/libmistlib.so
fetch windows-x86_64 mistlib.dll Runtime/Plugins/Windows/x86_64/mistlib.dll
fetch macos-aarch64 libmistlib.dylib Runtime/Plugins/macOS/ARM64/libmistlib.dylib

echo "Binaries updated. Update package.json, MistVersion.cs, checksums, documentation, and changelog before committing."
