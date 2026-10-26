#!/usr/bin/env bash

set -euo pipefail

# shellcheck source=scripts/release-common.sh
source "$(dirname "${BASH_SOURCE[0]}")/release-common.sh"

[[ $# -ge 1 && $# -le 2 ]] || die "usage: $0 <version> [--push]"
version="$1"
do_push=0
if [[ $# -eq 2 ]]; then
  [[ "$2" == --push ]] || die "unknown argument: $2"
  do_push=1
fi
require_version "$version"
tag="v$version"

[[ "$(git branch --show-current)" == develop ]] || die "release must run on develop"
[[ -z "$(git status --porcelain)" ]] || die "working tree must be clean; commit with guru s -f first"
[[ "$(package_version)" == "$version" ]] || die "package version must equal $version"
awk -v prefix="## [$version] - " 'index($0, prefix) == 1 { found = 1 } END { exit !found }' \
  "$package_root/CHANGELOG.md" || die "CHANGELOG is missing the $version section"
! git show-ref --verify --quiet "refs/tags/$tag" || die "local tag already exists: $tag"

remote_refs="$(git ls-remote origin refs/heads/develop refs/heads/main "refs/tags/$tag")" \
  || die "could not read origin refs"
remote_develop="$(awk '$2 == "refs/heads/develop" { print $1 }' <<<"$remote_refs")"
remote_main="$(awk '$2 == "refs/heads/main" { print $1 }' <<<"$remote_refs")"
remote_tag="$(awk -v tag="refs/tags/$tag" '$2 == tag { print $1 }' <<<"$remote_refs")"
[[ -z "$remote_tag" ]] || die "origin tag already exists: $tag"
head_before="$(git rev-parse HEAD)"
[[ "$head_before" == "$remote_develop" ]] || die "develop must be in sync with origin/develop; push or reconcile it first"
main_before="$(git rev-parse --verify refs/heads/main)" || die "local main is missing"
[[ -n "$remote_main" ]] || die "origin/main is missing"
git merge-base --is-ancestor "$main_before" "$head_before" || die "main cannot fast-forward to develop"
git merge-base --is-ancestor "$remote_main" "$head_before" || die "origin/main cannot fast-forward to develop; fetch and reconcile it first"

just check
[[ -z "$(git status --porcelain)" ]] || die "checks changed the working tree"
echo "Release plan:"
echo "  Fast-forward main from $main_before to $head_before."
echo "  Create annotated tag $tag on $head_before."
echo "  git push --atomic origin main $tag"
echo "  Return to develop; follow https://github.com/tik-choco-lab/mistlib-unity/actions"
if [[ "$do_push" -eq 0 ]]; then
  echo "Plan only. To publish: just release $version --push"
  exit 0
fi

pushed=0
tag_created=0
main_updated=0
rollback() {
  local rc=$?
  trap - EXIT INT TERM
  if [[ "$pushed" -eq 0 ]]; then
    [[ "$tag_created" -eq 0 ]] || git tag --delete "$tag" || true
    # Leave the release branch before restoring its ref; never reset the tree.
    if git switch --quiet develop; then
      if [[ "$main_updated" -eq 1 ]]; then
        git update-ref refs/heads/main "$main_before" "$head_before" \
          || echo "error: could not restore local main to $main_before" >&2
      fi
    else
      echo "error: could not return to develop; inspect local main before retrying" >&2
    fi
    echo "Release failed before push completed; local release refs rolled back where possible." >&2
  else
    git switch --quiet develop || true
  fi
  exit "$rc"
}
trap rollback EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

git switch --quiet main
git merge --ff-only --quiet develop
main_updated=1
git tag --annotate "$tag" --message "Release $tag" "$head_before"
tag_created=1
git push --atomic origin main "$tag"
pushed=1
git switch --quiet develop
trap - EXIT INT TERM
echo "Published $tag. Actions: https://github.com/tik-choco-lab/mistlib-unity/actions"
