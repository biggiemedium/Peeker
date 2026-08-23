using UnityEngine;

namespace Peeker.UI
{
    /// <summary>
    /// Ship-HUD palette shared by every Peeker UI element: dark slate surfaces,
    /// a single amber accent, color used only as a stroke/fill signal (never
    /// as decoration). Pulled 1:1 from the design's hex values.
    /// </summary>
    public static class PeekerColors
    {
        public static Color WindowBg = Hex("#14191b");
        public static Color WindowBorder = Hex("#333f42");

        public static Color HeaderBg = Hex("#101415");
        public static Color TitleText = Hex("#f0e9dd");
        public static Color MonoDim = Hex("#6f7d80");

        public static Color TabInactive = Hex("#8a9598");
        public static Color TabActive = Hex("#f0e9dd");
        public static Color TabActiveBg = Hex("#14191b");
        public static Color TabHoverBg = Hex("#1b2225");

        public static Color AttachedGreen = Hex("#7fb069");
        public static Color CloseBorder = Hex("#3d4a4d");
        public static Color CloseIcon = Hex("#8a9598");

        public static Color SidebarBg = Hex("#121718");
        public static Color SidebarBorder = Hex("#232c2e");
        public static Color RowHoverBg = Hex("#1a2123");
        public static Color RowSelectedBg = Hex("#1a2123");
        public static Color RowBorder = Hex("#1c2325");

        public static Color NameOn = Hex("#f0e9dd");
        public static Color NameOff = Hex("#7d8a8c");
        public static Color SubtitleMono = Hex("#6a7679");
        public static Color KeyBadgeText = Hex("#8a9598");
        public static Color KeyBadgeBorder = Hex("#2b3437");

        public static Color SwitchOffBorder = Hex("#3d4a4d");
        public static Color SwitchOffKnob = Hex("#5f6b6e");
        public static Color SwitchOnBg = HexA("#e8a33d", 0.16f);

        public static Color EmptyDash = Hex("#3d4a4d");
        public static Color EmptyText = Hex("#8a9598");
        public static Color EmptyHint = Hex("#5f6b6e");

        public static Color FooterHint = Hex("#5f6b6e");
        public static Color FooterLink = Hex("#8a9598");

        public static Color DetailBg = Hex("#14191b");
        public static Color DetailBorder = Hex("#232c2e");
        public static Color KeybindRowBg = Hex("#121718");
        public static Color KeybindLabel = Hex("#cfd6d4");
        public static Color KeybindHint = Hex("#6a7679");
        public static Color KeybindButtonBorder = Hex("#3d4a4d");

        public static Color DetailDescription = Hex("#97a2a4");
        public static Color IdleBadge = Hex("#7d8a8c");

        public static Color SettingLabel = Hex("#cfd6d4");
        public static Color SettingHint = Hex("#6a7679");
        public static Color SettingRowBorder = Hex("#1e2628");

        public static Color TrackBg = Hex("#2b3437");
        public static Color ReadoutBorder = Hex("#2b3437");

        public static Color SwatchRing = Hex("#3d4a4d");
        public static Color PopoverBg = Hex("#101415");
        public static Color PopoverBorder = Hex("#3d4a4d");

        public static Color EnumBorder = Hex("#2b3437");
        public static Color EnumUnselected = Hex("#8a9598");
        public static Color EnumSelectedText = Hex("#121718");

        public static Color ResetBorder = Hex("#3d4a4d");
        public static Color ResetText = Hex("#97a2a4");
        public static Color ResetHoverText = Hex("#f0e9dd");

        public static Color CaptureBackdrop = HexA("#0a0d0e", 0.88f);
        public static Color HueThumb = Hex("#f0e9dd");

        public static Color StatusBarBg = Hex("#101415");

        /// <summary>Overridable at runtime (design doc exposes this as the "accentColor" prop).</summary>
        public static Color Accent = Hex("#e8a33d");
        public static Color AccentHover = Hex("#f5c274");

        public static readonly Color[] DefaultSwatches =
        {
            Hex("#e8a33d"), Hex("#c8552f"), Hex("#7fb069"), Hex("#4f9ec4"), Hex("#f0e9dd")
        };

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
