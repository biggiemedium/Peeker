using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// The design leans on CSS's <c>style-hover</c> for almost every interactive
    /// element (rows, tabs, buttons). This is the uGUI equivalent: swap an
    /// Image/TMP color pair on enter/exit and report clicks (left + right).
    /// </summary>
    public class HoverElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Clicked;
        public Action RightClicked;

        private Graphic[] _watchedGraphics = Array.Empty<Graphic>();
        private Color[] _normalColors = Array.Empty<Color>();
        private Color[] _hoverColors = Array.Empty<Color>();
        private bool _hovering;

        /// <summary>Register a graphic (Image or TMP text) whose color should swap on hover.</summary>
        public void WatchColor(Graphic graphic, Color normal, Color hover)
        {
            int i = _watchedGraphics.Length;
            Array.Resize(ref _watchedGraphics, i + 1);
            Array.Resize(ref _normalColors, i + 1);
            Array.Resize(ref _hoverColors, i + 1);
            _watchedGraphics[i] = graphic;
            _normalColors[i] = normal;
            _hoverColors[i] = hover;
            graphic.color = normal;
        }

        public void SetBaseColors(Color[] normalColors)
        {
            for (int i = 0; i < _watchedGraphics.Length && i < normalColors.Length; i++)
            {
                _normalColors[i] = normalColors[i];
                if (!_hovering)
                    _watchedGraphics[i].color = normalColors[i];
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            Apply(_hoverColors);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            Apply(_normalColors);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke();
            else
                Clicked?.Invoke();
        }

        private void Apply(Color[] colors)
        {
            for (int i = 0; i < _watchedGraphics.Length; i++)
                if (_watchedGraphics[i] != null)
                    _watchedGraphics[i].color = colors[i];
        }
    }
}
