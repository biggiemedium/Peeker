using BepInEx.Logging;
using Peeker.Module;

namespace Peeker
{
    public class Peeker
    {
        
        public ModuleManager ModuleManager { get; private set; }

        public Peeker(ManualLogSource logger)
        {
            // ModuleManager registers all modules itself in its constructor.
            ModuleManager = new ModuleManager(logger);
        }

        /// <summary>
        /// Called every frame from Plugin.Update() to drive keybind polling.
        /// </summary>
        public void Update()
        {
            ModuleManager.Update();
        }
    }
}