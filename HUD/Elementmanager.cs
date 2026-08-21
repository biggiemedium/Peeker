using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.HUD
{
    /// <summary>
    /// Owns every HUD element: registration, lookup, edit mode and the OnGUI pipeline.
    /// Only one element can be dragged at a time, and hit-testing walks the list in
    /// reverse so the top-most element wins.
    /// </summary>
    public class ElementManager
    {
        private readonly List<HudElement> _elements = new List<HudElement>();
        private readonly ManualLogSource _log;

        private HudElement _dragging;
        private CursorLockMode _previousLockState;
        private bool _previousCursorVisible;

        public bool EditMode { get; private set; }

        /// <summary>Key that opens/closes the layout editor.</summary>
        public Key EditModeKey = Key.RightShift;

        public IReadOnlyList<HudElement> Elements { get { return _elements; } }

        public ElementManager(ManualLogSource log)
        {
            _log = log;
            AutoRegister(Assembly.GetExecutingAssembly());
        }

        // ---------------------------------------------------------------- registry

        public void Register(HudElement element)
        {
            if (element == null) return;
            if (_elements.Any(e => e.GetType() == element.GetType()))
            {
                _log.LogWarning("Duplicate HUD element ignored: " + element.Name);
                return;
            }

            _elements.Add(element);
            _log.LogInfo("Registered HUD element: " + element.Name);
        }

        /// <summary>Instantiates every non-abstract class marked with [HudElement].</summary>
        public void AutoRegister(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(HudElement).IsAssignableFrom(type)) continue;
                if (type.GetCustomAttribute<HudElementAttribute>() == null) continue;

                try
                {
                    Register((HudElement)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    _log.LogError("Failed to construct " + type.Name + ": " + ex);
                }
            }
        }

        public T Get<T>() where T : HudElement
        {
            return _elements.OfType<T>().FirstOrDefault();
        }

        public HudElement Get(string name)
        {
            return _elements.FirstOrDefault(
                e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public void ResetLayout()
        {
            foreach (HudElement element in _elements)
                element.Position.Reset();
        }

        // ------------------------------------------------------------- edit mode

        public void SetEditMode(bool value)
        {
            if (EditMode == value) return;
            EditMode = value;

            if (value)
            {
                _previousLockState = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (_dragging != null) _dragging.Position.EndDrag();
                _dragging = null;
                Cursor.lockState = _previousLockState;
                Cursor.visible = _previousCursorVisible;
                // TODO: persist positions here.
            }
        }

        public void ToggleEditMode()
        {
            SetEditMode(!EditMode);
        }

        // ---------------------------------------------------------------- update

        public void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[EditModeKey].wasPressedThisFrame)
                ToggleEditMode();

            if (EditMode)
            {
                // Many games re-lock the cursor every frame; re-assert ours.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            for (int i = 0; i < _elements.Count; i++)
            {
                HudElement element = _elements[i];
                if (!element.Enabled) continue;

                try { element.Update(); }
                catch (Exception ex) { _log.LogError(element.Name + " update: " + ex); }
            }
        }

        // ----------------------------------------------------------------- OnGUI

        /// <summary>
        /// OnGUI runs several times per frame with different event types.
        /// Layout  -> refresh sizes so hit-testing matches what is drawn.
        /// Mouse*  -> drag arbitration (edit mode only).
        /// Repaint -> the only pass that actually draws.
        /// </summary>
        public void OnGUI()
        {
            Event e = Event.current;

            switch (e.type)
            {
                case EventType.Layout:
                    SyncLayout();
                    break;

                case EventType.MouseDown:
                case EventType.MouseDrag:
                case EventType.MouseUp:
                    if (EditMode && HandleMouse(e)) e.Use();
                    break;

                case EventType.Repaint:
                    Draw();
                    break;
            }
        }

        private void SyncLayout()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                HudElement element = _elements[i];
                if (!element.Enabled && !EditMode) continue;

                try
                {
                    element.Position.Size = element.Measure();
                    element.Position.ClampToScreen();
                }
                catch (Exception ex) { _log.LogError(element.Name + " measure: " + ex); }
            }
        }

        private bool HandleMouse(Event e)
        {
            Vector2 mouse = e.mousePosition;

            if (e.type == EventType.MouseDrag)
            {
                if (_dragging == null) return false;
                _dragging.Position.Drag(mouse);
                return true;
            }

            if (e.type == EventType.MouseUp)
            {
                if (_dragging == null) return false;
                _dragging.Position.EndDrag();
                _dragging = null;
                return true;
            }

            // MouseDown — top-most hovered element wins.
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                HudElement element = _elements[i];
                if (!element.Position.Contains(mouse)) continue;

                if (e.button == 1)
                {
                    element.Toggle();
                }
                else if (e.button == 0 && element.Enabled && !element.Position.Locked)
                {
                    _dragging = element;
                    element.Position.BeginDrag(mouse);
                    BringToFront(element);
                }

                return true; // consumed either way so the game doesn't see the click
            }

            return false;
        }

        private void BringToFront(HudElement element)
        {
            _elements.Remove(element);
            _elements.Add(element);
        }

        private void Draw()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                HudElement element = _elements[i];
                if (!element.Enabled && !EditMode) continue;

                try
                {
                    if (EditMode) DrawEditOverlay(element);

                    if (!element.Enabled) continue; // disabled: outline only

                    if (EditMode) element.RenderDummy();
                    else element.Render();
                }
                catch (Exception ex) { _log.LogError(element.Name + " render: " + ex); }
            }
        }

        private void DrawEditOverlay(HudElement element)
        {
            Rect bounds = element.Position.Bounds;

            Color color;
            if (!element.Enabled) color = new Color(1f, 1f, 1f, 0.20f);
            else if (element.Position.Locked) color = new Color(1f, 0.35f, 0.35f, 0.65f);
            else if (element.Position.IsDragging) color = new Color(0.4f, 0.9f, 1f, 0.95f);
            else color = new Color(1f, 1f, 1f, 0.55f);

            HudRenderer.DrawRect(bounds, new Color(0f, 0f, 0f, 0.25f));
            HudRenderer.DrawBorder(bounds, color);
        }
    }
}