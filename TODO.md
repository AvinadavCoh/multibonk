# Multibonk — Status & TODO

**Target game version:** Megabonk v1.0.69
**Last verified build:** clean (`dotnet build Multibonk.sln -c Release`, 0 errors)
**Last in-game 2-player test:** *never* — see [Verification debt](#verification-debt)

> Read [Verification debt](#verification-debt) before trusting any ✅ below. Everything
> added after Nov 2025 was written against `Assembly-CSharp.dll` metadata and compiles,
> but has not been observed running with two real players.

---

## How sync works (orientation)

The mod is host-authoritative. A Harmony patch on the host detects a game event, raises a
`GameEvents` event, a `*EventHandler` turns that into a packet, and a `*PacketHandler` on the
client replays it. The same patch usually *blocks* the local action on clients so it doesn't
happen twice (once from local RNG, once from the host's packet).

```
[host] Harmony patch → GameEvents.TriggerX → XEventHandler → packet
                                                               ↓
[client] XPacketHandler → GameDispatcher.Enqueue → game call (main thread)
                          ↑ local patch returns false so it doesn't double-fire
```

Anything touching Unity objects must go through `GameDispatcher.Enqueue` — packets arrive on
a network thread and Il2Cpp calls from off-thread will crash.

Handlers and patches are registered by DI in [Multibonk.cs](Multibonk/Multibonk.cs). Harmony
patches are applied **by MelonLoader automatically** — do not call `Harmony.PatchAll()`, that
was a real bug (every patch ran twice).

---

## Implemented

### ✅ Working before Nov 2025 (observed in-game at the time)
- Player movement / rotation sync
- Character selection sync
- TCP transport, lobby, Steam invites + Rich Presence auto-join
- Player damage / death events on the wire
- Enemy spawn sync, enemy ID mapping, duplicate prevention
- XP sync (respects "Shared" mode), chest sync, shrine sync

### ✅ Implemented, compiles, **not yet observed in-game**

| Area | Where | Notes |
|---|---|---|
| Map seed | `MapSeedPatches.cs` | `GamePatchFlags.Seed` was sent but never applied — everyone got a different map. Now forced into `MapGenerationController` (testSeed/mapSeed), `RsgController.SetCustomSeed` and `Random.InitState`, varied per stage. |
| Summoner spawns | `EnemySyncPatches.cs` | v1.0.69 split `SpawnEnemy` into a 7-param (position) and 5-param (summonerId) overload. The 5-param path drives most stage spawning; blocked on clients, broadcast by host. |
| Enemy death (host side) | `EnemySyncPatches.cs` | `Kill()` became `Kill(string)` plus an `EnemyDied` overload, which silently killed the old by-name lookup. Now overload-aware. **Client side is still a stub — see below.** |
| Boss detection | `EnemySyncPatches.cs`, `BossSyncPatches.cs` | Spawn flag alone never identified bosses; `EnemyData.isBoss` is checked too, and the raw `EEnemyFlag` rides in `EnemySpawnPacket`. `EEnemyFlag`: None=0, Elite=1, Boss=2, StageBoss=4, Challenge=8, SummonerMiniboss=16, FinalBoss=32. |
| Enemy cache | `EnemyCachePreloader.cs` | Scrapes `EnemyManager` + `SummonerController` so clients hold every `EnemyData` the host might spawn. Previously the client cached types 4/8/25/28 while the host spawned 3/12/13. |
| Stage timeline | `WaveProgressionPatches.cs` | Megabonk has **no numbered waves**. Progression is `SummonerController` (via `EnemyManager.Instance.summonerController`) ticking a `StageTimeline`. Host broadcasts `StartEvent(eventIndex)` and `StartFinalSwarm()`; clients block local ticks and replay. |
| World pickups | `ItemDropPatches.cs` | All drops go through `PickupManager.SpawnPickup(EPickup, Vector3, int value, bool, float)` / `DespawnPickup(Pickup)`. Host broadcasts spawn (type/pos/value) + removal; clients suppress local RNG drops. |
| Fog of war | `MinimapSyncPatches.cs` | Fog is `FullMap.QueueRevealFog(Vector3)`. Host broadcasts positions throttled to ~2 units of movement, packed into TileX/TileY. |
| Shared XP/gold | `PlayerXpPatches.cs`, `PlayerGoldPatches.cs` | Now bidirectional (RoR2-style). Clients report gains via new client-sent packets (XP=5, GOLD=6); host applies and relays. Suppression flags prevent rebroadcast loops. |
| Player health reporting | `PlayerHealthSyncPatches.cs` | Clients report current/max health (packet 7); `LobbyContext` caches it for the Players HUD. |
| Clock sync | `TimeSyncPatches.cs` | `MyTime.stageTimer` / `runTimer` broadcast every 2s, applied client-side only past 0.3s drift, never over a client's own pause. |
| Pause sync | `TimeSyncPatches.cs` | Host pause/unpause propagates. A client's own local pause is tracked separately so the network only unpauses what the network paused. |
| Restart cleanup | `RestartPatches.cs` | Clears mod state on scene load/restart — fixes the blank screen after the host died and started a new run. |
| Single-player regression | `MainMenuPatches.cs` | `StartMap` was gated on `!IsHosting` without an `InMultiplayer` check, freezing single-player runs after a lobby closed. |

---

## Known gaps

### 🔴 Client packet handlers that only log
These receive a valid packet and do nothing to the game world. The README currently
overstates these as working.

| Handler | Missing |
|---|---|
| `EnemyDeathPacketHandler` | Never despawns the enemy. Host deaths broadcast fine and `EnemyIdMapper` has the GameObject mapping needed — the call to remove it was never written. Dead enemies likely linger on clients. |
| `EnemyHealthUpdatePacketHandler` | Never applies health. Enemy HP bars on clients don't reflect host damage. |
| `PlayerDeathPacketHandler` | No death visual, no lobby bookkeeping, no all-players-dead check. |
| `PlayerLevelUpPacketHandler` | Log only. Cosmetic — no notification UI. |

### 🔴 Other known-incomplete
- **`ShrineUseEventHandler` sends a placeholder ID** — `"shrine_" + Random.Range(0,1000)` and
  hardcoded type 0. The client can't identify *which* shrine was used, so shrine sync can only
  be accidentally correct.
- **`GameOverPatch` is disabled** (`Prepare()` returns `false` in `PlayerHealthSyncPatches.cs`).
  Game over still fires when any one player dies instead of when all are dead.
- **No client→host map reveal** — areas the client explores stay fogged for the host. Needs a
  new `ClientSentPacketId`.
- **Client pickup consumption isn't reflected on the host** — visual only; the XP/gold values
  themselves do sync.
- **Boss health bar / phase transitions** — `BossSyncPatches.cs:337`.
- **Boss interactable spawners other than the bush.**

---

## Verification debt

Nothing since the Nov 2025 session has been tested with two real players. The mod compiles
and the class/method names were read out of the v1.0.69 interop assembly, which catches
*renames* but not wrong assumptions about *when* a method is called or what blocking it does.

**Two-player checklist (none of this is confirmed):**
1. Both players get the same map — same terrain, same shrine and chest positions.
2. Swarm / miniboss alerts fire at the same moment on both screens.
3. Final swarm starts simultaneously.
4. XP/gold orbs and powerups appear at the same positions for both players.
5. Orbs collected by the host disappear on the client.
6. Areas explored by the host reveal on the client's map.
7. Stage/run timer stays within ~0.3s across players over a full run.
8. Host pausing pauses the client; host unpausing resumes it; the client's own upgrade screen
   doesn't get force-unpaused.
9. Enemies killed on the host disappear on the client *(expected to FAIL — stub handler)*.

**If timeline events double-fire on clients**, check the log for
`[Client] Blocked local timeline event` — the block prefix must run before the packet replay.

**If anything spawns/fires exactly twice**, check nothing reintroduced `Harmony.PatchAll()`.

---

## Suggested order of work

1. Run the two-player checklist and record what actually breaks. Most items below are guesses
   until this happens.
2. Fill in `EnemyDeathPacketHandler` — highest visible impact, and the mapping already exists.
3. Give shrines a real identity (position-based ID would be enough) so shrine sync is correct.
4. `EnemyHealthUpdatePacketHandler`, then player death handling / game-over gating.
5. Client→host map reveal + pickup consumption packets.
6. Boss health bar and phase sync.
