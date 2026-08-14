namespace Multibonk.Modules.Session
{
    /// <summary>
    /// Wire packet ids sent BY THE HOST, mirroring
    /// Multibonk/Networking/Comms/Base/PacketId.cs's ServerSentPacketId byte-for-byte.
    /// Only the lobby/handshake subset used by this module is listed; VALUES below
    /// exactly match the legacy enum (including the gaps) so a future module can add
    /// the rest of the table here without ever colliding with an id already claimed.
    /// </summary>
    public enum ServerSentPacketId : byte
    {
        LOBBY_PLAYER_LIST_PACKET = 0,
        PLAYER_SELECTED_CHARACTER = 1,
        START_GAME = 2,
        // 3 PAUSE_GAME, 4 UNPAUSE_GAME, 5 MAP_FINISHED_LOADING - not implemented yet.
        SPAWN_PLAYER_PACKET = 6,
        // 7-31 belong to other legacy systems (player move/rotate, enemies, pickups,
        // map, run lifecycle, ...) - out of scope for the session/lobby module.
    }

    /// <summary>
    /// Wire packet ids sent BY THE CLIENT, mirroring
    /// Multibonk/Networking/Comms/Base/PacketId.cs's ClientSentPacketId byte-for-byte.
    /// See <see cref="ServerSentPacketId"/> for the scope note.
    /// </summary>
    public enum ClientSentPacketId : byte
    {
        JOIN_LOBBY_PACKET = 0,
        CHARACTER_SELECTION = 1,
        GAME_LOADED_PACKET = 2,
        // 3-11 belong to other legacy systems - out of scope for this module.
    }
}
