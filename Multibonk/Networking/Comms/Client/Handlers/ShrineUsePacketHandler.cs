using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for shrine use packets
    /// When server broadcasts a shrine use, client applies the shrine effect locally
    /// </summary>
    public class ShrineUsePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.SHRINE_USE;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ShrineUsePacket(msg);

            MelonLogger.Msg($"[Client] Shrine used: {packet.ShrineId} (type {packet.ShrineType}) by player {packet.PlayerId}");

            // Queue the shrine activation to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TODO: Find the shrine management class and call its use method
                    // Example: ShrineManager.UseShrine(packet.ShrineId, packet.ShrineType);
                    // For now we just log it
                    MelonLogger.Msg($"[Client] Activating shrine {packet.ShrineId} locally");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to activate shrine: {ex.Message}");
                }
            });
        }
    }
}
