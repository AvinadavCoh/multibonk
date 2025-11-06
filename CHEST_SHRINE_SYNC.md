# Chest and Shrine Synchronization Implementation

## Overview
Added complete infrastructure for synchronizing chest openings and shrine/shop usage between players in multiplayer sessions.

## Implementation Date
November 6, 2025

## Components Created

### 1. Network Packets

#### ChestOpenPacket.cs
- **Purpose**: Broadcasts when any player opens a chest
- **Data**: ChestId (string), PlayerId (ushort)
- **Direction**: Server → All Clients
- **Packet ID**: 18 (CHEST_OPEN)

#### ShrineUsePacket.cs
- **Purpose**: Broadcasts when any player uses a shrine
- **Data**: ShrineId (string), PlayerId (ushort), ShrineType (int)
- **Direction**: Server → All Clients
- **Packet ID**: 19 (SHRINE_USE)

### 2. Client-Side Handlers

#### ChestOpenPacketHandler.cs
- Receives chest open broadcasts from server
- Queues chest opening on Unity main thread
- Ready to call game's chest opening method once discovered

#### ShrineUsePacketHandler.cs
- Receives shrine use broadcasts from server
- Queues shrine activation on Unity main thread
- Ready to call game's shrine method once discovered

### 3. Host-Side Event Handlers

#### ChestOpenEventHandler.cs
- Listens for `GameEvents.OpenChestEvent`
- Broadcasts chest openings to all connected clients
- Uses host's UUID as the player ID

#### ShrineUseEventHandler.cs
- Listens for `GameEvents.UseShrineEvent`
- Broadcasts shrine usage to all connected clients
- Generates shrine ID and type (placeholder until game structure discovered)

### 4. Game Patches

#### InteractableSyncPatches.cs
Contains two dynamic Harmony patches:

**ChestInteractPatch:**
- Attempts to find chest interaction methods at runtime
- Tries multiple possible class names:
  - `Il2Cpp.InteractableChest`
  - `Il2CppAssets.Scripts.Interactables.InteractableChest`
  - `InteractableChest`, `Chest`, `Il2Cpp.Chest`
- Looks for `Open()` or `Interact()` methods
- On successful open: Triggers `GameEvents.TriggerOpenChest(chestId)`
- Host-only execution (checks `LobbyPatchFlags.IsHosting`)

**ShrineInteractPatch:**
- Attempts to find shrine interaction methods at runtime
- Tries multiple possible class names:
  - `Il2Cpp.InteractableShrine`
  - `Il2CppAssets.Scripts.Interactables.InteractableShrine`
  - `InteractableShrine`, `Shrine`, `Il2Cpp.Shrine`
- Looks for `Use()` or `Interact()` methods
- On successful use: Triggers `GameEvents.TriggerUseShrine()`
- Host-only execution

### 5. Game Events

#### Added to GameEvents.cs:
```csharp
public static event Action<string> OpenChestEvent; // chestId
public static event Action UseShrineEvent;

public static void TriggerOpenChest(string chestId)
public static void TriggerUseShrine()
```

### 6. Registration

#### Updated Multibonk.cs:
- Registered `ChestOpenEventHandler` as `IGameEventHandler`
- Registered `ShrineUseEventHandler` as `IGameEventHandler`
- Registered `ChestOpenPacketHandler` as `IClientPacketHandler`
- Registered `ShrineUsePacketHandler` as `IClientPacketHandler`

## How It Works

### Chest Opening Flow:
1. **Host** interacts with a chest
2. `ChestInteractPatch.Postfix()` catches the interaction
3. Triggers `GameEvents.TriggerOpenChest(chestId)`
4. `ChestOpenEventHandler` receives the event
5. Creates `SendChestOpenPacket` and broadcasts to all clients
6. **Clients** receive packet via `ChestOpenPacketHandler`
7. Queues chest opening on main thread
8. *(TODO: Call game's method to open chest visually)*

### Shrine Usage Flow:
1. **Host** uses a shrine
2. `ShrineInteractPatch.Postfix()` catches the interaction
3. Triggers `GameEvents.TriggerUseShrine()`
4. `ShrineUseEventHandler` receives the event
5. Creates `SendShrineUsePacket` and broadcasts to all clients
6. **Clients** receive packet via `ShrineUsePacketHandler`
7. Queues shrine activation on main thread
8. *(TODO: Call game's method to activate shrine effect)*

## Current Status

### ✅ Completed:
- Network packet structure
- Client packet handlers
- Host event handlers
- Harmony patches with dynamic class discovery
- Event system integration
- Handler registration
- Documentation updates (README.md, INSTALL.txt)
- Build successful, DLL deployed to game

### ⚠️ Pending Testing:
- Patches need runtime testing to verify class names
- May need dnSpy inspection to find actual class/method names
- Client-side effect application needs game method discovery

### 🔧 Future Improvements:
1. **Discover actual game classes**:
   - Use dnSpy on Assembly-CSharp.dll
   - Search for: "Chest", "Shrine", "Interact"
   - Update patch class names if needed

2. **Implement client-side effects**:
   - Find chest opening animation method
   - Find shrine effect application method
   - Update packet handlers to call these methods

3. **Enhanced chest sync**:
   - Sync loot contents
   - Sync which items each player picks up
   - Prevent double-looting

4. **Enhanced shrine sync**:
   - Identify shrine type from game data
   - Sync actual shrine effects to all players
   - Handle different shrine types (health, damage, etc.)

## Architecture Notes

### Dynamic Patch Discovery
The patches use reflection to find classes at runtime because:
- IL2CPP class names can vary
- Namespace structure may differ across game versions
- Provides resilience against game updates

### Host Authority Model
- Only the host's interactions trigger broadcasts
- Prevents duplicate events
- Maintains consistent game state
- Clients receive and apply changes from host

### Thread Safety
- Game interactions queued via `GameDispatcher.Enqueue()`
- Ensures Unity API calls happen on main thread
- Prevents threading issues with Unity objects

## Testing Instructions

### When Testing:
1. **Host** starts server and enters game
2. **Client** connects to host
3. Navigate to an area with chests and shrines
4. **Host** opens a chest:
   - Check host log for: `[Host] Chest opened: {id}`
   - Check client log for: `[Client] Chest opened: {id} by player {id}`
5. **Host** uses a shrine:
   - Check host log for: `[Host] Shrine used`
   - Check client log for: `[Client] Shrine used: {id} (type {type}) by player {id}`

### Expected Behavior:
- Patches may log "Could not find" messages if class names don't match
- This is normal - patches gracefully disable if classes not found
- Infrastructure is ready and will work once correct classes discovered

### If Patches Don't Find Classes:
1. Open dnSpy
2. Load `D:\SteamLibrary\steamapps\common\Megabonk\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`
3. Search for: "Chest", "Shrine", "Interact"
4. Find the actual class and method names
5. Update `InteractableSyncPatches.cs` with correct names
6. Rebuild and test again

## Related Files
- `Multibonk/Networking/Comms/Base/Packet/ChestOpenPacket.cs`
- `Multibonk/Networking/Comms/Base/Packet/ShrineUsePacket.cs`
- `Multibonk/Networking/Comms/Client/Handlers/ChestOpenPacketHandler.cs`
- `Multibonk/Networking/Comms/Client/Handlers/ShrineUsePacketHandler.cs`
- `Multibonk/Game/Handlers/NetworkNotify/ChestOpenEventHandler.cs`
- `Multibonk/Game/Handlers/NetworkNotify/ShrineUseEventHandler.cs`
- `Multibonk/Game/Patches/InteractableSyncPatches.cs`
- `Multibonk/Game/GameEvents.cs`
- `Multibonk/Networking/Comms/Base/PacketId.cs`
- `Multibonk/Multibonk.cs`

## Success Metrics
- ✅ Compiles without errors
- ✅ All handlers registered
- ✅ Patches can be applied (with or without finding classes)
- ⏳ Runtime testing needed
- ⏳ Actual game class discovery needed
