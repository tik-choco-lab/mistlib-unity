# Changelog

## Unreleased

## [0.7.1] - 2026-10-26

- Require exactly MistLib engine 0.7.1; the package version matches the engine version.
- Breaking: renamed `MistEventType.Join` / `Leave` to `PeerConnected` / `PeerDisconnected` (2 / 3), with no
  compatibility aliases. The existing `AreaOfInterest*` names and native values 0–9 are unchanged.
- Added `RoomJoined` (10), `RoomJoinFailed` (11), and `RoomLeft` (12) to the unified application
  event contract. Media codes 100/101 remain reserved for wasm's separate callback API.
- Basic Room reports asynchronous join results and completed leaves. Capsule Sync waits for
  `RoomJoined` before publishing, displays join failures/room exits, and hides peers on room exit
  or `AllConnectionsLost`; room events are handled before filtering empty/self senders.
- Pinned all 13 application event values in contract tests and documented the unified payloads.
- Bundled the official MistLib 0.7.1 release libraries for Linux x86_64, Windows x86_64, and macOS
  ARM64; updated all three SHA-256 checksums and source/license provenance to tag `v0.7.1`.

## [0.7.0] - 2026-10-26

- Bundled MistLib 0.7.0 for Windows x86_64, Linux x86_64, and macOS ARM64; updated binary checksums
  and required exports. Initialization still requires an exact engine version match.
- Added `LeaveAllRooms()` and use native `leave_all_rooms` during disposal. `LeaveRoom(roomId)`
  continues to use `leave_room_id`, the per-room native entry point in 0.7.0.
- Added `MistDeliveryMethod.ReliableUnordered` (code 3), with reliable ordered fallback on peers
  without support, and demonstrated it in Basic Room.
- Added static `MistClient.GetNativeVersion()` and `GetNativeBuildInfoJson()` queries that do not
  require client initialization; initialized clients also expose version and build information.
- The engine now enables the CP50 storage transfer defaults, including reliable unordered bulk
  transfers and adaptive pacing. Configuration JSON continues to pass through to the engine.

- Moved the UPM package into `Packages/com.tik-choco.mistlib`; Git installs now use
  `?path=Packages/com.tik-choco.mistlib` before the revision fragment. Repository READMEs and
  development scripts remain outside the package and no longer carry Unity metadata.

- Added English, Japanese, and Simplified Chinese READMEs with matching installation, lifecycle,
  platform, and configuration privacy guidance.
- Removed resolved configuration JSON from Basic Room startup logs.
- Added current-file and native-string secret scanning to validation and release workflows, pinned
  workflow actions to commits, and disabled checkout credential persistence.
- Validation checks package metadata, tarball contents, local documentation links, and sample
  assembly definitions.

- `MistClientOptions` now runs with every field empty: an empty `ConfigJson` sends `{}` so the engine
  applies its own defaults, and an empty `NodeId` becomes a generated unique ID.
- `SignalingUrl` and `ConfigJson` are now both optional; supplying both is still rejected.
- Added `MistRuntimeHost.Copy Effective Config JSON`, which fills the Inspector field from
  `MistClient.GetConfigJson()` instead of from defaults restated in C#.
- Added the **Capsule Sync** sample: a locally driven capsule plus one capsule per peer, labelled
  with the coordinates each node reports, so position sync is visible rather than only logged.
- Fixed remote-capsule rollback by making the sequenced high-rate broadcast authoritative for
  rendering; AOI-scoped engine position snapshots now refresh liveness without changing position.
- Remote capsules are now hidden and pooled when they leave the AOI. Delayed position messages
  cannot make them reappear, and a returning peer is placed at its first fresh broadcast position
  before rendering resumes.
- `validate-package.sh` now checks that every sample declared in `package.json` exists.

## [0.6.2] - 2026-09-10

- Created the standalone `com.tik-choco.mistlib` UPM package.
- Added Windows x86_64, Linux x86_64, and macOS ARM64 native libraries from MistLib 0.6.2.
- Added native version verification, non-null zero-length FFI inputs, and main-thread callback pumping.
- Added a minimal room sample, package validation, and release packaging workflow.
