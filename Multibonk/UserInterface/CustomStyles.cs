using UnityEngine;

namespace Multibonk.UserInterface
{
    /// <summary>
    /// Custom UI styles for the mod to make it look more polished
    /// </summary>
    public static class CustomStyles
    {
        private static GUIStyle _titleStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _textFieldStyle;
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            // Title style - Large bold text
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.5f) } // Gold
            };

            // Header style - Medium bold text
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 1f) } // Light blue
            };

            // Label style - Regular text
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            // Button style - Styled buttons
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = 
                { 
                    textColor = Color.white,
                    background = MakeTex(2, 2, new Color(0.2f, 0.3f, 0.4f, 0.9f))
                },
                hover = 
                { 
                    textColor = new Color(1f, 0.9f, 0.5f),
                    background = MakeTex(2, 2, new Color(0.3f, 0.4f, 0.5f, 0.9f))
                },
                active = 
                { 
                    textColor = Color.white,
                    background = MakeTex(2, 2, new Color(0.1f, 0.2f, 0.3f, 0.9f))
                }
            };
            _buttonStyle.padding = new RectOffset { left = 10, right = 10, top = 8, bottom = 8 };

            // Box style - Window background
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = 
                { 
                    background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.95f))
                }
            };
            _boxStyle.border = new RectOffset { left = 3, right = 3, top = 3, bottom = 3 };

            // TextField style - Input fields
            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                normal = 
                { 
                    textColor = Color.white,
                    background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.2f, 0.9f))
                },
                focused = 
                { 
                    textColor = Color.white,
                    background = MakeTex(2, 2, new Color(0.2f, 0.25f, 0.3f, 0.9f))
                }
            };
            _textFieldStyle.padding = new RectOffset { left = 8, right = 8, top = 6, bottom = 6 };

            _initialized = true;
        }

        public static GUIStyle TitleStyle
        {
            get
            {
                if (!_initialized) Initialize();
                return _titleStyle;
            }
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                if (!_initialized) Initialize();
                return _headerStyle;
            }
        }

        public static GUIStyle LabelStyle
        {
            get
            {
                if (!_initialized) Initialize();
                return _labelStyle;
            }
        }

        public static GUIStyle ButtonStyle
        {
            get
            {
                if (!_initialized) Initialize();
                return _buttonStyle;
            }
        }

        public static GUIStyle BoxStyle
        {
            get
            {
                if (!_initialized) Initialize();
                return _boxStyle;
            }
        }

        public static GUIStyle TextFieldStyle
        {
            get
            {
                if (!_initialized) Initialize();
                return _textFieldStyle;
            }
        }

        // Helper to create solid color textures
        private static Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // Gradient background for health bars
        public static void DrawHealthBar(Rect rect, float percentage, string text = "")
        {
            // Background
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Health fill with gradient color
            Color healthColor;
            if (percentage > 0.6f)
                healthColor = new Color(0.2f, 0.9f, 0.3f, 0.9f); // Bright green
            else if (percentage > 0.3f)
                healthColor = new Color(0.95f, 0.85f, 0.2f, 0.9f); // Yellow
            else
                healthColor = new Color(0.95f, 0.2f, 0.2f, 0.9f); // Red

            Rect fillRect = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * percentage, rect.height - 4);
            GUI.color = healthColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

            // Border
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            DrawRectOutline(rect, 2);

            // Text overlay
            if (!string.IsNullOrEmpty(text))
            {
                GUI.color = Color.white;
                GUIStyle textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                
                // Text shadow for better readability
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, textStyle);
                GUI.color = Color.white;
                GUI.Label(rect, text, textStyle);
            }

            GUI.color = Color.white;
        }

        // Draw rectangle outline
        public static void DrawRectOutline(Rect rect, int thickness)
        {
            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            // Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        // Draw styled window background
        public static void DrawWindowBackground(Rect rect, string title = "")
        {
            // Main background
            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Border/frame
            GUI.color = new Color(0.3f, 0.4f, 0.5f, 1f);
            DrawRectOutline(rect, 2);

            // Title bar
            if (!string.IsNullOrEmpty(title))
            {
                Rect titleRect = new Rect(rect.x, rect.y, rect.width, 30);
                GUI.color = new Color(0.15f, 0.2f, 0.3f, 0.95f);
                GUI.DrawTexture(titleRect, Texture2D.whiteTexture);
                
                GUI.color = new Color(0.4f, 0.5f, 0.6f, 1f);
                DrawRectOutline(titleRect, 1);
                
                GUI.color = Color.white;
                GUI.Label(titleRect, title, TitleStyle);
            }

            GUI.color = Color.white;
        }
    }
}
