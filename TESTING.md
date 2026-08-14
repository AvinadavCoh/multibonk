# Testing the rebuild — foundation slice

This covers the **rebuilt** mod under `src/Multibonk/`, not the legacy `Multibonk/`.

## What is testable right now
The rebuild's foundation only:
- The mod loads under MelonLoader.
- Networking: host / join over TCP, the lobby **handshake**, and the packet registry.
- **Player-position sync**: a remote player shows up as a moving **capsule**.

## What is NOT here yet (don't expect it)
Enemies, XP/gold, pickups, chests/shrines, level-up, run-end, stage transitions — none of
those modules are ported to the rebuild yet. There is **no in-game UI**; you drive it with
keybinds. "Hosting" is a raw socket started by a keybind, separate from the game's own menus.

---

## 0. Deploy
Already automatic: building the project copies `Multibonk.dll` into the game's `Mods` folder,
**replacing** the legacy mod (same dll name — only one loads).

```bash
dotnet build src/Multibonk/Multibonk.csproj -c Release
```
Look for: `[Multibonk] Deployed Multibonk.dll to …\Mods`.

> To go back to the legacy mod, rebuild it: `dotnet build Multibonk.sln -c Release`.

## 1. Does the mod load?
Launch the game. In the MelonLoader console watch for:
```
Multibonk (rebuild) initializing…
Multibonk initialized.
```
plus a printed keybind legend (F6/F7/F8). If you see those, the rebuild loaded. ✅

---

## 2. Networking + handshake — mod HOSTS, test kit JOINS
Proves: transport, host handshake, packet registry — end to end, for real.

1. In game, press **F6**. Console: hosting on port 25565.
2. In a terminal:
   ```bash
   cd tools/MultibonkTestKit
   dotnet run -- join --host 127.0.0.1 --port 25565 --name TestBot
   ```
3. Watch **both** consoles. Expect on the game (host) side, `[Session]` lines:
   `JOIN_LOBBY … assigned uuid=2` → `Sent LOBBY_PLAYER_LIST` → `TestBot (uuid=2) selected character`.
   On the test-kit side: `Assigned player UUID=2`, then it waits for START_GAME.
4. Press **F8** in game → sends START_GAME. Test kit logs `START_GAME … seed=12345` and replies
   `GAME_LOADED`; the game logs `GAME_LOADED_PACKET from TestBot` → `Sent SPAWN_PLAYER_PACKET`.

If the handshake completes on both sides, the networking core + Session module work. ✅
If it stalls, note the **last** `[Session]` line printed — that's where it broke.

---

## 3. Player-position sync — see a remote capsule move
Proves: the local-player read, the move packets, and remote-body creation.

The host must be **in an actual run** for its local player to exist and for a remote body to
render, so:
1. Start a normal run in the game (pick a character, get into the map).
2. Press **F6** to host (this runs alongside the live game).
3. Join with the test kit (as in step 2). Its idle loop auto-sends `PLAYER_MOVE`/`PLAYER_ROTATE`
   every ~4s.
4. In the game console look for `[PlayerSync] Created remote-player capsule for uuid=…`, and
   look **in the world** for a capsule. It appears at the test kit's *synthetic* coordinates
   (may be near world origin, not next to you) and nudges every ~4s.
5. Meanwhile, as **you** move, the test kit console should log `PLAYER_MOVED_PACKET` lines with
   changing positions — that's your player broadcasting outward.

**Weak link to check first:** the capsule is created via `GameObject.CreatePrimitive`, which
was confirmed only by scanning the Unity dll, not at runtime. If the `[PlayerSync] Created …`
log appears but **no capsule** is visible, that call is the suspect — tell me and I'll switch
to an explicit mesh.

---

## 4. Client direction — mod JOINS a test-kit host
Proves the receive/apply side.

1. Terminal: `cd tools/MultibonkTestKit && dotnet run -- host --port 25565 --seed 12345`.
2. In game (in a run), press **F7** to join `127.0.0.1`. Console: `[Session] Connected …` →
   `Sent JOIN_LOBBY` → `LOBBY_PLAYER_LIST received` → `Sent CHARACTER_SELECTION`.
3. In the test-kit console press **g** to send START_GAME; the game logs `START_GAME received`
   → `Sent GAME_LOADED`.
4. The test kit's `host` mode has scenario keys (press `?` for the legend) — but note most of
   them (spawn enemy, drop pickup, etc.) exercise modules that **aren't ported yet**, so the
   game will just log the packet and do nothing. That's expected at this stage. Movement is the
   only apply path wired so far.

---

## Reporting back
For anything that fails, the most useful things to send me:
- The **last** `[Session]` / `[PlayerSync]` line before it stopped.
- Any red `[Multibonk]` error lines in the MelonLoader console.
- Whether the capsule appeared at all (step 3).

That tells me exactly which layer broke, and I fix from there.
