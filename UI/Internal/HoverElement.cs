using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// The design leans on CSS's <c>style-hover</c> for almost every interactive
    /// element (rows, tabs, buttons). This is the uGUI equivalent: swap
    /// Image/TMP/border color pairs on enter/exit and report clicks (left + right).
    /// </summary>
    public class HoverElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Clicked;
        public Action RightClicked;

        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly List<Color> _graphicNormal = new List<Color>();
        private readonly List<Color> _graphicHover = new List<Color>();

        private readonly List<UiFactory.BorderHandle> _borders = new List<UiFactory.BorderHandle>();
        private readonly List<Color> _borderNormal = new List<Color>();
        private readonly List<Color> _borderHover = new List<Color>();

        private bool _hovering;

        /// <summary>Register a graphic (Image or TMP text) whose color should swap on hover.</summary>
        public void WatchColor(Graphic graphic, Color normal, Color hover)
        {
            if (graphic == null) return;
            _graphics.Add(graphic);
            _graphicNormal.Add(normal);
            _graphicHover.Add(hover);
            graphic.color = _hovering ? hover : normal;
        }

        /// <summary>Register a <see cref="UiFactory.AddBorder"/> handle whose color should swap on hover.</summary>
        public void WatchBorder(UiFactory.BorderHandle border, Color normal, Color hover)
        {
            if (border == null) return;
            _borders.Add(border);
            _borderNormal.Add(normal);
            _borderHover.Add(hover);
            border.SetColor(_hovering ? hover : normal);
        }

        /// <summary>Re-baselines the non-hovered colors (e.g. a row became "selected").</summary>
        public void SetBaseColors(params Color[] normalColors)
        {
            for (int i = 0; i < _graphics.Count && i < normalColors.Length; i++)
            {
                _graphicNormal[i] = normalColors[i];
                if (!_hovering && _graphics[i] != null)
                    _graphics[i].color = normalColors[i];
            }
        }

        /// <summary>Re-baselines the hovered colors, the counterpart to <see cref="SetBaseColors"/>.</summary>
        public void SetHoverColors(params Color[] hoverColors)
        {
            for (int i = 0; i < _graphics.Count && i < hoverColors.Length; i++)
            {
                _graphicHover[i] = hoverColors[i];
                if (_hovering && _graphics[i] != null)
                    _graphics[i].color = hoverColors[i];
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            Apply(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            Apply(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke();
            else if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke();
        }

        private void OnDisable()
        {
            // Pointer exit is not delivered when the object is hidden underneath the
            // cursor, so a re-shown element would otherwise be stuck in hover colors.
            if (!_hovering) return;
            _hovering = false;
            Apply(false);
        }

        private void Apply(bool hover)
        {
            for (int i = 0; i < _graphics.Count; i++)
                if (_graphics[i] != null)
                    _graphics[i].color = hover ? _graphicHover[i] : _graphicNormal[i];

            for (int i = 0; i < _borders.Count; i++)
                _borders[i].SetColor(hover ? _borderHover[i] : _borderNormal[i]);
        }
    }
}
