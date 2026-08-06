using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Game.Patches;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;
using System;
using System.Linq;
using UnityEngine;

namespace Multibonk.Networking.Comms.Client.Handlers
{
    /// <summary>
    /// Client-side handler for chest open packets.
    ///
    /// Resolution strategy: the packet carries a quantized position key ("x_y_z")
    /// derived from Math.Round applied to the chest's world position.  Because map
    /// generation is seed-synced, chests spawn at identical world positions on both
    /// host and client, so this key is stable across machines.
    ///
    /// We find all InteractableChest scene objects, re-quantize each one's world
    /// position the same way the host did, and call Interact() on the match.
    ///
    /// InteractableSyncPatches.ApplyingNetworkChest is set to true for the duration
    /// of that call so the Harmony Postfix does not re-broadcast it.
    /// </summary>
    public class ChestOpenPacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.CHEST_OPEN;

        private const string ChestClassName =
            "Il2CppAssets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest";

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ChestOpenPacket(msg);

            MelonLogger.Msg($"[Client] Chest opened: {packet.ChestId} by player {packet.PlayerId}");

            // Game-object work must run on the Unity main thread.
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    ActivateChest(packet.ChestId);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Client] ChestOpenPacketHandler failed: {ex.Message}");
                    MelonLogger.Error($"Stack: {ex.StackTrace}");
                }
            });
        }

        private static void ActivateChest(string chestId)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (assembly == null)
            {
                MelonLogger.Warning("[Client] Could not find Assembly-CSharp");
                return;
            }

            var chestType = assembly.GetType(ChestClassName);
            if (chestType == null)
            {
                MelonLogger.Warning($"[Client] Could not find chest type: {ChestClassName}");
                return;
            }

            // Find all scene instances of InteractableChest.
            var findMethod = typeof(UnityEngine.Object).GetMethod(
                "FindObjectsOfType", new[] { typeof(Type) });
            if (findMethod == null)
            {
                MelonLogger.Warning("[Client] Could not find FindObjectsOfType(Type)");
                return;
            }

            var foundObjects = findMethod.Invoke(null, new object[] { chestType });
            if (foundObjects == null)
            {
                MelonLogger.Warning("[Client] No InteractableChest objects in scene");
                return;
            }

            var chests = ((Array)foundObjects).Cast<object>().ToArray();
            MelonLogger.Msg($"[Client] Found {chests.Length} chests in scene, looking for id={chestId}");

            // Parse the quantized position key ("x_y_z").
            if (!TryParseQuantizedId(chestId, out int tx, out int ty, out int tz))
            {
                MelonLogger.Warning($"[Client] Could not parse chest id: {chestId}");
                return;
            }

            // Find the Interact method once before the loop.
            var interactMethod = chestType.GetMethod("Interact",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (interactMethod == null)
            {
                MelonLogger.Warning($"[Client] Interact() not found on {ChestClassName}");
                return;
            }

            // Match by re-quantizing each chest's world position.
            object targetChest = null;
            foreach (var chest in chests)
            {
                var transform = chestType.GetProperty("transform")?.GetValue(chest);
                if (transform == null) continue;

                var posProp = transform.GetType().GetProperty("position");
                if (posProp == null) continue;

                var pos = (Vector3)posProp.GetValue(transform);
                int qx = (int)Math.Round((double)pos.x);
                int qy = (int)Math.Round((double)pos.y);
                int qz = (int)Math.Round((double)pos.z);

                if (qx == tx && qy == ty && qz == tz)
                {
                    targetChest = chest;
                    break;
                }
            }

            if (targetChest == null)
            {
                MelonLogger.Warning($"[Client] No InteractableChest found at position {chestId}");
                return;
            }

            // Activate, suppressing the Harmony Postfix to prevent re-broadcast.
            InteractableSyncPatches.ApplyingNetworkChest = true;
            try
            {
                interactMethod.Invoke(targetChest, null);
                MelonLogger.Msg($"[Client] Opened chest at {chestId}");
            }
            finally
            {
                InteractableSyncPatches.ApplyingNetworkChest = false;
            }
        }

        /// <summary>
        /// Parses a chest identity string of the form "x_y_z" produced by
        /// ChestInteractPatch using Math.Round.
        /// </summary>
        private static bool TryParseQuantizedId(string id, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrEmpty(id)) return false;

            var parts = id.Split('_');
            if (parts.Length != 3) return false;

            return int.TryParse(parts[0], out x)
                && int.TryParse(parts[1], out y)
                && int.TryParse(parts[2], out z);
        }
    }
}
