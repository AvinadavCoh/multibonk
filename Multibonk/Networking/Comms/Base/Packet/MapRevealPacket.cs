using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using UnityEngine;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Packet sent from host to clients when a map tile is revealed
    /// This keeps all players' minimaps synchronized
    /// </summary>
    public class MapRevealPacket
    {
        public int TileX { get; private set; }
        public int TileY { get; private set; }

        public MapRevealPacket(IncomingMessage msg)
        {
            TileX = msg.ReadInt();
            TileY = msg.ReadInt();
        }
    }

    /// <summary>
    /// Outgoing packet to broadcast map tile reveals
    /// </summary>
    public class SendMapRevealPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.MAP_REVEAL;

        public SendMapRevealPacket(int tileX, int tileY)
        {
            Message.WriteByte(Id);
            Message.WriteInt(tileX);
            Message.WriteInt(tileY);
        }
    }

    /// <summary>
    /// Packet for bulk map reveal synchronization (sent on join)
    /// This allows new players to see what the host has already explored
    /// </summary>
    public class MapRevealBulkPacket
    {
        public int[] TileXCoords { get; private set; }
        public int[] TileYCoords { get; private set; }

        public MapRevealBulkPacket(IncomingMessage msg)
        {
            int count = msg.ReadInt();
            TileXCoords = new int[count];
            TileYCoords = new int[count];

            for (int i = 0; i < count; i++)
            {
                TileXCoords[i] = msg.ReadInt();
                TileYCoords[i] = msg.ReadInt();
            }
        }
    }

    /// <summary>
    /// Outgoing bulk packet for sending all revealed tiles at once
    /// </summary>
    public class SendMapRevealBulkPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ServerSentPacketId.MAP_REVEAL_BULK;

        public SendMapRevealBulkPacket(int[] tileXCoords, int[] tileYCoords)
        {
            Message.WriteByte(Id);
            
            int count = Mathf.Min(tileXCoords.Length, tileYCoords.Length);
            Message.WriteInt(count);

            for (int i = 0; i < count; i++)
            {
                Message.WriteInt(tileXCoords[i]);
                Message.WriteInt(tileYCoords[i]);
            }
        }
    }
}
