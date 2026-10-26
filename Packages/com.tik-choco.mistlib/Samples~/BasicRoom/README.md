# Basic Room sample

This sample shows the minimum managed lifecycle. It runs with every field left empty: an empty node ID
becomes a generated unique ID, and an empty config JSON sends `{}`, which keeps every engine default.

1. Import the sample from Package Manager.
2. Add `BasicRoomExample` to an empty GameObject.
3. Enter Play Mode on a supported desktop platform.

This sample requires engine 0.7.1, bundled with package 0.7.1.
The Console logs the loaded engine version and node ID. **Broadcast Hello** sends with
`ReliableUnordered` (code 3): reliable delivery without ordering, falling back to reliable ordered
when a peer lacks support. **Leave Room** leaves only the configured room; **Leave All Rooms** leaves
every room. `Dispose()` also leaves every room when the component is destroyed.

`MistClient.GetNativeVersion()` and `MistClient.GetNativeBuildInfoJson()` can query the loaded engine
before initialization. After initialization, the same information is on `client.NativeVersion` and
`client.NativeBuildInfoJson`. Use `GetConfigJson()` to inspect effective settings without logging them.

Fill in `configJson` only to change a default. Set `signaling.nostr.relays` together with your own
`inviteSalt` and `inviteCode` to give the deployment its own discovery scope — the engine refuses the
built-in placeholder invite as soon as a configuration names public relays. Do not commit real
discovery credentials to a public project.

Room joins complete asynchronously. `RoomJoined` (10) reports readiness, `RoomJoinFailed` (11)
logs its UTF-8 failure or cancellation reason, and `RoomLeft` (12) reports completed cleanup.
Room events have an empty sender; use `RoomId` to identify the room. Peer connectivity is reported
separately by `PeerConnected` (2) and `PeerDisconnected` (3).
