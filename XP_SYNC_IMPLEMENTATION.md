# XP Sync Implementation - Complete Guide

## Overview
XP Sync has been implemented to synchronize experience points and level-up events across all players in a multiplayer session.

## What Was Implemented

### 1. **Core Events** (GameEvents.cs)
- Added `PlayerXpGainedEvent` - Triggered when a player gains XP
- Added trigger methods:
  - `TriggerPlayerLevelUp()` 
  - `TriggerPlayerXpGained(int xpAmount)`

### 2. **Network Packet IDs** (PacketId.cs)
- `PLAYER_XP_GAINED_PACKET = 9` - For broadcasting XP gains
- `PLAYER_LEVEL_UP_PACKET = 10` - For broadcasting level ups

### 3. **Packet Definitions**
Created two new packet types:

**PlayerXpGainedPacket.cs**
- `SendPlayerXpGainedPacket(ushort playerId, int xpAmount)` - Sent by host
- `PlayerXpGainedPacket` - Received by clients
- Payload: Player ID (2 bytes) + XP Amount (4 bytes)

**PlayerLevelUpPacket.cs**
- `SendPlayerLevelUpPacket(ushort playerId, int newLevel)` - Sent by host
- `PlayerLevelUpPacket` - Received by clients
- Payload: Player ID (2 bytes) + New Level (4 bytes)

### 4. **Client Handlers**

**PlayerXpGainedPacketHandler.cs**
- Receives XP gain notifications from server
- Currently logs the event
- Ready to integrate with UI/visual feedback

**PlayerLevelUpPacketHandler.cs**
- Receives level up notifications from server
- Currently logs the event  
- Ready to integrate with UI/visual feedback

### 5. **Network Event Handler**

**PlayerXpEventHandler.cs**
- Listens to local XP/level events
- Broadcasts them to all connected clients (host only)
- Registered in dependency injection system

### 6. **Game Hooks (TODO)**

**PlayerXpPatches.cs** - Template created, needs implementation
- Contains commented-out Harmony patches
- Requires finding actual game methods that handle XP/leveling

## How It Works

### Architecture Flow:

```
Game XP Event 
    ↓
[Harmony Patch] (TODO: Find actual method)
    ↓
GameEvents.TriggerPlayerXpGained(xpAmount)
    ↓
PlayerXpEventHandler (host only)
    ↓
SendPlayerXpGainedPacket → All Clients
    ↓
PlayerXpGainedPacketHandler (each client)
    ↓
Display XP gain notification
```

### Current State:
✅ Network infrastructure complete
✅ Packet definitions created
✅ Handlers registered
✅ Event system integrated
⚠️ **TODO:** Find and patch actual game XP methods

## Next Steps to Complete Implementation

### Required: Find Game XP Methods

You need to use **dnSpy** or **Il2CppDumper** to inspect the game assemblies and find:

1. **XP Gain Method** - Something like:
   - `MyPlayer.AddExperience(int amount)`
   - `PlayerInventory.GainXP(int xp)`
   - `PlayerStats.AddXP(int value)`

2. **Level Up Method** - Something like:
   - `PlayerInventory.LevelUp()`
   - `MyPlayer.OnLevelUp()`
   - `PlayerStats.IncreaseLevel()`

### Steps to Find Methods:

1. Open the game's Il2Cpp assemblies in dnSpy:
   - Navigate to `[Game Folder]/MelonLoader/Il2CppAssemblies/`
   - Look for: `Assembly-CSharp.dll` or `Il2CppAssets.Scripts.dll`

2. Search for these keywords:
   - "xp", "experience", "level", "AddXP", "GainXP", "LevelUp"

3. Once found, update `PlayerXpPatches.cs`:
   ```csharp
   [HarmonyPatch(typeof(ActualClassName), "ActualMethodName")]
   class AddXpPatch
   {
       static void Postfix(int xpAmount) // Match actual parameters
       {
           if (!LobbyPatchFlags.IsHosting) return;
           GameEvents.TriggerPlayerXpGained(xpAmount);
       }
   }
   ```

4. Uncomment the patches in `PlayerXpPatches.cs`

5. Rebuild and test!

## Testing

### Manual Test (After Finding Methods):

1. **Host a game** (Player A)
2. **Connect as client** (Player B)  
3. **Gain XP on host** (kill enemy, open chest, etc.)
4. **Check client console** - Should see:
   ```
   Player 1 gained 50 XP
   ```
5. **Level up on host**
6. **Check client console** - Should see:
   ```
   Player 1 leveled up to level 2!
   ```

## Files Modified/Created

### New Files:
- `Multibonk/Networking/Comms/Base/Packet/PlayerXpGainedPacket.cs`
- `Multibonk/Networking/Comms/Base/Packet/PlayerLevelUpPacket.cs`
- `Multibonk/Networking/Comms/Client/Handlers/PlayerXpGainedPacketHandler.cs`
- `Multibonk/Networking/Comms/Client/Handlers/PlayerLevelUpPacketHandler.cs`
- `Multibonk/Game/Handlers/NetworkNotify/PlayerXpEventHandler.cs`
- `Multibonk/Game/Patches/PlayerXpPatches.cs`

### Modified Files:
- `Multibonk/Game/GameEvents.cs` - Added XP events
- `Multibonk/Networking/Comms/Base/PacketId.cs` - Added packet IDs
- `Multibonk/Multibonk.cs` - Registered new handlers

## Potential Enhancements

### Future Improvements:

1. **UI Notifications**
   - Show floating "+50 XP" text when XP is gained
   - Display "LEVEL UP!" animation when leveling

2. **XP Bar Sync**
   - Sync current XP and XP-to-next-level
   - Update progress bars in lobby window

3. **Player Stats Display**
   - Add Level to player info in lobby (Name - Level X - Character)
   - Show level badges next to player names in-game

4. **Optimization**
   - Batch XP events if multiple gains happen rapidly
   - Compress packet size if needed

## Troubleshooting

### Build Errors:
- ✅ All current build warnings are normal (unused events are placeholders)
- If you see "PacketId not found" - Make sure PacketId.cs changes are saved
- If handlers not registered - Check Multibonk.cs dependency injection

### Runtime Issues:
- If XP not syncing - Check that Harmony patches are applied (once uncommented)
- If "Player X gained XP" not showing - Check MelonLoader console for errors
- If host doesn't broadcast - Ensure `LobbyPatchFlags.IsHosting` is true

## Example Output

When working correctly, you'll see in the MelonLoader console:

```
[Multibonk] Player 1 gained 25 XP
[Multibonk] Broadcasting XP gain: 25
[Multibonk] Player 1 gained 30 XP
[Multibonk] Player 1 leveled up to level 2!
[Multibonk] Broadcasting level up
```

---

**Status:** ✅ Infrastructure Complete | ⚠️ Awaiting Game Method Discovery
