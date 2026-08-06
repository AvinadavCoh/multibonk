using System;
using Il2CppAssets.Scripts.Actors.Enemies;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class EnemyHealthUpdatePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ENEMY_HEALTH_UPDATE_PACKET;

        public EnemyHealthUpdatePacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new EnemyHealthUpdatePacket(msg);

            DebugLogger.Log($"[EnemyHealthUpdatePacketHandler] Received health update for host enemy ID: {packet.EnemyId} — {packet.CurrentHealth}/{packet.MaxHealth}");

            if (!int.TryParse(packet.EnemyId, out int hostId))
            {
                MelonLogger.Warning($"[EnemyHealthUpdatePacketHandler] Could not parse enemy ID '{packet.EnemyId}' as int, ignoring packet");
                return;
            }

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    if (!EnemyIdMapper.TryGetEnemy(hostId, out Enemy enemy))
                    {
                        MelonLogger.Warning($"[EnemyHealthUpdatePacketHandler] No mapped enemy for host ID {hostId} — skipping health update");
                        return;
                    }

                    // Set maxHp before hp so the game does not clamp hp against the old max.
                    // We do NOT call Kill() even if CurrentHealth <= 0; the host issues a
                    // separate death packet and double-killing would double-fire EnemyDied().
                    enemy.maxHp = packet.MaxHealth;
                    enemy.hp = packet.CurrentHealth;

                    DebugLogger.Log($"[EnemyHealthUpdatePacketHandler] Updated enemy {hostId} to {packet.CurrentHealth}/{packet.MaxHealth}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[EnemyHealthUpdatePacketHandler] Exception while updating enemy {hostId} health: {ex}");
                }
            });
        }
    }
}
