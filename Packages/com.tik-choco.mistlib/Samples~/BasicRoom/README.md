# Basic Room sample

This sample shows the minimum managed lifecycle. It runs with every field left empty: an empty node ID
becomes a generated unique ID, and an empty config JSON sends `{}`, which keeps every engine default.

1. Import the sample from Package Manager.
2. Add `BasicRoomExample` to an empty GameObject.
3. Enter Play Mode on a supported desktop platform.

The Console logs the node ID and the configuration the engine resolved, which lists every key you can
override.

Fill in `configJson` only to change a default. Set `signaling.nostr.relays` together with your own
`inviteSalt` and `inviteCode` to give the deployment its own discovery scope — the engine refuses the
built-in placeholder invite as soon as a configuration names public relays. Do not commit real
discovery credentials to a public project.
