using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// Small code-only uGUI builder used by every Peeker panel. Leans on Unity's
    /// layout groups (Horizontal/VerticalLayoutGroup + LayoutElement) as the
    /// stand-in for the design's flexbox rows/columns, so panels reflow instead
    /// of relying on hand-placed rects.
    ///
    /// Layout rules this file assumes everywhere (getting these wrong is what
    /// collapsed the first version of the menu to zero-size panels):
    ///  * A child of a layout group must NEVER carry a ContentSizeFitter — the
    ///    parent already asks it for a preferred size. Use <see cref="HRow"/>/
    ///    <see cref="VCol"/> on the child and let that report the hug size.
    ///  * A child that should fill leftover space needs <see cref="Flexible"/>;
    ///    "childForceExpand" is only used when *every* child should stretch.
    ///  * A child of a plain RectTransform (no layout group) starts life 0x0 —
    ///    it must be given anchors (<see cref="StretchAll"/>) or an explicit size.
    /// </summary>
    public static class UiFactory
    {
        private static TMP_FontAsset _font;
        private static bool _fontLookupDone;

        /// <summary>
        /// TextMeshPro only auto-assigns a font when TMP_Settings resolves, which is
        /// not guaranteed inside a BepInEx plugin that spins UI up before the game's
        /// own TMP assets are touched. Fall back to any font asset already in memory.
        /// </summary>
        public static TMP_FontAsset ResolveFont()
        {
            if (_fontLookupDone && _font != null) return _font;

            try
            {
                if (TMP_Settings.instance != null)
                    _font = TMP_Settings.defaultFontAsset;

                if (_font == null)
                    _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

                if (_font == null)
                {
                    TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                    if (loaded != null && loaded.Length > 0)
                        _font = loaded[0];
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning("[Peeker] Font lookup failed: " + ex.Message);
            }

            // Only latch the result on success: the first call can happen before the
            // game has loaded a single TMP asset, and caching that miss would leave
            // every label in the menu blank for the rest of the session.
            if (_font != null)
            {
                if (!_fontLookupDone) Plugin.Log?.LogInfo("[Peeker] Using TMP font: " + _font.name);
                _fontLookupDone = true;
            }
            else
            {
                Plugin.Log?.LogWarning("[Peeker] No TMP_FontAsset resolved yet — menu text may be blank.");
            }

            return _font;
        }

        // The design uses typographic punctuation, but the font Lethal Company hands
        // us (LiberationSans SDF) has no glyph for most of it and renders a hollow box
        // instead — that is what turned the close button into a "□". Rather than
        // hard-coding an ASCII-only UI, ask the font and only substitute what it lacks.
        private static readonly Dictionary<char, string> GlyphFallbacks = new Dictionary<char, string>
        {
            { '✕', "X" },      // ✕ multiplication X
            { '×', "x" },      // × multiplication sign
            { '→', ">" },      // → rightwards arrow
            { '—', "-" },      // — em dash
            { '–', "-" },      // – en dash
            { '·', "-" },      // · middle dot
            { '•', "*" },      // • bullet
            { '…', "..." },    // … ellipsis
        };

        /// <summary>
        /// Replaces non-ASCII characters with ASCII stand-ins.
        ///
        /// Deliberately a fixed table rather than asking the font. TMP's
        /// <c>HasCharacter(c, searchFallbacks: true)</c> walks the fallback chain
        /// recursively, and a circular fallback reference — easy to end up with in a
        /// modded game — makes it recurse until the stack blows. A StackOverflow
        /// cannot be caught in .NET: the process dies instantly with nothing written
        /// to any log, which is exactly what happened. No glyph lookup is worth that.
        /// </summary>
        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            StringBuilder sb = null;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 128)
                {
                    sb?.Append(c);
                    continue;
                }

                if (sb == null) sb = new StringBuilder(text.Length).Append(text, 0, i);
                sb.Append(GlyphFallbacks.TryGetValue(c, out string replacement) ? replacement : "?");
            }

            return sb == null ? text : sb.ToString();
        }

        /// <summary>Sanitizing, no-op-if-unchanged text assignment for per-frame sync paths.</summary>
        public static void SetText(TMP_Text label, string text)
        {
            if (label == null) return;
            string safe = Sanitize(text) ?? string.Empty;
            if (label.text != safe) label.text = safe;
        }

        public static RectTransform Rect(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            return rect != null ? rect : go.AddComponent<RectTransform>();
        }

        public static RectTransform StretchAll(GameObject go)
        {
            var r = Rect(go);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        /// <summary>Empty layout node — a plain RectTransform child, no graphic.</summary>
        public static GameObject Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>
        /// Fully transparent Image so a GameObject can receive pointer events.
        /// A zero-alpha Image still raycasts (uGUI only alpha-tests when
        /// alphaHitTestMinimumThreshold is raised), which is exactly what
        /// buttons, drag tracks and hover rows need.
        /// </summary>
        public static Image HitArea(GameObject go)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal,
            float letterSpacing = 0f, bool wrap = false, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();

            TMP_FontAsset font = ResolveFont();
            if (font != null) tmp.font = font;

            tmp.text = Sanitize(content);
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.characterSpacing = letterSpacing;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            tmp.richText = false;

            // Labels sit on top of the button/row image that actually handles the
            // click; leaving them raycastable only steals hover events.
            tmp.raycastTarget = raycast;
            return tmp;
        }

        /// <summary>Live-updatable rectangle border built from up to 4 thin Images.</summary>
        public class BorderHandle
        {
            private readonly Image[] _sides;
            public BorderHandle(Image[] sides) => _sides = sides;

            public void SetColor(Color color)
            {
                foreach (var s in _sides)
                    if (s != null) s.color = color;
            }
        }

        public enum Sides { All, Bottom, Top, TopBottom }

        public static BorderHandle AddBorder(Transform parent, Color color, float thickness, Sides sides = Sides.All)
        {
            var made = new System.Collections.Generic.List<Image>();

            if (sides == Sides.All || sides == Sides.Top || sides == Sides.TopBottom)
                made.Add(EdgeStrip(parent, "BorderTop", color, thickness, TextAnchor.UpperCenter, true));
            if (sides == Sides.All || sides == Sides.Bottom || sides == Sides.TopBottom)
                made.Add(EdgeStrip(parent, "BorderBottom", color, thickness, TextAnchor.LowerCenter, true));
            if (sides == Sides.All)
            {
                made.Add(EdgeStrip(parent, "BorderLeft", color, thickness, TextAnchor.MiddleLeft, false));
                made.Add(EdgeStrip(parent, "BorderRight", color, thickness, TextAnchor.MiddleRight, false));
            }

            return new BorderHandle(made.ToArray());
        }

        private static Image EdgeStrip(Transform parent, string name, Color color, float thickness, TextAnchor anchor, bool horizontal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;

            if (horizontal)
            {
                bool top = anchor == TextAnchor.UpperCenter;
                r.anchorMin = new Vector2(0, top ? 1 : 0);
                r.anchorMax = new Vector2(1, top ? 1 : 0);
                r.pivot = new Vector2(0.5f, top ? 1 : 0);
                r.sizeDelta = new Vector2(0, thickness);
                r.anchoredPosition = Vector2.zero;
            }
            else
            {
                bool left = anchor == TextAnchor.MiddleLeft;
                r.anchorMin = new Vector2(left ? 0 : 1, 0);
                r.anchorMax = new Vector2(left ? 0 : 1, 1);
                r.pivot = new Vector2(left ? 0 : 1, 0.5f);
                r.sizeDelta = new Vector2(thickness, 0);
                r.anchoredPosition = Vector2.zero;
            }

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;   // decoration must never swallow a click

            // Border strips overlay their parent's edge and must never be treated as a
            // flex child by a parent that happens to be a Horizontal/VerticalLayoutGroup.
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        public static HorizontalLayoutGroup HRow(GameObject go, float spacing = 0, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool controlW = true, bool controlH = true, bool expandW = false, bool expandH = false)
        {
            var g = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = align;
            g.childControlWidth = controlW;
            g.childControlHeight = controlH;
            g.childForceExpandWidth = expandW;
            g.childForceExpandHeight = expandH;
            g.padding = padding ?? new RectOffset();
            return g;
        }

        public static VerticalLayoutGroup VCol(GameObject go, float spacing = 0, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool controlW = true, bool controlH = true, bool expandW = false, bool expandH = false)
        {
            var g = go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = align;
            g.childControlWidth = controlW;
            g.childControlHeight = controlH;
            g.childForceExpandWidth = expandW;
            g.childForceExpandHeight = expandH;
            g.padding = padding ?? new RectOffset();
            return g;
        }

        public static LayoutElement Element(GameObject go)
            => go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();

        public static LayoutElement Fixed(GameObject go, float? width = null, float? height = null)
        {
            var le = Element(go);
            if (width.HasValue) { le.preferredWidth = width.Value; le.minWidth = width.Value; }
            if (height.HasValue) { le.preferredHeight = height.Value; le.minHeight = height.Value; }
            return le;
        }

        /// <summary>Preferred size without a hard minimum, so the parent can still shrink it.</summary>
        public static LayoutElement Preferred(GameObject go, float? width = null, float? height = null)
        {
            var le = Element(go);
            if (width.HasValue) le.preferredWidth = width.Value;
            if (height.HasValue) le.preferredHeight = height.Value;
            return le;
        }

        public static LayoutElement Flexible(GameObject go, float flexW = 1, float flexH = 0)
        {
            var le = Element(go);
            le.flexibleWidth = flexW;
            le.flexibleHeight = flexH;
            return le;
        }

        /// <summary>Zero-size flex spacer — the "justify-content: space-between" trick.</summary>
        public static GameObject Spacer(Transform parent, float flexW = 1, float flexH = 0)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Flexible(go, flexW, flexH);
            return go;
        }

        public static RectOffset Padding(int l, int t, int r, int b) => new RectOffset(l, r, t, b);

        /// <summary>
        /// Only valid on a node whose parent is NOT a layout group (popovers, free-floating
        /// windows). Inside a layout group the parent already drives the size and a fitter
        /// here fights it.
        /// </summary>
        public static ContentSizeFitter AutoSize(GameObject go, bool horizontal = true, bool vertical = true)
        {
            var fitter = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return fitter;
        }

        /// <summary>
        /// Destroys every child. Reparents first: Destroy is deferred to end-of-frame,
        /// so rows rebuilt in the same frame would otherwise be laid out alongside the
        /// corpses of the old ones.
        /// </summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        /// <summary>Vertical, mouse-wheel scrollable content column with no visible scrollbar chrome.</summary>
        public static RectTransform ScrollColumn(Transform parent, string name)
        {
            // RectMask2D rather than Mask: no stencil buffer, no material juggling, and
            // it doesn't care that the viewport graphic is invisible. The graphic is
            // still needed so the mouse wheel has something to raycast against.
            var viewport = new GameObject(name + "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(parent, false);
            StretchAll(viewport);
            var vpImage = viewport.GetComponent<Image>();
            vpImage.color = new Color(1, 1, 1, 0.001f);
            vpImage.raycastTarget = true;

            var content = new GameObject(name + "Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            contentRect.anchoredPosition = Vector2.zero;

            VCol(content, 0, null, TextAnchor.UpperLeft, true, true, true, false);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // `parent` must be a dedicated container (not a shared layout node) — this
            // adds ScrollRect directly onto it.
            var sr = parent.gameObject.GetComponent<ScrollRect>();
            if (sr == null)
            {
                sr = parent.gameObject.AddComponent<ScrollRect>();
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 32f;
            }
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = contentRect;

            return contentRect;
        }
    }
}
