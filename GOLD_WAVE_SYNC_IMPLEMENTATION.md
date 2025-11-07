# Implementation Summary - Gold & Wave Sync

## New DLL Version
**SHA256**: `EBB45E802CB03A7AE510A27623A344AB2E45104DFE93FD6BFE2AA02C77D711C2`

## What Was Implemented

### 1. Gold/Coin Synchronization
**Status**: ⚠️ Infrastructure Complete - Needs dnSpy Investigation

**Files Created**:
- `PlayerGoldGainedPacket.cs` - Packet for broadcasting gold collection
- `PlayerGoldGainedPacketHandler.cs` - Client-side handler to apply gold
- `PlayerGoldPatches.cs` - Server-side patches (commented out, needs method discovery)
- `PlayerGoldEventHandler.cs` - Event broadcaster

**How It Works**:
1. Host collects gold → Patch intercepts collection
2. Host broadcasts gold amount to all clients
3. Clients receive packet and add gold locally (if GoldSharingMode = "Shared")

**TODO - Needs Testing**:
```
1. Open game in dnSpy
2. Search for: "gold", "coin", "AddGold", "AddCoins", "CollectCoin"
3. Find the class that handles gold pickup (likely CoinPickup, CollectibleCoin, or PlayerInventory)
4. Find the method that adds gold (likely AddGold, AddCoins, or OnPickup)
5. Update PlayerGoldPatches.cs with correct class and method names
6. Uncomment the patches
7. Rebuild and test
```

**Expected Behavior**:
- When GoldSharingMode = "Shared": Both players get gold when one picks it up
- When GoldSharingMode = "Individual": Only the player who picks it up gets it
- Logs will show: `[Client] ✓ Applied X gold to local player`

---

### 2. Wave Progression Synchronization
**Status**: ⚠️ Infrastructure Complete - Needs dnSpy Investigation

**Files Created**:
- `WaveStartPacket.cs` - Packet for wave start
- `WaveCompletePacket.cs` - Packet for wave complete
- `WaveStartPacketHandler.cs` - Client-side handler for wave start
- `WaveCompletePacketHandler.cs` - Client-side handler for wave complete
- `WaveProgressionPatches.cs` - Server-side patches (commented out, needs method discovery)
- `WaveStartEventHandler.cs` - Event broadcaster for wave start
- `WaveCompleteEventHandler.cs` - Event broadcaster for wave complete

**How It Works**:
1. Host starts a wave → Patch intercepts wave start
2. Host broadcasts wave number to all clients
3. Clients receive packet and sync their wave state
4. Same process for wave completion

**TODO - Needs Testing**:
```
1. Open game in dnSpy
2. Search for: "wave", "Wave", "WaveManager", "WaveController", "StartWave"
3. Find the wave management class (likely WaveManager, EnemyWaveController, etc.)
4. Find methods: StartWave, CompleteWave, or similar
5. Update WaveProgressionPatches.cs with correct class and method names
6. Uncomment the patches
7. Rebuild and test
```

**Expected Behavior**:
- Both players see the same wave number
- Wave transitions happen simultaneously
- Logs will show: 
  - `[Host] Wave X starting, broadcasting...`
  - `[Client] Wave X started`
  - `[Client] ✓ Synchronized to wave X`

---

## Packet IDs Added
- `PLAYER_GOLD_GAINED = 22`
- `WAVE_START = 23`
- `WAVE_COMPLETE = 24`

## Services Registered
- `PlayerGoldEventHandler` - Broadcasts gold gains
- `PlayerGoldGainedPacketHandler` - Receives gold gains on client
- `WaveStartEventHandler` - Broadcasts wave starts
- `WaveCompleteEventHandler` - Broadcasts wave completions
- `WaveStartPacketHandler` - Receives wave starts on client
- `WaveCompletePacketHandler` - Receives wave completions on client

## Testing Instructions

### For Gold Sync:
1. Set `GoldSharingMode = "Shared"` in mod preferences
2. Host picks up a coin/gold
3. Check both client and host logs for:
   - `[Host] Player collected X gold, broadcasting...`
   - `[Client] Received gold gain packet: X gold`
   - `[Client] ✓ Applied X gold to local player`
4. Verify both players' gold totals match

### For Wave Sync:
1. Start a multiplayer game
2. Progress through waves on host
3. Check logs for:
   - `[Host] Wave X starting, broadcasting...`
   - `[Client] Wave X started`
4. Verify both players are on the same wave
5. Complete a wave and check completion logs

### If Not Working:
1. Check logs for error messages
2. Look for "Could not find [ClassName]" warnings
3. Use dnSpy to find the correct class/method names
4. Update the commented patches in:
   - `PlayerGoldPatches.cs`
   - `WaveProgressionPatches.cs`
5. Rebuild and redeploy

---

## Code Structure

All new synchronization features follow the same pattern:

```
1. Packet Definition (Base/Packet/)
   - SendXPacket: Outgoing packet structure
   - XPacket: Incoming packet parsing

2. Client Handler (Client/Handlers/)
   - Receives packet from server
   - Queues action on Unity main thread via GameDispatcher
   - Applies game state changes using reflection

3. Server Patch (Game/Patches/)
   - Harmony patch on game method
   - Prefix: Block execution on client
   - Postfix: Broadcast event to all clients

4. Event Handler (Game/Handlers/NetworkNotify/)
   - Subscribes to GameEvent
   - Broadcasts packet to all connected players

5. Game Event (GameEvents.cs)
   - Event definition
   - Trigger method
```

This makes adding new sync features consistent and maintainable.

---

## Next Steps

**High Priority**:
1. Investigate host blank screen on death/restart bug
2. Find gold collection methods with dnSpy
3. Find wave progression methods with dnSpy
4. Test enemy cache preloader (previous build)
5. Test XP/Chest/Shrine handlers (previous build)

**Medium Priority**:
6. Boss detection alternative (flag doesn't work)
7. Interactable boss spawner sync
8. Item/loot drop synchronization

**Low Priority**:
9. Minimap sync validation
10. Game pause/unpause sync

---

## dnSpy Investigation Guide

### Opening the Game in dnSpy:
1. Navigate to: `[Game Install]/MelonLoader/Il2CppAssemblies/`
2. Open `Assembly-CSharp.dll` in dnSpy
3. Use Edit → Search Assembly (Ctrl+Shift+K)

### For Gold Collection:
Search terms: `gold`, `coin`, `money`, `currency`, `AddGold`, `AddCoins`, `CollectGold`, `PickupCoin`
Look for classes like:
- `CoinPickup`
- `GoldPickup`
- `CollectibleCoin`
- `PlayerInventory.AddGold`

### For Wave Progression:
Search terms: `wave`, `Wave`, `StartWave`, `CompleteWave`, `WaveManager`, `WaveController`
Look for classes like:
- `WaveManager`
- `EnemyWaveController`
- `WaveSystem`
- Methods: `StartWave()`, `OnWaveComplete()`, `NextWave()`

### What to Note:
1. Full class name (including namespace)
2. Method name
3. Method parameters (type and order)
4. Return type
5. Any relevant properties/fields

---

## Files Modified This Session

**New Packets**:
- `PlayerGoldGainedPacket.cs`
- `WaveStartPacket.cs`
- `WaveCompletePacket.cs`

**New Handlers**:
- `PlayerGoldGainedPacketHandler.cs`
- `WaveStartPacketHandler.cs`
- `WaveCompletePacketHandler.cs`

**New Patches** (need dnSpy):
- `PlayerGoldPatches.cs`
- `WaveProgressionPatches.cs`

**New Event Handlers**:
- `PlayerGoldEventHandler.cs`
- `WaveStartEventHandler.cs`
- `WaveCompleteEventHandler.cs`

**Modified Files**:
- `GameEvents.cs` - Added PlayerGoldGainedEvent, WaveStartEvent, WaveCompleteEvent
- `PacketId.cs` - Added packet IDs 22, 23, 24
- `Multibonk.cs` - Registered new handlers
- `TODO.md` - Updated implementation status
