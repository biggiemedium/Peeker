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
    /// </summary>
    public static class UiFactory
    {
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

        public static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal,
            float letterSpacing = 0f, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.characterSpacing = letterSpacing;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
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

            // Border strips overlay their parent's edge and must never be treated as a
            // flex child by a parent that happens to be a HorizontalLayoutGroup/VerticalLayoutGroup.
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        public static HorizontalLayoutGroup HRow(GameObject go, float spacing = 0, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool controlW = false, bool controlH = false, bool expandW = false, bool expandH = false)
        {
            var g = go.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = align;
            g.childControlWidth = controlW;
            g.childControlHeight = controlH;
            g.childForceExpandWidth = expandW;
            g.childForceExpandHeight = expandH;
            if (padding != null) g.padding = padding;
            return g;
        }

        public static VerticalLayoutGroup VCol(GameObject go, float spacing = 0, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool controlW = false, bool controlH = false, bool expandW = false, bool expandH = false)
        {
            var g = go.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = align;
            g.childControlWidth = controlW;
            g.childControlHeight = controlH;
            g.childForceExpandWidth = expandW;
            g.childForceExpandHeight = expandH;
            if (padding != null) g.padding = padding;
            return g;
        }

        public static LayoutElement Fixed(GameObject go, float? width = null, float? height = null)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (width.HasValue) { le.preferredWidth = width.Value; le.minWidth = width.Value; }
            if (height.HasValue) { le.preferredHeight = height.Value; le.minHeight = height.Value; }
            return le;
        }

        public static LayoutElement Flexible(GameObject go, float flexW = 1, float flexH = 0)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.flexibleWidth = flexW;
            le.flexibleHeight = flexH;
            return le;
        }

        /// <summary>Zero-size flex spacer — the "justify-content: space-between" trick.</summary>
        public static void Spacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Flexible(go, 1, 0);
        }

        public static RectOffset Padding(int l, int t, int r, int b) => new RectOffset(l, r, t, b);

        /// <summary>
        /// For "hug my content" composites: a padded button/badge/tab wrapping a
        /// LayoutGroup whose own rect should shrink/grow to fit that content.
        /// </summary>
        public static ContentSizeFitter AutoSize(GameObject go, bool horizontal = true, bool vertical = true)
        {
            var fitter = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return fitter;
        }

        /// <summary>Vertical, mouse-wheel scrollable content column with no visible scrollbar chrome.</summary>
        public static RectTransform ScrollColumn(Transform parent, string name)
        {
            var viewport = new GameObject(name + "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(parent, false);
            StretchAll(viewport);
            var vpImage = viewport.GetComponent<Image>();
            vpImage.color = new Color(1, 1, 1, 0.001f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject(name + "Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
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
                sr.viewport = (RectTransform)viewport.transform;
                sr.content = contentRect;
                sr.scrollSensitivity = 24f;
            }

            return contentRect;
        }
    }
}
