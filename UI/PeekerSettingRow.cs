using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Peeker.Settings;
using Peeker.UI.Internal;

namespace Peeker.UI
{
    /// <summary>
    /// One setting, one compact row inside a module's settings tray. Every row is the
    /// same shape — label on the left, control on the right — so a tray of mixed types
    /// still scans as a single list. Returns a refresh delegate so the owning panel can
    /// resync without rebuilding anything.
    /// </summary>
    public static class PeekerSettingRow
    {
        private const float RowHeight = 17f;
        private const int RowRadius = 3;

        public static Action Build(Transform parent, Setting setting)
        {
            if (setting.IsBoolSetting) return BuildBool(parent, setting);
            if (setting.IsNumberSetting) return BuildNumber(parent, setting);
            if (setting.IsColorSetting) return BuildColor(parent, setting);
            return BuildEnum(parent, setting);
        }

        // ---- shared chrome -------------------------------------------------

        /// <summary>Label on the left, room for a control on the right, whole row clickable.</summary>
        private static GameObject SimpleRow(Transform parent, Setting setting, out HoverElement hover)
        {
            var row = UiFactory.Node(parent, "S_" + setting.Name);
            Image bg = UiFactory.Rounded(row, Color.clear, RowRadius);
            UiFactory.HRow(row, 6, UiFactory.Padding(3, 0, 3, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(row, height: RowHeight);

            TextMeshProUGUI label = UiFactory.Text(row.transform, "Label", setting.Name, 9.5f, PeekerColors.MonoDim);
            UiFactory.Flexible(label.gameObject, 1, 0);

            hover = row.AddComponent<HoverElement>();
            hover.WatchColor(bg, Color.clear, PeekerColors.RowHoverBg);
            hover.WatchColor(label, PeekerColors.MonoDim, PeekerColors.KeybindLabel);
            return row;
        }

        /// <summary>Hides or shows a row according to its visibility predicate.</summary>
        private static void ApplyVisibility(GameObject row, Setting setting)
        {
            if (row.activeSelf != setting.IsVisible) row.SetActive(setting.IsVisible);
        }

        // ---- bool ----------------------------------------------------------

        private static Action BuildBool(Transform parent, Setting setting)
        {
            GameObject row = SimpleRow(parent, setting, out HoverElement hover);

            // Pill track. Radius is capped at height/2 - 1 so the 9-slice borders still
            // fit inside the rect; anything larger and Unity squashes the corners.
            var track = UiFactory.Node(row.transform, "Track");
            Image trackImage = UiFactory.Rounded(track, PeekerColors.SwitchOffBg, 5);
            trackImage.raycastTarget = false;
            UiFactory.Fixed(track, 24f, 12f);

            Image knob = UiFactory.Glyph(track.transform, "Knob", UiFactory.CircleSprite(),
                PeekerColors.SwitchOffKnob, 10f);
            RectTransform knobRect = knob.rectTransform;
            knobRect.anchorMin = new Vector2(0f, 0.5f);
            knobRect.anchorMax = new Vector2(0f, 0.5f);
            knobRect.pivot = new Vector2(0f, 0.5f);
            knobRect.sizeDelta = new Vector2(10f, 10f);

            void Refresh()
            {
                bool on = setting.BoxedValue is bool b && b;
                trackImage.color = on ? PeekerColors.Accent : PeekerColors.SwitchOffBg;
                knob.color = on ? PeekerColors.SwitchOnKnob : PeekerColors.SwitchOffKnob;
                knobRect.anchoredPosition = new Vector2(on ? 13f : 1f, 0f);
                ApplyVisibility(row, setting);
            }

            hover.Clicked = () =>
            {
                setting.BoxedValue = !(setting.BoxedValue is bool b && b);
                Refresh();
            };

            Refresh();
            return Refresh;
        }

        // ---- enum / combo --------------------------------------------------

        private static Action BuildEnum(Transform parent, Setting setting)
        {
            List<object> options = ResolveOptions(setting);
            GameObject row = SimpleRow(parent, setting, out HoverElement hover);

            // The value reads as a chip so it looks pressable, unlike a bare label.
            var chip = UiFactory.Node(row.transform, "Chip");
            Image chipBg = UiFactory.Rounded(chip, PeekerColors.ButtonBg, RowRadius);
            chipBg.raycastTarget = false;
            UiFactory.HRow(chip, 0, UiFactory.Padding(6, 1, 6, 1), TextAnchor.MiddleCenter, true, true, false, false);

            TextMeshProUGUI value = UiFactory.Text(chip.transform, "Value", "", 9f, PeekerColors.KeybindLabel,
                TextAlignmentOptions.Midline, letterSpacing: 0.05f);

            void Refresh()
            {
                object current = setting.BoxedValue;
                UiFactory.SetText(value, current == null ? "-" : current.ToString().ToUpperInvariant());
                ApplyVisibility(row, setting);
            }

            void Step(int direction)
            {
                if (options.Count == 0) return;

                int i = options.FindIndex(o => Equals(o, setting.BoxedValue));
                if (i < 0) i = 0;                                        // unknown value: start from the top
                int next = ((i + direction) % options.Count + options.Count) % options.Count;

                try { setting.BoxedValue = options[next]; }
                catch (Exception ex) { Plugin.Log?.LogWarning("[Peeker] " + setting.Name + ": " + ex.Message); }
                Refresh();
            }

            hover.WatchColor(chipBg, PeekerColors.ButtonBg, PeekerColors.ButtonHoverBg);
            hover.Clicked = () => Step(1);
            hover.RightClicked = () => Step(-1);

            Refresh();
            return Refresh;
        }

        private static List<object> ResolveOptions(Setting setting)
        {
            if (setting.HasComboBox) return setting.BoxedComboBox.ToList();

            Type t = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;
            if (t.IsEnum) return Enum.GetValues(t).Cast<object>().ToList();

            return new List<object>();
        }

        // ---- color ---------------------------------------------------------

        private static Action BuildColor(Transform parent, Setting setting)
        {
            GameObject row = SimpleRow(parent, setting, out HoverElement hover);

            var swatch = UiFactory.Node(row.transform, "Swatch");
            Image fill = UiFactory.Rounded(swatch, Color.white, RowRadius);
            fill.raycastTarget = false;
            UiFactory.Fixed(swatch, 26f, 11f);
            UiFactory.RoundedOutline(swatch.transform, PeekerColors.SwatchRing, RowRadius);

            void Refresh()
            {
                Color c = ToColor(setting.BoxedValue);
                c.a = 1f;                     // the swatch shows hue, not the setting's alpha
                fill.color = c;
                ApplyVisibility(row, setting);
            }

            void Step(int direction)
            {
                Color[] presets = PeekerColors.DefaultSwatches;
                Color current = ToColor(setting.BoxedValue);

                int i = Array.FindIndex(presets, c => Approximately(c, current));
                if (i < 0) i = 0;
                int next = ((i + direction) % presets.Length + presets.Length) % presets.Length;

                Color picked = presets[next];
                picked.a = current.a;

                Type t = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;
                setting.BoxedValue = t == typeof(Color32) ? (object)(Color32)picked : picked;
                Refresh();
            }

            hover.Clicked = () => Step(1);
            hover.RightClicked = () => Step(-1);

            Refresh();
            return Refresh;
        }

        private static Color ToColor(object boxed)
        {
            if (boxed is Color c) return c;
            if (boxed is Color32 c32) return c32;
            return Color.white;
        }

        private static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        // ---- number --------------------------------------------------------

        private static Action BuildNumber(Transform parent, Setting setting)
        {
            bool integral = IsIntegral(setting.ValueType);

            var row = UiFactory.Node(parent, "S_" + setting.Name);
            Image rowBg = UiFactory.Rounded(row, Color.clear, RowRadius);   // also the drag surface
            UiFactory.VCol(row, 3, UiFactory.Padding(3, 2, 3, 4), TextAnchor.UpperLeft, true, true, true, false);

            var top = UiFactory.Node(row.transform, "Top");
            UiFactory.HRow(top, 4, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(top, height: 12f);

            TextMeshProUGUI label = UiFactory.Text(top.transform, "Label", setting.Name, 9.5f, PeekerColors.MonoDim);
            UiFactory.Flexible(label.gameObject, 1, 0);
            TextMeshProUGUI value = UiFactory.Text(top.transform, "Value", "", 9.5f, PeekerColors.KeybindLabel,
                TextAlignmentOptions.MidlineRight);

            // A 4px bar is too thin for rounded 9-slicing to read, so track and fill are
            // plain quads. At this size the square ends are invisible anyway.
            GameObject track = UiFactory.Panel(row.transform, "Track", PeekerColors.TrackBg);
            track.GetComponent<Image>().raycastTarget = false;
            UiFactory.Fixed(track, height: 4f);

            GameObject fill = UiFactory.Panel(track.transform, "Fill", PeekerColors.Accent);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;

            Image knob = UiFactory.Glyph(track.transform, "Knob", UiFactory.CircleSprite(), PeekerColors.Accent, 8f);
            RectTransform knobRect = knob.rectTransform;
            knobRect.anchorMin = new Vector2(0f, 0.5f);
            knobRect.anchorMax = new Vector2(0f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(8f, 8f);

            var hover = row.AddComponent<HoverElement>();
            hover.WatchColor(rowBg, Color.clear, PeekerColors.RowHoverBg);
            hover.WatchColor(label, PeekerColors.MonoDim, PeekerColors.KeybindLabel);

            void Refresh()
            {
                double v = ToDouble(setting.BoxedValue);
                double min, max;
                Range(setting, integral, out min, out max);
                float t = max > min ? Mathf.Clamp01((float)((v - min) / (max - min))) : 0f;

                // Only write when it moved: assigning anchors dirties the layout, and
                // this refresher runs every frame the tray is open.
                if (!Mathf.Approximately(fillRect.anchorMax.x, t))
                {
                    fillRect.anchorMax = new Vector2(t, 1f);
                    knobRect.anchorMin = new Vector2(t, 0.5f);
                    knobRect.anchorMax = new Vector2(t, 0.5f);
                    knobRect.anchoredPosition = Vector2.zero;
                }

                UiFactory.SetText(value, integral ? v.ToString("0") : v.ToString(Math.Abs(v) >= 10 ? "0.#" : "0.##"));
                ApplyVisibility(row, setting);
            }

            var drag = row.AddComponent<NormalizedDragArea>();
            drag.Changed = (x, _) =>
            {
                double min, max;
                Range(setting, integral, out min, out max);
                double raw = min + x * (max - min);

                try
                {
                    setting.BoxedValue = integral
                        ? (object)Convert.ToInt64(Math.Round(raw))
                        : (object)raw;
                }
                catch (Exception ex) { Plugin.Log?.LogWarning("[Peeker] " + setting.Name + ": " + ex.Message); }

                Refresh();
            };

            Refresh();
            return Refresh;
        }

        private static void Range(Setting setting, bool integral, out double min, out double max)
        {
            min = setting.BoxedMin != null ? Convert.ToDouble(setting.BoxedMin) : 0d;
            max = setting.BoxedMax != null ? Convert.ToDouble(setting.BoxedMax) : (integral ? 100d : 1d);
            if (max <= min) max = min + 1d;
        }

        private static double ToDouble(object boxed)
        {
            try { return Convert.ToDouble(boxed); }
            catch { return 0d; }
        }

        private static bool IsIntegral(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(sbyte) || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort);
        }
    }
}
