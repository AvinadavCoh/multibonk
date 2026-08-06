using Multibonk.Game;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    /// <summary>
    /// In-game HUD showing health bars for all players in the lobby.
    /// Sizes itself to the player count instead of using a fixed-height box.
    /// </summary>
    public class PlayerHealthHUD : WindowBase
    {
        private const float RowHeight = 46f;
        private LobbyContext lobbyContext;

        public PlayerHealthHUD(LobbyContext context) : base(new Rect(10, 120, 250, 100))
        {
            lobbyContext = context;
        }

        protected override void RenderWindow(Rect rect)
        {
            // Only show if in multiplayer and game is active
            if (!LobbyPatchFlags.InMultiplayer)
                return;

            var players = lobbyContext.GetPlayers();
            if (players.Count == 0)
                return;

            // Size the panel to its content
            windowRect.height = CustomStyles.TitleBarHeight + 10 + players.Count * RowHeight + 6;
            rect = windowRect;

            CustomStyles.DrawWindowBackground(rect, "PLAYERS");

            float x = rect.x + 10;
            float width = rect.width - 20;
            float y = rect.y + CustomStyles.TitleBarHeight + 8;

            foreach (var player in players)
            {
                DrawPlayerHealthBar(player, x, y, width);
                y += RowHeight;
            }
        }

        private void DrawPlayerHealthBar(LobbyPlayer player, float x, float y, float width)
        {
            bool isMe = player.UUID == lobbyContext.GetMyself()?.UUID;
            var nameStyle = isMe
                ? new GUIStyle(CustomStyles.LabelStyle) { normal = { textColor = CustomStyles.Accent } }
                : CustomStyles.LabelStyle;

            string nameLabel = player.Level > 1
                ? $"{player.Name}  Lv.{player.Level}"
                : player.Name;
            GUI.Label(new Rect(x, y, width, 18), nameLabel, nameStyle);

            // My own bar reads live game health; other players use synced values
            float currentHealth = player.CurrentHealth;
            float maxHealth = player.MaxHealth;

            if (isMe && TryGetLocalHealth(out var liveCurrent, out var liveMax))
            {
                currentHealth = liveCurrent;
                maxHealth = liveMax;
            }

            if (maxHealth <= 0f) maxHealth = 100f;
            float healthPercent = currentHealth / maxHealth;

            var healthBarRect = new Rect(x, y + 20, width, 20);
            CustomStyles.DrawHealthBar(healthBarRect, healthPercent, $"{currentHealth:F0} / {maxHealth:F0}");
        }

        private static bool TryGetLocalHealth(out float current, out float max)
        {
            current = 0f;
            max = 0f;
            try
            {
                // PlayerHealth is not a Unity component - it lives on PlayerInventory
                var health = Il2CppAssets.Scripts.Actors.Player.MyPlayer.Instance?.inventory?.playerHealth;
                if (health == null)
                    return false;

                current = health.hp;
                max = health.maxHp;
                return max > 0f;
            }
            catch
            {
                return false;
            }
        }
    }
}
