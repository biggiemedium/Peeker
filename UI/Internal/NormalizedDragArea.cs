using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// Press-and-drag surface that reports the pointer's position inside its own
    /// RectTransform as a 0..1/0..1 ratio for the whole gesture (down through drag).
    /// Mirrors the "one drag handler covers slider, SV field, hue/alpha strips"
    /// pattern from the design doc.
    ///
    /// Implements the full drag trio so an enclosing ScrollRect can't hijack the
    /// gesture halfway through, and guarantees a raycast target exists — without a
    /// Graphic on this GameObject the EventSystem never delivers a pointer event.
    /// </summary>
    public class NormalizedDragArea : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>x, y in 0..1, y measured top-to-bottom (top = 0) to match on-screen layout.</summary>
        public Action<float, float> Changed;

        private void Awake()
        {
            if (GetComponent<Graphic>() == null)
                UiFactory.HitArea(gameObject);
        }

        public void OnPointerDown(PointerEventData eventData) => Report(eventData);

        public void OnBeginDrag(PointerEventData eventData) => Report(eventData);

        public void OnDrag(PointerEventData eventData) => Report(eventData);

        public void OnEndDrag(PointerEventData eventData) => Report(eventData);

        private void Report(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect r = rect.rect;
            if (r.width <= 0f || r.height <= 0f) return;

            float x = Mathf.Clamp01((local.x - r.x) / r.width);
            float y = Mathf.Clamp01(1f - (local.y - r.y) / r.height);
            Changed?.Invoke(x, y);
        }
    }
}
