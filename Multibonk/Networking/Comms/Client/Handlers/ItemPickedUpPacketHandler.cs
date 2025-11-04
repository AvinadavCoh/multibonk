using MelonLoader;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class ItemPickedUpPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ITEM_PICKED_UP_PACKET;

        public ItemPickedUpPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ItemPickedUpPacket(msg);

            MelonLogger.Msg($"Player {packet.PlayerId} picked up item: {packet.ItemId}");

            // TODO: Remove the item from the game world
            // This will require finding the game's item removal method
            // For now, we just log it
        }
    }
}
