using MelonLoader;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class ItemDroppedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ITEM_DROPPED_PACKET;

        public ItemDroppedPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ItemDroppedPacket(msg);

            MelonLogger.Msg($"Item dropped: {packet.ItemId} at position {packet.Position}, type: {packet.ItemType}");

            // TODO: Spawn the item in the game world
            // This will require finding the game's item spawning method
            // For now, we just log it
        }
    }
}
