using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Peeker.Module;
using Peeker.UI.Internal;

namespace Peeker.UI
{
    /// <summary>
    /// Left column: category header, module rows for the active category (or the
    /// empty state), and the footer hint / HUD-editor link.
    /// </summary>
    public class PeekerModuleSidebar
    {
        public Action<Module.Module> SelectRequested;
        public Action<Module.Module> ToggleRequested;
        public Action RequestHudEditor;

        private readonly RectTransform _content;
        private readonly TextMeshProUGUI _categoryLabel;
        private readonly TextMeshProUGUI _enabledLabel;
        private readonly GameObject _bodyContainer;
        private readonly GameObject _emptyState;
        private readonly ScrollRect _scrollRect;

        public static PeekerModuleSidebar Build(Transform parent)
        {
            var root = UiFactory.Panel(parent, "Sidebar", PeekerColors.SidebarBg);
            UiFactory.Fixed(root, width: 420f);
            UiFactory.VCol(root, 0, null, TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.AddBorder(root.transform, PeekerColors.SidebarBorder, 1f, UiFactory.Sides.All);

            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(root.transform, false);
            UiFactory.HRow(header, 0, UiFactory.Padding(20, 14, 20, 10), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(header, height: 38f);

            var categoryLabel = UiFactory.Text(header.transform, "CategoryLabel", "", 10, PeekerColors.MonoDim,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.18f);
            UiFactory.Spacer(header.transform);
            var enabledLabel = UiFactory.Text(header.transform, "EnabledLabel", "", 10, PeekerColors.MonoDim,
                TextAlignmentOptions.MidlineRight, letterSpacing: 0.18f);

            var bodyContainer = new GameObject("Body", typeof(RectTransform));
            bodyContainer.transform.SetParent(root.transform, false);
            UiFactory.Flexible(bodyContainer, 0, 1);
            var listContent = UiFactory.ScrollColumn(bodyContainer.transform, "ModuleList");

            var emptyState = new GameObject("EmptyState", typeof(RectTransform));
            emptyState.transform.SetParent(bodyContainer.transform, false);
            UiFactory.StretchAll(emptyState);
            UiFactory.VCol(emptyState, 10, UiFactory.Padding(40, 60, 40, 60), TextAnchor.MiddleCenter, true, true, false, false);

            var dash = UiFactory.Panel(emptyState.transform, "Icon", Color.clear);
            UiFactory.Fixed(dash, 34f, 34f);
            UiFactory.AddBorder(dash.transform, PeekerColors.EmptyDash, 1f);

            UiFactory.Text(emptyState.transform, "Title", "No modules in this category", 17, PeekerColors.EmptyText,
                TextAlignmentOptions.Center);
            UiFactory.Text(emptyState.transform, "Hint", "Register(new YourModule()) in ModuleManager", 10,
                PeekerColors.EmptyHint, TextAlignmentOptions.Center, letterSpacing: 0.06f);

            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(root.transform, false);
            UiFactory.HRow(footer, 0, UiFactory.Padding(20, 12, 20, 12), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(footer, height: 40f);
            UiFactory.AddBorder(footer.transform, PeekerColors.SidebarBorder, 1f, UiFactory.Sides.Top);

            UiFactory.Text(footer.transform, "Hint", "RIGHT-CLICK ROW TO TOGGLE", 10, PeekerColors.FooterHint,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.1f);
            UiFactory.Spacer(footer.transform);
            var hudLink = UiFactory.Text(footer.transform, "HudLink", "HUD LAYOUT →", 15, PeekerColors.FooterLink,
                TextAlignmentOptions.MidlineRight, letterSpacing: 0.12f);
            UiFactory.AddBorder(hudLink.gameObject.transform, PeekerColors.SwitchOffBorder, 1f, UiFactory.Sides.Bottom);
            var linkHover = hudLink.gameObject.AddComponent<HoverElement>();
            linkHover.WatchColor(hudLink, PeekerColors.FooterLink, PeekerColors.Accent);

            var sidebar = new PeekerModuleSidebar(listContent, categoryLabel, enabledLabel, bodyContainer, emptyState);
            linkHover.Clicked = () => sidebar.RequestHudEditor?.Invoke();
            return sidebar;
        }

        private PeekerModuleSidebar(RectTransform content, TextMeshProUGUI categoryLabel, TextMeshProUGUI enabledLabel,
            GameObject bodyContainer, GameObject emptyState)
        {
            _content = content;
            _categoryLabel = categoryLabel;
            _enabledLabel = enabledLabel;
            _bodyContainer = bodyContainer;
            _emptyState = emptyState;
            _scrollRect = bodyContainer.GetComponent<ScrollRect>();
        }

        public void Refresh(ModuleCategory category, IReadOnlyList<Module.Module> modules, Module.Module selected)
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);

            _categoryLabel.text = "MODULES · " + category.ToString().ToUpperInvariant();
            int on = modules.Count(m => m.Toggled);
            _enabledLabel.text = $"{on}/{modules.Count} ON";

            bool hasModules = modules.Count > 0;
            _emptyState.SetActive(!hasModules);
            _content.gameObject.SetActive(hasModules);
            if (_scrollRect != null) _scrollRect.enabled = hasModules;

            foreach (Module.Module module in modules)
                BuildRow(module, module == selected);
        }

        private void BuildRow(Module.Module module, bool selected)
        {
            var row = UiFactory.Panel(_content, "Row_" + module.Name, selected ? PeekerColors.RowSelectedBg : Color.clear);
            UiFactory.HRow(row, 0, null, TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(row, height: 66f);
            UiFactory.AddBorder(row.transform, PeekerColors.RowBorder, 1f, UiFactory.Sides.Bottom);
            var rowImage = row.GetComponent<Image>();

            var bar = UiFactory.Panel(row.transform, "Bar", module.Toggled ? PeekerColors.Accent : Color.clear);
            UiFactory.Fixed(bar, width: 3f);
            UiFactory.Flexible(bar, 0, 1);

            var nameBlock = new GameObject("NameBlock", typeof(RectTransform));
            nameBlock.transform.SetParent(row.transform, false);
            UiFactory.VCol(nameBlock, 3, UiFactory.Padding(16, 0, 0, 0), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Flexible(nameBlock, 1, 0);

            var nameText = UiFactory.Text(nameBlock.transform, "Name", module.Name, 20,
                module.Toggled ? PeekerColors.NameOn : PeekerColors.NameOff, letterSpacing: 0.05f);
            UiFactory.Text(nameBlock.transform, "Subtitle", Subtitle(module), 10, PeekerColors.SubtitleMono,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.08f);

            var right = new GameObject("Right", typeof(RectTransform));
            right.transform.SetParent(row.transform, false);
            UiFactory.HRow(right, 14, UiFactory.Padding(0, 0, 18, 0), TextAnchor.MiddleRight, true, true, false, false);
            UiFactory.AutoSize(right, true, false);

            var keyBadge = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image));
            keyBadge.transform.SetParent(right.transform, false);
            keyBadge.GetComponent<Image>().color = Color.clear;
            UiFactory.HRow(keyBadge, 0, UiFactory.Padding(8, 3, 8, 3), TextAnchor.MiddleCenter, true, true, false, false);
            UiFactory.AutoSize(keyBadge, true, true);
            UiFactory.AddBorder(keyBadge.transform, PeekerColors.KeyBadgeBorder, 1f);
            UiFactory.Text(keyBadge.transform, "Key", module.Keybind == Key.None ? "—" : module.Keybind.ToString(),
                11, PeekerColors.KeyBadgeText, TextAlignmentOptions.Midline, letterSpacing: 0.06f);

            var toggle = PeekerToggleSwitch.Create(right.transform, 40, 18);
            toggle.Set(module.Toggled, notify: false);
            toggle.Changed = _ => ToggleRequested?.Invoke(module);

            var hover = row.AddComponent<HoverElement>();
            hover.WatchColor(rowImage, selected ? PeekerColors.RowSelectedBg : Color.clear, PeekerColors.RowHoverBg);
            hover.Clicked = () => SelectRequested?.Invoke(module);
            hover.RightClicked = () => ToggleRequested?.Invoke(module);
        }

        private static string Subtitle(Module.Module module)
        {
            bool dev = module.GetType().GetCustomAttributes(true).Any(a => a.GetType().Name == "DevelopmentAttribute");
            return (dev ? "DEV · " : "") + module.Settings.Count + " SETTINGS";
        }
    }
}
