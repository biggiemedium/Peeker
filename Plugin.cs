using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

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
            Logger.LogWarning($"=== PEEKER BUILD {DateTime.Now:HH:mm:ss} ===");

            // Without this, a throw anywhere in construction leaves Peeker null and
            // every later Update/OnGUI throws a NullReference instead of the real
            // cause — which is exactly how a startup failure ends up looking like
            // "the menu just doesn't open".
            try
            {
                Peeker = new Peeker(Logger);
            }
            catch (Exception ex)
            {
                Logger.LogError("Peeker failed to initialise — the mod is inert this session: " + ex);
            }
        }

        // The menu controller is created HERE, not in Awake. Awake runs before the
        // scene is fully loaded: a GameObject created then is not registered in the
        // active scene, so Unity never pumps its Update — which is why the hotkey
        // did nothing. InitMenu is idempotent, so this stays a single controller.
        private void Start()
        {
            if (Peeker == null) return;
            try { Peeker.InitMenu(); }
            catch (Exception ex) { Logger.LogError("Peeker menu init failed: " + ex); }
        }

        private int _updateTicks;
        private float _nextHeartbeat;

        private void Update()
        {
            // Proves whether Unity pumps the plugin at all. If neither this nor
            // PeekerMenuController's heartbeat ever appears, no Peeker code is running
            // per-frame and the hotkeys could never fire, whatever the key handling does.
            if (_updateTicks++ == 0)
            {
                Logger.LogInfo("[Peeker] Plugin.Update pump is alive (Peeker=" + (Peeker != null) + ").");
                _nextHeartbeat = UnityEngine.Time.unscaledTime + 15f;
            }
            else if (UnityEngine.Time.unscaledTime >= _nextHeartbeat && UnityEngine.Time.unscaledTime < 90f)
            {
                _nextHeartbeat = UnityEngine.Time.unscaledTime + 15f;
                Logger.LogInfo("[Peeker] Plugin.Update heartbeat, ticks=" + _updateTicks);
            }

            if (Peeker == null) return;
            try { Peeker.Update(); }
            catch (Exception ex) { Logger.LogError(ex); }
        }

        private void OnGUI()
        {
            if (Peeker == null) return;
            try { Peeker.OnGUI(); }
            catch (Exception ex) { Logger.LogError(ex); }
        }

        private void OnEnable()  => Logger.LogWarning("OnEnable");
        private void OnDisable() => Logger.LogWarning("OnDisable");
        private void OnDestroy() => Logger.LogWarning("OnDestroy");
    }
}
