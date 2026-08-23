using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Peeker.HUD;
using Peeker.Module;
using Peeker.Settings;
using Peeker.Tracking;
using Peeker.UI;

namespace Peeker
{
    public class Peeker
    {
        public static Peeker Instance { get; private set; }

        public ModuleManager ModuleManager { get; private set; }
        public EntityManager EntityManager { get; private set; }
        public ElementManager ElementManager { get; private set; }
        public NotificationManager NotificationManager { get; private set; }
        public PeekerMenuController Menu { get; set; }

        /// <summary>Shorthand so modules can write Peeker.Entities.</summary>
        public static EntityManager Entities { get { return Instance?.EntityManager; } }

        /// <summary>Shorthand so modules can write Peeker.Hud.Get&lt;WatermarkElement&gt;().</summary>
        public static ElementManager Hud { get { return Instance?.ElementManager; } }

        /// <summary>Shorthand so modules can write Peeker.Notifications.Push(...).</summary>
        public static NotificationManager Notifications { get { return Instance?.NotificationManager; } }

        /// <summary>Shorthand so modules can ignore their hotkeys while the menu is up.</summary>
        public static bool MenuOpen { get { return Instance?.Menu?.IsOpen ?? false; } }

        private ManualLogSource _logger;

        public Peeker(ManualLogSource logger)
        {
            Instance = this;
            _logger = logger;
            EntityManager = new EntityManager();

            // Before ModuleManager: notification modules touch this during
            // construction, and a startToggled one would Attach immediately.
            NotificationManager = new NotificationManager(logger);

            ModuleManager = new ModuleManager(logger);
            ElementManager = new ElementManager(logger);

            // Restore saved toggles / keybinds / setting values before anything
            // reads them. Missing or malformed files leave the defaults alone.
            PeekerConfig.Load(ModuleManager, logger);

            // The menu controller is NOT created here. Awake runs before the scene is
            // live, so a GameObject created now never joins the active update loop and
            // its Update/LateUpdate are never pumped. Plugin.Start calls InitMenu().
        }

        /// <summary>
        /// Creates the menu controller. Must be called from Start(), not Awake():
        /// by Start the scene is loaded and stable, so the new GameObject is properly
        /// registered and actually receives MonoBehaviour callbacks.
        /// </summary>
        public void InitMenu()
        {
            if (Menu != null) return;

            Menu = PeekerMenuController.Create(ModuleManager);
            if (Menu == null) return;

            Menu.SaveConfigRequested += () =>
            {
                bool saved = PeekerConfig.Save(ModuleManager, _logger);
                Menu.FlashStatus(saved ? "CONFIG SAVED · " + PeekerConfig.FileName : "CONFIG SAVE FAILED · SEE LOG");
            };
        }

        public void Update()
        {
            Guard("EntityManager", EntityManager.Update);        // must run before modules
            Guard("ModuleManager", ModuleManager.Update);
            Guard("NotificationManager", NotificationManager.Update);  // after modules so this frame's pushes can show
            Guard("ElementManager", ElementManager.Update);
        }

        public void OnGUI()
        {
            Guard("ModuleManager.OnGUI", ModuleManager.OnGUI);
            Guard("ElementManager.OnGUI", ElementManager.OnGUI);       // HUD draws last so it sits on top
        }

        // Names of subsystems that have already thrown, so a per-frame failure logs
        // once instead of filling the log with the same stack 60 times a second.
        private readonly HashSet<string> _reportedFailures = new HashSet<string>();

        /// <summary>
        /// Runs one subsystem in isolation. Without this a throw anywhere stops every
        /// later subsystem for that frame, and the log says nothing about which one
        /// actually broke.
        /// </summary>
        private void Guard(string subsystem, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                if (_reportedFailures.Add(subsystem))
                    Plugin.Log?.LogError("[Peeker] " + subsystem + " threw (further copies suppressed): " + ex);
            }
        }
    }
}