using Multibonk.Game;
using Multibonk.Networking.Lobby;
using UnityEngine;

namespace Multibonk.UserInterface.Window
{
    /// <summary>
    /// In-game HUD showing health bars for all players in the lobby
    /// Displays during gameplay, not in menus
    /// </summary>
    public class PlayerHealthHUD : WindowBase
    {
        private LobbyContext lobbyContext;

        public PlayerHealthHUD(LobbyContext context) : base(new Rect(10, 100, 250, 200))
        {
            lobbyContext = context;
        }

        protected override void RenderWindow(Rect rect)
        {
            // Only show if in multiplayer and game is active
            if (!LobbyPatchFlags.InMultiplayer)
                return;

            // Draw styled window background
            CustomStyles.DrawWindowBackground(rect, "🎮 Players");

            GUILayout.BeginArea(new Rect(rect.x + 5, rect.y + 35, rect.width - 10, rect.height - 40));

            // Display each player's health
            foreach (var player in lobbyContext.GetPlayers())
            {
                DrawPlayerHealthBar(player);
                GUILayout.Space(8);
            }

            GUILayout.EndArea();
        }

        private void DrawPlayerHealthBar(LobbyPlayer player)
        {
            // Player name with icon
            string playerIcon = player.UUID == lobbyContext.GetMyself().UUID ? "👤" : "🎮";
            GUILayout.Label($"{playerIcon} {player.Name}", CustomStyles.LabelStyle);

            // TODO: Get actual player health from game
            // For now using placeholder values (randomized for demo)
            float currentHealth = 60f + (player.UUID * 5f) % 40f; // Varied for each player
            float maxHealth = 100f;
            float healthPercent = currentHealth / maxHealth;

            // Health bar
            Rect healthBarRect = GUILayoutUtility.GetRect(220, 22);
            string healthText = $"{currentHealth:F0} / {maxHealth:F0} HP";
            CustomStyles.DrawHealthBar(healthBarRect, healthPercent, healthText);
        }
    }
}
