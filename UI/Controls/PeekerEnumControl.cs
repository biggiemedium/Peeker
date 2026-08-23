using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Peeker.Settings;
using Peeker.UI.Internal;

namespace Peeker.UI.Controls
{
    /// <summary>Segmented option control for enum/combo-box settings.</summary>
    public static class PeekerEnumControl
    {
        public static void Build(Transform parent, Setting setting, Action onChanged)
        {
            var options = ResolveOptions(setting);

            var group = new GameObject("EnumGroup", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            UiFactory.HRow(group, 0, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.AutoSize(group, true, true);
            UiFactory.AddBorder(group.transform, PeekerColors.EnumBorder, 1f);

            var optionGos = new List<(GameObject go, TextMeshProUGUI label, HoverElement hover, object value)>();

            void RefreshAll()
            {
                object current = setting.BoxedValue;
                foreach (var o in optionGos)
                {
                    bool selected = Equals(o.value, current);
                    var img = o.go.GetComponent<UnityEngine.UI.Image>();
                    img.color = selected ? PeekerColors.Accent : Color.clear;
                    o.label.color = selected ? PeekerColors.EnumSelectedText : PeekerColors.EnumUnselected;
                }
            }

            foreach (object option in options)
            {
                var go = new GameObject("Option", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(group.transform, false);
                UiFactory.HRow(go, 0, UiFactory.Padding(16, 7, 16, 7), TextAnchor.MiddleCenter, true, true, false, false);
                UiFactory.AutoSize(go, true, true);

                var label = UiFactory.Text(go.transform, "Label", option.ToString().ToUpperInvariant(), 14,
                    PeekerColors.EnumUnselected, TextAlignmentOptions.Midline, letterSpacing: 0.12f);

                var hover = go.AddComponent<HoverElement>();
                hover.Clicked = () =>
                {
                    setting.BoxedValue = option;
                    RefreshAll();
                    onChanged?.Invoke();
                };

                optionGos.Add((go, label, hover, option));
            }

            RefreshAll();
        }

        private static List<object> ResolveOptions(Setting setting)
        {
            if (setting.HasComboBox)
                return setting.BoxedComboBox.ToList();

            Type t = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;
            if (t.IsEnum)
                return Enum.GetValues(t).Cast<object>().ToList();

            // No declared options and not an enum type — fall back to whatever the
            // current value already is, so the control still renders something.
            return new List<object> { setting.BoxedValue };
        }
    }
}
