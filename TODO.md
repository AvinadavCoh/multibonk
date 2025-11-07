# TODO List

## High Priority

### Host Death/Restart Bug
- **Issue**: When host dies and starts a new game, they get a blank screen
- **Location**: Game restart logic
- **Notes**: Need to investigate game state cleanup on death/restart
- **Status**: Not started

## Medium Priority

### Enemy Cache Mismatch
- **Issue**: Client caches different enemy types than what host spawns
  - Client cache: Types 4, 8, 25, 28
  - Host spawning: Types 3, 12, 13
- **Problem**: Client can't spawn enemies it hasn't cached
- **Possible Solutions**:
  1. Sync enemy spawn seeds between host/client
  2. Pre-populate cache with all enemy types at game start
  3. Send EnemyData in the packet (might be too large)
  4. Request missing EnemyData from host when needed

### Boss Detection
- **Issue**: Boss flag always shows as "None" in logs
- **Investigation Needed**: 
  - Check if EEnemyFlag enum has "Boss" value
  - May need different method to detect bosses
  - Perhaps check EnemyData properties instead of spawn flag

### Interactable Bush Boss
- **Issue**: Bush interactable doesn't spawn boss on client
- **Notes**: May be separate spawning mechanism than regular enemies
- **Needs Investigation**: Check how interactables trigger spawns

## Implementation Status

### ✅ Completed
- Enemy spawning synchronization
- Enemy combat sync (damage/death)
- Enemy ID mapping system
- Duplicate enemy prevention
- XP sync handler (respects "Shared" mode)
- Chest sync handler
- Shrine sync handler
- Player movement/rotation sync
- Player damage/death sync
- Character selection sync
- Enemy cache preloader (loads all enemy types at game start)

### ⚠️ Partial
- Boss synchronization (spawning works, but flag detection doesn't)
- Chest/Shrine sync (handlers implemented, needs testing)
- Gold sync (infrastructure complete, needs dnSpy for method names)
- Wave progression sync (infrastructure complete, needs dnSpy for method names)

### ❌ Not Implemented
- Item/loot synchronization (needs dnSpy investigation)
- Minimap sync (infrastructure exists, needs validation)
- Boss interactable spawner sync (infrastructure exists, needs testing)
