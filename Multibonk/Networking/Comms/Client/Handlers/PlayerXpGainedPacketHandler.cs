using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for shared XP from another player.
    /// Applies it via PlayerXpPatches.ApplyNetworkXp (suppressed so it isn't echoed back).
    /// </summary>
    public class PlayerXpGainedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_XP_GAINED_PACKET;

        public PlayerXpGainedPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerXpGainedPacket(msg);

            DebugLogger.Log($"[Client] Player {packet.PlayerId} gained {packet.XpAmount} XP (shared)");

            GameDispatcher.Enqueue(() =>
            {
                if (Preferences.GetXpSharingMode() != Preferences.LootDistributionMode.Shared)
                    return;

                PlayerXpPatches.ApplyNetworkXp(packet.XpAmount);
                SyncTelemetry.RecordApplied(SyncChannel.XpGain);
            });
        }
    }
}
