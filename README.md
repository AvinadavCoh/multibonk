# Megabonk Multiplayer Mod

![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg) ![Status: Unstable](https://img.shields.io/badge/Status-Unstable-red.svg)

**Status:** Early Stages

This project is an open-source mod that enables multiplayer functionality for **Megabonk**. It is currently in its early development stages, so expect incomplete features and experimental implementations.

## Features

Built against **Megabonk v1.0.69**.

`Untested` below means the feature is implemented and compiles, but has not yet been
confirmed in a real two-player session. See [TODO.md](TODO.md) for the full status.

| Feature | Status | Description |
|---------|--------|-------------|
| Player synchronization | ![OK](https://img.shields.io/badge/OK-green.svg) | Players can see each other in real-time |
| TCP connection | ![OK](https://img.shields.io/badge/OK-green.svg) | Reliable network connection established |
| Steam integration | ![OK](https://img.shields.io/badge/OK-green.svg) | Invite friends via Steam overlay, auto-join through Rich Presence |
| Character selection sync | ![OK](https://img.shields.io/badge/OK-green.svg) | Everyone sees who picked what |
| Enemy spawn sync | ![OK](https://img.shields.io/badge/OK-green.svg) | Enemies spawn for all players |
| Chest sync | ![OK](https://img.shields.io/badge/OK-green.svg) | Chest interactions broadcast to all players |
| Map seed sync | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Host seed forced into map generation so everyone gets the same terrain, shrines and chests |
| Stage timeline sync | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Swarms, miniboss alerts and the final swarm replay from the host's timeline |
| Item drops sync | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Host broadcasts pickup type, position and value; clients suppress local RNG drops |
| Minimap / fog sync | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Host's explored areas reveal on client maps (not yet the other direction) |
| Shared XP & gold | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Bidirectional — client pickups count toward the shared pool (RoR2-style) |
| Clock & pause sync | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Stage/run timers stay aligned; host pause propagates |
| Player damage sync | ![Untested](https://img.shields.io/badge/Untested-yellow.svg) | Damage and health changes reported to the host and shown on the Players HUD |
| Shrine/Shop sync | ![Partial](https://img.shields.io/badge/Partial-orange.svg) | Broadcast works, but shrines are identified by a placeholder random ID |
| Boss synchronization | ![Partial](https://img.shields.io/badge/Partial-orange.svg) | Spawning and boss detection work; health bar and phase transitions do not |
| Enemy death sync | ![Broken](https://img.shields.io/badge/Broken-red.svg) | Host broadcasts deaths, but the client handler never despawns the enemy |
| Enemy health sync | ![Broken](https://img.shields.io/badge/Broken-red.svg) | Packets arrive; client handler does not apply them |
| Player death sync | ![Broken](https://img.shields.io/badge/Broken-red.svg) | Death events sync, but no visuals and game-over still fires on the first death |
| And much more planned! | ![Planned](https://img.shields.io/badge/Planned-orange.svg) | -|

## Getting Started

### Easy Way (Steam Integration) 🎮

1. Install [MelonLoader](https://melonwiki.xyz/#) for Megabonk
2. Download the Multibonk mod (when available) and place it in `Megabonk/Mods/`
3. Launch the game and press **F5** to open the multiplayer menu
4. Click **"💬 Steam Friends Overlay"** to invite friends directly through Steam
5. Friends can accept your invite and auto-join your game!

### Manual Way (IP/Port)

1. Download and install with the steps provided at [Melon Loader Website](https://melonwiki.xyz/#)  
2. Follow the steps on the Melon Loader website to install Melon Loader  
3. Download a mod release (STILL NOT AVAILABLE)  
4. Paste the downloaded release folder into the game's folder  
5. Join the game and host a lobby  
6. Use ngrok for tunneling your IP to your friend, or use RadminVPN. The server is always started at port 25565  
7. Tell your friend to join the game and connect with IP:PORT

## Documentation

- **[TODO.md](TODO.md)** — current status, how sync is structured, known gaps, and what to
  work on next. This is the one that is kept up to date.
- `STEAM_INTEGRATION.md` — Steam invite / Rich Presence flow. Still accurate.
- `ENEMY_SYNC_OPTIMIZATION_GUIDE.md`, `XP_SYNC_IMPLEMENTATION.md`,
  `GOLD_WAVE_SYNC_IMPLEMENTATION.md`, `CHEST_SHRINE_SYNC.md` — written before the v1.0.69
  API changes. Useful as background on *why* things are shaped the way they are, but the
  class and method names in them are partly out of date. Trust `TODO.md` and the source.

## Contributing

Contributions are very welcome and highly needed to help improve the mod!

To contribute:

1. Create a fork of this repository.  
2. Clone your fork locally.  
3. Open the `.sln` project in Visual Studio.  
4. Remove any references that are pointing to other paths.  
5. In Visual Studio, right-click the project dependencies and select **Add Project Reference**.  
6. Add all references from the game folders:  
   - `MelonLoader/Il2CppAssemblies`  
   - `MelonLoader/net6`  
7. Implement your feature or fix.  
8. Push your changes and open a Pull Request.  

Every contribution helps make the multiplayer experience better!

## Contact me

Discord: guijas5308