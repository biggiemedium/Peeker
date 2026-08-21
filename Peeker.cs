using BepInEx.Logging;
using Peeker.HUD;
using Peeker.Module;
using Peeker.Tracking;

namespace Peeker
{
    public class Peeker
    {
        public static Peeker Instance { get; private set; }

        public ModuleManager ModuleManager { get; private set; }
        public EntityManager EntityManager { get; private set; }
        public ElementManager ElementManager { get; private set; }

        /// <summary>Shorthand so modules can write Peeker.Entities.</summary>
        public static EntityManager Entities { get { return Instance?.EntityManager; } }

        /// <summary>Shorthand so modules can write Peeker.Hud.Get&lt;WatermarkElement&gt;().</summary>
        public static ElementManager Hud { get { return Instance?.ElementManager; } }

        public Peeker(ManualLogSource logger)
        {
            Instance = this;
            EntityManager = new EntityManager();
            ModuleManager = new ModuleManager(logger);
            ElementManager = new ElementManager(logger);
        }

        public void Update()
        {
            EntityManager.Update();   // must run before modules
            ModuleManager.Update();
            ElementManager.Update();
        }

        public void OnGUI()
        {
            ModuleManager.OnGUI();
            ElementManager.OnGUI();   // HUD draws last so it sits on top
        }
    }
}