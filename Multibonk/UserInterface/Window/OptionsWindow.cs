using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    public class OptionsWindow : WindowBase
    {
        private const float WindowWidth = 500f;
        private const float WindowHeight = 600f;

        public event Action OpenSteamOverlayRequested;

        private bool isOpen = false;
        private bool steamOverlayAvailable = false;
        private string steamTunnelStatus = string.Empty;

        // Cache for UI
        private bool pvpEnabled;
        private bool reviveEnabled;
        private string reviveDelayInput;
        private string reviveDelayError = string.Empty;
        private Preferences.LootDistributionMode xpMode;
        private Preferences.LootDistributionMode goldMode;
        private Preferences.LootDistributionMode chestMode;

        // GUIStyles - will be initialized in RenderWindow
        private GUIStyle titleStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle descriptionLabelStyle;
        private GUIStyle errorLabelStyle;

        public OptionsWindow() : base(new Rect(80f, 80f, WindowWidth, WindowHeight))
        {
            RefreshFromPreferences();
        }

        public bool IsOpen => isOpen;

        public void Show()
        {
            RefreshFromPreferences();
            isOpen = true;
        }

        public void Hide()
        {
            isOpen = false;
        }

        public void SetSteamOverlayAvailability(bool available)
        {
            steamOverlayAvailable = available;
        }

        public void SetSteamTunnelStatus(string status)
        {
            steamTunnelStatus = status;
        }

        protected override void RenderWindow(Rect rect)
        {
            if (!isOpen) return;

            InitializeStyles();

            CustomStyles.DrawWindowBackground(rect, "⚙️ Gameplay Options");

            GUILayout.BeginArea(new Rect(rect.x + 15, rect.y + 45, rect.width - 30, rect.height - 60));

            GUILayout.Label("Multiplayer Settings", titleStyle);
            GUILayout.Space(15);

            // PvP Section
            DrawToggleSection("Player vs Player (PvP)", 
                "Allow players to damage each other in combat.",
                ref pvpEnabled, 
                v => Preferences.PvpEnabled.Value = v);

            GUILayout.Space(10);

            // Revive Section
            GUILayout.Label("Revive System", sectionTitleStyle);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            bool newReviveEnabled = GUILayout.Toggle(reviveEnabled, " Enable player revives", CustomStyles.LabelStyle);
            if (newReviveEnabled != reviveEnabled)
            {
                reviveEnabled = newReviveEnabled;
                Preferences.ReviveEnabled.Value = reviveEnabled;
            }
            GUILayout.EndHorizontal();

            if (reviveEnabled)
            {
                GUILayout.Space(5);
                GUILayout.Label("Players can revive fallen teammates.", descriptionLabelStyle);
                
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Revive delay (seconds):", CustomStyles.LabelStyle, GUILayout.Width(180));
                string newInput = GUILayout.TextField(reviveDelayInput, CustomStyles.TextFieldStyle, GUILayout.Width(80));
                if (newInput != reviveDelayInput)
                {
                    reviveDelayInput = newInput;
                    if (float.TryParse(reviveDelayInput, out var delay) && delay >= 0f)
                    {
                        Preferences.ReviveTimeSeconds.Value = delay;
                        reviveDelayError = string.Empty;
                    }
                    else
                    {
                        reviveDelayError = "Invalid number. Must be >= 0.";
                    }
                }
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(reviveDelayError) && reviveEnabled)
                {
                    GUILayout.Label(reviveDelayError, errorLabelStyle);
                }
            }

            GUILayout.Space(15);

            // Loot Distribution Sections
            DrawDistributionSection("Experience Sharing", ref xpMode, Preferences.SetXpSharingMode,
                "Shared: everyone gains XP together.",
                "Individual: only the collector gains XP.",
                "Duplicated: each drop spawns for every player.");

            GUILayout.Space(10);

            DrawDistributionSection("Gold Sharing", ref goldMode, Preferences.SetGoldSharingMode,
                "Shared: the team uses one shared wallet.",
                "Individual: everyone keeps their own gold.",
                "Duplicated: pickups reward every player equally.");

            GUILayout.Space(10);

            DrawDistributionSection("Chest Loot", ref chestMode, Preferences.SetChestSharingMode,
                "Shared: chest contents go to the team pool.",
                "Individual: first player to open gets all loot.",
                "Duplicated: each player receives the full chest loot.");

            GUILayout.Space(15);

            // Steam section
            DrawSteamOverlaySection();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("✓ Apply & Close", CustomStyles.ButtonStyle, GUILayout.Height(35)))
            {
                Hide();
            }

            GUILayout.EndArea();
        }

        private void InitializeStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(CustomStyles.HeaderStyle);
                titleStyle.fontSize = 16;
                titleStyle.fontStyle = FontStyle.Bold;
            }

            if (sectionTitleStyle == null)
            {
                sectionTitleStyle = new GUIStyle(CustomStyles.HeaderStyle);
                sectionTitleStyle.fontSize = 13;
            }

            if (descriptionLabelStyle == null)
            {
                descriptionLabelStyle = new GUIStyle(CustomStyles.LabelStyle);
                descriptionLabelStyle.fontSize = 11;
                descriptionLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                descriptionLabelStyle.wordWrap = true;
            }

            if (errorLabelStyle == null)
            {
                errorLabelStyle = new GUIStyle(CustomStyles.LabelStyle);
                errorLabelStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
            }
        }

        private void RefreshFromPreferences()
        {
            pvpEnabled = Preferences.PvpEnabled.Value;
            reviveEnabled = Preferences.ReviveEnabled.Value;
            reviveDelayInput = Preferences.ReviveTimeSeconds.Value.ToString("0.##");
            xpMode = Preferences.GetXpSharingMode();
            goldMode = Preferences.GetGoldSharingMode();
            chestMode = Preferences.GetChestSharingMode();
        }

        private void DrawToggleSection(string title, string description, ref bool cache, System.Action<bool> setter)
        {
            GUILayout.Label(title, sectionTitleStyle);
            GUILayout.Space(5);

            bool newValue = GUILayout.Toggle(cache, $" {description}", CustomStyles.LabelStyle);
            if (newValue != cache)
            {
                cache = newValue;
                setter(newValue);
            }
        }

        private void DrawDistributionSection(string title, 
            ref Preferences.LootDistributionMode cache, 
            System.Action<Preferences.LootDistributionMode> setter,
            string sharedDescription,
            string individualDescription,
            string duplicatedDescription)
        {
            GUILayout.Label(title, sectionTitleStyle);
            GUILayout.Space(5);

            var newMode = cache;

            if (GUILayout.Toggle(cache == Preferences.LootDistributionMode.Shared, " Shared", CustomStyles.LabelStyle))
                newMode = Preferences.LootDistributionMode.Shared;
            GUILayout.Label($"   {sharedDescription}", descriptionLabelStyle);

            GUILayout.Space(3);

            if (GUILayout.Toggle(cache == Preferences.LootDistributionMode.Individual, " Individual", CustomStyles.LabelStyle))
                newMode = Preferences.LootDistributionMode.Individual;
            GUILayout.Label($"   {individualDescription}", descriptionLabelStyle);

            GUILayout.Space(3);

            if (GUILayout.Toggle(cache == Preferences.LootDistributionMode.Duplicated, " Duplicated", CustomStyles.LabelStyle))
                newMode = Preferences.LootDistributionMode.Duplicated;
            GUILayout.Label($"   {duplicatedDescription}", descriptionLabelStyle);

            if (newMode != cache)
            {
                cache = newMode;
                setter(newMode);
            }
        }

        private void DrawSteamOverlaySection()
        {
            GUILayout.Label("Steam Tunneling", sectionTitleStyle);
            GUILayout.Label("Use the Steam overlay to discover and join friend lobbies.", descriptionLabelStyle);

            if (!string.IsNullOrEmpty(steamTunnelStatus))
            {
                GUILayout.Label(steamTunnelStatus, descriptionLabelStyle);
            }

            bool previous = GUI.enabled;
            GUI.enabled = steamOverlayAvailable;
            if (GUILayout.Button("💬 Open Steam Friends Overlay", CustomStyles.ButtonStyle, GUILayout.Height(30)))
            {
                OpenSteamOverlayRequested?.Invoke();
            }
            GUI.enabled = previous;
        }

        public new void Handle()
        {
            if (isOpen)
            {
                base.Handle();
            }
        }
    }
}
