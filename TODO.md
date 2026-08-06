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
| Enemy death (host side) | `EnemySyncPatches.cs` | `Kill()` became `Kill(string)` plus an `EnemyDied` overload, which silently killed the old by-name lookup. Now overload-aware. |
| Enemy death (client side) | `EnemyDeathPacketHandler.cs`, `EnemyIdMapper.cs` | `TryGetEnemy` resolves the host enemy ID to the client's `Enemy` instance; `Kill("network")` runs the full death path (animation, dissolve). `ItemDropPatches` blocks client loot spawns so pickups don't duplicate. `SyncTelemetry.RecordApplied(EnemyDeath)` is called on success so the desync detector tracks it. |
| Enemy health (client side) | `EnemyHealthUpdatePacketHandler.cs` | Sets `enemy.maxHp` before `enemy.hp` (avoids clamping hp against the old max). Does **not** call `Kill()` even at zero — the host issues a separate death packet; double-killing would double-fire `EnemyDied()`. Boss HP bars update automatically because `Il2Cpp.EnemyHpBar.Update()` polls `enemy.hp` every frame — no additional sync needed. |
| Boss detection | `EnemySyncPatches.cs`, `BossSyncPatches.cs` | Spawn flag alone never identified bosses; `EnemyData.isBoss` is checked too, and the raw `EEnemyFlag` rides in `EnemySpawnPacket`. `EEnemyFlag`: None=0, Elite=1, Boss=2, StageBoss=4, Challenge=8, SummonerMiniboss=16, FinalBoss=32. |
| Final-boss spawner sync | `BossSyncPatches.cs` (`BossSpawnerFinalInteractPatch`) | `InteractableBossSpawnerFinal.Interact` is patched alongside the regular `InteractableBossSpawner`. Host broadcasts `TriggerBossSpawnerActivate(position, spawnerType=1)`; clients find the nearest spawner of either type and call `Interact()` through `AllowNetworkInteract`. |
| Enemy cache | `EnemyCachePreloader.cs` | Scrapes `EnemyManager` + `SummonerController` so clients hold every `EnemyData` the host might spawn. Previously the client cached types 4/8/25/28 while the host spawned 3/12/13. |
| Stage timeline | `WaveProgressionPatches.cs` | Megabonk has **no numbered waves**. Progression is `SummonerController` (via `EnemyManager.Instance.summonerController`) ticking a `StageTimeline`. Host broadcasts `StartEvent(eventIndex)` and `StartFinalSwarm()`; clients block local ticks and replay. |
| World pickups | `ItemDropPatches.cs` | All drops go through `PickupManager.SpawnPickup` / `DespawnPickup`. Host→client: broadcasts spawn (type/pos/value) + removal. Client→host: when the local player consumes a host-spawned pickup, `DespawnPickupPatch` fires `TriggerClientPickupConsumed(hostId)` → `ItemPickedUpEventHandler` sends `SendClientPickupConsumedPacket` → `PickupConsumedServerPacketHandler` despawns the host copy and relays `ITEM_PICKED_UP` to all clients. |
| Fog of war | `MinimapSyncPatches.cs` | Bidirectional. `FullMap.QueueRevealFog` is patched on both sides with the same ~2-unit throttle. Host→client: `SendMapRevealPacket`. Client→host: `SendClientMapRevealPacket` → `MapRevealServerPacketHandler` applies on host and relays to other clients. Loop suppression via `ApplyingNetworkReveal`. |
| Shrine/chest identity | `InteractableSyncPatches.cs`, `ShrineUseEventHandler.cs`, `ShrineUsePacketHandler.cs`, `ChestOpenPacketHandler.cs` | ID is the world position quantized to whole units (`Math.Round`, formatted as `"x_y_z"`). The old `"shrine_" + Random.Range(0,1000)` placeholder is removed. Clients scan all scene instances of the shrine type (or `InteractableChest`), re-quantize each position, and call `Interact()` on the match via `ApplyingNetworkShrine`/`ApplyingNetworkChest` to suppress re-broadcast. See Known gaps for the rounding-boundary risk. |
| Multiplayer run-end gating | `RunCoordinator.cs`, `PlayerHealthSyncPatches.cs` (`GameOverPatch`), `RunTimeoutPatches.cs`, `LobbyService.cs` | `GameOverPatch` is enabled. `GameManager.OnDied()` is suppressed on all machines until the host confirms every lobby player is dead, then broadcasts `RUN_OVER`. Dead players stay in the game world while others are alive. `RunTimeoutPatches` opens the gate after 15 s if `RUN_OVER` never arrives (host-crash escape hatch; client only). `LobbyService` subscribes to `ServerProtocol.OnClientDisconnected` and calls `TryEndRun` after removing the dropped player, so a client disconnect doesn't soft-lock surviving players. `RunCoordinator.Reset()` / `RunReset` event clears all per-run state on restart. |
| Player death | `PlayerDeathPacketHandler.cs`, `PlayerDiedReportEventHandler.cs`, `PlayerDeathEventHandler.cs` | Host marks itself dead and checks the all-dead condition via `PlayerDeathEventHandler`. Clients send `PLAYER_DIED_PACKET` upstream; `PlayerDiedServerPacketHandler` marks them dead in `LobbyContext` and calls `TryEndRun`. `PlayerDeathPacketHandler` on each client marks the dead player's `LobbyContext` entry (`IsDead = true`) for lobby-state consistency. No remote death visual — see Known gaps. |
| Shared XP/gold | `PlayerXpPatches.cs`, `PlayerGoldPatches.cs` | Bidirectional (RoR2-style). Clients report gains via client-sent packets (XP=5, GOLD=6); host applies and relays. Suppression flags prevent rebroadcast loops. |
| Player health reporting | `PlayerHealthSyncPatches.cs` | Clients report current/max health (packet 7); `LobbyContext` caches it for the Players HUD. |
| Level display | `PlayerLevelUpPacketHandler.cs`, `LobbyContext.cs` (`LobbyPlayer.Level`), `PlayerHealthHUD.cs` | Level-up packets update `LobbyPlayer.Level`; the HUD renders `"Name  Lv.N"` when level > 1. |
| Clock sync | `TimeSyncPatches.cs` | `MyTime.stageTimer` / `runTimer` broadcast every 2s, applied client-side only past 0.3s drift, never over a client's own pause. |
| Pause sync | `TimeSyncPatches.cs` | Host pause/unpause propagates. A client's own local pause is tracked separately so the network only unpauses what the network paused. |
| Restart cleanup | `RestartPatches.cs` | Clears mod state on scene load/restart — fixes the blank screen after the host died and started a new run. |
| Single-player regression | `MainMenuPatches.cs` | `StartMap` was gated on `!IsHosting` without an `InMultiplayer` check, freezing single-player runs after a lobby closed. |

---

## Known gaps

| Gap | Details |
|---|---|
| Position-key identity (shrine/chest) | Identity is `Math.Round(world_pos)` formatted as `"x_y_z"`. Two interactables within 0.5 units of each other produce the same key (wrong target activated). Positions near a .5 boundary are sensitive to floating-point non-determinism — if map generation produces subtly different floats on host vs client the key mismatches and the activation is silently skipped. Risk is low in practice (shrines and chests are spaced widely), but the first test session should include shrine use to confirm. |
| Boss phase transitions | `Il2Cpp.FinalFightController.StartPhase(int)` / `currentPhase` exist in the API dump. Whether `StartPhase` is triggered by `FixedUpdate` polling `boss.hp` or by an `OnEnemyDamage` event cannot be determined from the API alone. If it polls `boss.hp` (the more likely pattern), phases may already sync correctly because `EnemyHealthUpdatePacketHandler` keeps `hp` aligned on clients. If it is event-driven, phases will diverge silently. Adding a phase sync packet without confirming the trigger risks double-transitions on the client. Left for investigation with a running game. |
| Remote player death visual | `PlayerDeathPacketHandler` sets `IsDead = true` on the lobby seat (lobby bookkeeping) but spawns no corpse or death effect. The API dump does not expose a safe remote-death entry point. |
| GameOverPatch resolution failure | If `Il2Cpp.GameManager` or `GameManager.OnDied` cannot be resolved at startup, `GameOverPatch.Prepare()` returns false and logs `MelonLogger.Error`. Run-end gating is silently disabled — every player death immediately ends the run (single-player behaviour restored). Check the startup log for `[GameOverPatch] ... DISABLED` before trusting multiplayer game-over to work. |

---

## Co-op scope: what a full mod still needs

*From a systematic pass over the v1.0.69 assembly plus web research on the game and the two
other known co-op mods (MegabonkTogether, BonkWithFriends), Aug 2026.*

Everything above makes a **basic** two-player session hold together — same map, shared enemies,
shared XP/gold, synced deaths. But Megabonk was built single-player, and several of its systems
assume exactly one player. Those are not yet handled. This section is the real forward roadmap;
the per-item confidence is marked because metadata proves an API *exists*, not *when* it fires.

### Design decisions needed first (these are yours, not mine to guess)

Several items below can't be built until we decide how co-op should *feel*. Each is a genuine
fork, and the two other mods disagree, so there's no obvious default.

| Decision | Options / what other mods do | Affects |
|---|---|---|
| **Shared vs separate XP & upgrades** | We currently share XP (both level at once). MegabonkTogether defaults to *separate* loot/XP (independent builds) with a shared-mode toggle that doubles XP to compensate. The one substantive community thread wanted *separate* so players specialise. | The whole level-up system below. Pick this before touching upgrades. |
| **Enemy scaling for 2 players** | Base game is tuned for one player; two players trivialise it. MegabonkTogether raises spawn credits + enemy caps; BonkWithFriends exposes HP/spawn-rate config. Currently we do **nothing** — waves will feel too easy. | Spawn counts, enemy HP. A balance decision, needs its own packet/patch once chosen. |
| **Combat during upgrade pick** | Single-player pauses on level-up. In co-op, if only the picking player pauses, the other keeps taking damage and can die mid-menu. Options: pause everyone while any player picks, or grant i-frames and keep playing (MegabonkTogether's approach). | Upgrade screen sync + pause propagation. |
| **Silver (meta-currency) split** | Each machine currently writes full run silver to its own save — both players get 100%. Could be intended (co-op incentive) or should be split. | `ProgressionSaveFile.AddSilver`; low urgency, may be a no-op decision. |
| **Friendly fire** | Community wants it as an optional toggle, not default. | Only if we add player-damage-to-player at all. |

Also, both other mods **disable Steam achievements/leaderboards during netplay** to avoid
invalid unlocks and possible bans. We should do the same before any public release.

### Prioritized gap roadmap

**1. Enemy AI targets only the host — HIGH, verified.**
`MyPlayer.Instance` is a static singleton (confirmed: one per machine, no player registry
exists). Enemy AI runs host-authoritatively and chases that singleton — which on the host is the
host's player. The client's player is effectively invisible to enemy AI: it draws no aggro and
takes no directed damage, while the host takes all of it. This is the most fundamental asymmetry
in the mod and arguably matters more than teleport. Fixing it means feeding client player
positions into the host's enemy targeting, not just routing a packet — the targeting code has to
consider a *list* of players. Non-trivial; likely needs its own investigation pass.

**2. Level-up upgrade screen — HIGH, verified (highest frequency).**
Fires every time the shared XP pool crosses a threshold, i.e. constantly. Today each machine
opens its own `LevelupScreen` and calls `UpgradePicker.ShuffleUpgrades()` against a *diverged*
`UnityEngine.Random` state, so the two players see different offers; the pick itself is never
broadcast; and the non-picking player isn't paused or even notified. Blocked on the "shared vs
separate" decision above. Minimum regardless of that choice: re-seed `ShuffleUpgrades` with a
shared `(seed, level, stage)` key so offers match, and add a "player N is choosing" signal.

**3. Stage transition / teleport coordination — HIGH, verified.** *(what you asked about)*
The flow is understood: boss dies → portal activates → `InteractablePortal.Interact()` runs the
`DoLoadNextStage()` coroutine → `MapController.LoadNextStage()` does a full **scene reload** →
`MyPlayer.TeleportPlayerNextStage()` drops the player at the seed-determined spawn. Map seed for
the new stage is already synced, so layouts match. What's missing is **coordination**: the host
can hit the portal the instant the boss dies and force-transition a client who is mid-fight or
mid-upgrade-screen — a scene reload while the level-up UI is open is undefined behaviour. Needs a
readiness handshake: host announces "transition pending", clients close any open UI and ack,
host waits (reuse the `RunTimeoutPatches` 15s-timeout pattern), then everyone loads together.
Also: replace the reflection in `StageTransitionPacketHandler` with the typed
`Il2Cpp.InteractablePortal` reference already used in `BossSyncPatches`, and handle the
`LevelupScreen.isLevelingUp` case.

**4. Final boss is structurally broken in co-op — HIGH, likely (confirm in test).**
The final fight gates boss vulnerability on charging pylons (`FinalFightController.pylons`,
`BossPylon.chargeProgress`) by player proximity. Each machine only sees its *own* player charging,
so if the two players split up across pylons, neither machine ever sees all pylons charged and
`PylonsDone()` never fires — the boss never becomes vulnerable. Separately,
`FianlBossCinematic.OnStageBossDied` may not fire on clients (the final portal might never appear).
Both need confirming the moment someone reaches the final boss; if confirmed, pylon charge
progress must be broadcast and phase/portal spawn made host-authoritative.

**5. In-run merchant/crafting interactables unsynced — MEDIUM, verified.**
`InteractableShadyGuy.Interact()` (roaming merchant) and `InteractableMicrowave.UseMicrowave()`
(crafting) aren't patched. Shared gold means the *cost* is already deducted for everyone, but the
*item* only lands in the buyer's inventory. `InteractableCage` (needs a key item) has the same
problem. All three extend the same interactable pattern as shrines/chests — the position-key sync
we already built should extend to them directly.

**6. bossCurses / challenge-shrine drift — MEDIUM, likely (confirm in test).**
`GameManager.bossCurses` drives difficulty scaling and relies entirely on cursed-shrine `Interact()`
replay staying consistent; worth adding to `StateDigestPacket` so the desync detector catches
drift. `InteractableShrineChallenge.EnemyDied()` is a local callback that may not fire on clients
when enemies die via our `Kill("network")` path — if so the challenge reward is never granted on
the client. Both are cheap to confirm in the first session.

**7. Meta-progression / silver / achievements — LOW, verified.**
Per-machine today (both players get full silver, achievements credit only the machine that
triggered them). Mostly a documentation/decision item (see the table above), plus disabling
Steam achievements during netplay before release.

---

## Desync detector

A two-player session used to produce no evidence — things either felt wrong or didn't. The
detector turns one session into a readable diff of which subsystem drifted.

**How it works.** Both sides keep a ledger ([SyncTelemetry.cs](Multibonk/Game/Diagnostics/SyncTelemetry.cs)):
the host counts what it **sent** per channel, the client counts what it **applied** to the game
world. Every 5s the host broadcasts a `StateDigestPacket` — stage/run timers, gold, level, live
enemy and pickup counts, plus its sent counters. The client diffs that against its own state and
logs the result.

The client-side counter is only incremented *after* the game call succeeds, never on receipt.
That's the whole point: a channel the host keeps sending on while the client applies nothing
points to a handler that is silently failing.

**Reading the log** (`MultibonkLogs/Multibonk_*.log`, grep for `[SyncCheck]`):

```
[SyncCheck] #12 t=120.4 OK
[SyncCheck] #13 t=125.4 DESYNC (2 issue(s))
[SyncCheck]   EnemyDeath: host sent 61, local applied 0 - handler appears to be a no-op
[SyncCheck]   enemies: host 47 / local ledger 12 / local mappings 12
```

- `handler appears to be a no-op` — the client's applied counter for that channel never
  incremented. `EnemyDeathPacketHandler` is now fully instrumented, so seeing
  `EnemyDeath: applied 0` on the first run does **not** mean the handler is a stub — it means
  the apply path is failing. Check for `[EnemyDeathPacketHandler] No mapped enemy` warnings in
  the same log window; if every death packet produces that warning, `EnemyIdMapper` is not
  registering spawns correctly and the spawn handler is the real problem. If the warnings are
  absent, look for exceptions in the catch block.
- `N never applied` — packets are arriving but the apply path is bailing out (guard, null, catch).
- `applied twice?` — a local action wasn't blocked, so it happens once locally and once from
  the host packet.
- `ledger says N applied but only M are mapped` — spawns counted as applied without producing
  a usable enemy.
- `digest sequence jumped` — the transport itself is losing packets; treat other findings in
  that window with suspicion.
- `XpGain` / `GoldGain` are bidirectional, so a mismatch there is expected and is reported as
  a `note:`, not an issue.

**Caveat:** the detector reports, it never corrects. Having it paper over a failure would
defeat it.

---

## Verification debt

The codebase is feature-complete for a **basic** two-player session (same map, shared
enemies/XP/gold, synced deaths and stage transitions) — but see [Co-op scope](#co-op-scope-what-a-full-mod-still-needs)
for the systems a *full* co-op experience still needs (enemy AI targeting, upgrade screen,
final boss, merchants). Nothing has been tested with two real players. Every feature after the
Nov 2025 session was written against `Assembly-CSharp.dll` metadata, which catches *renames* but
not wrong assumptions about *when* a method is called or what blocking it does.

**The next step is a single two-player session. Read the `[SyncCheck]` log afterwards — that
log, not this list, is the real backlog.**

**What is most likely to break (watch these first):**

1. **Position-key identity** — use a shrine and a chest during the session. If activation
   silently fails (no effect on either screen), the quantized "x_y_z" key mismatched. Check
   the log for `[Client] No ... found at position` warnings from `ShrineUsePacketHandler` or
   `ChestOpenPacketHandler`.

2. **Run-end gating** — let both players die (in sequence, not simultaneously). The game-over
   screen must not appear until the second player is dead. Also test a mid-run disconnect:
   drop one client's connection while the other is alive; the run should continue normally and
   end when the surviving player dies (or also disconnects).

3. **Enemy despawn** — kill an enemy on the host. It should disappear on the client within a
   frame. If it lingers, `TryGetEnemy` is missing the mapping — check that spawn packets are
   arriving and that `EnemyIdMapper.RegisterMapping` is being called with a non-null `Enemy`
   cast. The `[SyncCheck] EnemyDeath: applied 0` line plus `No mapped enemy` warnings together
   confirm a mapping gap.

4. **Boss phases** — reach the final boss. Monitor whether `FinalFightController.currentPhase`
   advances on the client at the same thresholds. If phases are already correct (hp-polling
   trigger) this is a free pass; if they diverge, a phase-sync packet will be needed.

**Standard checks (carry over from before):**
- Both players get the same map — same terrain, same shrine and chest positions.
- Swarm / miniboss alerts fire at the same moment on both screens.
- Final swarm starts simultaneously.
- XP/gold orbs and powerups appear at the same positions for both players.
- Orbs collected by either player disappear on the other's screen.
- Areas explored by either player reveal on the other's map.
- Stage/run timer stays within ~0.3s across players over a full run.
- Host pausing pauses the client; host unpausing resumes it; the client's own upgrade screen
  doesn't get force-unpaused.

**If timeline events double-fire on clients**, check the log for
`[Client] Blocked local timeline event` — the block prefix must run before the packet replay.

**If anything spawns/fires exactly twice**, check nothing reintroduced `Harmony.PatchAll()`.

---

## Suggested order of work

**Phase 0 — validate what exists (do this first).**
1. **Run one two-player session and read the `[SyncCheck]` output.** Pay particular attention
   to the four high-risk areas in Verification debt above. That log is the real backlog for the
   existing features — don't build on top of unvalidated sync.
2. If position-key identity fails for shrines/chests, switch from `Math.Round` to a bucketed
   grid (floor to nearest 0.5) and re-test.
3. While in that session, confirm the cheap unknowns from the roadmap: does the final boss
   become vulnerable? do boss phases track? does a challenge shrine grant its reward on the
   client? These need eyes-on, not code.

**Phase 1 — decisions (see [Co-op scope](#co-op-scope-what-a-full-mod-still-needs)).**
4. Decide shared-vs-separate XP/upgrades, the enemy-scaling model, and the upgrade-pause policy.
   These three gate the biggest remaining feature (the level-up screen) and each other.

**Phase 2 — build the structural gaps, hardest first.**
5. Enemy AI targeting so enemies see the client player (roadmap #1).
6. Level-up upgrade screen per the Phase-1 decision (roadmap #2).
7. Stage-transition readiness handshake — the teleport coordination (roadmap #3).
8. Final boss pylon/phase sync if Phase 0 confirmed it's broken (roadmap #4).

**Phase 3 — fill-ins.**
9. Merchant/microwave/cage interactables (roadmap #5) — reuses the existing position-key sync.
10. `bossCurses` into the desync digest; challenge-shrine reward broadcast if Phase 0 showed it
    missing (roadmap #6).
11. Remote death visual; enemy scaling tuning; achievement/leaderboard suppression for release.
