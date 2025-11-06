using MelonLoader;

namespace Multibonk
{
    public static class Preferences
    {
        public enum LootDistributionMode
        {
            Shared,      // Team shares resources
            Individual,  // Each player keeps their own
            Duplicated   // Each pickup rewards all players
        }

        private static readonly MelonPreferences_Category category;

        public static readonly MelonPreferences_Entry<string> IpAddress;
        public static readonly MelonPreferences_Entry<string> PlayerName;

        // Gameplay rules
        public static readonly MelonPreferences_Entry<bool> PvpEnabled;
        public static readonly MelonPreferences_Entry<bool> ReviveEnabled;
        public static readonly MelonPreferences_Entry<float> ReviveTimeSeconds;
        public static readonly MelonPreferences_Entry<string> XpSharingMode;
        public static readonly MelonPreferences_Entry<string> GoldSharingMode;
        public static readonly MelonPreferences_Entry<string> ChestSharingMode;

        static Preferences()
        {
            category = MelonPreferences.CreateCategory("Multibonk", "General Settings");

            IpAddress = category.CreateEntry("IpAddress", "127.0.0.1", description: "IP address used at connection window");
            PlayerName = category.CreateEntry("PlayerName", "PlayerName", description: "Default player name used when connecting to a lobby.");

            // Gameplay preferences
            PvpEnabled = category.CreateEntry("PvpEnabled", false, description: "Enable player-versus-player damage in multiplayer sessions.");
            ReviveEnabled = category.CreateEntry("ReviveEnabled", true, description: "Allow players to revive fallen teammates.");
            ReviveTimeSeconds = category.CreateEntry("ReviveTimeSeconds", 5f, description: "Delay, in seconds, before a downed player can be revived.");

            XpSharingMode = category.CreateEntry("XpSharingMode", LootDistributionMode.Shared.ToString(), description: "How experience orbs are distributed between players.");
            GoldSharingMode = category.CreateEntry("GoldSharingMode", LootDistributionMode.Shared.ToString(), description: "How collected gold is distributed between players.");
            ChestSharingMode = category.CreateEntry("ChestSharingMode", LootDistributionMode.Shared.ToString(), description: "How chest loot is distributed between players.");
        }

        public static LootDistributionMode GetXpSharingMode() => ParseEnumEntry(XpSharingMode, LootDistributionMode.Shared);
        public static void SetXpSharingMode(LootDistributionMode mode) => XpSharingMode.Value = mode.ToString();

        public static LootDistributionMode GetGoldSharingMode() => ParseEnumEntry(GoldSharingMode, LootDistributionMode.Shared);
        public static void SetGoldSharingMode(LootDistributionMode mode) => GoldSharingMode.Value = mode.ToString();

        public static LootDistributionMode GetChestSharingMode() => ParseEnumEntry(ChestSharingMode, LootDistributionMode.Shared);
        public static void SetChestSharingMode(LootDistributionMode mode) => ChestSharingMode.Value = mode.ToString();

        private static LootDistributionMode ParseEnumEntry(MelonPreferences_Entry<string> entry, LootDistributionMode fallback)
        {
            if (System.Enum.TryParse<LootDistributionMode>(entry.Value, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }
    }
}
