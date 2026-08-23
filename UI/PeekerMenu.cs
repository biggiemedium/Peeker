using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Peeker.Module;
using Peeker.UI.Internal;

namespace Peeker.UI
{
    /// <summary>
    /// Top-level composition: title bar (brand, category tabs, attached status,
    /// close), the sidebar/detail body row, and the bottom hint bar. Owns the
    /// module-manager-facing selection state; <see cref="PeekerMenuController"/>
    /// just opens/closes this.
    /// </summary>
    public class PeekerMenu
    {
        public Action CloseRequested;
        public Action HudEditorRequested;

        public GameObject Root { get; }
        public bool IsCapturing => _detail.IsCapturing;

        private readonly ModuleManager _manager;
        private readonly PeekerModuleSidebar _sidebar;
        private readonly PeekerModuleDetailPanel _detail;
        private readonly TextMeshProUGUI _statusRight;
        private readonly Transform _tabsContainer;
        private readonly List<(ModuleCategory category, GameObject go, UiFactory.BorderHandle underline, TextMeshProUGUI label, TextMeshProUGUI count)> _tabs
            = new List<(ModuleCategory, GameObject, UiFactory.BorderHandle, TextMeshProUGUI, TextMeshProUGUI)>();

        private ModuleCategory _category;
        private Module.Module _selected;

        public static PeekerMenu Build(Transform parent, Canvas canvas, ModuleManager manager)
        {
            var overlayLayer = new GameObject("PeekerOverlayLayer", typeof(RectTransform));
            overlayLayer.transform.SetParent(parent, false);
            UiFactory.StretchAll(overlayLayer);

            var root = UiFactory.Panel(parent, "PeekerWindow", PeekerColors.WindowBg);
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(1180, 662);
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            UiFactory.VCol(root, 0, null, TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.AddBorder(root.transform, PeekerColors.WindowBorder, 1f);
            overlayLayer.transform.SetAsLastSibling();

            // -- Header --------------------------------------------------
            var header = UiFactory.Panel(root.transform, "Header", PeekerColors.HeaderBg);
            UiFactory.HRow(header, 0, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(header, height: 62f);
            UiFactory.AddBorder(header.transform, PeekerColors.WindowBorder, 1f, UiFactory.Sides.Bottom);

            var brand = new GameObject("Brand", typeof(RectTransform));
            brand.transform.SetParent(header.transform, false);
            UiFactory.HRow(brand, 16, UiFactory.Padding(22, 0, 22, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.AutoSize(brand, true, false);

            var logo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logo.transform.SetParent(brand.transform, false);
            logo.GetComponent<Image>().color = Color.clear;
            UiFactory.Fixed(logo, 26f, 26f);
            UiFactory.AddBorder(logo.transform, PeekerColors.Accent, 2f);
            var logoDot = UiFactory.Panel(logo.transform, "Dot", PeekerColors.Accent);
            var logoDotRect = (RectTransform)logoDot.transform;
            logoDotRect.anchorMin = logoDotRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoDotRect.sizeDelta = new Vector2(8, 8);

            var brandText = new GameObject("BrandText", typeof(RectTransform));
            brandText.transform.SetParent(brand.transform, false);
            UiFactory.VCol(brandText, 1, null, TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.Text(brandText.transform, "Title", "PEEKER", 22, PeekerColors.TitleText, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 0.22f);
            UiFactory.Text(brandText.transform, "Version", "v0.4.1 · BEPINEX 5.4.21", 10, PeekerColors.MonoDim,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.14f);

            var tabsContainer = new GameObject("Tabs", typeof(RectTransform));
            tabsContainer.transform.SetParent(header.transform, false);
            UiFactory.HRow(tabsContainer, 2, UiFactory.Padding(6, 0, 6, 0), TextAnchor.LowerLeft, true, true, false, false);
            UiFactory.Flexible(tabsContainer, 1, 0);

            var statusGroup = new GameObject("Status", typeof(RectTransform));
            statusGroup.transform.SetParent(header.transform, false);
            UiFactory.HRow(statusGroup, 10, UiFactory.Padding(0, 0, 20, 0), TextAnchor.MiddleRight, true, true, false, false);
            UiFactory.AutoSize(statusGroup, true, false);

            var attached = new GameObject("Attached", typeof(RectTransform));
            attached.transform.SetParent(statusGroup.transform, false);
            UiFactory.HRow(attached, 8, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.AutoSize(attached, true, true);
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(attached.transform, false);
            UiFactory.Fixed(dot, 7f, 7f);
            var dotImg = dot.GetComponent<Image>();
            dotImg.color = PeekerColors.AttachedGreen;
            MakeCircle(dotImg);
            dot.AddComponent<Pulse>().PeriodSeconds = 2.4f;
            UiFactory.Text(attached.transform, "Label", "ATTACHED", 11, PeekerColors.KeyBadgeText,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.1f);

            var closeButton = new GameObject("Close", typeof(RectTransform), typeof(Image));
            closeButton.transform.SetParent(statusGroup.transform, false);
            closeButton.GetComponent<Image>().color = Color.clear;
            UiFactory.Fixed(closeButton, 30f, 30f);
            var closeBorder = UiFactory.AddBorder(closeButton.transform, PeekerColors.CloseBorder, 1f);
            UiFactory.HRow(closeButton, 0, null, TextAnchor.MiddleCenter, true, true, false, false);
            var closeIcon = UiFactory.Text(closeButton.transform, "Icon", "✕", 16, PeekerColors.CloseIcon, TextAlignmentOptions.Midline);
            var closeHover = closeButton.AddComponent<HoverElement>();
            closeHover.WatchColor(closeIcon, PeekerColors.CloseIcon, PeekerColors.Accent);
            var closeBorderHover = closeButton.AddComponent<BorderHoverBridge>();
            closeBorderHover.Bind(closeBorder, PeekerColors.CloseBorder, PeekerColors.Accent);

            // -- Body --------------------------------------------------
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(root.transform, false);
            UiFactory.HRow(body, 0, null, TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.Fixed(body, height: 560f);

            var sidebar = PeekerModuleSidebar.Build(body.transform);
            var detail = PeekerModuleDetailPanel.Build(body.transform, canvas, overlayLayer.GetComponent<RectTransform>());

            // -- Footer status bar --------------------------------------------------
            var statusBar = UiFactory.Panel(root.transform, "StatusBar", PeekerColors.StatusBarBg);
            UiFactory.HRow(statusBar, 0, UiFactory.Padding(22, 0, 22, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(statusBar, height: 40f);
            UiFactory.AddBorder(statusBar.transform, PeekerColors.WindowBorder, 1f, UiFactory.Sides.Top);

            var hintGroup = new GameObject("Hints", typeof(RectTransform));
            hintGroup.transform.SetParent(statusBar.transform, false);
            UiFactory.HRow(hintGroup, 26, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.AutoSize(hintGroup, true, false);
            UiFactory.Text(hintGroup.transform, "H1", "INSERT — MENU", 10, PeekerColors.MonoDim, letterSpacing: 0.14f);
            UiFactory.Text(hintGroup.transform, "H2", "RIGHT SHIFT — HUD EDITOR", 10, PeekerColors.MonoDim, letterSpacing: 0.14f);
            UiFactory.Text(hintGroup.transform, "H3", "ESC — CLOSE", 10, PeekerColors.MonoDim, letterSpacing: 0.14f);

            UiFactory.Spacer(statusBar.transform);
            var statusRight = UiFactory.Text(statusBar.transform, "StatusRight", "", 10, PeekerColors.MonoDim,
                TextAlignmentOptions.MidlineRight, letterSpacing: 0.14f);

            var menu = new PeekerMenu(root, manager, sidebar, detail, statusRight, tabsContainer.transform);

            closeHover.Clicked = () => menu.CloseRequested?.Invoke();
            sidebar.SelectRequested = m => menu.SelectModule(m);
            sidebar.ToggleRequested = m => { m.Toggle(); menu.RefreshAll(); };
            sidebar.RequestHudEditor = () => menu.HudEditorRequested?.Invoke();

            menu.BuildTabs();
            menu.SelectCategory(menu._tabs.Count > 0 ? menu._tabs[0].category : default, force: true);
            return menu;
        }

        private static Sprite _circleSprite;

        private static void MakeCircle(Image image)
        {
            if (_circleSprite == null)
            {
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        float a = Mathf.Clamp01(r - d);
                        pixels[y * size + x] = new Color(1, 1, 1, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }

            image.sprite = _circleSprite;
        }

        private PeekerMenu(GameObject root, ModuleManager manager, PeekerModuleSidebar sidebar, PeekerModuleDetailPanel detail,
            TextMeshProUGUI statusRight, Transform tabsContainer)
        {
            Root = root;
            _manager = manager;
            _sidebar = sidebar;
            _detail = detail;
            _statusRight = statusRight;
            _tabsContainer = tabsContainer;
        }

        private void BuildTabs()
        {
            foreach (ModuleCategory category in Enum.GetValues(typeof(ModuleCategory)))
            {
                var go = new GameObject("Tab_" + category, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_tabsContainer, false);
                go.GetComponent<Image>().color = Color.clear;
                UiFactory.HRow(go, 9, UiFactory.Padding(20, 0, 20, 0), TextAnchor.MiddleLeft, true, true, false, false);
                UiFactory.Fixed(go, height: 44f);
                UiFactory.AutoSize(go, true, false);
                var underline = UiFactory.AddBorder(go.transform, Color.clear, 2f, UiFactory.Sides.Bottom);

                var label = UiFactory.Text(go.transform, "Label", category.ToString().ToUpperInvariant(), 16,
                    PeekerColors.TabInactive, TextAlignmentOptions.MidlineLeft, letterSpacing: 0.16f);
                var count = UiFactory.Text(go.transform, "Count", "00", 11, PeekerColors.MonoDim,
                    TextAlignmentOptions.MidlineLeft);

                var hover = go.AddComponent<HoverElement>();
                var image = go.GetComponent<Image>();
                hover.Clicked = () => SelectCategory(category);
                hover.WatchColor(image, Color.clear, PeekerColors.TabHoverBg);

                _tabs.Add((category, go, underline, label, count));
            }
        }

        public void SelectCategory(ModuleCategory category, bool force = false)
        {
            if (!force && category.Equals(_category) && _selected != null) return;
            _category = category;

            var inCategory = _manager.GetModulesByCategory(category);
            _selected = inCategory.FirstOrDefault();
            RefreshAll();
        }

        public void SelectModule(Module.Module module)
        {
            _selected = module;
            RefreshAll();
        }

        public void RefreshAll()
        {
            var inCategory = _manager.GetModulesByCategory(_category);

            foreach (var tab in _tabs)
            {
                bool active = tab.category.Equals(_category);
                int n = _manager.GetModulesByCategory(tab.category).Count;
                tab.count.text = n.ToString("00");
                tab.label.color = active ? PeekerColors.TabActive : PeekerColors.TabInactive;
                tab.go.GetComponent<Image>().color = active ? PeekerColors.TabActiveBg : Color.clear;
                tab.underline.SetColor(active ? PeekerColors.Accent : Color.clear);
            }

            _sidebar.Refresh(_category, inCategory, _selected);

            if (_selected != null)
            {
                _detail.ShowModule(_selected);
                _statusRight.text = "SELECTED · " + _selected.Name.ToUpperInvariant();
            }
            else
            {
                _detail.ShowNoSelection();
                _statusRight.text = "";
            }
        }

        private class BorderHoverBridge : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            private UiFactory.BorderHandle _border;
            private Color _normal, _hover;
            public void Bind(UiFactory.BorderHandle border, Color normal, Color hover) { _border = border; _normal = normal; _hover = hover; }
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => _border.SetColor(_hover);
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => _border.SetColor(_normal);
        }
    }
}
