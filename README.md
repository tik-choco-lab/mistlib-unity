<div align="center">

# mistlib-unity

**Peer-to-peer networking for shared 3D spaces in Unity.**

English · [日本語](README.ja.md) · [简体中文](README.zh.md)

</div>

Unity Package Manager bindings for [MistLib](https://github.com/tik-choco-lab/mistlib), with a C# API
and bundled MistLib 0.6.2 native libraries. Peers communicate through WebRTC data channels and find
each other through Nostr relays. The engine uses 3D proximity to manage its area of interest and peer
connections.

## Status

This package is **experimental**. The 0.6.2 native ABI has no shutdown or
callback-unregistration function. Use one initialized client per process; repeated Editor Play Mode
sessions and managed domain reloads are unsupported. Restart the Editor between sessions.
`Dispose()` leaves the client's rooms and stops managed event delivery, but the native runtime stays
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
development branch. A version reference such as `#v0.6.2` works only after that tag exists.
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

The bundled binaries are from the MistLib `v0.6.2` GitHub Release. Initialization checks the native
version against the package engine version. The upstream binaries and their SHA-256 digests are
available from the [MistLib v0.6.2 release](https://github.com/tik-choco-lab/mistlib/releases/tag/v0.6.2).

The managed Unity bindings, samples, and documentation are **Apache-2.0**. The MistLib native engine is
**MPL-2.0**; its dependencies have their own licenses. See [LICENSE.md](LICENSE.md),
[LICENSES](Packages/com.tik-choco.mistlib/LICENSES), and [Third Party Notices](Packages/com.tik-choco.mistlib/Third%20Party%20Notices.md).
