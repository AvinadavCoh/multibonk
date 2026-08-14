# STATE — Multibonk (co-op multiplayer mod for Megabonk, IL2CPP / MelonLoader)

**Goal:** a clean, from-scratch rewrite of the co-op mod that is drift-resistant
(typed Il2Cpp interop, no string reflection) and testable solo via the test kit.
"Done" = a 2-player run works end-to-end and is validated with evidence.

## Decision (this session)
Rebuilding from scratch by the user's explicit, reaffirmed instruction. The legacy
mod at `Multibonk/` stays in git + on disk as READ-ONLY REFERENCE until the new mod
reaches parity, then it is deleted. New code lives under `src/` and depends on nothing
in `Multibonk/`. Keep what was proven (TCP host-authoritative transport, binary packets,
desync detector, the test kit); fix what hurt (string-reflection patches, feature code
smeared across 3 dirs, hand-wired DI packet lists).

## Target architecture
- Typed Il2Cpp references everywhere — a game update breaks the BUILD, not silently at runtime.
- One self-contained module per game system (players/enemies/pickups/xp/levelup/run-end/...),
  each owning its patches + packets + host&client logic + Reset(), behind a common interface.
- One packet registry (id → serialize → deserialize → handler) — no drift, whole protocol in one place.
- Pure-.NET networking core the test kit can link.

## Now
- Committed & pushed: (1) building skeleton + csproj, (2) networking core `src/Multibonk/Net/`
  (pure .NET, wire-compatible with legacy framing/primitives), (3) PacketRegistry + Net facade
  + MainThread dispatcher, pumped from Mod.OnUpdate.
- Module system (`src/Multibonk/Modules/IModule.cs`, `ModuleHost.cs` composition root) +
  first feature module, Session/Lobby (`src/Multibonk/Modules/Session/`): packet ids
  + IPacket bodies matching legacy byte-for-byte (JoinLobby, LobbyPlayerList, SelectCharacter,
  PlayerSelectedCharacter, StartGame, GameLoaded, SpawnPlayer), a thread-safe `LobbyPlayerRegistry`,
  and host+client handshake logic in `SessionModule`. `Net.cs` gained `OnSessionStarted`/
  `OnSessionEnded` events (guarded against double-firing) that `ModuleHost` subscribes to.
  Dev-trigger keybinds in `Mod.cs`: F6 host :25565, F7 join 127.0.0.1:25565, F8 host-only
  broadcast START_GAME(seed=12345).
- Just finished: **Player-Position-Sync module** (`src/Multibonk/Modules/Players/`:
  `PacketIds.cs`, `Packets.cs`, `PlayerSyncModule.cs`) - the first slice that makes two
  connected players visibly move relative to each other.
  - `IModule` gained a `Tick()` default-no-op method; `ModuleHost.TickAll()` calls it on
    every installed module; `Mod.OnUpdate()` calls `ModuleHost.TickAll()` every frame,
    after `Net.PumpReceive()`/`MainThread.Drain()` (main thread, so Tick() and packet
    handlers may touch Unity/IL2CPP directly).
  - Local player transform read via `Il2CppAssets.Scripts.Actors.Player.MyPlayer.Instance.transform`
    (confirmed via api.txt: `static MyPlayer get_Instance()`; `.transform` is inherited
    from `Component` since `MyPlayer : MonoBehaviour` - same pattern legacy code already
    uses in `GameLoadedEventHandler.cs`). Null `Instance` (not in a run yet) is skipped
    silently, per spec.
  - Remote players are mod-created primitive capsules
    (`UnityEngine.GameObject.CreatePrimitive(PrimitiveType.Capsule)` - confirmed present
    in `UnityEngine.CoreModule.dll` via binary string scan: `CreatePrimitive_Public_Static_GameObject_PrimitiveType_0`),
    one per remote uuid, created lazily on first MOVED/ROTATED, with the auto-added
    `CapsuleCollider` destroyed immediately (visual marker only, must not interact with
    game physics). Destroyed on disconnect (host: `Net.OnClientDisconnected`) and on
    `OnSessionEnd` (all of them).
  - Wire reuses legacy ids/layouts exactly: PLAYER_MOVE_PACKET(3)/PLAYER_ROTATE_PACKET(4)
    client→host (uncompressed floats), PLAYER_MOVED_PACKET(7)/PLAYER_ROTATED_PACKET(8)
    host→clients (MOVED uncompressed floats + uuid; ROTATED byte-compressed
    angle/360*255 + uuid) - cross-checked against both the legacy packet classes AND
    `tools/MultibonkTestKit`'s `Protocol/{ClientPackets,HostSendPackets,
    ClientPacketDecoder,ServerPackets}.cs`, which already sends/decodes these exact
    layouts (`JoinMode.RunIdleMovement` sends PLAYER_MOVE/ROTATE every 4s).
  - Ownership: each machine sends its own transform every frame (throttled: >0.05 units
    moved or >2° rotated, capped ~18Hz); host relays a client's MOVE/ROTATE to every
    OTHER client (`BroadcastExcept`) after updating that client's capsule locally, so
    3+ players converge; host's own movement is `Broadcast`-ed directly, tagged with its
    own uuid (`SessionModule.LocalUuid`, newly exposed - was private before).
    `Connection.PlayerUuid` (previously declared but unused) is now actually stamped by
    `SessionModule.HandleJoinLobby` so this module can map an incoming `Connection` to a
    uuid without depending on the lobby registry.
- Status: [RAN] `dotnet build src/Multibonk/Multibonk.csproj -c Release` → 0 errors, 0 warnings
  (includes the new Players module + IModule/ModuleHost/SessionModule/Mod.cs edits).
  [UNVERIFIED] at runtime — never launched the game with the mod loaded, never run the test kit
  against it, never seen a capsule actually render or move. Wire-format compatibility was
  checked [PROXY] by reading legacy source + the test kit's own send/decode code byte-for-byte,
  not by capturing an actual packet.

## Next (in order)
1. **Runtime-validate the session slice + player-sync slice together**: launch the game with
   the mod, F6 to host, `dotnet run -- join` from `tools/MultibonkTestKit` against it, confirm
   the full handshake log sequence, then confirm the test kit's idle-movement loop (every 4s)
   produces `[PlayerSync] Created remote-player capsule for uuid=...` in the game's log and an
   actual visible capsule that moves; then swap roles (F7 join a `host`-mode test kit instance)
   and confirm the mirror direction (real player moving → test kit logs PLAYER_MOVE_PACKET
   received). THIS IS UNTESTED - do it before adding anything else to this module.
2. Real player spawn (SpawnPlayerPacket/ECharacter) - SessionModule's SPAWN_PLAYER
   characterByte is still a placeholder (0); a later slice may replace/augment the capsule
   with a real character model once that exists, but the capsule is the durable fallback.
3. Port remaining features module-by-module, testing each with the kit as we go.

## Known broken / unverified
- `src/Multibonk/Net/` (NetWriter, NetReader, IPacket, Connection, NetServer, NetClient,
  PacketRegistry, MainThread, Net facade) + `Log.cs` — builds clean — [PROXY]. Never opened
  a real socket / sent a real packet yet — [UNVERIFIED] at runtime.
- `src/Multibonk/Modules/` (IModule, ModuleHost, Session/*, Players/*) — builds clean —
  [PROXY]. Wire layouts were hand-matched against legacy + the test kit by reading source,
  not by capturing bytes — [UNVERIFIED] at runtime. SessionModule's SPAWN_PLAYER
  characterByte is always 0 (placeholder - no ECharacter/player-module lookup exists yet).
  PlayerSyncModule has NEVER been run against the real game - the MyPlayer.Instance/
  transform read path, the CreatePrimitive(Capsule) call, and the throttle/threshold
  values are all [UNVERIFIED] at runtime, confirmed only against the api.txt dump and a
  binary string scan of UnityEngine.CoreModule.dll (method existence, not behavior).
- Legacy mod: builds (0 errors) but was never validated in a 2-player game — [PROXY]. Reference only.

## Environment
- Build legacy (reference): `dotnet build Multibonk.sln -c Release --nologo -v q`
- Build new: (added once scaffolded) `dotnet build src/Multibonk/Multibonk.csproj -c Release`
- Game refs: `D:\SteamLibrary\steamapps\common\Megabonk\MelonLoader\{Il2CppAssemblies,net6}`
- Full API dump for interop lookups: scratchpad `api.txt` (regen via scratchpad/dumper).
- Test kit: `tools/MultibonkTestKit` — `dotnet run -- host|join`. Standalone, not in the sln.

## Key files
- `Multibonk/` — LEGACY mod, reference only, do not extend.
- `tools/MultibonkTestKit/` — solo test rig (fake host / fake client). Keep.
- `TODO.md` — co-op scope, decided design (shared XP; level-up = pause-everyone), gap roadmap.
- `src/` — the new clean mod (being built).
