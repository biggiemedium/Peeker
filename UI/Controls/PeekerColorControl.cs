using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Peeker.Settings;
using Peeker.UI.Internal;

namespace Peeker.UI.Controls
{
    /// <summary>
    /// Swatch row + picker button that opens a floating SV/hue/alpha popover.
    /// Bound to a <see cref="Setting"/> whose value type is Color or Color32.
    /// </summary>
    public static class PeekerColorControl
    {
        private const int SvTextureSize = 48;
        private const int StripLength = 128;

        public static void Build(Transform parent, Canvas canvas, RectTransform overlayLayer, Setting setting,
            bool preferUpward, Action onChanged)
        {
            Color Get() => ToColor(setting.BoxedValue);
            void Set(Color c) { setting.BoxedValue = FromColor(setting.ValueType, c); onChanged?.Invoke(); }

            var row = new GameObject("ColorRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            UiFactory.HRow(row, 10, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(row, height: 28f);

            foreach (Color swatch in PeekerColors.DefaultSwatches)
            {
                var sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                sw.transform.SetParent(row.transform, false);
                UiFactory.Fixed(sw, 26f, 26f);
                var img = sw.GetComponent<Image>();
                img.color = swatch;
                var ring = UiFactory.AddBorder(sw.transform, Color.clear, 2f);

                var hover = sw.AddComponent<HoverElement>();
                hover.Clicked = () => Set(swatch);
                sw.AddComponent<SwatchRing>().Bind(ring, swatch, Get);
            }

            var button = new GameObject("PickerButton", typeof(RectTransform), typeof(Image));
            button.transform.SetParent(row.transform, false);
            button.GetComponent<Image>().color = Color.clear;
            var buttonBorder = UiFactory.AddBorder(button.transform, PeekerColors.SwatchRing, 1f);
            UiFactory.HRow(button, 8, UiFactory.Padding(6, 5, 10, 5), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.AutoSize(button, true, true);

            var chip = new GameObject("Chip", typeof(RectTransform), typeof(Image));
            chip.transform.SetParent(button.transform, false);
            UiFactory.Fixed(chip, 16f, 16f);
            var chipImg = chip.GetComponent<Image>();
            UiFactory.AddBorder(chip.transform, PeekerColors.SwatchRing, 1f);

            var hexLabel = UiFactory.Text(button.transform, "Hex", "", 11, PeekerColors.TabInactive,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.06f);

            GameObject popover = null;

            void RefreshChip()
            {
                Color c = Get();
                chipImg.color = c;
                hexLabel.text = ToHex(c).ToUpperInvariant();
            }

            var buttonHover = button.AddComponent<HoverElement>();
            buttonHover.WatchColor(hexLabel, PeekerColors.TabInactive, PeekerColors.TitleText);

            buttonHover.Clicked = () =>
            {
                if (popover != null) { Destroy(popover); popover = null; buttonBorder.SetColor(PeekerColors.SwatchRing); return; }
                buttonBorder.SetColor(PeekerColors.Accent);
                popover = BuildPopover(overlayLayer, canvas, (RectTransform)button.transform, preferUpward, Get, c =>
                {
                    Set(c);
                    RefreshChip();
                }, () =>
                {
                    Destroy(popover);
                    popover = null;
                    buttonBorder.SetColor(PeekerColors.SwatchRing);
                });
            };

            RefreshChip();
        }

        private static GameObject BuildPopover(RectTransform overlayLayer, Canvas canvas, RectTransform anchor,
            bool upward, Func<Color> getColor, Action<Color> setColor, Action close)
        {
            var pop = UiFactory.Panel(overlayLayer, "ColorPopover", PeekerColors.PopoverBg);
            var popRect = (RectTransform)pop.transform;
            popRect.sizeDelta = new Vector2(268, 0);
            UiFactory.AddBorder(pop.transform, PeekerColors.PopoverBorder, 1f);
            UiFactory.VCol(pop, 12, UiFactory.Padding(16, 16, 16, 16), TextAnchor.UpperLeft, true, true, false, false);
            pop.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PositionPopover(overlayLayer, canvas, anchor, popRect, upward);

            // -- SV field --------------------------------------------------
            var sv = new GameObject("SV", typeof(RectTransform), typeof(RawImage));
            sv.transform.SetParent(pop.transform, false);
            UiFactory.Fixed(sv, height: 150f);
            UiFactory.Flexible(sv, 1, 0);
            var svImage = sv.GetComponent<RawImage>();
            var svTexture = new Texture2D(SvTextureSize, SvTextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            svImage.texture = svTexture;

            var svCursor = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
            svCursor.transform.SetParent(sv.transform, false);
            var svCursorRect = (RectTransform)svCursor.transform;
            svCursorRect.sizeDelta = new Vector2(12, 12);
            var svCursorImg = svCursor.GetComponent<Image>();
            svCursorImg.color = Color.clear;
            UiFactory.AddBorder(svCursor.transform, Color.white, 2f);

            var svDrag = sv.AddComponent<NormalizedDragArea>();

            // -- Hue strip --------------------------------------------------
            var hue = new GameObject("Hue", typeof(RectTransform), typeof(RawImage));
            hue.transform.SetParent(pop.transform, false);
            UiFactory.Fixed(hue, height: 14f);
            UiFactory.Flexible(hue, 1, 0);
            hue.GetComponent<RawImage>().texture = BuildHueTexture();

            var hueCursor = ThinCursor(hue.transform);
            var hueDrag = hue.AddComponent<NormalizedDragArea>();

            // -- Alpha strip --------------------------------------------------
            var alpha = new GameObject("Alpha", typeof(RectTransform), typeof(RawImage));
            alpha.transform.SetParent(pop.transform, false);
            UiFactory.Fixed(alpha, height: 14f);
            UiFactory.Flexible(alpha, 1, 0);
            var alphaImage = alpha.GetComponent<RawImage>();
            var alphaTexture = new Texture2D(StripLength, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            alphaImage.texture = alphaTexture;

            var alphaCursor = ThinCursor(alpha.transform);
            var alphaDrag = alpha.AddComponent<NormalizedDragArea>();

            // -- Readout row --------------------------------------------------
            var readoutRow = new GameObject("Readout", typeof(RectTransform));
            readoutRow.transform.SetParent(pop.transform, false);
            UiFactory.HRow(readoutRow, 10, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(readoutRow, height: 20f);
            UiFactory.Flexible(readoutRow, 1, 0);

            var chipMini = new GameObject("Chip", typeof(RectTransform), typeof(Image));
            chipMini.transform.SetParent(readoutRow.transform, false);
            UiFactory.Fixed(chipMini, 16f, 16f);
            var chipMiniImg = chipMini.GetComponent<Image>();

            var hexReadout = UiFactory.Text(readoutRow.transform, "Hex", "", 11, PeekerColors.TitleText,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.06f);

            UiFactory.Spacer(readoutRow.transform);

            var rgbaReadout = UiFactory.Text(readoutRow.transform, "Rgba", "", 11, PeekerColors.TabInactive,
                TextAlignmentOptions.MidlineRight, letterSpacing: 0.06f);

            // -- Footer row --------------------------------------------------
            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(pop.transform, false);
            UiFactory.HRow(footer, 10, UiFactory.Padding(0, 10, 0, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.AddBorder(footer.transform, PeekerColors.SidebarBorder, 1f, UiFactory.Sides.Top);
            UiFactory.Fixed(footer, height: 34f);
            UiFactory.Flexible(footer, 1, 0);

            UiFactory.Text(footer.transform, "Caption", "UNITY COLOR · RGBA 0–1", 10, PeekerColors.MonoDim,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.12f);
            UiFactory.Spacer(footer.transform);
            var done = UiFactory.Text(footer.transform, "Done", "DONE", 10, PeekerColors.Accent,
                TextAlignmentOptions.MidlineRight, letterSpacing: 0.12f);
            var doneHover = done.gameObject.AddComponent<HoverElement>();
            doneHover.Clicked = close;

            void Refresh()
            {
                Color c = getColor();
                Color.RGBToHSV(c, out float h, out float s, out float v);

                FillSv(svTexture, h);
                svCursorRect.anchorMin = svCursorRect.anchorMax = new Vector2(s, v);

                hueCursor.anchorMin = hueCursor.anchorMax = new Vector2(h, 0.5f);

                FillAlpha(alphaTexture, c);
                alphaCursor.anchorMin = alphaCursor.anchorMax = new Vector2(c.a, 0.5f);

                chipMiniImg.color = c;
                hexReadout.text = ToHex(c).ToUpperInvariant();
                rgbaReadout.text = $"{c.r:0.00} {c.g:0.00} {c.b:0.00} {c.a:0.00}";
            }

            svDrag.Changed = (x, y) =>
            {
                Color c = getColor();
                Color.RGBToHSV(c, out float h, out _, out _);
                Color next = Color.HSVToRGB(h, x, 1f - y);
                next.a = c.a;
                setColor(next);
                Refresh();
            };

            hueDrag.Changed = (x, _) =>
            {
                Color c = getColor();
                Color.RGBToHSV(c, out _, out float s, out float v);
                Color next = Color.HSVToRGB(x, s <= 0 ? 1 : s, v <= 0 ? 1 : v);
                next.a = c.a;
                setColor(next);
                Refresh();
            };

            alphaDrag.Changed = (x, _) =>
            {
                Color c = getColor();
                c.a = x;
                setColor(c);
                Refresh();
            };

            Refresh();
            return pop;
        }

        private static RectTransform ThinCursor(Transform parent)
        {
            var go = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(4, 18);
            go.GetComponent<Image>().color = PeekerColors.HueThumb;
            return rect;
        }

        private static void PositionPopover(RectTransform overlayLayer, Canvas canvas, RectTransform anchor, RectTransform popRect, bool upward)
        {
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);

            Vector3 worldPoint = upward ? corners[1] : corners[3]; // top-left when opening up, bottom-left opening down
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayLayer, screenPoint, cam, out Vector2 local);

            // `local` is relative to overlayLayer's pivot; anchoredPosition for a (0,0)-anchored
            // child is relative to its parent's bottom-left corner, so re-base by rect.min.
            Vector2 anchoredBase = local - overlayLayer.rect.min;

            popRect.pivot = new Vector2(1f, upward ? 0f : 1f);
            popRect.anchorMin = popRect.anchorMax = new Vector2(0f, 0f);
            popRect.anchoredPosition = anchoredBase + new Vector2(0, upward ? 8 : -8);
        }

        private static Texture2D BuildHueTexture()
        {
            var tex = new Texture2D(StripLength, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[StripLength * 4];
            for (int x = 0; x < StripLength; x++)
            {
                Color c = Color.HSVToRGB((float)x / (StripLength - 1), 1f, 1f);
                for (int y = 0; y < 4; y++)
                    pixels[y * StripLength + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static void FillSv(Texture2D tex, float hue)
        {
            var pixels = new Color32[SvTextureSize * SvTextureSize];
            for (int y = 0; y < SvTextureSize; y++)
            {
                // Texture row 0 is the bottom of the rendered rect (Unity's UV convention),
                // and the design wants full brightness at the top, black at the bottom.
                float v = (float)y / (SvTextureSize - 1);
                for (int x = 0; x < SvTextureSize; x++)
                {
                    float s = (float)x / (SvTextureSize - 1);
                    pixels[y * SvTextureSize + x] = Color.HSVToRGB(hue, s, v);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        private static void FillAlpha(Texture2D tex, Color baseColor)
        {
            var pixels = new Color32[StripLength * 8];
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < StripLength; x++)
                {
                    float a = (float)x / (StripLength - 1);
                    bool checker = ((x / 8) + (y / 8)) % 2 == 0;
                    Color bg = checker ? new Color(0.227f, 0.259f, 0.271f) : new Color(0.125f, 0.157f, 0.157f);
                    Color c = Color.Lerp(bg, new Color(baseColor.r, baseColor.g, baseColor.b, 1f), a);
                    pixels[y * StripLength + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        private static Color ToColor(object boxed)
        {
            if (boxed is Color c) return c;
            if (boxed is Color32 c32) return c32;
            return Color.white;
        }

        private static object FromColor(Type valueType, Color c)
        {
            Type t = Nullable.GetUnderlyingType(valueType) ?? valueType;
            return t == typeof(Color32) ? (object)(Color32)c : (object)c;
        }

        private static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        private static void Destroy(UnityEngine.Object obj)
        {
            if (obj != null) UnityEngine.Object.Destroy(obj);
        }

        /// <summary>Keeps a swatch's selection ring in sync without a per-frame Update loop.</summary>
        private class SwatchRing : MonoBehaviour
        {
            private UiFactory.BorderHandle _ring;
            private Color _swatch;
            private Func<Color> _current;

            public void Bind(UiFactory.BorderHandle ring, Color swatch, Func<Color> current)
            {
                _ring = ring;
                _swatch = swatch;
                _current = current;
            }

            private void OnEnable() => InvokeRepeating(nameof(Tick), 0f, 0.1f);
            private void OnDisable() => CancelInvoke(nameof(Tick));

            private void Tick()
            {
                bool selected = _current != null && ApproximatelyEqual(_current(), _swatch);
                _ring.SetColor(selected ? PeekerColors.Accent : Color.clear);
            }

            private static bool ApproximatelyEqual(Color a, Color b) =>
                Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }
    }
}
