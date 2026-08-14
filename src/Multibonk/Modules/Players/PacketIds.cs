namespace Multibonk.Modules.Players
{
    /// <summary>
    /// Wire packet ids used by this module, mirroring
    /// Multibonk/Networking/Comms/Base/PacketId.cs's ClientSentPacketId byte-for-byte
    /// (see Multibonk/Networking/Comms/Base/Packet/PlayerMovePacket.cs and
    /// PlayerRotatePacket.cs). Kept as this module's own enum (distinct from
    /// Modules/Session/PacketIds.cs's same-named types) so PlayerSyncModule owns its
    /// packet ids end-to-end, per the one-module-per-feature design in STATE.md - the
    /// numeric VALUES still exactly match the shared legacy wire protocol, which is all
    /// that matters for on-the-wire compatibility.
    ///
    /// WIRE ID COLLISION NOTE (same situation as Session's PacketIds.cs): id 3 doubles as
    /// ServerSentPacketId.PAUSE_GAME and id 4 as ServerSentPacketId.UNPAUSE_GAME - neither
    /// implemented by any module yet, so PlayerSyncModule's dispatch branch for "we are
    /// the client receiving this id" is a no-op for both.
    /// </summary>
    public enum ClientSentPacketId : byte
    {
        PLAYER_MOVE_PACKET = 3,
        PLAYER_ROTATE_PACKET = 4,
    }

    /// <summary>
    /// Wire packet ids used by this module, mirroring
    /// Multibonk/Networking/Comms/Base/PacketId.cs's ServerSentPacketId byte-for-byte
    /// (see Multibonk/Networking/Comms/Base/Packet/PlayerMovedPacket.cs and
    /// PlayerRotatedPacket.cs).
    ///
    /// WIRE ID COLLISION NOTE: id 7 doubles as ClientSentPacketId.PLAYER_HEALTH_PACKET
    /// and id 8 as ClientSentPacketId.MAP_REVEAL_PACKET - neither implemented by any
    /// module yet, so PlayerSyncModule's dispatch branch for "we are the host receiving
    /// this id" is a no-op for both.
    /// </summary>
    public enum ServerSentPacketId : byte
    {
        PLAYER_MOVED_PACKET = 7,
        PLAYER_ROTATED_PACKET = 8,
    }
}
