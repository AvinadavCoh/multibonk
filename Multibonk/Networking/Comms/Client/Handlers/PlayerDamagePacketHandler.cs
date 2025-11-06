using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for player damage packets
    /// Shows damage feedback and health changes for other players
    /// </summary>
    public class PlayerDamagePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_DAMAGE;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerDamagePacket(msg);

            MelonLogger.Msg($"[Client] Player {packet.PlayerId} took {packet.DamageAmount:F1} damage. Health: {packet.CurrentHealth:F1}/{packet.MaxHealth:F1}");

            // Queue the damage update to happen on the main Unity thread
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    // TODO: Update player health UI/bar
                    // TODO: Show damage numbers/feedback
                    // Example: PlayerHealthUI.UpdateHealth(packet.PlayerId, packet.CurrentHealth, packet.MaxHealth);
                    MelonLogger.Msg($"[Client] Updating health display for player {packet.PlayerId}");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[Client] Failed to handle player damage: {ex.Message}");
                }
            });
        }
    }
}
