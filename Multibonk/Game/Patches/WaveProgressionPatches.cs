using HarmonyLib;
using MelonLoader;
using Multibonk.Game;
using System.Linq;

namespace Multibonk.Game.Patches
{
    /// <summary>
    /// Patches to synchronize wave progression between players
    /// 
    /// IMPLEMENTATION STATUS: Needs dnSpy investigation
    /// 
    /// TODO: Find the correct class and methods that handle wave progression
    /// Search in dnSpy for:
    /// - "wave", "Wave", "WAVE"
    /// - Classes like: WaveManager, WaveController, EnemyWaveSystem, etc.
    /// - Methods like: StartWave, CompleteWave, NextWave, OnWaveComplete
    /// 
    /// Wave system might track:
    /// - Current wave number
    /// - Enemies remaining in wave
    /// - Wave timer
    /// - Wave rewards
    /// 
    /// Once found, update the patches below with correct type and method names.
    /// </summary>
    public static class WaveProgressionPatches
    {
        /*
        [HarmonyPatch] // TODO: Add correct type and method
        class StartWavePatch
        {
            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null)
                {
                    MelonLogger.Warning("Could not find Assembly-CSharp for wave patch");
                    return null;
                }

                // TODO: Find the correct wave manager class
                var waveManagerType = assembly.GetType("Il2Cpp.WaveManager") 
                    ?? assembly.GetType("Il2CppAssets.Scripts.WaveManager")
                    ?? assembly.GetType("Il2Cpp.EnemyWaveController");

                if (waveManagerType == null)
                {
                    MelonLogger.Warning("Could not find WaveManager type - wave sync disabled");
                    return null;
                }

                // TODO: Find the method that starts a wave
                var startWaveMethod = waveManagerType.GetMethod("StartWave")
                    ?? waveManagerType.GetMethod("BeginWave")
                    ?? waveManagerType.GetMethod("OnWaveStart");

                if (startWaveMethod == null)
                {
                    MelonLogger.Warning("Could not find StartWave method - wave sync disabled");
                    return null;
                }

                MelonLogger.Msg($"Found {waveManagerType.Name}.{startWaveMethod.Name} for wave sync patching");
                return startWaveMethod;
            }

            // Prefix: Block wave start on client (host controls wave progression)
            static bool Prefix()
            {
                if (!LobbyPatchFlags.IsHosting)
                {
                    MelonLogger.Msg("[Client] Blocked local wave start (will receive from server)");
                    return false; // Block execution
                }
                return true; // Allow host to start waves
            }

            // Postfix: Broadcast wave start to all clients
            static void Postfix(int waveNumber) // TODO: Match actual parameter
            {
                if (!LobbyPatchFlags.IsHosting) return;

                MelonLogger.Msg($"[Host] Wave {waveNumber} starting, broadcasting...");
                GameEvents.TriggerWaveStart(waveNumber);
            }
        }
        */

        /*
        [HarmonyPatch] // TODO: Add correct type and method
        class CompleteWavePatch
        {
            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null) return null;

                // TODO: Find the correct wave manager class
                var waveManagerType = assembly.GetType("Il2Cpp.WaveManager") 
                    ?? assembly.GetType("Il2CppAssets.Scripts.WaveManager");

                if (waveManagerType == null)
                {
                    MelonLogger.Warning("Could not find WaveManager for wave complete patch");
                    return null;
                }

                // TODO: Find the method that completes a wave
                var completeWaveMethod = waveManagerType.GetMethod("CompleteWave")
                    ?? waveManagerType.GetMethod("OnWaveComplete")
                    ?? waveManagerType.GetMethod("EndWave");

                if (completeWaveMethod == null)
                {
                    MelonLogger.Warning("Could not find CompleteWave method - wave sync disabled");
                    return null;
                }

                MelonLogger.Msg($"Found {waveManagerType.Name}.{completeWaveMethod.Name} for wave sync patching");
                return completeWaveMethod;
            }

            static void Postfix(int waveNumber) // TODO: Match actual parameter
            {
                if (!LobbyPatchFlags.IsHosting) return;

                MelonLogger.Msg($"[Host] Wave {waveNumber} completed, broadcasting...");
                GameEvents.TriggerWaveComplete(waveNumber);
            }
        }
        */

        // ALTERNATIVE: Patch wave counter directly if it's just a field update
        /*
        [HarmonyPatch] // TODO: Add correct type and property/field
        class WaveNumberPropertyPatch
        {
            static MethodBase TargetMethod()
            {
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null) return null;

                var waveManagerType = assembly.GetType("Il2Cpp.WaveManager");
                if (waveManagerType == null) return null;

                // Find the setter for currentWave or similar property
                var waveNumberProp = waveManagerType.GetProperty("currentWave")
                    ?? waveManagerType.GetProperty("waveNumber");

                if (waveNumberProp == null || waveNumberProp.SetMethod == null)
                {
                    MelonLogger.Warning("Could not find wave number property setter");
                    return null;
                }

                return waveNumberProp.SetMethod;
            }

            static void Postfix(int value)
            {
                if (!LobbyPatchFlags.IsHosting) return;

                MelonLogger.Msg($"[Host] Wave number changed to {value}, broadcasting...");
                GameEvents.TriggerWaveStart(value);
            }
        }
        */
    }
}
