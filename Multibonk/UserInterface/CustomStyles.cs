using UnityEngine;

namespace Multibonk.UserInterface
{
    /// <summary>
    /// Shared dark theme for all mod windows.
    /// Rounded 9-slice panel/button textures are generated at runtime (no bundled assets),
    /// with the 1px border baked directly into the texture.
    /// NOTE: styles must be created lazily from inside OnGUI (GUI.skin requires it).
    /// </summary>
    public static class CustomStyles
    {
        // ---- Palette ----
        public static readonly Color Accent       = new Color(0.95f, 0.78f, 0.30f);  // gold
        public static readonly Color PanelBg      = new Color(0.070f, 0.075f, 0.090f, 0.97f);
        public static readonly Color TitleBg      = new Color(0.110f, 0.115f, 0.140f, 1f);
        public static readonly Color BorderCol    = new Color(0.28f, 0.30f, 0.36f, 1f);
        public static readonly Color TextCol      = new Color(0.92f, 0.92f, 0.95f);
        public static readonly Color SubtleCol    = new Color(0.62f, 0.64f, 0.70f);
        public static readonly Color ErrorCol     = new Color(0.95f, 0.35f, 0.35f);
        public static readonly Color RowAltCol    = new Color(1f, 1f, 1f, 0.04f);

        // Layout constants shared by windows
        public const float TitleBarHeight = 32f;
        public const float Pad = 14f;

        private static GUIStyle _titleStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _subtleStyle;
        private static GUIStyle _errorStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _primaryButtonStyle;
        private static GUIStyle _dangerButtonStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _panelStyle;
        private static GUIStyle _titleBarStyle;
        private static GUIStyle _textFieldStyle;
        private static GUIStyle _textFieldFocusedStyle;
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Accent }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SubtleCol }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = TextCol }
            };

            _subtleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = SubtleCol }
            };

            _errorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = ErrorCol }
            };

            _buttonStyle = MakeButton(
                new Color(0.16f, 0.17f, 0.21f),
                new Color(0.22f, 0.24f, 0.30f),
                new Color(0.12f, 0.13f, 0.16f),
                TextCol, TextCol);

            _primaryButtonStyle = MakeButton(
                new Color(0.42f, 0.33f, 0.11f),
                new Color(0.55f, 0.43f, 0.15f),
                new Color(0.33f, 0.26f, 0.09f),
                new Color(1f, 0.95f, 0.80f), Color.white);

            _dangerButtonStyle = MakeButton(
                new Color(0.38f, 0.14f, 0.14f),
                new Color(0.52f, 0.19f, 0.19f),
                new Color(0.28f, 0.10f, 0.10f),
                new Color(1f, 0.85f, 0.85f), Color.white);

            // NOTE: the IL2CPP RectOffset proxy has no 4-arg constructor - use initializer syntax
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeRoundedTex(24, 8, PanelBg, BorderCol, true, true) },
                border = new RectOffset { left = 10, right = 10, top = 10, bottom = 10 }
            };

            _titleBarStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeRoundedTex(24, 8, TitleBg, BorderCol, true, false) },
                border = new RectOffset { left = 10, right = 10, top = 10, bottom = 10 }
            };

            // Inner grouping box (sections)
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeRoundedTex(16, 5, new Color(1f, 1f, 1f, 0.035f), new Color(1f, 1f, 1f, 0.07f), true, true) },
                border = new RectOffset { left = 7, right = 7, top = 7, bottom = 7 },
                padding = new RectOffset { left = 10, right = 10, top = 8, bottom = 8 }
            };

            var fieldBg = MakeRoundedTex(14, 4, new Color(0.10f, 0.105f, 0.13f), new Color(0.24f, 0.26f, 0.32f), true, true);
            _textFieldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextCol, background = fieldBg },
                border = new RectOffset { left = 6, right = 6, top = 6, bottom = 6 },
                padding = new RectOffset { left = 9, right = 9, top = 5, bottom = 5 },
                clipping = TextClipping.Clip
            };

            _textFieldFocusedStyle = new GUIStyle(_textFieldStyle)
            {
                normal =
                {
                    textColor = Color.white,
                    background = MakeRoundedTex(14, 4, new Color(0.12f, 0.125f, 0.16f), Accent, true, true)
                }
            };

            _initialized = true;
        }

        private static GUIStyle MakeButton(Color normal, Color hover, Color active, Color textNormal, Color textHover)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset { left = 8, right = 8, top = 8, bottom = 8 },
                padding = new RectOffset { left = 10, right = 10, top = 6, bottom = 6 },
                normal = { textColor = textNormal, background = MakeRoundedTex(18, 6, normal, Lighten(normal, 0.06f), true, true) },
                hover  = { textColor = textHover,  background = MakeRoundedTex(18, 6, hover,  Lighten(hover, 0.08f),  true, true) },
                active = { textColor = textHover,  background = MakeRoundedTex(18, 6, active, Lighten(active, 0.05f), true, true) }
            };
            return style;
        }

        private static Color Lighten(Color c, float amount) =>
            new Color(Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount), Mathf.Clamp01(c.b + amount), c.a);

        /// <summary>
        /// IL2CPP-safe alternative to GUILayout.Space() which causes unstripping failures
        /// </summary>
        public static void Space(float pixels)
        {
            GUILayout.Label("", GUILayout.Height(pixels));
        }

        public static GUIStyle TitleStyle            { get { EnsureInit(); return _titleStyle; } }
        public static GUIStyle HeaderStyle           { get { EnsureInit(); return _headerStyle; } }
        public static GUIStyle LabelStyle            { get { EnsureInit(); return _labelStyle; } }
        public static GUIStyle SubtleStyle           { get { EnsureInit(); return _subtleStyle; } }
        public static GUIStyle ErrorStyle            { get { EnsureInit(); return _errorStyle; } }
        public static GUIStyle ButtonStyle           { get { EnsureInit(); return _buttonStyle; } }
        public static GUIStyle PrimaryButtonStyle    { get { EnsureInit(); return _primaryButtonStyle; } }
        public static GUIStyle DangerButtonStyle     { get { EnsureInit(); return _dangerButtonStyle; } }
        public static GUIStyle BoxStyle              { get { EnsureInit(); return _boxStyle; } }
        public static GUIStyle PanelStyle            { get { EnsureInit(); return _panelStyle; } }
        public static GUIStyle TextFieldStyle        { get { EnsureInit(); return _textFieldStyle; } }
        public static GUIStyle TextFieldFocusedStyle { get { EnsureInit(); return _textFieldFocusedStyle; } }

        private static void EnsureInit()
        {
            if (!_initialized) Initialize();
        }

        /// <summary>
        /// Generates a rounded-rect texture with a 1px border baked in.
        /// Used as a 9-sliced GUIStyle background so it scales to any size.
        /// </summary>
        private static Texture2D MakeRoundedTex(int size, int radius, Color fill, Color border, bool roundTop, bool roundBottom)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture y=0 is bottom; "top" of the widget is y = size-1
                    bool topHalf = y >= size / 2;
                    bool useRound = topHalf ? roundTop : roundBottom;

                    // Distance to the nearest edge, accounting for rounded corners
                    float edgeDist;
                    int cx = x < radius ? radius : (x > size - 1 - radius ? size - 1 - radius : -1);
                    int cy = y < radius ? radius : (y > size - 1 - radius ? size - 1 - radius : -1);

                    if (useRound && cx >= 0 && cy >= 0)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        edgeDist = radius - d;
                    }
                    else
                    {
                        edgeDist = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    }

                    float alpha = Mathf.Clamp01(edgeDist + 0.5f);
                    Color c = edgeDist < 1.5f ? border : fill;
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Draws the standard window chrome: drop shadow, rounded panel, title bar with
        /// gold accent underline, and centered title. Content should start at
        /// rect.y + TitleBarHeight + Pad.
        /// </summary>
        public static void DrawWindowBackground(Rect rect, string title = "")
        {
            try
            {
                EnsureInit();

                // Soft drop shadow
                GUI.color = new Color(0f, 0f, 0f, 0.35f);
                GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 4, rect.width, rect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Panel
                GUI.Box(rect, "", _panelStyle);

                if (title != null && title.Length > 0)
                {
                    var titleRect = new Rect(rect.x, rect.y, rect.width, TitleBarHeight);
                    GUI.Box(titleRect, "", _titleBarStyle);

                    // Accent underline
                    GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
                    GUI.DrawTexture(new Rect(rect.x + 1, rect.y + TitleBarHeight - 1, rect.width - 2, 1), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    GUI.Label(titleRect, title, _titleStyle);
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in DrawWindowBackground: {ex.Message}");
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Thin horizontal divider line for separating sections (absolute positioning).
        /// </summary>
        public static void DrawDivider(float x, float y, float width)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTexture(new Rect(x, y, width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>
        /// Returns a ping color: green (good) / yellow (ok) / red (bad).
        /// </summary>
        public static Color PingColor(int ping)
        {
            if (ping < 60) return new Color(0.35f, 0.85f, 0.40f);
            if (ping < 120) return new Color(0.95f, 0.85f, 0.30f);
            return new Color(0.95f, 0.35f, 0.35f);
        }

        // Health bar with rounded background and color-coded fill
        public static void DrawHealthBar(Rect rect, float percentage, string text = "")
        {
            EnsureInit();
            percentage = Mathf.Clamp01(percentage);

            // Background (rounded)
            GUI.Box(rect, "", _textFieldStyle);

            // Fill
            Color healthColor;
            if (percentage > 0.6f)
                healthColor = new Color(0.25f, 0.80f, 0.35f, 0.95f);
            else if (percentage > 0.3f)
                healthColor = new Color(0.95f, 0.80f, 0.25f, 0.95f);
            else
                healthColor = new Color(0.90f, 0.25f, 0.25f, 0.95f);

            var fillRect = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * percentage, rect.height - 4);
            if (fillRect.width > 0.5f)
            {
                GUI.color = healthColor;
                GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Text overlay with shadow
            if (text != null && text.Length > 0)
            {
                var textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, textStyle);
                GUI.color = Color.white;
                GUI.Label(rect, text, textStyle);
            }
        }

        // Draw rectangle outline (kept for compatibility)
        public static void DrawRectOutline(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }
    }
}
