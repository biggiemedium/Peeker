using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Peeker.Module;

namespace Peeker.UI
{
    /// <summary>
    /// Drop-in entry point: <c>PeekerMenuController.Create(moduleManager)</c> once
    /// from your plugin's Awake/Start. Owns the canvas, the Insert/Esc/Right-Shift
    /// hotkeys, and cursor lock/visibility while the menu is open.
    /// </summary>
    public class PeekerMenuController : MonoBehaviour
    {
        private PeekerMenu _menu;
        private GameObject _canvasGo;
        private bool _open;
        private bool _broken;   // Init failed — stop trying every frame
        private bool _warnedNoKeyboard;
        private CursorLockMode _previousLockState;
        private bool _previousCursorVisible;

        public bool IsOpen => _open;

        public static PeekerMenuController Create(ModuleManager manager)
        {
            var go = new GameObject("PeekerMenuController");
            Object.DontDestroyOnLoad(go);
            var controller = go.AddComponent<PeekerMenuController>();
            controller.Init(manager);
            return controller;
        }

        private void Init(ModuleManager manager)
        {
            try
            {
                if (EventSystem.current == null)
                {
                    var esGo = new GameObject("PeekerEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    esGo.transform.SetParent(transform, false);
                    Plugin.Log.LogInfo("[Peeker] Created EventSystem (none existed).");
                }
                else
                {
                    Plugin.Log.LogInfo("[Peeker] Reusing existing EventSystem: " + EventSystem.current.name);
                }

                _canvasGo = new GameObject("PeekerCanvas", typeof(RectTransform));
                _canvasGo.transform.SetParent(transform, false);
                var canvas = _canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760; // push well above anything the game draws

                var scaler = _canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                _canvasGo.AddComponent<GraphicRaycaster>();

                _menu = PeekerMenu.Build(_canvasGo.transform, canvas, manager);
                _menu.CloseRequested += Close;
                _menu.HudEditorRequested += OnHudEditorRequested;

                _canvasGo.SetActive(false);
                Plugin.Log.LogInfo("[Peeker] Menu built OK. Canvas sortingOrder=" + canvas.sortingOrder);
            }
            catch (System.Exception ex)
            {
                _broken = true;
                Plugin.Log.LogError("[Peeker] PeekerMenuController.Init failed — menu will not open: " + ex);
            }
        }

        /// <summary>
        /// The design's "HUD LAYOUT →" link / Right Shift hotkey point at a layout-editor
        /// screen that wasn't part of this handoff. Hook your own screen here.
        /// </summary>
        private void OnHudEditorRequested()
        {
            // Peeker.Hud.ToggleEditor();
        }

        private void Update()
        {
            if (_broken) return;

            var kb = Keyboard.current;
            if (kb == null)
            {
                if (!_warnedNoKeyboard)
                {
                    _warnedNoKeyboard = true;
                    Plugin.Log.LogWarning(
                        "[Peeker] Keyboard.current is null — the new Input System isn't active in this game " +
                        "(Player Settings > Active Input Handling is likely 'Input Manager (Old)'). " +
                        "Falling back to legacy UnityEngine.Input for Peeker's hotkeys.");
                }
                LegacyInputFallback();
                return;
            }

            if (_menu.IsCapturing) return;

            if (kb[Key.RightArrow].wasPressedThisFrame)
            {
                Plugin.Log.LogInfo("[Peeker] RightArrow pressed, toggling menu (currently " + (_open ? "open" : "closed") + ").");
                SetOpen(!_open);
            }

            if (_open && kb[Key.Escape].wasPressedThisFrame)
                SetOpen(false);

            if (kb[Key.RightShift].wasPressedThisFrame)
                _menu.HudEditorRequested?.Invoke();
        }

        /// <summary>
        /// Used only when the new Input System package isn't active in the host game.
        /// Keeps the same three hotkeys via the legacy Input Manager.
        /// </summary>
        private void LegacyInputFallback()
        {
#pragma warning disable CS0618
            if (_menu.IsCapturing) return;

            if (Input.GetKeyDown(KeyCode.Insert))
                SetOpen(!_open);

            if (_open && Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);

            if (Input.GetKeyDown(KeyCode.RightShift))
                _menu.HudEditorRequested?.Invoke();
#pragma warning restore CS0618
        }

        private void LateUpdate()
        {
            if (_broken || !_open) return;
            // Most games re-lock the cursor in their own Update, so setting it
            // once on open isn't enough.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            if (_broken || _open == open) return;
            _open = open;

            // Toggling the canvas rather than Root also takes PeekerOverlayLayer
            // with it, so dropdowns don't linger after a close.
            _canvasGo.SetActive(open);

            if (open)
            {
                _previousLockState = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _menu.RefreshAll();
            }
            else
            {
                Cursor.lockState = _previousLockState;
                Cursor.visible = _previousCursorVisible;
            }
        }
    }
}