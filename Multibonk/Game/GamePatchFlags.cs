using Il2Cpp;
using UnityEngine;

namespace Multibonk.Game
{

    public static class GamePatchFlags
    {
        private static readonly System.Random _rng = new System.Random();

        public static bool CharacterDataInitialized = false;
        public static List<CharacterData> CharacterData = new List<CharacterData>();

        public static Dictionary<ushort, SpawnedNetworkPlayer> PlayersCache = new Dictionary<ushort, SpawnedNetworkPlayer>();

        public static int Seed { get; set; } = _rng.Next(int.MinValue, int.MaxValue);

        public static bool AllowStartMapCall { get; set; } = false; 

        public static Vector3 LastPlayerPosition { get; set; }
        public static Quaternion LastPlayerRotation { get; set; }

        public static GameplayRulesSnapshot GameplayRules { get; private set; } = GameplayRulesSnapshot.FromPreferences();

        public static void SetGameplayRules(GameplayRulesSnapshot snapshot)
        {
            GameplayRules = snapshot;
        }

        /// <summary>
        /// Clears all game state when starting a new game
        /// Call this when returning to character selection or starting a new run
        /// </summary>
        public static void ClearGameState()
        {
            PlayersCache.Clear();
            Seed = _rng.Next(int.MinValue, int.MaxValue);
            AllowStartMapCall = false;
            LastPlayerPosition = Vector3.zero;
            LastPlayerRotation = Quaternion.identity;
            
            MelonLoader.MelonLogger.Msg("[GamePatchFlags] Cleared game state for new game");
        }
    }
}
