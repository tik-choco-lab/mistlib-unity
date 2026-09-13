# Capsule Sync

Two capsules in one MistLib room. The blue capsule is the local node and follows WASD; every orange
capsule is a peer, placed at the position MistLib reports for that node. Each capsule carries its
node ID and coordinates, so the two windows can be compared directly.

## Run it

1. Add `CapsuleSyncExample` to an empty GameObject in any scene. The ground, markers, camera, light,
   and capsules are created at runtime, so nothing else has to be set up.
2. Start a second node by one of the routes in the next section.
3. Keep `Room` identical in both, and leave `Node Id` empty so each process generates its own.

Move the blue capsule in one window and watch the orange capsule follow in the other. The HUD lists
every peer with its last received coordinates, which path delivered them, and how long ago.

## Running two nodes at once

One process can host exactly one node. `mistlib-native` keeps its runtime, its callbacks, and its
room sessions in process-global state, so the second `MistClient.Initialize` in a process throws
`InvalidOperationException`. Two GameObjects in one scene, or two additive scenes, will not work —
each node needs its own process. Any of these gives you that:

| Route | How | Notes |
| --- | --- | --- |
| Multiplayer Play Mode | Install `com.unity.multiplayer.playmode`, open **Window > Multiplayer > Multiplayer Play Mode**, activate Player 2, then press Play | Fastest loop. Each virtual player is a separate Editor process, which is what makes it work here. |
| Editor plus a build | Build a standalone player of the scene, launch it, then press Play in the Editor | No extra package. Also the way to check a real player build. |
| Two builds | Launch the standalone player twice | Closest to what a user runs. |

Launching the same player twice differs by platform:

- **Windows** — run the `.exe` twice. Leave **Player Settings > Resolution and Presentation > Force
  Single Instance** off.
- **Linux** — run the binary twice, for example `./CapsuleSync.x86_64 &` then `./CapsuleSync.x86_64`.
- **macOS** — a second double-click only focuses the running app; use `open -n ./CapsuleSync.app`.

Two different components can share a room the same way, as long as each runs in its own process: the
restriction is one `MistClient` per process, not one behaviour per room.

The 0.6.2 engine cannot unload its native runtime, so repeated Play Mode sessions in one Editor
process are not supported. Restart the Editor (and any virtual players) between runs if a node stops
producing events.

## What the sample shows

Position travels over two paths, but only the high-rate stream drives visible motion:

| Path | Rate | Purpose |
| --- | --- | --- |
| `MistClient.UpdatePosition` → `NodePositionUpdated` event | about 1/s while inside the AOI | The engine's own position for the node. It drives the area of interest and refreshes peer liveness, but never changes the rendered position because it may repeat a sparse, older snapshot. |
| `MistClient.SendMessage` broadcast → `Raw` event | `Send Interval`, 10/s by default | The sample's own sequenced publish, which drives the remote capsule's visible movement. |

The engine payload contains `{"x":…,"y":…,"z":…}`. The sample's broadcast adds a monotonically
increasing `sequence` field. The DataChannel remains unreliable and unordered; the receiver drops
any broadcast whose sequence is not newer than the last one it accepted.

A remote capsule is initially hidden, then appears at its first accepted broadcast position and
interpolates toward later broadcasts. Engine snapshots are never used for visible motion, and a
reordered broadcast cannot pull the capsule backwards. If the HUD says `hidden`, the high-rate
stream has not arrived yet or the peer is outside the AOI.

## Area of interest

The engine only reports nodes within `aoiRange`, 10 units by default. Walk the capsules more than
that apart and the peer leaves the area of interest. The sample immediately hides its capsule but
keeps the object and its last broadcast sequence for reuse. In-flight position messages cannot
make an out-of-range peer reappear. If the peer returns before `Peer Timeout Seconds`, its existing
object appears at the first fresh broadcast position; otherwise the hidden object is destroyed.
To see more of the map at once, raise the range in `Config Json`:

```json
{ "aoiRange": 40.0 }
```

Every other key keeps its engine default. See the package README for what a configuration may set.

## Connecting

The sample runs on the engine's default Nostr signaling, so both processes need outbound HTTPS and
WebSocket access, and a peer can take a few seconds to appear after Play Mode starts. A deployment
that wants its own discovery scope sets `signaling.nostr.relays`, `inviteSalt`, and `inviteCode` in
`Config Json`.
