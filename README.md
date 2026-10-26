<div align="center">

# mistlib-unity

**Peer-to-peer networking for shared 3D spaces in Unity.**

English · [日本語](README.ja.md) · [简体中文](README.zh.md)

</div>

Unity Package Manager bindings for [MistLib](https://github.com/tik-choco-lab/mistlib), with a C# API
and bundled MistLib engine 0.7.1. The package and engine versions both use 0.7.1.
Peers communicate through WebRTC data channels and find each other through Nostr relays. The engine
uses 3D proximity to manage its area of interest and peer connections.

## Status

This package is **experimental**. The 0.7.1 native ABI has no shutdown or
callback-unregistration function. Use one initialized client per process; repeated Editor Play Mode
sessions and managed domain reloads are unsupported. Restart the Editor between sessions.
`Dispose()` leaves all rooms and stops managed event delivery, but the native runtime stays
alive until the process exits.

## Requirements

- Unity 6000.0 or newer.
- Windows x86_64, Linux x86_64, or macOS on Apple silicon.
- Windows: Microsoft Visual C++ runtime providing `VCRUNTIME140.dll`.
- Linux: glibc 2.34 or newer and compatible OpenSSL 3 (`libssl.so.3`, `libcrypto.so.3`).
- macOS: the native library targets macOS 11 or newer; also meet your Unity version's OS requirements.
- Outbound HTTPS/WebSocket access for discovery, and WebRTC connectivity through ICE/STUN or TURN.

Android, iOS, WebGL, Windows ARM64, Intel macOS, and Universal macOS players are unsupported.

## Install

The UPM package lives in `Packages/com.tik-choco.mistlib/`. Repository READMEs and development
scripts are outside that directory. When migrating from the old root layout, update the Git
dependency URL to include the `?path=` parameter shown below.

For a local checkout, open Unity Package Manager, choose **Install package from disk**, and select
`Packages/com.tik-choco.mistlib/package.json`.

Add the `main` branch Git URL through Package Manager or put it in
`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.tik-choco.mistlib": "https://github.com/tik-choco-lab/mistlib-unity.git?path=Packages/com.tik-choco.mistlib#main"
  }
}
```

The fragment after `#` selects a Git branch, tag, or commit. Use `#develop` to install the
development branch. A version reference such as `#v0.7.1` works only after that tag exists.
Branch references follow that branch when you update the package; use a commit hash when you need a
fixed revision.

## Minimal use

Add this component to an empty GameObject. Empty options keep the native engine's defaults, and an
empty `NodeId` generates a unique ID.

```csharp
using TikChoco.MistLib;
using UnityEngine;

public sealed class RoomExample : MonoBehaviour
{
    private MistClient client;

    private void Start()
    {
        client = new MistClient();
        client.EventReceived += item =>
            Debug.Log($"MistLib: {item.Type} from {item.FromNodeId}");
        client.Initialize(new MistClientOptions { InitialRoom = "lobby" });
    }

    private void Update()
    {
        if (client != null && client.IsInitialized)
            client.PumpCallbacks();
    }

    private void OnDestroy() => client?.Dispose();
}
```

Native callbacks are copied into managed queues; `PumpCallbacks()` delivers them on the thread that
called `Initialize`, normally Unity's main thread. `MistRuntimeHost` can own the client and pump
callbacks from `Update` for you.

`client.LeaveRoom("lobby")` leaves one room; `client.LeaveAllRooms()` leaves every room.
`Dispose()` also leaves every room. In the 0.7.1 C ABI, per-room leave still uses `leave_room_id`;
the deprecated native `leave_room()` takes no arguments and leaves all rooms.

`SendMessage` accepts these `MistDeliveryMethod` values:

| Value | Code | Delivery |
| --- | --- | --- |
| `ReliableOrdered` | 0 | Reliable, in order |
| `UnreliableOrdered` | 1 | May drop messages, preserves order |
| `Unreliable` | 2 | May drop or reorder messages |
| `ReliableUnordered` | 3 | Reliable, may reorder; falls back to reliable ordered on peers without support |

`MistClient.GetNativeVersion()` and `MistClient.GetNativeBuildInfoJson()` query the loaded library
without initializing a client. After initialization, use `client.NativeVersion` and
`client.NativeBuildInfoJson` for the version and exact build details. Initialization requires 0.7.1.

## Events in engine 0.7.1

`MistEventType` uses the engine's unified native/wasm application codes. Package 0.7.1 renames
`Join` to `PeerConnected` and `Leave` to `PeerDisconnected`, without compatibility aliases, to
separate peer connectivity from room lifecycle. The existing `AreaOfInterest*` names spell out AOI
and retain their meaning and values.

| Code | `MistEventType` | `FromNodeId` / `Data` |
| --- | --- | --- |
| 0 | `Raw` | Sender / application bytes |
| 1 | `Overlay` | Sender / overlay application bytes (debugging) |
| 2 | `PeerConnected` | Peer / UTF-8 `joined` |
| 3 | `PeerDisconnected` | Peer / UTF-8 `left` |
| 4 | `Neighbors` | Synthetic `rust` / JSON node array |
| 5 | `AreaOfInterestEntered` | Node / UTF-8 `entered` |
| 6 | `AreaOfInterestLeft` | Node / UTF-8 `left` |
| 7 | `NodePositionUpdated` | Node / JSON `{"x":..,"y":..,"z":..}` on every AOI tick, even without movement |
| 8 | `AreaOfInterestNodes` | Synthetic `rust` / JSON AOI node array |
| 9 | `AllConnectionsLost` | Local node / JSON `{"reason":"allConnectionsLost","connectedCount":0}` |
| 10 | `RoomJoined` | Empty / empty bytes |
| 11 | `RoomJoinFailed` | Empty / UTF-8 failure or cancellation reason |
| 12 | `RoomLeft` | Empty / empty bytes |

Every event carries `RoomId` through the native v2 callback. Handle room events before excluding
empty senders. `Initialize` starts the join; `RoomJoined` reports readiness (also on a rejoin to an
active room). A canceled startup reports `RoomJoinFailed`; `RoomLeft` reports an active session's
completed cleanup, once per room when leaving all rooms. `AllConnectionsLost` fires once after a
tick observes a previously connected session become empty, rearms after reconnection, and does not
fire for an initially empty room or explicit leave.

Codes 100/101 remain reserved for the separate wasm media callback (`TrackAdded`/`TrackRemoved`);
this native Unity wrapper has no corresponding media callback and does not add them to `MistEventType`.

## Samples

Import samples from the Package Manager window.

| Sample | What it demonstrates |
| --- | --- |
| [Capsule Sync](Packages/com.tik-choco.mistlib/Samples~/CapsuleSync/README.md) | Move a local capsule with WASD or arrow keys and see peer capsules follow sequenced position broadcasts. Start here. |
| [Basic Room](Packages/com.tik-choco.mistlib/Samples~/BasicRoom/README.md) | Initialize, join a room, pump callbacks, and log events. |

Two nodes require two processes: an Editor and a standalone build, or two standalone players. Use the
same room and discovery settings in both. See the Capsule Sync README for setup instructions and
Multiplayer Play Mode notes. The samples are demonstrations; they do not implement application-level
authentication or validate game actions from peers.

## Configuration and privacy

Set only the fields you want to override. For example:

```csharp
client.Initialize(new MistClientOptions
{
    InitialRoom = "lobby",
    ConfigJson = "{\"aoiRange\":20.0}"
});
```

Omitted keys keep the native defaults. `GetConfigJson()` returns the resolved configuration, including
any configured credentials. Unknown keys are rejected. `SignalingUrl` selects the legacy WebSocket
transport and cannot be combined with a non-empty `ConfigJson`.

0.7.1 enables the engine's CP50 storage transfer defaults, including reliable unordered bulk
transfers and adaptive pacing. Override them through `storageTransfer` in flat configuration JSON
or `storage.transfer` in full configuration JSON; omitted keys keep the engine defaults.

For your own discovery scope, configure `signaling.nostr.relays`, `inviteSalt`, and `inviteCode`.
Peers must use matching invite settings. The engine rejects explicitly configured public relays with
its built-in placeholder invite pair. The defaults connect to external relay-list and STUN services;
they are suitable for trying the samples, and should be reviewed for your deployment. Invite settings
scope discovery and do not replace application-level authentication. WebRTC peers and network
services may learn IP addresses and connection metadata.

Keep real invite codes, TURN credentials, and private keys out of source control. Avoid logging the
resolved configuration. **Copy Effective Config JSON** on `MistRuntimeHost` copies it into a serialized
Inspector field; remove credentials before saving or sharing the scene or prefab. Credentials embedded
in a distributed Unity player can be extracted, even when kept out of this repository.

## Native engine and license

The bundled Linux x86_64, Windows x86_64, and macOS ARM64 libraries are from the official
[MistLib v0.7.1 release](https://github.com/tik-choco-lab/mistlib/releases/tag/v0.7.1).
Initialization requires exactly engine 0.7.1, matching the package version.
`scripts/fetch-engine.sh 0.7.1` fetches these artifacts; `bash scripts/validate-package.sh` checks
the package structure, matching version declarations, native exports, and all three SHA-256 digests.

The managed Unity bindings, samples, and documentation are **Apache-2.0**. The MistLib native engine is
**MPL-2.0**; its dependencies have their own licenses. See [LICENSE.md](LICENSE.md),
[LICENSES](Packages/com.tik-choco.mistlib/LICENSES), and [Third Party Notices](Packages/com.tik-choco.mistlib/Third%20Party%20Notices.md).
