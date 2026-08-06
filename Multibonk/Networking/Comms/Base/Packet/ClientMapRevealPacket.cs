using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Base.Packet
{
    /// <summary>
    /// Client -> server: "I revealed this map area".
    /// TileX/TileY carry world-space X/Z coordinates, matching the host's convention
    /// in MinimapSyncPatches (Mathf.RoundToInt of worldPos.x / worldPos.z).
    /// The server applies the reveal to its own FullMap and relays it to the other
    /// clients as a server-sent MAP_REVEAL packet, so all players converge on the same
    /// fog state.
    /// </summary>
    public class SendClientMapRevealPacket : OutgoingPacket
    {
        public readonly byte Id = (byte)ClientSentPacketId.MAP_REVEAL_PACKET;

        public SendClientMapRevealPacket(int tileX, int tileY)
        {
            Message.WriteByte(Id);
            Message.WriteInt(tileX);
            Message.WriteInt(tileY);
        }
    }

    internal class ClientMapRevealPacket
    {
        public int TileX { get; private set; }
        public int TileY { get; private set; }

        public ClientMapRevealPacket(IncomingMessage msg)
        {
            TileX = msg.ReadInt();
            TileY = msg.ReadInt();
        }
    }
}
