using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Server broadcasts when a player dies
    /// In multiplayer, players don't leave the lobby on death
    /// Game only ends when all players are dead
    /// </summary>
    public class SendPlayerDeathPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.PLAYER_DEATH;

        public SendPlayerDeathPacket(ushort playerId, Vector3 deathPosition)
        {
            Message.WriteByte(Id);
            Message.WriteUShort(playerId);
            Message.WriteFloat(deathPosition.x);
            Message.WriteFloat(deathPosition.y);
            Message.WriteFloat(deathPosition.z);
        }
    }

    internal class PlayerDeathPacket
    {
        public ushort PlayerId { get; private set; }
        public Vector3 DeathPosition { get; private set; }

        public PlayerDeathPacket(IncomingMessage msg)
        {
            PlayerId = msg.ReadUShort();
            float x = msg.ReadFloat();
            float y = msg.ReadFloat();
            float z = msg.ReadFloat();
            DeathPosition = new Vector3(x, y, z);
        }
    }
}
