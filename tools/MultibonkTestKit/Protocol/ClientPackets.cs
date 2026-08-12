using Multibonk.Networking.Comms.Base;

namespace MultibonkTestKit.Protocol
{
    /// <summary>
    /// Hand-written client -> host packet bodies for MultibonkTestKit.
    ///
    /// These deliberately are NOT linked from Multibonk/Networking/Comms/Base/Packet/*
    /// because those files depend on Il2Cpp/UnityEngine/Multibonk.Networking.Lobby types
    /// that don't exist outside the modded game process (per task constraints). Field
    /// order/types below were copied by hand from reading the real source files so the
    /// wire format matches exactly:
    ///   JoinLobbyPacket.cs, SelectCharacterPacket.cs, GameLoadedPacket.cs,
    ///   PlayerMovePacket.cs, PlayerRotatePacket.cs, ClientLootPackets.cs,
    ///   ClientHealthPacket.cs, PlayerDiedPacket.cs, LevelupCoordPackets.cs.
    ///
    /// They build on the REAL linked OutgoingPacket/OutgoingMessage/PacketId types, so
    /// packet ids and primitive encodings (WriteInt/WriteFloat/WriteString/...) can never
    /// drift from the mod.
    /// </summary>
    public sealed class SendJoinLobbyPacket : OutgoingPacket
    {
        // JoinLobbyPacket.cs: WriteByte(id); WriteInt(version); WriteString(playerName)
        public SendJoinLobbyPacket(int modVersion, string playerName)
        {
            Message.WriteByte((byte)ClientSentPacketId.JOIN_LOBBY_PACKET);
            Message.WriteInt(modVersion);
            Message.WriteString(playerName);
        }
    }

    public sealed class SendSelectCharacterPacket : OutgoingPacket
    {
        // SelectCharacterPacket.cs: WriteByte(id); WriteString(characterName)
        public SendSelectCharacterPacket(string characterName)
        {
            Message.WriteByte((byte)ClientSentPacketId.CHARACTER_SELECTION);
            Message.WriteString(characterName);
        }
    }

    public sealed class SendGameLoadedPacket : OutgoingPacket
    {
        // GameLoadedPacket.cs: WriteByte(id); pos.x,y,z (float); rot.x,y,z,w (float)
        public SendGameLoadedPacket(float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW)
        {
            Message.WriteByte((byte)ClientSentPacketId.GAME_LOADED_PACKET);
            Message.WriteFloat(posX);
            Message.WriteFloat(posY);
            Message.WriteFloat(posZ);
            Message.WriteFloat(rotX);
            Message.WriteFloat(rotY);
            Message.WriteFloat(rotZ);
            Message.WriteFloat(rotW);
        }
    }

    public sealed class SendPlayerMovePacket : OutgoingPacket
    {
        // PlayerMovePacket.cs: WriteByte(id); position.x,y,z (float)
        public SendPlayerMovePacket(float x, float y, float z)
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_MOVE_PACKET);
            Message.WriteFloat(x);
            Message.WriteFloat(y);
            Message.WriteFloat(z);
        }
    }

    public sealed class SendPlayerRotatePacket : OutgoingPacket
    {
        // PlayerRotatePacket.cs (client-sent id 4): WriteByte(id); euler.x,y,z (float) - NOT byte-compressed
        public SendPlayerRotatePacket(float eulerX, float eulerY, float eulerZ)
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_ROTATE_PACKET);
            Message.WriteFloat(eulerX);
            Message.WriteFloat(eulerY);
            Message.WriteFloat(eulerZ);
        }
    }

    public sealed class SendClientXpGainedPacket : OutgoingPacket
    {
        // ClientLootPackets.cs: WriteByte(id); WriteInt(xpAmount)
        public SendClientXpGainedPacket(int xpAmount)
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_XP_GAINED_PACKET);
            Message.WriteInt(xpAmount);
        }
    }

    public sealed class SendClientGoldGainedPacket : OutgoingPacket
    {
        // ClientLootPackets.cs: WriteByte(id); WriteInt(goldAmount)
        public SendClientGoldGainedPacket(int goldAmount)
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_GOLD_GAINED_PACKET);
            Message.WriteInt(goldAmount);
        }
    }

    public sealed class SendClientHealthPacket : OutgoingPacket
    {
        // ClientHealthPacket.cs: WriteByte(id); currentHealth, maxHealth, damageAmount (float)
        public SendClientHealthPacket(float currentHealth, float maxHealth, float damageAmount)
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_HEALTH_PACKET);
            Message.WriteFloat(currentHealth);
            Message.WriteFloat(maxHealth);
            Message.WriteFloat(damageAmount);
        }
    }

    public sealed class SendPlayerDiedPacket : OutgoingPacket
    {
        // PlayerDiedPacket.cs: WriteByte(id) only - server identifies sender by Connection
        public SendPlayerDiedPacket()
        {
            Message.WriteByte((byte)ClientSentPacketId.PLAYER_DIED_PACKET);
        }
    }

    public sealed class SendLevelupDonePacket : OutgoingPacket
    {
        // LevelupCoordPackets.cs: WriteByte(id) only - server identifies sender by Connection
        public SendLevelupDonePacket()
        {
            Message.WriteByte((byte)ClientSentPacketId.LEVELUP_DONE_PACKET);
        }
    }
}
