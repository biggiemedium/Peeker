using UnityEngine;

namespace Peeker.UI
{
    /// <summary>
    /// The menu's palette: three steps of near-black surface, two steps of stroke,
    /// three steps of text, and one amber accent that is the *only* saturated colour
    /// in the UI. Anything that is "on" is amber; anything else is grey. That single
    /// rule is what keeps a wall of small rows readable at a glance.
    /// </summary>
    public static class PeekerColors
    {
        // ---- surfaces (darkest at the back, lightest at the front) ------------

        /// <summary>Panel body.</summary>
        public static Color WindowBg = Hex("#181d22");

        /// <summary>Panel title bar — one step above the body.</summary>
        public static Color HeaderBg = Hex("#212930");

        /// <summary>Recessed area: an expanded module's settings tray.</summary>
        public static Color SidebarBg = Hex("#12161a");

        /// <summary>Small raised controls: buttons, badges, enum values.</summary>
        public static Color ButtonBg = Hex("#252e36");
        public static Color ButtonHoverBg = Hex("#2f3a44");

        /// <summary>Row states.</summary>
        public static Color RowHoverBg = Hex("#232b33");
        public static Color RowActiveBg = HexA("#ffb03a", 0.10f);
        public static Color RowActiveHoverBg = HexA("#ffb03a", 0.17f);

        // ---- strokes ----------------------------------------------------------

        public static Color WindowBorder = Hex("#333e48");
        public static Color BorderSoft = Hex("#262f37");

        // ---- text --------------------------------------------------------------

        /// <summary>Titles and enabled module names.</summary>
        public static Color TitleText = Hex("#e8edf2");

        /// <summary>Disabled module names, setting values.</summary>
        public static Color NameOff = Hex("#95a3af");

        /// <summary>Labels, hints, keybinds — everything secondary.</summary>
        public static Color MonoDim = Hex("#6d7c88");

        /// <summary>The quietest text in the UI: placeholders and empty states.</summary>
        public static Color EmptyHint = Hex("#4e5a64");

        public static Color KeybindLabel = Hex("#c3ced8");

        // ---- accent --------------------------------------------------------------

        /// <summary>Overridable at runtime (the design exposes this as the "accentColor" prop).</summary>
        public static Color Accent = Hex("#ffb03a");
        public static Color AccentHover = Hex("#ffc972");
        public static Color AccentDim = HexA("#ffb03a", 0.35f);

        // ---- controls ---------------------------------------------------------------

        public static Color TrackBg = Hex("#2c363f");
        public static Color SwitchOffBg = Hex("#333e48");
        public static Color SwitchOffKnob = Hex("#7e8b96");
        public static Color SwitchOnKnob = Hex("#1a1e22");

        public static Color SwatchRing = Hex("#4a5661");

        public static readonly Color[] DefaultSwatches =
        {
            Hex("#ffb03a"), Hex("#ff5d47"), Hex("#7fd06e"), Hex("#4fb0e0"),
            Hex("#c77dff"), Hex("#f0e9dd"),
        };

        // ---- helpers -----------------------------------------------------------------

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        public static Color HexA(string hex, float alpha)
        {
            Color c = Hex(hex);
            c.a = alpha;
            return c;
        }

        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
