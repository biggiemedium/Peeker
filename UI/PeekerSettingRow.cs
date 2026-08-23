using System;
using TMPro;
using UnityEngine;
using Peeker.Settings;
using Peeker.UI.Internal;
using Peeker.UI.Controls;

namespace Peeker.UI
{
    /// <summary>Builds one settings-list row, dispatching to the right control by value type.</summary>
    public static class PeekerSettingRow
    {
        public static GameObject Build(Transform parent, Canvas canvas, RectTransform overlayLayer,
            Setting setting, int index, int total, Action onChanged)
        {
            var row = new GameObject("SettingRow_" + setting.Name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            UiFactory.HRow(row, 30, UiFactory.Padding(0, 15, 0, 15), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(row, height: 64f);
            UiFactory.AddBorder(row.transform, PeekerColors.SettingRowBorder, 1f, UiFactory.Sides.Bottom);

            var labelBlock = new GameObject("LabelBlock", typeof(RectTransform));
            labelBlock.transform.SetParent(row.transform, false);
            UiFactory.VCol(labelBlock, 3, null, TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.Fixed(labelBlock, width: 260f);

            UiFactory.Text(labelBlock.transform, "Label", setting.Name, 17, PeekerColors.SettingLabel);
            string hint = !string.IsNullOrEmpty(setting.Description) ? setting.Description : TypeTag(setting);
            UiFactory.Text(labelBlock.transform, "Hint", hint, 10, PeekerColors.SettingHint,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.06f, wrap: true);

            if (setting.IsNumberSetting)
            {
                var slot = new GameObject("ControlSlot", typeof(RectTransform));
                slot.transform.SetParent(row.transform, false);
                UiFactory.Flexible(slot, 1, 0);
                PeekerNumberControl.Build(slot.transform, setting, onChanged);
            }
            else
            {
                UiFactory.Spacer(row.transform);

                if (setting.IsBoolSetting)
                {
                    var sw = PeekerToggleSwitch.Create(row.transform, 46, 20);
                    sw.Set((bool)setting.BoxedValue, notify: false);
                    sw.Changed = value =>
                    {
                        setting.BoxedValue = value;
                        onChanged?.Invoke();
                    };
                }
                else if (setting.IsColorSetting)
                {
                    bool upward = index >= total / 2f;
                    PeekerColorControl.Build(row.transform, canvas, overlayLayer, setting, upward, onChanged);
                }
                else
                {
                    PeekerEnumControl.Build(row.transform, setting, onChanged);
                }
            }

            row.SetActive(setting.IsVisible);
            return row;
        }

        private static string TypeTag(Setting setting)
        {
            if (setting.IsBoolSetting) return "BooleanSetting";
            if (setting.IsColorSetting) return "ColorSetting";
            if (setting.IsNumberSetting) return "NumberSetting" + (setting.HasRange ? $" · {setting.BoxedMin}–{setting.BoxedMax}" : "");
            Type t = Nullable.GetUnderlyingType(setting.ValueType) ?? setting.ValueType;
            return $"EnumSetting<{t.Name}>";
        }
    }
}
