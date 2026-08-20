using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module
{
    /// <summary>
    /// Registers and provides lookup for every Module in the project.
    /// This is the single source of truth: add a `Register(new X())` line
    /// below and it's live. Nothing outside this class needs to know
    /// what modules exist.
    /// </summary>
    public class ModuleManager : List<Module>
    {
        private readonly ManualLogSource _logger;

        public ModuleManager(ManualLogSource logger)
        {
            _logger = logger;

            // MODULE REGISTERY !!!!!

            // Visual
            Register(new ESPModule());

            // Movement

            // Combat

            // Exploit

            

            SortModules();
        }

        /// <summary>
        /// Registers a module. Throws on a null module or a duplicate name,
        /// same as the Java version.
        /// </summary>
        public void Register(Module module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module), "Module cannot be null.");

            if (this.Any(m => string.Equals(m.Name, module.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Module already registered: {module.Name}");

            Add(module);
            _logger?.LogInfo($"Registered module: {module.Name}");
        }

        /// <summary>
        /// Development-tagged modules first, everything else alphabetical.
        /// </summary>
        private void SortModules()
        {
            Sort((a, b) =>
            {
                bool aDev = Attribute.IsDefined(a.GetType(), typeof(DevelopmentAttribute));
                bool bDev = Attribute.IsDefined(b.GetType(), typeof(DevelopmentAttribute));

                if (aDev && !bDev) return -1;
                if (!aDev && bDev) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
        }

        public Module GetModuleByName(string name)
        {
            return this.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public List<Module> GetModulesByCategory(ModuleCategory category)
        {
            return this.Where(m => m.Category == category).ToList();
        }

        public T GetModule<T>() where T : Module
        {
            return this.OfType<T>().FirstOrDefault();
        }

        public bool IsModuleToggled<T>() where T : Module
        {
            var module = GetModule<T>();
            return module != null && module.Toggled;
        }

        /// <summary>
        /// Poll for keybind presses. Call once per frame from Plugin.Update().
        /// </summary>
        public void Update()
        {
            var kb = Keyboard.current;
            foreach (var module in this)
            {
                if (module.Toggled) module.Update();
                if (kb == null || module.Keybind == Key.None) continue;
                if (kb[module.Keybind].wasPressedThisFrame)
                {
                    module.Toggle();
                    _logger?.LogInfo($"{module.Name} -> {(module.Toggled ? "ON" : "OFF")}");
                }
            }
        }
    }
}