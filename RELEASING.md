# Releasing mistlib-unity

Use Bash, Node.js/npm, just >= 1.55, Git, curl, unzip, and the native inspection
tools used by `scripts/validate-package.sh`. GitHub CLI is optional for status.
Run `just status` to compare the bundled engine with the latest MistLib release.

1. On `develop`, run `just bump X.Y.Z`. It downloads the official engine binaries,
   updates versions, checksums, provenance, README mentions, and dates the
   `Unreleased` changelog content in UTC. It then runs `just check`. Repeating the
   current version skips the download and preserves its existing changelog section.
   Unrelated uncommitted changes are refused unless you pass `--allow-dirty`.
2. Review the diff and release notes, then commit with
   `guru s -f "chore(release): vX.Y.Z"`. The scripts never create commits.
   Use `guru s -f` because guru's diff scanner cannot handle the native binary diff.
3. Push the reviewed commit to `origin/develop`, then run `just release X.Y.Z`.
   This checks the clean tree, branch, origin/develop, version, changelog, absent
   tag, fast-forward eligibility, and `just check`, then prints the plan only.
4. Run `just release X.Y.Z --push` to repeat those checks, fast-forward `main`,
   create an annotated `vX.Y.Z` tag, and push `main` and the tag atomically.
   It returns to `develop` and prints the Actions URL. A failure before the push
   completes rolls back the local main/tag changes. Once pushed, inspect Actions
   before taking further action; the tag workflow validates and publishes the release.

The Justfile and this guide live outside the UPM package and are not shipped in
its tarball. Users following `#main` receive the release when they update. To pin
a published release, use a tag instead of a branch in `Packages/manifest.json`:

```json
"com.tik-choco.mistlib": "https://github.com/tik-choco-lab/mistlib-unity.git?path=Packages/com.tik-choco.mistlib#vX.Y.Z"
```
