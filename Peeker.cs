using BepInEx.Logging;
using Peeker.Module;
using Peeker.Tracking;

namespace Peeker
{
    public class Peeker
    {
        public static Peeker Instance { get; private set; }

        public ModuleManager ModuleManager { get; private set; }
        public EntityManager EntityManager { get; private set; }

        /// <summary>Shorthand so modules can write Peeker.Entities.</summary>
        public static EntityManager Entities => Instance?.EntityManager;

        public Peeker(ManualLogSource logger)
        {
            Instance = this;
            EntityManager = new EntityManager();
            ModuleManager = new ModuleManager(logger);
        }

        public void Update()
        {
            EntityManager.Update();   // must run before modules
            ModuleManager.Update();
        }

        public void OnGUI() => ModuleManager.OnGUI();
    }
}