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
    /// Classic click-GUI layout: one small floating panel per category, dropped in a
    /// row across the top-left of the screen. Each panel is draggable by its title bar
    /// and collapses when that bar is clicked. Inside, one compact row per module —
    /// left-click toggles it, the chevron opens its settings tray underneath.
    ///
    /// Nothing here is full-screen. The only screen-sized object is the root, and it
    /// deliberately carries no Image, so clicks in the gaps between panels fall through
    /// to the game instead of being swallowed by the menu.
    /// </summary>
    public class PeekerMenu
    {
        private const float PanelWidth = 172f;
        private const float PanelGap = 10f;
        private const float PanelOriginX = 44f;
        private const float PanelOriginY = 44f;

        private const float HeaderHeight = 26f;
        private const float ModuleRowHeight = 22f;

        private const int PanelRadius = 6;
        private const int RowRadius = 4;

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
            public Image AccentBar;
            public HoverElement RowHover;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Key;
            public TextMeshProUGUI Bind;
            public GameObject Dropdown;
            public RectTransform Chevron;
            public readonly List<Action> Refreshers = new List<Action>();

            /// <summary>Last state pushed into the row's colours, so Sync can skip the no-op case.</summary>
            public bool Painted;
            public bool PaintedOn;
        }

        // ---- construction ------------------------------------------------------

        public static PeekerMenu Build(Transform parent, Canvas canvas, ModuleManager manager)
        {
            var root = UiFactory.Node(parent, "PeekerMenu");
            UiFactory.StretchAll(root);
            // No Image and no layout group on the root: panels place themselves, and
            // the gaps between them stay click-through.

            HoverElement saveHover;
            HoverElement closeHover;
            TextMeshProUGUI hint = BuildStatusBar(root.transform, out saveHover, out closeHover);

            var menu = new PeekerMenu(root, manager, hint);
            saveHover.Clicked = () => menu.SaveConfigRequested?.Invoke();
            closeHover.Clicked = () => menu.CloseRequested?.Invoke();

            int index = 0;
            foreach (ModuleCategory category in Enum.GetValues(typeof(ModuleCategory)))
                menu.BuildPanel(root.transform, category, index++);

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

        /// <summary>Floating pill along the bottom of the screen: brand, hint line, save, close.</summary>
        private static TextMeshProUGUI BuildStatusBar(Transform parent, out HoverElement saveHover,
            out HoverElement closeHover)
        {
            var bar = UiFactory.Node(parent, "StatusBar");
            RectTransform rect = UiFactory.Rect(bar);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 34f);

            // A fitter is legal here: the parent is a plain node, not a layout group.
            UiFactory.AutoSize(bar);
            UiFactory.HRow(bar, 10, UiFactory.Padding(14, 6, 8, 6), TextAnchor.MiddleLeft, true, true, false, false);

            UiFactory.Shadow(bar.transform, PanelRadius, 0.4f);
            UiFactory.RoundedBackground(bar.transform, PeekerColors.HeaderBg, PanelRadius);

            Image dot = UiFactory.Glyph(bar.transform, "Dot", UiFactory.CircleSprite(), PeekerColors.Accent, 6f);
            UiFactory.Fixed(dot.gameObject, 6f, 6f);

            UiFactory.Text(bar.transform, "Brand", "PEEKER", 11f, PeekerColors.TitleText,
                TextAlignmentOptions.Midline, FontStyles.Bold, 0.22f);

            VerticalRule(bar.transform, 12f);

            TextMeshProUGUI hint = UiFactory.Text(bar.transform, "Hint", "", 9.5f, PeekerColors.MonoDim,
                TextAlignmentOptions.Midline, letterSpacing: 0.06f);

            saveHover = BarButton(bar.transform, "Save", "SAVE", PeekerColors.Accent, PeekerColors.AccentHover);
            closeHover = BarButton(bar.transform, "Close", "X", PeekerColors.MonoDim, PeekerColors.TitleText);

            // Drawn last so the stroke sits over the fill and the buttons.
            UiFactory.RoundedOutline(bar.transform, PeekerColors.WindowBorder, PanelRadius);
            return hint;
        }

        private static HoverElement BarButton(Transform parent, string name, string label, Color text, Color textHover)
        {
            var button = UiFactory.Node(parent, name);
            UiFactory.HRow(button, 0, UiFactory.Padding(10, 4, 10, 4), TextAnchor.MiddleCenter, true, true, false, false);
            Image bg = UiFactory.Rounded(button, PeekerColors.ButtonBg, RowRadius);

            TextMeshProUGUI caption = UiFactory.Text(button.transform, "Label", label, 9.5f, text,
                TextAlignmentOptions.Midline, FontStyles.Bold, 0.14f);

            var hover = button.AddComponent<HoverElement>();
            hover.WatchColor(bg, PeekerColors.ButtonBg, PeekerColors.ButtonHoverBg);
            hover.WatchColor(caption, text, textHover);
            return hover;
        }

        /// <summary>One free-floating category panel.</summary>
        private void BuildPanel(Transform parent, ModuleCategory category, int index)
        {
            var panel = UiFactory.Node(parent, "Cat_" + category);
            RectTransform rect = UiFactory.Rect(panel);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);
            rect.anchoredPosition = new Vector2(PanelOriginX + index * (PanelWidth + PanelGap), -PanelOriginY);

            // Width is pinned, height hugs the rows. Legal because the parent is a plain node.
            UiFactory.AutoSize(panel, horizontal: false, vertical: true);
            UiFactory.VCol(panel, 0, null, TextAnchor.UpperLeft, true, true, true, false);

            UiFactory.Shadow(panel.transform, PanelRadius);
            UiFactory.RoundedBackground(panel.transform, PeekerColors.WindowBg, PanelRadius);

            List<Module.Module> modules = _manager.GetModulesByCategory(category);

            // -- title bar --
            var header = UiFactory.Node(panel.transform, "Header");
            Image headerBg = UiFactory.RoundedTop(header, PeekerColors.HeaderBg, PanelRadius);
            UiFactory.HRow(header, 8, UiFactory.Padding(9, 0, 9, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(header, height: HeaderHeight);

            Image arrow = UiFactory.Glyph(header.transform, "Arrow", UiFactory.TriangleSprite(), PeekerColors.Accent, 8f);
            UiFactory.Fixed(arrow.gameObject, 8f, 8f);

            TextMeshProUGUI title = UiFactory.Text(header.transform, "Title", category.ToString().ToUpperInvariant(),
                10.5f, PeekerColors.TitleText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, 0.14f);
            UiFactory.Flexible(title.gameObject, 1, 0);

            UiFactory.Text(header.transform, "Count", modules.Count.ToString(), 9.5f, PeekerColors.EmptyHint,
                TextAlignmentOptions.MidlineRight);

            GameObject divider = HorizontalRule(panel.transform);

            // -- rows --
            var body = UiFactory.Node(panel.transform, "Body");
            UiFactory.VCol(body, 2, UiFactory.Padding(5, 5, 5, 6), TextAnchor.UpperLeft, true, true, true, false);

            if (modules.Count == 0)
            {
                var empty = UiFactory.Node(body.transform, "Empty");
                UiFactory.HRow(empty, 0, UiFactory.Padding(4, 4, 4, 4), TextAnchor.MiddleLeft, true, true, true, false);
                UiFactory.Text(empty.transform, "Label", "no modules", 9.5f, PeekerColors.EmptyHint);
            }

            foreach (Module.Module module in modules)
                BuildModule(body.transform, module);

            UiFactory.RoundedOutline(panel.transform, PeekerColors.WindowBorder, PanelRadius);

            // -- drag to move, click to collapse --
            var headerHover = header.AddComponent<HoverElement>();
            headerHover.WatchColor(headerBg, PeekerColors.HeaderBg, PeekerColors.ButtonHoverBg);

            var drag = header.AddComponent<DragHandle>();
            drag.Target = rect;
            drag.Click = () =>
            {
                bool open = !body.activeSelf;
                body.SetActive(open);
                divider.SetActive(open);
                arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, open ? 0f : 90f);

                // Collapsed, the title bar *is* the whole panel, so it has to round all four
                // corners rather than sit square-bottomed on top of the (now hidden) body.
                headerBg.sprite = open
                    ? UiFactory.RoundedTopSprite(PanelRadius)
                    : UiFactory.RoundedSprite(PanelRadius);
            };
        }

        private void BuildModule(Transform parent, Module.Module module)
        {
            var wrapper = UiFactory.Node(parent, "Mod_" + module.Name);
            UiFactory.VCol(wrapper, 2, null, TextAnchor.UpperLeft, true, true, true, false);

            var row = UiFactory.Node(wrapper.transform, "Row");
            Image rowBg = UiFactory.Rounded(row, Color.clear, RowRadius);
            UiFactory.HRow(row, 6, UiFactory.Padding(7, 0, 5, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(row, height: ModuleRowHeight);

            // 2px amber ticker down the left edge: the fastest "is this on?" read there is.
            GameObject bar = UiFactory.Panel(row.transform, "Bar", PeekerColors.WindowBorder);
            Image barImage = bar.GetComponent<Image>();
            barImage.raycastTarget = false;
            UiFactory.Fixed(bar, 2f, 12f);

            TextMeshProUGUI name = UiFactory.Text(row.transform, "Name", module.Name, 11.5f, PeekerColors.NameOff);
            UiFactory.Flexible(name.gameObject, 1, 0);

            TextMeshProUGUI key = UiFactory.Text(row.transform, "Key", "", 9f, PeekerColors.MonoDim,
                TextAlignmentOptions.MidlineRight);
            UiFactory.Preferred(key.gameObject, width: 40f);

            var dropdown = UiFactory.Node(wrapper.transform, "Dropdown");
            UiFactory.Rounded(dropdown, PeekerColors.SidebarBg, RowRadius);
            UiFactory.VCol(dropdown, 1, UiFactory.Padding(9, 5, 7, 6), TextAnchor.UpperLeft, true, true, true, false);
            dropdown.SetActive(false);

            var view = new ModuleView
            {
                Module = module,
                AccentBar = barImage,
                Name = name,
                Key = key,
                Dropdown = dropdown,
            };

            // The chevron gets its own hit area on top of the row, so opening the tray
            // can never toggle the module by accident.
            var chevron = UiFactory.Node(row.transform, "Chevron");
            UiFactory.HitArea(chevron);
            UiFactory.Fixed(chevron, 14f, 14f);

            Image chevronIcon = UiFactory.Glyph(chevron.transform, "Icon", UiFactory.TriangleSprite(),
                PeekerColors.EmptyHint, 7f);
            RectTransform iconRect = chevronIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localEulerAngles = new Vector3(0f, 0f, 90f);
            view.Chevron = iconRect;

            view.Bind = BuildBindRow(dropdown.transform, view);

            foreach (Settings.Setting setting in module.Settings)
                view.Refreshers.Add(PeekerSettingRow.Build(dropdown.transform, setting));

            if (module.Settings.Count == 0)
                UiFactory.Text(dropdown.transform, "NoSettings", "no settings", 9f, PeekerColors.EmptyHint);

            var hover = row.AddComponent<HoverElement>();
            hover.WatchColor(rowBg, Color.clear, PeekerColors.RowHoverBg);
            hover.Clicked = () => { module.Toggle(); Sync(); };
            hover.RightClicked = () => ToggleDropdown(view);
            view.RowHover = hover;

            var chevronHover = chevron.AddComponent<HoverElement>();
            chevronHover.WatchColor(chevronIcon, PeekerColors.EmptyHint, PeekerColors.TitleText);
            chevronHover.Clicked = () => ToggleDropdown(view);
            chevronHover.RightClicked = () => ToggleDropdown(view);

            _views.Add(view);
        }

        private void ToggleDropdown(ModuleView view)
        {
            bool open = !view.Dropdown.activeSelf;
            view.Dropdown.SetActive(open);
            view.Chevron.localEulerAngles = new Vector3(0f, 0f, open ? 0f : 90f);
            if (open) Sync();
        }

        /// <summary>The "bind" line at the top of a module's settings tray.</summary>
        private TextMeshProUGUI BuildBindRow(Transform parent, ModuleView view)
        {
            var row = UiFactory.Node(parent, "Bind");
            Image rowBg = UiFactory.Rounded(row, Color.clear, 3);
            UiFactory.HRow(row, 4, UiFactory.Padding(3, 2, 3, 2), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(row, height: 16f);

            TextMeshProUGUI label = UiFactory.Text(row.transform, "Label", "bind", 9f, PeekerColors.MonoDim);
            UiFactory.Flexible(label.gameObject, 1, 0);
            TextMeshProUGUI value = UiFactory.Text(row.transform, "Value", "", 9f, PeekerColors.KeybindLabel,
                TextAlignmentOptions.MidlineRight);

            var hover = row.AddComponent<HoverElement>();
            hover.WatchColor(rowBg, Color.clear, PeekerColors.RowHoverBg);
            hover.Clicked = () =>
            {
                _capturing = view;
                UiFactory.SetText(value, "press a key");
                value.color = PeekerColors.Accent;
            };

            return value;
        }

        /// <summary>Thin vertical rule that breaks the status bar into sections.</summary>
        private static void VerticalRule(Transform parent, float height)
        {
            GameObject go = UiFactory.Panel(parent, "Sep", PeekerColors.WindowBorder);
            go.GetComponent<Image>().raycastTarget = false;
            UiFactory.Fixed(go, 1f, height);
        }

        /// <summary>1px rule under a panel's title bar.</summary>
        private static GameObject HorizontalRule(Transform parent)
        {
            GameObject go = UiFactory.Panel(parent, "Divider", PeekerColors.BorderSoft);
            go.GetComponent<Image>().raycastTarget = false;
            UiFactory.Fixed(go, height: 1f);
            return go;
        }

        // ---- state -----------------------------------------------------------

        /// <summary>Cheap resync of every visible label. Safe to call each frame.</summary>
        public void Sync()
        {
            foreach (ModuleView view in _views)
            {
                bool on = view.Module.Toggled;

                // Colours only change when the module does; Sync runs every frame.
                if (!view.Painted || view.PaintedOn != on)
                {
                    view.Painted = true;
                    view.PaintedOn = on;

                    view.Name.color = on ? PeekerColors.TitleText : PeekerColors.NameOff;
                    view.AccentBar.color = on ? PeekerColors.Accent : PeekerColors.WindowBorder;

                    // Re-baseline both hover colours so an enabled row keeps its amber wash.
                    view.RowHover.SetBaseColors(on ? PeekerColors.RowActiveBg : Color.clear);
                    view.RowHover.SetHoverColors(on ? PeekerColors.RowActiveHoverBg : PeekerColors.RowHoverBg);
                }

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
                _hint.color = PeekerColors.MonoDim;
            }

            Sync();
        }

        public void SetToggleKeyHint(string keyName)
        {
            _baseHint = keyName.ToUpperInvariant() +
                        " MENU   |   CLICK TOGGLE   |   ARROW SETTINGS   |   DRAG TITLE MOVE   |   ESC CLOSE";
            if (_flashUntil <= 0f) UiFactory.SetText(_hint, _baseHint);
        }

        public void FlashStatus(string message, float seconds = 2.5f)
        {
            UiFactory.SetText(_hint, message.ToUpperInvariant());
            _hint.color = PeekerColors.Accent;
            _flashUntil = Time.unscaledTime + seconds;
        }

        // ---- keybind capture --------------------------------------------------

        public void AbortCapture()
        {
            if (_capturing == null) return;
            ModuleView view = _capturing;
            _capturing = null;
            view.Bind.color = PeekerColors.KeybindLabel;
            UiFactory.SetText(view.Bind, KeyLabel(view.Module.Keybind));
        }

        private void CaptureKey(Key key)
        {
            if (_capturing == null) return;

            ModuleView view = _capturing;
            _capturing = null;
            view.Module.SetKeybind(key);
            view.Bind.color = PeekerColors.KeybindLabel;
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
