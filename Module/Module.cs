using System;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace Peeker.Module
{
    public abstract class Module : SettingsHolder
    {
        public string Name { get; private set; }

        public Key Keybind { get; private set; }

        public bool Toggled { get; private set; }
        public ModuleCategory Category { get; private set; }

        // Each module owns its own Harmony instance so its patches can be
        // applied/removed independently of every other module.
        private Harmony _harmony;

        // Override to declare the Harmony patch container class(es) this module
        // owns. They're patched on enable and unpatched on disable.
        protected virtual Type[] PatchTypes => Array.Empty<Type>();

        protected Module(string name, Key keybind, ModuleCategory category, bool startToggled = false)
        {
            Name = name;
            Keybind = keybind;
            Category = category;
            Toggled = startToggled;
        }

        public void Toggle() => SetToggled(!Toggled);

        public void SetToggled(bool value)
        {
            if (Toggled == value)
                return;               // <-- guarantees OnEnabled/OnDisabled run once

            Toggled = value;

            if (Toggled)
            {
                ApplyPatches();
                OnEnabled();
            }
            else
            {
                OnDisabled();
                RemovePatches();
            }
        }

        private void ApplyPatches()
        {
            var types = PatchTypes;
            if (types == null || types.Length == 0)
                return;

            if (_harmony == null)
                _harmony = new Harmony($"dev.px.peeker.module.{Name}");

            foreach (var t in types)
                _harmony.CreateClassProcessor(t).Patch();
        }

        private void RemovePatches()
        {
            _harmony?.UnpatchSelf();
        }

        protected abstract void OnEnabled();
        protected abstract void OnDisabled();

        public virtual void Update() { }

        public virtual void OnGUI() { }
    }
}