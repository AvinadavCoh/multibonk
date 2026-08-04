namespace Multibonk.Networking.Comms.Base
{
    public enum ServerSentPacketId : byte
    {
       LOBBY_PLAYER_LIST_PACKET = 0,
       PLAYER_SELECTED_CHARACTER = 1,
       START_GAME = 2,
       PAUSE_GAME = 3,
       UNPAUSE_GAME = 4,
       MAP_FINISHED_LOADING = 5,
       SPAWN_PLAYER_PACKET = 6,

       PLAYER_MOVED_PACKET = 7,
       PLAYER_ROTATED_PACKET = 8,
       PLAYER_XP_GAINED_PACKET = 9,
       PLAYER_LEVEL_UP_PACKET = 10,
       ITEM_DROPPED_PACKET = 11,
       ITEM_PICKED_UP_PACKET = 12,
       ENEMY_DEATH_PACKET = 13,
       ENEMY_HEALTH_UPDATE_PACKET = 14,
       ENEMY_SPAWN_PACKET = 15,
       MAP_REVEAL = 16,
       MAP_REVEAL_BULK = 17,
       CHEST_OPEN = 18,
       SHRINE_USE = 19,
       PLAYER_DAMAGE = 20,
       PLAYER_DEATH = 21,
       PLAYER_GOLD_GAINED = 22,
       WAVE_START = 23,
       WAVE_COMPLETE = 24,
       BOSS_SPAWNER_ACTIVATE = 25,
       STAGE_TRANSITION = 26,
       TIME_SYNC = 27,
    }

    public enum ClientSentPacketId : byte
    {
        JOIN_LOBBY_PACKET = 0,
        CHARACTER_SELECTION = 1,
        GAME_LOADED_PACKET = 2,
        PLAYER_MOVE_PACKET = 3,
        PLAYER_ROTATE_PACKET = 4,
        PLAYER_XP_GAINED_PACKET = 5,
        PLAYER_GOLD_GAINED_PACKET = 6,
        PLAYER_HEALTH_PACKET = 7
    }
}
