using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Peeker.UI.Internal
{
    /// <summary>
    /// Title-bar behaviour for a floating panel: press and drag to move
    /// <see cref="Target"/>, press and release without moving to fire
    /// <see cref="Click"/> (used for collapse/expand).
    ///
    /// Click detection is done here rather than through <c>IPointerClickHandler</c>
    /// on purpose. uGUI still delivers a pointer *click* after a drag when the same
    /// GameObject is both the press and the drag target, so a plain click handler
    /// would collapse the panel every time you finished dragging it.
    /// </summary>
    public class DragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>The panel to move. Its parent supplies the coordinate space.</summary>
        public RectTransform Target;

        /// <summary>Fired on release when the pointer never left the dead zone.</summary>
        public Action Click;

        private const float DeadZone = 4f;   // squared below

        private Vector2 _pointerStart;
        private Vector2 _targetStart;
        private bool _pressed;
        private bool _moved;

        private void Awake()
        {
            // No Graphic means the EventSystem never routes a pointer here at all.
            if (GetComponent<Graphic>() == null) UiFactory.HitArea(gameObject);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            _moved = false;
            _pressed = TryPoint(eventData, out _pointerStart);
            if (!_pressed) return;

            _targetStart = Target.anchoredPosition;
            Target.SetAsLastSibling();   // uGUI paints in sibling order: touched panel wins overlaps
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pressed || !TryPoint(eventData, out Vector2 now)) return;

            Vector2 delta = now - _pointerStart;
            if (!_moved && delta.sqrMagnitude < DeadZone * DeadZone) return;

            _moved = true;
            Target.anchoredPosition = Clamp(_targetStart + delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            bool wasDrag = _moved;
            _pressed = false;
            _moved = false;
            if (!wasDrag) Click?.Invoke();
        }

        private bool TryPoint(PointerEventData eventData, out Vector2 local)
        {
            local = Vector2.zero;
            if (Target == null) return false;

            var space = Target.parent as RectTransform;
            return space != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                space, eventData.position, eventData.pressEventCamera, out local);
        }

        /// <summary>
        /// Keeps the panel inside its parent. Assumes the top-left anchoring every
        /// Peeker panel uses: x grows right from 0, y grows *down* from 0.
        /// </summary>
        private Vector2 Clamp(Vector2 position)
        {
            var space = Target.parent as RectTransform;
            if (space == null) return position;

            Rect bounds = space.rect;
            Rect self = Target.rect;

            float maxX = Mathf.Max(0f, bounds.width - self.width);
            float minY = Mathf.Min(0f, self.height - bounds.height);

            return new Vector2(Mathf.Clamp(position.x, 0f, maxX), Mathf.Clamp(position.y, minY, 0f));
        }
    }
}
