# MultibonkTestKit

A standalone .NET console tool that impersonates a **second Multibonk player** so the
host-authoritative co-op mod can be tested from **one computer** instead of two.

It performs the real client join/select-character/game-loaded handshake against a
running Multibonk host (the actual modded game, hosting a lobby), so the host sees a
genuine second player in its lobby UI. It then logs and tallies every packet the host
sends, and lets you trigger specific client events (death, level-up-done, XP/gold gain,
hard disconnect) on demand via the keyboard - which is exactly the stuff that's hard to
stage reliably with two humans on two PCs.

It is a plain .NET console app: **no MelonLoader, no Il2Cpp, no UnityEngine, no game
install required.** It only needs the `.NET 8` SDK and a TCP route to the host.

## What it does NOT do

It does not render a game, spawn a real player model, or move around convincingly - it
sends a dummy identity position and a slow synthetic "idle sway" so the host doesn't see
a statue, but there is no real game world behind it. Anything the host does that depends
on the *real* game simulation on the client side (physics, real player position, actual
enemy AI reacting to the fake player) is out of scope by design.

## Requirements

- .NET 8 SDK (or 9+, which can still build a `net8.0` target - checked with
  `dotnet --list-sdks` at build time).
- A Multibonk host already running and hosting a lobby (the real game, with the mod,
  with "Host" clicked) on a reachable `host:port`.

## Running it

From this folder:

```
dotnet run -- join --host 127.0.0.1 --port 25565 --name TestBot --character Warrior
```

Flags:
- `--host` (default `127.0.0.1`)
- `--port` (default `25565`)
- `--name` (default `TestBot`) - the display name that shows up in the host's lobby.
- `--character` (default `Warrior`) - **best effort.** The host only rejects an
  unrecognized character name at the *spawn* step (it does
  `Enum.Parse<ECharacter>(selectedCharacter)` when placing you in the world, wrapped in
  a try/catch, so a bad name won't crash the host - it'll just skip physically spawning
  your fake player and log a spawn exception). We could not read the real
  `ECharacter` enum values from source because that type only exists in the Il2Cpp
  game assembly, not in the mod's plain-C# source tree. Pass `--character` with a name
  that matches a real character in your build of the game if you need the spawn step
  to succeed; the join/lobby/character-selection/game-loaded handshake succeeds either
  way.

## What the handshake does (matches the real client)

1. Connect over TCP.
2. Send `JOIN_LOBBY_PACKET` (version=100, name).
3. Wait for `LOBBY_PLAYER_LIST_PACKET` back; the tool's own player UUID is always the
   **first** entry in that list (this mirrors the real client, which does
   `LobbyContext.SetMyself(packet.Players[0])`).
4. Send `CHARACTER_SELECTION`.
5. Wait (indefinitely, up to 10 minutes) for the host to click **Start Game** in their
   lobby UI - the tool cannot do this for you, it's a host-side manual action.
6. On `START_GAME`, wait 1s (simulating map load) and send `GAME_LOADED_PACKET` with a
   dummy position `(0,0,0)` and identity rotation - this is what makes the host place
   and spawn the fake player.
7. From then on it runs as a live "connected player": it decodes and logs everything
   the host sends, sends a `PLAYER_HEALTH_PACKET` heartbeat every 5s (so the host's
   Players HUD has data), and a low-rate synthetic move/rotate every 4s.

## Interactive keys (while running)

| Key | Sends                       | Tests |
|-----|------------------------------|-------|
| `d` | `PLAYER_DIED_PACKET`         | run-end / all-dead gating |
| `l` | `LEVELUP_DONE_PACKET`        | level-up wait-for-all gate |
| `x` | `PLAYER_XP_GAINED_PACKET`    | shared XP relay |
| `g` | `PLAYER_GOLD_GAINED_PACKET`  | shared gold relay |
| `k` | hard disconnect (TCP RST, no graceful close) | disconnect soft-lock fix |
| `q` | print summary and exit       | - |

Also `Ctrl+C` prints the summary and exits.

If stdin isn't a real interactive console (e.g. output was redirected, or launched from
some IDE run panes), the tool detects that up front, logs a warning, disables the key
handlers, and just stays connected/logging until the host disconnects or the process is
killed - it will not crash.

## Output

- Console: timestamped, aligned `[SEND]` / `[RECV]` / `[INFO]` / `[WARN]` / `[ERROR]`
  lines. Decoded packets show their fields; undecoded/unknown ones show
  `len=<n> hex=<preview>` instead of guessing at a wrong layout.
- File: a full copy of the same log is written to `testkit-<timestamp>.log` in the
  current directory.
- On exit (`q`, `k`+later `q`, Ctrl+C, or the host closing the connection), a **SESSION
  SUMMARY** is printed: a per-packet-type count table for both directions, plus an
  explicit **NEVER RECEIVED** section listing every known `ServerSentPacketId` the host
  never sent this session. That list is the main diagnostic value - e.g. if
  `ENEMY_DEATH_PACKET: never received` shows up after you killed enemies during the
  session, the host never actually broadcast enemy deaths to this client.

## Protocol notes / how this avoids drifting from the mod

- `PacketId.cs`, `OutgoingMessage.cs`, `IncomingMessage.cs`, and `OutgoingPacket.cs` are
  **linked** (not copied) straight from `Multibonk/Networking/Comms/Base/` via
  `<Compile Include="..." Link="..." />` in the `.csproj`. Packet ids and the
  read/write primitives (`WriteInt`/`ReadInt`/`WriteString`/`WriteBool`/...) can
  therefore never drift out of sync with the mod - if the mod changes them, this tool
  is compiled against the new version automatically.
- The wire framing (`Net/WireConnection.cs`) is a byte-exact hand port of
  `Connection.cs` (that file can't be linked directly - it references MelonLoader):
  a 2-byte little-endian `short` length prefix covering only the payload
  (packet-id byte + fields, **not** the 2 header bytes), max total frame size 1024
  bytes (so payload <= 1022 bytes), no checksum/trailer.
- The individual packet bodies (`Protocol/ClientPackets.cs` for what we send,
  `Protocol/ServerPackets.cs` for what we decode) are **hand-written**, per the task's
  explicit instruction not to link anything under
  `Multibonk/Networking/Comms/Base/Packet/` (several of those files depend on
  `Il2Cpp`/`UnityEngine`/`Multibonk.Networking.Lobby`, which don't exist outside the
  modded game process). Field order/types were copied by hand while reading the real
  `Send*Packet` source classes, and are commented with which source file each came from.
