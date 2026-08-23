using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// Press-and-drag surface that reports the pointer's position inside its own
    /// RectTransform as a 0..1/0..1 ratio for the whole gesture (down through drag).
    /// Mirrors the "one drag handler covers slider, SV field, hue/alpha strips"
    /// pattern from the design doc.
    /// </summary>
    public class NormalizedDragArea : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        /// <summary>x, y in 0..1, y measured top-to-bottom (top = 0) to match on-screen layout.</summary>
        public Action<float, float> Changed;

        public void OnPointerDown(PointerEventData eventData) => Report(eventData);

        public void OnDrag(PointerEventData eventData) => Report(eventData);

        private void Report(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect r = rect.rect;
            float x = Mathf.Clamp01((local.x - r.x) / r.width);
            float y = Mathf.Clamp01(1f - (local.y - r.y) / r.height);
            Changed?.Invoke(x, y);
        }
    }
}
