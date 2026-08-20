using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;

namespace Peeker
{
    [BepInPlugin("dev.px.peeker", "Peeker", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        
        public static Plugin Instance { get; private set; }
        
        private readonly Harmony _harmony = new Harmony("dev.px.peeker");

        // Our main class where our modules will be loaded in
        public Peeker Peeker { get; private set; }
        
        internal static ManualLogSource Log;

        private void Awake()
        {
            Instance = this;
            Log = Logger; // expose it statically
            Logger.LogWarning($"=== PEEKER BUILD {System.DateTime.Now:HH:mm:ss} ===");
            Peeker = new Peeker(Logger);
        }

        private int _f;
        private void Update()
        {
            if (_f++ % 300 == 0)
            {
                Logger.LogInfo($"tick {_f} kb={(Keyboard.current == null ? "NULL" : "ok")}");
            }
                
            try { Peeker.Update(); }
            catch (System.Exception ex) { Logger.LogError(ex); }
        }
        
        private void OnEnable()  => Logger.LogWarning("OnEnable");
        private void OnDisable() => Logger.LogWarning("OnDisable");
        private void OnDestroy() => Logger.LogWarning("OnDestroy");
    }
}