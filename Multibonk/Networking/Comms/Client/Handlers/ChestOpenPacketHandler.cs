using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for chest open packets
    /// When server broadcasts a chest opening, client opens their local chest
    /// </summary>
    public class ChestOpenPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.CHEST_OPEN;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ChestOpenPacket(msg);

            MelonLogger.Msg($"[Client] Chest opened: {packet.ChestId} by player {packet.PlayerId}");

            // Queue the chest opening to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TODO: Find the chest management class and call its open method
                    // Example: ChestManager.OpenChest(packet.ChestId);
                    // For now we just log it
                    MelonLogger.Msg($"[Client] Opening chest {packet.ChestId} locally");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to open chest: {ex.Message}");
                }
            });
        }
    }
}
