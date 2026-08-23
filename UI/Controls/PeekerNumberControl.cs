using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Peeker.Settings;
using Peeker.UI.Internal;

namespace Peeker.UI.Controls
{
    /// <summary>Draggable slider + numeric readout bound to a numeric <see cref="Setting"/>.</summary>
    public static class PeekerNumberControl
    {
        public static void Build(Transform parent, Setting setting, Action onChanged)
        {
            bool isIntegral = IsIntegral(setting.ValueType);

            var row = new GameObject("NumberRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            UiFactory.HRow(row, 18, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Flexible(row, 1, 0);
            UiFactory.Fixed(row, height: 26f);

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(row.transform, false);
            UiFactory.Flexible(track, 1, 0);
            UiFactory.Fixed(track, height: 26f);

            var trackLine = UiFactory.Panel(track.transform, "TrackLine", PeekerColors.TrackBg);
            var lineRect = (RectTransform)trackLine.transform;
            lineRect.anchorMin = new Vector2(0, 0.5f);
            lineRect.anchorMax = new Vector2(1, 0.5f);
            lineRect.sizeDelta = new Vector2(0, 2f);
            lineRect.anchoredPosition = Vector2.zero;

            var fill = UiFactory.Panel(trackLine.transform, "Fill", PeekerColors.Accent);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handle = UiFactory.Panel(trackLine.transform, "Handle", PeekerColors.Accent);
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(10, 12);

            var readout = UiFactory.Text(row.transform, "Readout", "", 14, PeekerColors.TitleText, TextAlignmentOptions.MidlineRight);
            UiFactory.Fixed(readout.gameObject, width: 78f);
            UiFactory.AddBorder(readout.gameObject.transform, PeekerColors.ReadoutBorder, 1f, UiFactory.Sides.Bottom);

            var drag = track.AddComponent<NormalizedDragArea>();

            void Refresh()
            {
                double value = Convert.ToDouble(setting.BoxedValue);
                (double min, double max) = EffectiveRange(setting, value, isIntegral);
                double t = max > min ? (value - min) / (max - min) : 0;
                t = Math.Min(1, Math.Max(0, t));

                fillRect.anchorMax = new Vector2((float)t, 1);
                handleRect.anchorMin = handleRect.anchorMax = new Vector2((float)t, 0.5f);
                readout.text = FormatValue(value, isIntegral);
            }

            drag.Changed = (x, _) =>
            {
                double value = Convert.ToDouble(setting.BoxedValue);
                (double min, double max) = EffectiveRange(setting, value, isIntegral);
                double raw = min + x * (max - min);
                double stepped = ApplyStep(raw, min, max, isIntegral);
                setting.BoxedValue = isIntegral ? (object)Convert.ToInt64(stepped) : (object)stepped;
                Refresh();
                onChanged?.Invoke();
            };

            Refresh();
        }

        private static bool IsIntegral(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(sbyte) || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort);
        }

        /// <summary>
        /// Real range when the setting declares one; otherwise a synthesized display
        /// range so the slider still behaves (the base Setting API has no concept of
        /// an unranged-but-slidable number — this keeps the control usable anyway).
        /// </summary>
        private static (double min, double max) EffectiveRange(Setting setting, double value, bool isIntegral)
        {
            double min = setting.BoxedMin != null ? Convert.ToDouble(setting.BoxedMin) : Math.Min(0, value);
            double max = setting.BoxedMax != null ? Convert.ToDouble(setting.BoxedMax) : Math.Max(isIntegral ? 10 : 1, value * 2);
            if (max <= min) max = min + (isIntegral ? 1 : 1.0);
            return (min, max);
        }

        private static double ApplyStep(double raw, double min, double max, bool isIntegral)
        {
            if (isIntegral)
                return Math.Round(raw);

            double step = NiceStep(max - min);
            double stepped = Math.Round((raw - min) / step) * step + min;
            return Math.Min(max, Math.Max(min, stepped));
        }

        private static double NiceStep(double range)
        {
            if (range <= 0) return 0.01;
            double step = range / 200.0;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(step)));
            double normalized = step / magnitude;
            double nice = normalized < 1.5 ? 1 : normalized < 3.5 ? 2 : normalized < 7.5 ? 5 : 10;
            return nice * magnitude;
        }

        private static string FormatValue(double value, bool isIntegral)
        {
            if (isIntegral)
                return value.ToString("0");

            int digits = 2;
            double abs = Math.Abs(value);
            if (abs >= 100) digits = 0;
            else if (abs >= 10) digits = 1;
            return value.ToString("F" + digits);
        }
    }
}
