set shell := ["bash", "-c"]
set positional-arguments

default:
    @just --list

# Validate the package with a disposable npm cache.
validate:
    @set -euo pipefail; cache="$(mktemp -d)"; trap 'rm -rf "$cache"' EXIT; npm_config_cache="$cache" ./scripts/validate-package.sh

# Scan current files and native binary strings for secrets.
scan:
    ./scripts/scan-secrets.sh

# Run all release checks.
check: validate scan

# Show package, engine, tags, and the latest published engine release.
status:
    ./scripts/release-status.sh

# Update the bundled engine and release metadata; accepts --allow-dirty.
bump version *args:
    ./scripts/bump-engine.sh "$@"

# Check the release plan; pass --push to publish main and the tag.
release version *args:
    ./scripts/release.sh "$@"
