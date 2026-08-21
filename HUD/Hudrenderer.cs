using UnityEngine;

namespace Peeker.HUD
{
    /// <summary>
    /// Thin IMGUI drawing layer. Deliberately minimal — swap the internals for a
    /// shader/mesh based renderer later without touching element code.
    /// Every method here must only be called during a Repaint pass.
    /// </summary>
    public static class HudRenderer
    {
        private static Texture2D _white;
        private static GUIStyle _text;

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        public static GUIStyle TextStyle
        {
            get
            {
                if (_text == null)
                {
                    _text = new GUIStyle(GUI.skin.label);
                    _text.fontSize = 14;
                    _text.alignment = TextAnchor.UpperLeft;
                    _text.padding = new RectOffset(0, 0, 0, 0);
                    _text.wordWrap = false;
                    _text.richText = true;
                }
                return _text;
            }
        }

        public static void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = prev;
        }

        public static void DrawBorder(Rect rect, Color color, float thickness = 1f)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void DrawText(Vector2 pos, string text, Color color, bool shadow = true)
        {
            Vector2 size = MeasureText(text);
            Color prev = GUI.color;

            if (shadow)
            {
                GUI.color = new Color(0f, 0f, 0f, color.a * 0.75f);
                GUI.Label(new Rect(pos.x + 1f, pos.y + 1f, size.x, size.y), text, TextStyle);
            }

            GUI.color = color;
            GUI.Label(new Rect(pos.x, pos.y, size.x, size.y), text, TextStyle);
            GUI.color = prev;
        }

        public static Vector2 MeasureText(string text)
        {
            return TextStyle.CalcSize(new GUIContent(text));
        }
    }
}