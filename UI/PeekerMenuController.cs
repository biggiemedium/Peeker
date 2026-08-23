using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Peeker.Module;

namespace Peeker.UI
{
    /// <summary>
    /// Drop-in entry point: <c>PeekerMenuController.Create(moduleManager)</c> once
    /// from your plugin's Awake/Start. Owns the canvas, its own EventSystem, the
    /// open/close hotkeys, and cursor lock/visibility while the menu is open.
    /// </summary>
    public class PeekerMenuController : MonoBehaviour
    {
        /// <summary>Open/close key. Change before <see cref="Create"/> to remap.</summary>
        public static Key ToggleKey = Key.RightArrow;

        /// <summary>Second open/close key, so one dead binding can't lock you out.</summary>
        public static Key AltToggleKey = Key.Insert;

        /// <summary>Legacy-Input equivalent of <see cref="ToggleKey"/>, used only if the new Input System is inactive.</summary>
        public static KeyCode LegacyToggleKey = KeyCode.RightArrow;

        /// <summary>
        /// Logs a heartbeat for the first minute so a silent failure is diagnosable:
        /// no heartbeat at all means Unity isn't pumping this component's Update.
        /// </summary>
        public static bool Diagnostics = true;

        /// <summary>Only one controller may exist; <see cref="Create"/> is idempotent.</summary>
        public static PeekerMenuController Instance { get; private set; }

        /// <summary>Fires when "SAVE CONFIG" is clicked. Nothing is persisted by default.</summary>
        public event Action SaveConfigRequested;

        /// <summary>Fires on the HUD-editor link / Right Shift.</summary>
        public event Action HudEditorRequested;

        private ModuleManager _manager;
        private PeekerMenu _menu;
        private GameObject _canvasGo;
        private GameObject _eventSystemGo;
        private EventSystem _eventSystem;
        private EventSystem _previousEventSystem;

        private bool _open;
        private bool _broken;   // Init failed — stop trying every frame
        private bool _warnedNoKeyboard;
        private CursorLockMode _previousLockState;
        private bool _previousCursorVisible;

        public bool IsOpen => _open;

        /// <summary>True while the keybind-capture overlay is swallowing key presses.</summary>
        public bool IsCapturing => !_broken && _open && _menu != null && _menu.IsCapturing;

        public static PeekerMenuController Create(ModuleManager manager)
        {
            if (Instance != null)
            {
                Plugin.Log?.LogInfo("[Peeker] Menu controller already exists — reusing it.");
                return Instance;
            }

            var go = new GameObject("PeekerMenuController");
            DontDestroyOnLoad(go);
            var controller = go.AddComponent<PeekerMenuController>();
            if (controller == null)
            {
                Plugin.Log?.LogError("[Peeker] AddComponent<PeekerMenuController> returned null — the menu cannot run.");
                return null;
            }

            controller._manager = manager;
            Instance = controller;
            Plugin.Log?.LogInfo("[Peeker] Menu controller created (toggle: " + ToggleKey + " or " + AltToggleKey + ").");
            return controller;
        }

        private void Awake()
        {
            Plugin.Log?.LogInfo("[Peeker] Menu controller Awake — GameObject '" + name +
                                "', activeInHierarchy=" + gameObject.activeInHierarchy + ", enabled=" + enabled);
        }

        /// <summary>
        /// The canvas is built the first time the menu is opened rather than at plugin
        /// Awake. At Awake no scene has loaded yet, so TMP's font assets and the game's
        /// screen resolution aren't necessarily resolvable — building then is what
        /// produced an empty window.
        /// </summary>
        private bool EnsureBuilt()
        {
            if (_broken) return false;
            if (_menu != null) return true;

            try
            {
                // Bracketing logs: if the process dies between these two lines with
                // no exception, it died inside Unity/TMP rather than in managed code
                // we can catch.
                Plugin.Log?.LogInfo("[Peeker] Building menu...");

                BuildEventSystem();

                _canvasGo = new GameObject("PeekerCanvas", typeof(RectTransform));
                _canvasGo.transform.SetParent(transform, false);
                var canvas = _canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760; // push well above anything the game draws

                var scaler = _canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var raycaster = _canvasGo.AddComponent<GraphicRaycaster>();
                raycaster.ignoreReversedGraphics = true;
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

                Plugin.Log?.LogInfo("[Peeker] Canvas ready; font = " +
                                    (Internal.UiFactory.ResolveFont()?.name ?? "NONE") + ". Building widgets...");

                _menu = PeekerMenu.Build(_canvasGo.transform, canvas, _manager);
                _menu.CloseRequested += Close;
                _menu.HudEditorRequested += () => HudEditorRequested?.Invoke();
                _menu.SaveConfigRequested += () => SaveConfigRequested?.Invoke();
                _menu.SetToggleKeyHint(ToggleKey.ToString());

                _canvasGo.SetActive(false);
                Plugin.Log?.LogInfo("[Peeker] Menu built OK. Toggle key = " + ToggleKey +
                                    ", canvas sortingOrder = " + canvas.sortingOrder);
                return true;
            }
            catch (Exception ex)
            {
                _broken = true;
                Plugin.Log?.LogError("[Peeker] Building the Peeker menu failed — it will not open: " + ex);
                if (_canvasGo != null) Destroy(_canvasGo);
                _canvasGo = null;
                _menu = null;
                return false;
            }
        }

        /// <summary>
        /// Peeker ships its own EventSystem rather than borrowing the game's. It stays
        /// disabled while the menu is closed (so the game keeps its own UI input), and
        /// is pushed to the front of EventSystem's internal list while open — Unity only
        /// ever pumps <c>EventSystem.current</c>, so sharing the game's would leave our
        /// clicks at the mercy of whatever action map the game currently has enabled.
        /// </summary>
        private void BuildEventSystem()
        {
            try
            {
                // Built inactive so the input module never wakes up without an
                // actions asset, and so the game keeps its own EventSystem until
                // the moment the menu opens.
                _eventSystemGo = new GameObject("PeekerEventSystem");
                _eventSystemGo.transform.SetParent(transform, false);
                _eventSystemGo.SetActive(false);

                _eventSystem = _eventSystemGo.AddComponent<EventSystem>();
                var module = _eventSystemGo.AddComponent<InputSystemUIInputModule>();
                if (module.actionsAsset == null)
                    module.AssignDefaultActions();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[Peeker] Could not build a private EventSystem (" + ex.Message +
                                       "); falling back to the game's.");
                if (_eventSystemGo != null) Destroy(_eventSystemGo);
                _eventSystemGo = null;
                _eventSystem = null;
            }
        }

        private int _updateTicks;
        private float _nextHeartbeat;

        /// <summary>
        /// Proves whether Unity is pumping this component at all. If the log has no
        /// "[Peeker] heartbeat" lines, Update never ran and no key could ever be seen;
        /// if it has them but pressing the key logs nothing, key detection is the fault.
        /// </summary>
        private void Heartbeat(Keyboard kb)
        {
            if (!Diagnostics) return;

            if (_updateTicks++ == 0)
            {
                Plugin.Log?.LogInfo("[Peeker] Update pump is alive. Keyboard.current = " +
                                    (kb == null ? "null" : kb.displayName ?? kb.name));
                _nextHeartbeat = Time.unscaledTime + 10f;
                return;
            }

            if (Time.unscaledTime < _nextHeartbeat) return;
            _nextHeartbeat = Time.unscaledTime + 10f;

            if (Time.unscaledTime > 90f) { Diagnostics = false; return; }   // stop nagging

            Plugin.Log?.LogInfo($"[Peeker] heartbeat: open={_open} broken={_broken} built={_menu != null} " +
                                $"keyboard={(kb != null)} ticks={_updateTicks}");
        }

        private void Update()
        {
            Heartbeat(Keyboard.current);

            if (_broken) return;

            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                if (!_warnedNoKeyboard)
                {
                    _warnedNoKeyboard = true;
                    Plugin.Log?.LogWarning(
                        "[Peeker] Keyboard.current is null — the new Input System isn't active in this game " +
                        "(Player Settings > Active Input Handling is likely 'Input Manager (Old)'). " +
                        "Falling back to legacy UnityEngine.Input for Peeker's hotkeys.");
                }
                LegacyInputFallback();
                return;
            }

            // While rebinding, every key belongs to the capture overlay.
            if (IsCapturing) return;

            if (Pressed(kb, ToggleKey) || Pressed(kb, AltToggleKey))
            {
                Plugin.Log?.LogInfo("[Peeker] Toggle key pressed (menu currently " + (_open ? "open" : "closed") + ").");
                SetOpen(!_open);
            }

            if (_open && kb[Key.Escape].wasPressedThisFrame)
                SetOpen(false);

            if (_open && kb[Key.RightShift].wasPressedThisFrame)
                HudEditorRequested?.Invoke();

            if (_open && _menu != null) _menu.Tick();
        }

        /// <summary>
        /// Keyboard's indexer throws on Key.None and on out-of-range values, and a
        /// throw here would take the whole Update with it every frame.
        /// </summary>
        private static bool Pressed(Keyboard kb, Key key)
        {
            if (key == Key.None) return false;

            try
            {
                KeyControl control = kb[key];
                return control != null && control.wasPressedThisFrame;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[Peeker] Cannot read key " + key + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Used only when the new Input System package isn't active in the host game.
        /// Keeps the same hotkeys via the legacy Input Manager.
        /// </summary>
        private void LegacyInputFallback()
        {
#pragma warning disable CS0618
            if (IsCapturing) return;

            if (Input.GetKeyDown(LegacyToggleKey))
                SetOpen(!_open);

            if (_open && Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);

            if (_open && Input.GetKeyDown(KeyCode.RightShift))
                HudEditorRequested?.Invoke();

            if (_open && _menu != null) _menu.Tick();
#pragma warning restore CS0618
        }

        private void LateUpdate()
        {
            if (_broken || !_open) return;

            // Most games re-lock the cursor in their own Update, so setting it
            // once on open isn't enough.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Same for EventSystem.current: anything the game enables mid-frame could
            // otherwise take the front of the list and starve our canvas of input.
            if (_eventSystem != null && EventSystem.current != _eventSystem)
                EventSystem.current = _eventSystem;
        }

        /// <summary>Shows a transient message on the right of the menu's status bar.</summary>
        public void FlashStatus(string message)
        {
            if (_menu != null) _menu.FlashStatus(message);
        }

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            if (_broken || _open == open) return;
            if (open && !EnsureBuilt()) return;
            if (!open && _menu == null) { _open = false; return; }

            _open = open;

            // Toggling the canvas rather than Root also takes PeekerOverlayLayer
            // with it, so popovers don't linger after a close.
            _canvasGo.SetActive(open);

            if (open)
            {
                _previousLockState = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                _previousEventSystem = EventSystem.current;
                if (_eventSystemGo != null)
                {
                    _eventSystemGo.SetActive(true);
                    EventSystem.current = _eventSystem;
                }
                else if (EventSystem.current == null)
                {
                    Plugin.Log?.LogWarning("[Peeker] No EventSystem available — the menu will draw but not respond to clicks.");
                }

                _menu.RefreshAll();
            }
            else
            {
                _menu.AbortCapture();
                Cursor.lockState = _previousLockState;
                Cursor.visible = _previousCursorVisible;

                if (_eventSystemGo != null)
                    _eventSystemGo.SetActive(false);
                if (_previousEventSystem != null)
                    EventSystem.current = _previousEventSystem;
                _previousEventSystem = null;
            }

            Plugin.Log?.LogInfo("[Peeker] Menu " + (open ? "opened" : "closed") + ".");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
