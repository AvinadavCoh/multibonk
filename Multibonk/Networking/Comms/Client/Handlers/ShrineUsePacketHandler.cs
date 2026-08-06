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
    /// Client-side handler for shrine use packets.
    ///
    /// Resolution strategy: the packet carries a quantized position key ("x_y_z")
    /// and a shrine-type index (0-5 mapping to the six Il2Cpp shrine classes).
    /// We find all scene objects of that type, re-quantize each one's world
    /// position the same way the host did, and call Interact() on the match.
    ///
    /// InteractableSyncPatches.ApplyingNetworkShrine is set to true for the
    /// duration of that call so the Harmony Postfix does not re-broadcast it.
    /// </summary>
    public class ShrineUsePacketHandler : IClientPacketHandler
    {
        public byte PacketId => (byte)ServerSentPacketId.SHRINE_USE;

        public void Handle(IncomingMessage msg, Connection conn)
        {
            var packet = new ShrineUsePacket(msg);

            MelonLogger.Msg($"[Client] Shrine use received: id={packet.ShrineId} type={packet.ShrineType} player={packet.PlayerId}");

            // Game-object work must run on the Unity main thread.
            GameDispatcher.Enqueue(() =>
            {
                try
                {
                    ActivateShrine(packet.ShrineId, packet.ShrineType);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Client] ShrineUsePacketHandler failed: {ex.Message}");
                    MelonLogger.Error($"Stack: {ex.StackTrace}");
                }
            });
        }

        private static void ActivateShrine(string shrineId, int shrineType)
        {
            // --- Resolve the target class name from the type index ---
            string className = GetShrineClassName(shrineType);

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (assembly == null)
            {
                MelonLogger.Warning("[Client] Could not find Assembly-CSharp");
                return;
            }

            var shrineClass = assembly.GetType(className);
            if (shrineClass == null)
            {
                MelonLogger.Warning($"[Client] Could not find shrine class: {className}");
                return;
            }

            // --- Find all scene instances of that shrine type ---
            var findMethod = typeof(UnityEngine.Object).GetMethod(
                "FindObjectsOfType", new[] { typeof(Type) });
            if (findMethod == null)
            {
                MelonLogger.Warning("[Client] Could not find FindObjectsOfType(Type)");
                return;
            }

            var foundObjects = findMethod.Invoke(null, new object[] { shrineClass });
            if (foundObjects == null)
            {
                MelonLogger.Warning($"[Client] No {className} objects in scene");
                return;
            }

            var shrines = ((Array)foundObjects).Cast<object>().ToArray();
            MelonLogger.Msg($"[Client] Found {shrines.Length} {className} in scene, looking for id={shrineId}");

            // --- Parse the quantized position key ---
            // Format: "x_y_z" where each part is a whole-unit-rounded integer.
            if (!TryParseQuantizedId(shrineId, out int tx, out int ty, out int tz))
            {
                MelonLogger.Warning($"[Client] Could not parse shrine id: {shrineId}");
                return;
            }

            // --- Match by quantized position ---
            object targetShrine = null;
            var interactMethod = shrineClass.GetMethod("Interact",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (interactMethod == null)
            {
                MelonLogger.Warning($"[Client] Interact() not found on {className}");
                return;
            }

            foreach (var shrine in shrines)
            {
                var transform = shrineClass.GetProperty("transform")?.GetValue(shrine);
                if (transform == null) continue;

                var posProp = transform.GetType().GetProperty("position");
                if (posProp == null) continue;

                var pos = (Vector3)posProp.GetValue(transform);
                int qx = (int)Math.Round((double)pos.x);
                int qy = (int)Math.Round((double)pos.y);
                int qz = (int)Math.Round((double)pos.z);

                if (qx == tx && qy == ty && qz == tz)
                {
                    targetShrine = shrine;
                    break;
                }
            }

            if (targetShrine == null)
            {
                MelonLogger.Warning($"[Client] No {className} found at position {shrineId}");
                return;
            }

            // --- Activate, suppressing the Harmony Postfix to prevent re-broadcast ---
            InteractableSyncPatches.ApplyingNetworkShrine = true;
            try
            {
                bool result = (bool)interactMethod.Invoke(targetShrine, null);
                MelonLogger.Msg($"[Client] Activated {className} at {shrineId} (result={result})");
            }
            finally
            {
                InteractableSyncPatches.ApplyingNetworkShrine = false;
            }
        }

        /// <summary>
        /// Maps the ShrineType index (from the packet) to the fully-qualified
        /// Il2Cpp class name.  Must stay in sync with
        /// InteractableSyncPatches.ShrineInteractPatch.ShrineClassNames.
        /// </summary>
        private static string GetShrineClassName(int shrineType)
        {
            // Mirror of InteractableSyncPatches.ShrineInteractPatch.ShrineClassNames
            return shrineType switch
            {
                0 => "Il2Cpp.InteractableShrineBalance",
                1 => "Il2Cpp.InteractableShrineChallenge",
                2 => "Il2Cpp.InteractableShrineCursed",
                3 => "Il2Cpp.InteractableShrineGreed",
                4 => "Il2Cpp.InteractableShrineMagnet",
                5 => "Il2Cpp.InteractableShrineMoai",
                _ => "Il2Cpp.InteractableShrineBalance",
            };
        }

        /// <summary>
        /// Parses a shrine identity string of the form "x_y_z" produced by
        /// ShrineUseEventHandler using Math.Round.
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
