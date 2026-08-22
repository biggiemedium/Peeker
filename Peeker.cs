using BepInEx.Logging;
using Peeker.HUD;
using Peeker.Module;
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

        public Peeker(ManualLogSource logger)
        {
            Instance = this;
            EntityManager = new EntityManager();

            // Before ModuleManager: notification modules touch this during
            // construction, and a startToggled one would Attach immediately.
            NotificationManager = new NotificationManager(logger);

            ModuleManager = new ModuleManager(logger);
            ElementManager = new ElementManager(logger);

            // Self-driving MonoBehaviour: nothing to pump from Update.
            Menu = PeekerMenuController.Create(ModuleManager);
        }

        public void Update()
        {
            EntityManager.Update();        // must run before modules
            ModuleManager.Update();
            NotificationManager.Update();  // after modules so this frame's pushes can show
            ElementManager.Update();
        }

        public void OnGUI()
        {
            ModuleManager.OnGUI();
            ElementManager.OnGUI();        // HUD draws last so it sits on top
        }
    }
}