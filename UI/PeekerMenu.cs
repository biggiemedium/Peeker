using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Peeker.Module;
using Peeker.UI.Internal;

namespace Peeker.UI
{
    /// <summary>
    /// Bare-bones cheat-menu layout: one collapsible column per category, each listing
    /// its modules. Left-click a module toggles it; right-click drops its settings down
    /// underneath. No tabs, no detail panel, no popovers.
    /// </summary>
    public class PeekerMenu
    {
        private const float ColumnWidth = 186f;
        private const float ModuleRowHeight = 21f;

        public Action CloseRequested;
        public Action HudEditorRequested;
        public Action SaveConfigRequested;

        public GameObject Root { get; }
        public bool IsCapturing => _capturing != null;

        private readonly ModuleManager _manager;
        private readonly List<ModuleView> _views = new List<ModuleView>();
        private readonly TextMeshProUGUI _hint;

        private string _baseHint = "";
        private float _flashUntil;
        private ModuleView _capturing;

        private class ModuleView
        {
            public Module.Module Module;
            public Image RowBackground;
            public HoverElement RowHover;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Key;
            public TextMeshProUGUI Bind;
            public GameObject Dropdown;
            public readonly List<Action> Refreshers = new List<Action>();
        }

        public static PeekerMenu Build(Transform parent, Canvas canvas, ModuleManager manager)
        {
            var root = UiFactory.Node(parent, "PeekerMenu");
            UiFactory.StretchAll(root);
            // No background image on the root: clicks outside the columns fall through
            // to the game instead of being swallowed by an invisible full-screen panel.
            UiFactory.VCol(root, 6, UiFactory.Padding(14, 14, 14, 14), TextAnchor.UpperLeft, true, true, false, false);

            var topBar = UiFactory.Node(root.transform, "TopBar");
            UiFactory.HRow(topBar, 14, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(topBar, height: 16f);

            var hint = UiFactory.Text(topBar.transform, "Hint", "", 10, PeekerColors.MonoDim, letterSpacing: 0.08f);

            var saveButton = UiFactory.Panel(topBar.transform, "Save", Color.clear);
            UiFactory.HRow(saveButton, 0, UiFactory.Padding(6, 1, 6, 1), TextAnchor.MiddleCenter, true, true, false, false);
            var saveLabel = UiFactory.Text(saveButton.transform, "Label", "SAVE CONFIG", 10, PeekerColors.Accent,
                TextAlignmentOptions.Midline, letterSpacing: 0.08f);
            var saveHover = saveButton.AddComponent<HoverElement>();
            saveHover.WatchColor(saveLabel, PeekerColors.Accent, PeekerColors.AccentHover);

            var columns = UiFactory.Node(root.transform, "Columns");
            UiFactory.HRow(columns, 6, null, TextAnchor.UpperLeft, true, true, false, false);

            var menu = new PeekerMenu(root, manager, hint);
            saveHover.Clicked = () => menu.SaveConfigRequested?.Invoke();

            foreach (ModuleCategory category in Enum.GetValues(typeof(ModuleCategory)))
                menu.BuildColumn(columns.transform, category);

            root.AddComponent<KeyCatcher>().Owner = menu;
            menu.Sync();
            return menu;
        }

        private PeekerMenu(GameObject root, ModuleManager manager, TextMeshProUGUI hint)
        {
            Root = root;
            _manager = manager;
            _hint = hint;
        }

        // ---- construction ---------------------------------------------------

        private void BuildColumn(Transform parent, ModuleCategory category)
        {
            var column = UiFactory.Panel(parent, "Col_" + category, PeekerColors.WindowBg);
            UiFactory.Fixed(column, width: ColumnWidth);
            UiFactory.VCol(column, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var header = UiFactory.Panel(column.transform, "Header", PeekerColors.HeaderBg);
            UiFactory.HRow(header, 6, UiFactory.Padding(8, 0, 8, 0), TextAnchor.MiddleLeft, true, true, false, true);
            UiFactory.Fixed(header, height: 22f);

            var arrow = UiFactory.Text(header.transform, "Arrow", "-", 11, PeekerColors.Accent);
            UiFactory.Fixed(arrow.gameObject, width: 8f);
            var title = UiFactory.Text(header.transform, "Title", category.ToString().ToUpperInvariant(), 11,
                PeekerColors.TitleText, letterSpacing: 0.1f);
            UiFactory.Flexible(title.gameObject, 1, 0);

            var body = UiFactory.Node(column.transform, "Body");
            UiFactory.VCol(body, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var headerHover = header.AddComponent<HoverElement>();
            headerHover.WatchColor(header.GetComponent<Image>(), PeekerColors.HeaderBg, PeekerColors.TabHoverBg);
            headerHover.Clicked = () =>
            {
                bool open = !body.activeSelf;
                body.SetActive(open);
                UiFactory.SetText(arrow, open ? "-" : "+");
            };

            List<Module.Module> modules = _manager.GetModulesByCategory(category);
            if (modules.Count == 0)
            {
                var empty = UiFactory.Node(body.transform, "Empty");
                UiFactory.HRow(empty, 0, UiFactory.Padding(10, 3, 10, 3), TextAnchor.MiddleLeft, true, true, true, false);
                UiFactory.Text(empty.transform, "Label", "(none)", 10, PeekerColors.EmptyHint);
            }

            foreach (Module.Module module in modules)
                BuildModule(body.transform, module);

            UiFactory.AddBorder(column.transform, PeekerColors.WindowBorder, 1f);
        }

        private void BuildModule(Transform parent, Module.Module module)
        {
            var wrapper = UiFactory.Node(parent, "Mod_" + module.Name);
            UiFactory.VCol(wrapper, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            var row = UiFactory.Panel(wrapper.transform, "Row", Color.clear);
            UiFactory.HRow(row, 4, UiFactory.Padding(10, 0, 8, 0), TextAnchor.MiddleLeft, true, true, false, true);
            UiFactory.Fixed(row, height: ModuleRowHeight);

            var name = UiFactory.Text(row.transform, "Name", module.Name, 12, PeekerColors.NameOff);
            UiFactory.Flexible(name.gameObject, 1, 0);

            var key = UiFactory.Text(row.transform, "Key", "", 9, PeekerColors.MonoDim, TextAlignmentOptions.MidlineRight);
            UiFactory.Preferred(key.gameObject, width: 44f);

            var dropdown = UiFactory.Panel(wrapper.transform, "Dropdown", PeekerColors.SidebarBg);
            UiFactory.VCol(dropdown, 0, UiFactory.Padding(14, 3, 8, 5), TextAnchor.UpperLeft, true, true, true, false);
            dropdown.SetActive(false);

            var view = new ModuleView
            {
                Module = module,
                RowBackground = row.GetComponent<Image>(),
                Name = name,
                Key = key,
                Dropdown = dropdown,
            };

            view.Bind = BuildBindRow(dropdown.transform, view);

            foreach (Settings.Setting setting in module.Settings)
                view.Refreshers.Add(PeekerSettingRow.Build(dropdown.transform, setting));

            if (module.Settings.Count == 0)
                UiFactory.Text(dropdown.transform, "NoSettings", "no settings", 9, PeekerColors.EmptyHint);

            var hover = row.AddComponent<HoverElement>();
            hover.WatchColor(view.RowBackground, Color.clear, PeekerColors.RowHoverBg);
            hover.Clicked = () => { module.Toggle(); Sync(); };
            hover.RightClicked = () =>
            {
                dropdown.SetActive(!dropdown.activeSelf);
                if (dropdown.activeSelf) Sync();
            };
            view.RowHover = hover;

            _views.Add(view);
        }

        /// <summary>The "bind" line at the top of a module's dropdown.</summary>
        private TextMeshProUGUI BuildBindRow(Transform parent, ModuleView view)
        {
            var row = UiFactory.Panel(parent, "Bind", Color.clear);
            UiFactory.HRow(row, 4, UiFactory.Padding(0, 2, 0, 2), TextAnchor.MiddleLeft, true, true, false, true);
            UiFactory.Fixed(row, height: 17f);

            var label = UiFactory.Text(row.transform, "Label", "bind", 10, PeekerColors.MonoDim);
            UiFactory.Flexible(label.gameObject, 1, 0);
            var value = UiFactory.Text(row.transform, "Value", "", 10, PeekerColors.KeybindLabel,
                TextAlignmentOptions.MidlineRight);

            var hover = row.AddComponent<HoverElement>();
            hover.WatchColor(row.GetComponent<Image>(), Color.clear, PeekerColors.RowHoverBg);
            hover.Clicked = () =>
            {
                _capturing = view;
                UiFactory.SetText(value, "press a key");
            };

            return value;
        }

        // ---- state -----------------------------------------------------------

        /// <summary>Cheap resync of every visible label. Safe to call each frame.</summary>
        public void Sync()
        {
            foreach (ModuleView view in _views)
            {
                bool on = view.Module.Toggled;
                view.Name.color = on ? PeekerColors.Accent : PeekerColors.NameOff;
                UiFactory.SetText(view.Key, KeyLabel(view.Module.Keybind));

                if (view == _capturing) continue;   // shows "press a key" until it resolves
                UiFactory.SetText(view.Bind, KeyLabel(view.Module.Keybind));

                if (!view.Dropdown.activeSelf) continue;
                foreach (Action refresh in view.Refreshers) refresh();
            }
        }

        public void RefreshAll() => Sync();

        public void Tick()
        {
            if (_flashUntil > 0f && Time.unscaledTime >= _flashUntil)
            {
                _flashUntil = 0f;
                UiFactory.SetText(_hint, _baseHint);
            }

            Sync();
        }

        public void SetToggleKeyHint(string keyName)
        {
            _baseHint = keyName.ToUpperInvariant() +
                        " MENU   |   LEFT-CLICK TOGGLE   |   RIGHT-CLICK SETTINGS   |   ESC CLOSE";
            if (_flashUntil <= 0f) UiFactory.SetText(_hint, _baseHint);
        }

        public void FlashStatus(string message, float seconds = 2.5f)
        {
            UiFactory.SetText(_hint, message.ToUpperInvariant());
            _flashUntil = Time.unscaledTime + seconds;
        }

        // ---- keybind capture --------------------------------------------------

        public void AbortCapture()
        {
            if (_capturing == null) return;
            ModuleView view = _capturing;
            _capturing = null;
            UiFactory.SetText(view.Bind, KeyLabel(view.Module.Keybind));
        }

        private void CaptureKey(Key key)
        {
            if (_capturing == null) return;

            ModuleView view = _capturing;
            _capturing = null;
            view.Module.SetKeybind(key);
            UiFactory.SetText(view.Bind, KeyLabel(key));
            Sync();
        }

        private static string KeyLabel(Key key) => key == Key.None ? "-" : key.ToString();

        /// <summary>Polls the keyboard while a bind row is waiting. Escape clears the binding.</summary>
        private class KeyCatcher : MonoBehaviour
        {
            public PeekerMenu Owner;

            private void Update()
            {
                if (Owner == null || Owner._capturing == null) return;

                Keyboard kb = Keyboard.current;
                if (kb == null) return;

                if (kb.escapeKey.wasPressedThisFrame)
                {
                    Owner.CaptureKey(Key.None);
                    return;
                }

                // allKeys, not Enum.GetValues(typeof(Key)) — the latter includes
                // sentinels that Keyboard's indexer throws on.
                foreach (KeyControl control in kb.allKeys)
                {
                    if (control == null || !control.wasPressedThisFrame) continue;
                    Owner.CaptureKey(control.keyCode);
                    return;
                }
            }
        }
    }
}
