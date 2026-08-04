using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for shared gold from another player.
    /// Applies it via PlayerGoldPatches.ApplyNetworkGold (suppressed so it isn't echoed back).
    /// </summary>
    public class PlayerGoldGainedPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.PLAYER_GOLD_GAINED;

        public PlayerGoldGainedPacketHandler() { }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new PlayerGoldGainedPacket(msg);

            DebugLogger.Log($"[Client] Received shared gold: {packet.GoldAmount}");

            GameDispatcher.Enqueue(() =>
            {
                if (Preferences.GetGoldSharingMode() != Preferences.LootDistributionMode.Shared)
                    return;

                PlayerGoldPatches.ApplyNetworkGold(packet.GoldAmount);
            });
        }
    }
}
