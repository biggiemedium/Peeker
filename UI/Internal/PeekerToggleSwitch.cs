using System;
using UnityEngine;
using UnityEngine.UI;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// The on/off rail-and-knob switch used for module rows and boolean settings.
    /// Two sizes in the design: 46x20 (settings rows) and 40x18 (compact module rows).
    /// </summary>
    public class PeekerToggleSwitch : MonoBehaviour
    {
        public Action<bool> Changed;

        private Image _track;
        private RectTransform _knobRect;
        private Image _knob;
        private bool _on;
        private float _width;
        private float _height;

        public static PeekerToggleSwitch Create(Transform parent, float width, float height)
        {
            var go = new GameObject("ToggleSwitch", typeof(RectTransform), typeof(Image), typeof(PeekerToggleSwitch));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);
            UiFactory.Fixed(go, width, height);

            var track = go.GetComponent<Image>();
            track.color = Color.clear;

            var border = UiFactory.AddBorder(go.transform, PeekerColors.SwitchOffBorder, 1f);

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            knobGo.transform.SetParent(go.transform, false);
            var knobRect = (RectTransform)knobGo.transform;
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(0, 1);
            knobRect.pivot = new Vector2(0, 1);
            float knobSize = height - 6f;
            knobRect.sizeDelta = new Vector2(knobSize, knobSize);
            knobRect.anchoredPosition = new Vector2(2f, -2f);
            var knob = knobGo.GetComponent<Image>();

            var hover = go.AddComponent<HoverElement>();

            var sw = go.GetComponent<PeekerToggleSwitch>();
            sw._track = track;
            sw._knobRect = knobRect;
            sw._knob = knob;
            sw._width = width;
            sw._height = height;
            sw._borderRef = border;

            hover.Clicked = () => sw.Set(!sw._on);
            return sw;
        }

        private UiFactory.BorderHandle _borderRef;

        public bool On => _on;

        public void Set(bool on, bool notify = true)
        {
            _on = on;
            float knobSize = _height - 6f;
            _track.color = on ? PeekerColors.SwitchOnBg : Color.clear;
            _borderRef.SetColor(on ? PeekerColors.Accent : PeekerColors.SwitchOffBorder);
            _knob.color = on ? PeekerColors.Accent : PeekerColors.SwitchOffKnob;
            _knobRect.anchoredPosition = new Vector2(on ? _width - _height + 1f : 2f, -2f);

            if (notify)
                Changed?.Invoke(on);
        }
    }
}
