using System;
using UnityEngine;

namespace Peeker.HUD
{
    /// <summary>
    /// Owns position, size and drag state for a single element.
    /// Knows nothing about rendering or input polling — the manager feeds it mouse
    /// positions that are already in GUI space (origin top-left, y grows downward).
    /// </summary>
    public class ElementPosition
    {
        /// <summary>
        /// Override this if you scale the GUI with GUI.matrix; clamping must use the
        /// same coordinate space the mouse is reported in.
        /// </summary>
        public static Func<Vector2> ScreenSizeProvider = () => new Vector2(Screen.width, Screen.height);

        private readonly Vector2 _defaultPosition;
        private Vector2 _position;
        private Vector2 _size;
        private Vector2 _dragOffset;

        public bool Locked { get; set; }
        public bool IsDragging { get; private set; }

        public Vector2 Position
        {
            get { return _position; }
            set { _position = Clamp(value); }
        }

        public Vector2 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public Rect Bounds
        {
            get { return new Rect(_position.x, _position.y, _size.x, _size.y); }
        }

        public ElementPosition(Vector2 defaultPosition, Vector2 initialSize, bool locked)
        {
            _defaultPosition = defaultPosition;
            _position = defaultPosition;
            _size = initialSize;
            Locked = locked;
        }

        public bool Contains(Vector2 point)
        {
            return Bounds.Contains(point);
        }

        public void BeginDrag(Vector2 mouse)
        {
            if (Locked) return;
            IsDragging = true;
            _dragOffset = mouse - _position;
        }

        public void Drag(Vector2 mouse)
        {
            if (!IsDragging || Locked) return;
            Position = mouse - _dragOffset;
        }

        public void EndDrag()
        {
            IsDragging = false;
        }

        /// <summary>Re-clamp after a resolution change or a size change.</summary>
        public void ClampToScreen()
        {
            _position = Clamp(_position);
        }

        public void Reset()
        {
            IsDragging = false;
            Position = _defaultPosition;
        }

        private Vector2 Clamp(Vector2 p)
        {
            Vector2 screen = ScreenSizeProvider();
            return new Vector2(
                Mathf.Clamp(p.x, 0f, Mathf.Max(0f, screen.x - _size.x)),
                Mathf.Clamp(p.y, 0f, Mathf.Max(0f, screen.y - _size.y)));
        }
    }
}