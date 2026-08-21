using System;
using System.Reflection;
using UnityEngine;

namespace Peeker.HUD
{
    /// <summary>
    /// Base class for every HUD element. Coordinates metadata + position and exposes
    /// the lifecycle hooks. Subclasses only implement Measure() and Render().
    /// </summary>
    public abstract class HudElement
    {
        public HudElementAttribute Meta { get; private set; }
        public ElementPosition Position { get; private set; }

        public string Name { get { return Meta.Name; } }
        public string Description { get { return Meta.Description; } }

        private bool _enabled;

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                if (_enabled) OnEnable();
                else OnDisable();
            }
        }

        protected HudElement()
        {
            Meta = GetType().GetCustomAttribute<HudElementAttribute>();
            if (Meta == null)
                throw new InvalidOperationException(
                    "Missing [HudElement] attribute on " + GetType().Name);

            Position = new ElementPosition(
                new Vector2(Meta.X, Meta.Y),
                new Vector2(Meta.Width, Meta.Height),
                Meta.Locked);

            _enabled = Meta.EnabledByDefault;
        }

        /// <summary>
        /// Reported once per frame (Layout pass) so drag bounds always match what is drawn.
        /// Return the pixel size this element will occupy.
        /// </summary>
        public virtual Vector2 Measure()
        {
            return Position.Size;
        }

        /// <summary>Draw the element. Only called during the Repaint pass.</summary>
        public abstract void Render();

        /// <summary>
        /// Drawn instead of Render() while in edit mode — override when the real element
        /// needs live game state that isn't available on the menu screen.
        /// </summary>
        public virtual void RenderDummy()
        {
            Render();
        }

        /// <summary>Per-frame logic. Runs from Peeker.Update, never from OnGUI.</summary>
        public virtual void Update() { }

        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }

        public void Toggle()
        {
            Enabled = !Enabled;
        }
    }
}