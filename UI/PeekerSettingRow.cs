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
    /// One setting, one compact row inside a module's dropdown. Deliberately plain:
    /// a label, a value on the right, and click/drag to change it. Returns a refresh
    /// delegate so the owning panel can resync without rebuilding anything.
    /// </summary>
    public static class PeekerSettingRow
    {
        public static Action Build(Transform parent, Setting setting)
        {
            if (setting.IsBoolSetting) return BuildBool(parent, setting);
            if (setting.IsNumberSetting) return BuildNumber(parent, setting);
            if (setting.IsColorSetting) return BuildColor(parent, setting);
            return BuildEnum(parent, setting);
        }

        // ---- shared chrome -------------------------------------------------

        /// <summary>Label on the left, value on the right, whole row clickable.</summary>
        private static GameObject SimpleRow(Transform parent, Setting setting, out TextMeshProUGUI value,
            out HoverElement hover)
        {
            var row = UiFactory.Panel(parent, "S_" + setting.Name, Color.clear);
            UiFactory.HRow(row, 4, UiFactory.Padding(0, 2, 0, 2), TextAnchor.MiddleLeft, true, true, false, true);
            UiFactory.Fixed(row, height: 17f);

            var label = UiFactory.Text(row.transform, "Label", setting.Name, 10, PeekerColors.MonoDim);
            UiFactory.Flexible(label.gameObject, 1, 0);

            value = UiFactory.Text(row.transform, "Value", "", 10, PeekerColors.KeybindLabel,
                TextAlignmentOptions.MidlineRight);

            hover = row.AddComponent<HoverElement>();
            hover.WatchColor(row.GetComponent<Image>(), Color.clear, PeekerColors.RowHoverBg);
            return row;
        }

        // ---- bool ----------------------------------------------------------

        private static Action BuildBool(Transform parent, Setting setting)
        {
            GameObject row = SimpleRow(parent, setting, out TextMeshProUGUI value, out HoverElement hover);

            void Refresh()
            {
                bool on = setting.BoxedValue is bool b && b;
                UiFactory.SetText(value, on ? "ON" : "OFF");
                value.color = on ? PeekerColors.Accent : PeekerColors.NameOff;
                if (row.activeSelf != setting.IsVisible) row.SetActive(setting.IsVisible);
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
            GameObject row = SimpleRow(parent, setting, out TextMeshProUGUI value, out HoverElement hover);

            void Refresh()
            {
                object current = setting.BoxedValue;
                UiFactory.SetText(value, current == null ? "-" : current.ToString().ToUpperInvariant());
                if (row.activeSelf != setting.IsVisible) row.SetActive(setting.IsVisible);
            }

            hover.Clicked = () =>
            {
                if (options.Count == 0) return;

                int i = options.FindIndex(o => Equals(o, setting.BoxedValue));
                object next = options[(i + 1) % options.Count];   // -1 wraps to 0, which is what we want

                try { setting.BoxedValue = next; }
                catch (Exception ex) { Plugin.Log?.LogWarning("[Peeker] " + setting.Name + ": " + ex.Message); }
                Refresh();
            };

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
            GameObject row = SimpleRow(parent, setting, out TextMeshProUGUI value, out HoverElement hover);
            value.gameObject.SetActive(false);   // a swatch reads better than a hex string here

            var swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            swatch.transform.SetParent(row.transform, false);
            UiFactory.Fixed(swatch, 22f, 10f);
            var img = swatch.GetComponent<Image>();
            img.raycastTarget = false;

            void Refresh()
            {
                img.color = ToColor(setting.BoxedValue);
                if (row.activeSelf != setting.IsVisible) row.SetActive(setting.IsVisible);
            }

            // Bare bones: cycle the preset swatches. A real picker can come later.
            hover.Clicked = () =>
            {
                Color[] presets = PeekerColors.DefaultSwatches;
                Color current = ToColor(setting.BoxedValue);

                int i = Array.FindIndex(presets, c => Approximately(c, current));
                Color next = presets[(i + 1) % presets.Length];
                next.a = current.a;

                Type t = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;
                setting.BoxedValue = t == typeof(Color32) ? (object)(Color32)next : next;
                Refresh();
            };

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
            UiFactory.HitArea(row);                     // the whole row is the drag surface
            UiFactory.VCol(row, 1, UiFactory.Padding(0, 2, 0, 3), TextAnchor.UpperLeft, true, true, true, false);

            var top = UiFactory.Node(row.transform, "Top");
            UiFactory.HRow(top, 4, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(top, height: 13f);

            var label = UiFactory.Text(top.transform, "Label", setting.Name, 10, PeekerColors.MonoDim);
            UiFactory.Flexible(label.gameObject, 1, 0);
            var value = UiFactory.Text(top.transform, "Value", "", 10, PeekerColors.KeybindLabel,
                TextAlignmentOptions.MidlineRight);

            var track = UiFactory.Panel(row.transform, "Track", PeekerColors.TrackBg);
            UiFactory.Fixed(track, height: 3f);
            track.GetComponent<Image>().raycastTarget = false;

            var fill = UiFactory.Panel(track.transform, "Fill", PeekerColors.Accent);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;

            void Refresh()
            {
                double v = ToDouble(setting.BoxedValue);
                (double min, double max) = Range(setting, integral);
                float t = max > min ? Mathf.Clamp01((float)((v - min) / (max - min))) : 0f;

                // Only write when it moved: assigning anchors dirties the layout, and
                // this refresher runs every frame the dropdown is open.
                if (!Mathf.Approximately(fillRect.anchorMax.x, t))
                    fillRect.anchorMax = new Vector2(t, 1);

                UiFactory.SetText(value, integral ? v.ToString("0") : v.ToString(Math.Abs(v) >= 10 ? "0.#" : "0.##"));
                if (row.activeSelf != setting.IsVisible) row.SetActive(setting.IsVisible);
            }

            var drag = row.AddComponent<NormalizedDragArea>();
            drag.Changed = (x, _) =>
            {
                (double min, double max) = Range(setting, integral);
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

        private static (double min, double max) Range(Setting setting, bool integral)
        {
            double min = setting.BoxedMin != null ? Convert.ToDouble(setting.BoxedMin) : 0d;
            double max = setting.BoxedMax != null ? Convert.ToDouble(setting.BoxedMax) : (integral ? 100d : 1d);
            if (max <= min) max = min + 1d;
            return (min, max);
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
