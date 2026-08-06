using System;
using Il2CppAssets.Scripts.Actors.Enemies;
using MelonLoader;
using Multibonk.Game.Diagnostics;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    public class EnemyDeathPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.ENEMY_DEATH_PACKET;

        public EnemyDeathPacketHandler()
        {
        }

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new EnemyDeathPacket(msg);

            DebugLogger.Log($"[EnemyDeathPacketHandler] Received death for host enemy ID: {packet.EnemyId}");

            if (!int.TryParse(packet.EnemyId, out int hostId))
            {
                MelonLogger.Warning($"[EnemyDeathPacketHandler] Could not parse enemy ID '{packet.EnemyId}' as int, ignoring packet");
                return;
            }

            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    if (!EnemyIdMapper.TryGetEnemy(hostId, out Enemy enemy))
                    {
                        MelonLogger.Warning($"[EnemyDeathPacketHandler] No mapped enemy for host ID {hostId} — already cleaned up or never spawned");
                        return;
                    }

                    if (enemy.IsDead() || enemy.IsDeadOrDyingNextFrame())
                    {
                        MelonLogger.Warning($"[EnemyDeathPacketHandler] Enemy {hostId} is already dead, skipping Kill()");
                        return;
                    }

                    // Kill() runs the game's full death path (animation, dissolve, loot).
                    // ItemDropPatches blocks pickup spawns on clients so loot duplication
                    // is not a concern.  EnemyDiedPatch.Postfix will remove the mapping
                    // via RemoveMapping() when Kill() triggers EnemyDied() internally.
                    enemy.Kill("network");

                    SyncTelemetry.RecordApplied(SyncChannel.EnemyDeath);

                    DebugLogger.Log($"[EnemyDeathPacketHandler] Successfully killed enemy {hostId}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[EnemyDeathPacketHandler] Exception while killing enemy {hostId}: {ex}");
                }
            });
        }
    }
}
