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
- Status: [RAN] `dotnet build src/Multibonk/Multibonk.csproj -c Release` → 0 errors.
  [UNVERIFIED] at runtime — no socket opened / packet sent against the test kit or game yet.

## Next (in order)
1. Module lifecycle interface: `IModule { Register(PacketRegistry); Reset(); }` + a composition
   root in Mod.cs that instantiates the module list and installs them.
2. First vertical slice (first game-touching code): Session/Lobby module (host starts NetServer,
   client joins, handshake, player registry) + Player-position sync module (typed [HarmonyPatch]
   on the game's player movement; spawn remote player bodies). **Decide the remote-player
   representation here.** Validate with the test kit (join mode) BEFORE moving on.
3. Port remaining features module-by-module, testing each with the kit as we go.

## Known broken / unverified
- `src/Multibonk/Net/` (NetWriter, NetReader, IPacket, Connection, NetServer, NetClient,
  PacketRegistry, MainThread, Net facade) + `Log.cs` — builds clean — [PROXY]. Never opened
  a real socket / sent a real packet yet — [UNVERIFIED] at runtime. No feature modules or
  concrete packets exist yet, so nothing calls Net.Handlers.Register or NetFacade.StartHost/
  JoinHost outside compile-time.
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
