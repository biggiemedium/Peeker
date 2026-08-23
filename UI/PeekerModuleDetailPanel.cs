using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Peeker.Module;
using Peeker.Settings;
using Peeker.UI.Internal;

namespace Peeker.UI
{
    /// <summary>
    /// Right panel: selected module's header, keybind row, settings list and
    /// footer actions, plus the full-panel keybind-capture overlay.
    /// </summary>
    public class PeekerModuleDetailPanel
    {
        public Action SaveConfigRequested;

        public bool IsCapturing => _captureOverlay.activeSelf;

        private readonly GameObject _noSelectionState;
        private readonly GameObject _selectionState;
        private readonly GameObject _captureOverlay;
        private readonly TextMeshProUGUI _kicker;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _statusBadge;
        private readonly UiFactory.BorderHandle _statusBorder;
        private readonly TextMeshProUGUI _description;
        private readonly TextMeshProUGUI _keybindLabel;
        private readonly UiFactory.BorderHandle _keybindBorder;
        private readonly RectTransform _settingsContent;
        private readonly TextMeshProUGUI _patchLine;

        private readonly Canvas _canvas;
        private readonly RectTransform _overlayLayer;

        private Module.Module _current;
        private readonly Dictionary<GameObject, Setting> _rowSettings = new Dictionary<GameObject, Setting>();
        private bool _subscribed;

        public static PeekerModuleDetailPanel Build(Transform parent, Canvas canvas, RectTransform overlayLayer)
        {
            var root = UiFactory.Panel(parent, "DetailPanel", PeekerColors.DetailBg);
            UiFactory.Flexible(root, 1, 1);

            // -- No-selection state --------------------------------------------------
            var noSel = new GameObject("NoSelection", typeof(RectTransform));
            noSel.transform.SetParent(root.transform, false);
            UiFactory.StretchAll(noSel);
            UiFactory.VCol(noSel, 12, null, TextAnchor.MiddleCenter, true, true, false, false);

            var dash = UiFactory.Panel(noSel.transform, "Icon", Color.clear);
            UiFactory.Fixed(dash, 40f, 40f);
            UiFactory.AddBorder(dash.transform, PeekerColors.EmptyDash, 1f);
            UiFactory.Text(noSel.transform, "Title", "Nothing selected", 20, PeekerColors.EmptyText, TextAlignmentOptions.Center, letterSpacing: 0.04f);
            UiFactory.Text(noSel.transform, "Hint", "PICK A MODULE TO EDIT ITS SETTINGS", 10, PeekerColors.EmptyHint,
                TextAlignmentOptions.Center, letterSpacing: 0.1f);

            // -- Selection state --------------------------------------------------
            var sel = new GameObject("Selection", typeof(RectTransform));
            sel.transform.SetParent(root.transform, false);
            UiFactory.StretchAll(sel);
            UiFactory.VCol(sel, 0, null, TextAnchor.UpperLeft, true, true, false, false);

            // Header
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(sel.transform, false);
            UiFactory.VCol(header, 12, UiFactory.Padding(30, 22, 30, 18), TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.AddBorder(header.transform, PeekerColors.DetailBorder, 1f, UiFactory.Sides.Bottom);

            var headerTop = new GameObject("Top", typeof(RectTransform));
            headerTop.transform.SetParent(header.transform, false);
            UiFactory.HRow(headerTop, 20, null, TextAnchor.LowerLeft, true, true, false, false);

            var titleBlock = new GameObject("TitleBlock", typeof(RectTransform));
            titleBlock.transform.SetParent(headerTop.transform, false);
            UiFactory.VCol(titleBlock, 6, null, TextAnchor.UpperLeft, true, true, false, false);

            var kicker = UiFactory.Text(titleBlock.transform, "Kicker", "", 10, PeekerColors.Accent,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.18f);
            var name = UiFactory.Text(titleBlock.transform, "Name", "", 34, PeekerColors.TitleText,
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 0.04f);

            UiFactory.Spacer(headerTop.transform);

            var statusBadge = new GameObject("StatusBadge", typeof(RectTransform), typeof(Image));
            statusBadge.transform.SetParent(headerTop.transform, false);
            statusBadge.GetComponent<Image>().color = Color.clear;
            UiFactory.HRow(statusBadge, 0, UiFactory.Padding(12, 5, 12, 5), TextAnchor.MiddleCenter, true, true, false, false);
            UiFactory.AutoSize(statusBadge, true, true);
            var statusBorder = UiFactory.AddBorder(statusBadge.transform, PeekerColors.IdleBadge, 1f);
            var statusText = UiFactory.Text(statusBadge.transform, "Text", "", 10, PeekerColors.IdleBadge,
                TextAlignmentOptions.Midline, letterSpacing: 0.2f);

            var description = UiFactory.Text(header.transform, "Description", "", 16, PeekerColors.DetailDescription,
                TextAlignmentOptions.TopLeft, wrap: true);
            UiFactory.Fixed(description.gameObject, width: 560f);

            // Keybind row
            var keybindRow = UiFactory.Panel(sel.transform, "KeybindRow", PeekerColors.KeybindRowBg);
            UiFactory.HRow(keybindRow, 0, UiFactory.Padding(30, 14, 30, 14), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(keybindRow, height: 58f);
            UiFactory.AddBorder(keybindRow.transform, PeekerColors.DetailBorder, 1f, UiFactory.Sides.Bottom);

            var keybindTextBlock = new GameObject("Labels", typeof(RectTransform));
            keybindTextBlock.transform.SetParent(keybindRow.transform, false);
            UiFactory.VCol(keybindTextBlock, 2, null, TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.Text(keybindTextBlock.transform, "Label", "Keybind", 17, PeekerColors.KeybindLabel);
            UiFactory.Text(keybindTextBlock.transform, "Hint", "UnityEngine.InputSystem.Key", 10, PeekerColors.KeybindHint,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0f);

            UiFactory.Spacer(keybindRow.transform);

            var keybindButton = new GameObject("KeybindButton", typeof(RectTransform), typeof(Image));
            keybindButton.transform.SetParent(keybindRow.transform, false);
            keybindButton.GetComponent<Image>().color = Color.clear;
            UiFactory.HRow(keybindButton, 0, UiFactory.Padding(20, 9, 20, 9), TextAnchor.MiddleCenter, true, true, false, false);
            UiFactory.AutoSize(keybindButton, false, true);
            UiFactory.Fixed(keybindButton, width: 150f);
            var keybindBorder = UiFactory.AddBorder(keybindButton.transform, PeekerColors.KeybindButtonBorder, 1f);
            var keybindLabel = UiFactory.Text(keybindButton.transform, "Label", "", 13, PeekerColors.KeybindLabel,
                TextAlignmentOptions.Midline, letterSpacing: 0.16f);
            var keybindHover = keybindButton.AddComponent<HoverElement>();
            keybindHover.WatchColor(keybindLabel, PeekerColors.KeybindLabel, PeekerColors.AccentHover);
            var keybindBorderHover = keybindButton.AddComponent<BorderHoverBridge>();
            keybindBorderHover.Bind(keybindBorder, PeekerColors.KeybindButtonBorder, PeekerColors.Accent);

            // Settings list + footer
            var listOuter = new GameObject("SettingsListOuter", typeof(RectTransform));
            listOuter.transform.SetParent(sel.transform, false);
            UiFactory.VCol(listOuter, 0, UiFactory.Padding(30, 8, 30, 0), TextAnchor.UpperLeft, true, true, false, false);
            UiFactory.Flexible(listOuter, 0, 1);

            var scrollHolder = new GameObject("ScrollHolder", typeof(RectTransform));
            scrollHolder.transform.SetParent(listOuter.transform, false);
            UiFactory.Flexible(scrollHolder, 0, 1);
            var settingsContent = UiFactory.ScrollColumn(scrollHolder.transform, "Settings");

            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(listOuter.transform, false);
            UiFactory.HRow(footer, 10, UiFactory.Padding(0, 20, 0, 18), TextAnchor.MiddleLeft, true, true, false, false);
            UiFactory.Fixed(footer, height: 56f);

            var patchLine = UiFactory.Text(footer.transform, "PatchLine", "", 10, PeekerColors.FooterHint,
                TextAlignmentOptions.MidlineLeft, letterSpacing: 0.08f, wrap: true);
            UiFactory.Flexible(patchLine.gameObject, 1, 0);

            var actionGroup = new GameObject("Actions", typeof(RectTransform));
            actionGroup.transform.SetParent(footer.transform, false);
            UiFactory.HRow(actionGroup, 10, null, TextAnchor.MiddleRight, true, true, false, false);
            UiFactory.AutoSize(actionGroup, true, true);

            var resetButton = MakeFooterButton(actionGroup.transform, "RESET", PeekerColors.ResetBorder, PeekerColors.ResetText, PeekerColors.ResetHoverText, PeekerColors.ResetHoverText);
            var saveButton = MakeFooterButton(actionGroup.transform, "SAVE CONFIG", PeekerColors.Accent, PeekerColors.Accent, PeekerColors.Accent, PeekerColors.Accent);

            // -- Capture overlay --------------------------------------------------
            var overlay = UiFactory.Panel(root.transform, "CaptureOverlay", PeekerColors.CaptureBackdrop);
            UiFactory.StretchAll(overlay);
            UiFactory.VCol(overlay, 22, null, TextAnchor.MiddleCenter, true, true, false, false);
            overlay.SetActive(false);

            UiFactory.Text(overlay.transform, "Listening", "LISTENING FOR INPUT", 11, PeekerColors.Accent,
                TextAlignmentOptions.Center, letterSpacing: 0.3f);

            var pressBox = new GameObject("PressBox", typeof(RectTransform), typeof(Image));
            pressBox.transform.SetParent(overlay.transform, false);
            pressBox.GetComponent<Image>().color = Color.clear;
            UiFactory.HRow(pressBox, 0, UiFactory.Padding(48, 24, 48, 24), TextAnchor.MiddleCenter, true, true, false, false);
            UiFactory.AutoSize(pressBox, true, true);
            var pressBorder = UiFactory.AddBorder(pressBox.transform, PeekerColors.Accent, 1f);
            var pressText = UiFactory.Text(pressBox.transform, "Text", "Press any key", 40, PeekerColors.TitleText,
                TextAlignmentOptions.Midline, letterSpacing: 0.1f);
            pressText.gameObject.AddComponent<Pulse>().PeriodSeconds = 1.6f;

            var hints = new GameObject("Hints", typeof(RectTransform));
            hints.transform.SetParent(overlay.transform, false);
            UiFactory.HRow(hints, 26, null, TextAnchor.MiddleCenter, true, true, false, false);
            UiFactory.AutoSize(hints, true, true);
            UiFactory.Text(hints.transform, "Esc", "ESC — CLEAR", 11, PeekerColors.TabInactive,
                TextAlignmentOptions.Midline, letterSpacing: 0.12f);
            var cancelText = UiFactory.Text(hints.transform, "Cancel", "CANCEL", 11, PeekerColors.TabInactive,
                TextAlignmentOptions.Midline, letterSpacing: 0.12f);
            UiFactory.AddBorder(cancelText.gameObject.transform, PeekerColors.SwitchOffBorder, 1f, UiFactory.Sides.Bottom);
            var cancelHover = cancelText.gameObject.AddComponent<HoverElement>();

            var panel = new PeekerModuleDetailPanel(canvas, overlayLayer, noSel, sel, overlay, kicker, name,
                statusText, statusBorder, description, keybindLabel, keybindBorder, settingsContent, patchLine);

            keybindHover.Clicked = () => panel.BeginCapture();
            cancelHover.Clicked = () => panel.CancelCapture();
            resetButton.Clicked = () => panel.ResetCurrentSettings();
            saveButton.Clicked = () => panel.SaveConfigRequested?.Invoke();

            var captureBehaviour = overlay.AddComponent<KeybindCaptureBehaviour>();
            captureBehaviour.KeyCaptured = key => panel.FinishCapture(key);

            return panel;
        }

        private static HoverElement MakeFooterButton(Transform parent, string label, Color border, Color text, Color hoverBorder, Color hoverText)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.clear;
            UiFactory.HRow(go, 0, UiFactory.Padding(18, 8, 18, 8), TextAnchor.MiddleCenter, true, true, false, false);
            UiFactory.AutoSize(go, true, true);
            var borderHandle = UiFactory.AddBorder(go.transform, border, 1f);
            var textEl = UiFactory.Text(go.transform, "Label", label, 15, text, TextAlignmentOptions.Midline, letterSpacing: 0.12f);

            var hover = go.AddComponent<HoverElement>();
            hover.WatchColor(textEl, text, hoverText);
            var enterExit = go.AddComponent<BorderHoverBridge>();
            enterExit.Bind(borderHandle, border, hoverBorder);
            return hover;
        }

        /// <summary>HoverElement only swaps Graphic colors it watches; borders are strips, not a Graphic on this object.</summary>
        private class BorderHoverBridge : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            private UiFactory.BorderHandle _border;
            private Color _normal, _hover;
            public void Bind(UiFactory.BorderHandle border, Color normal, Color hover) { _border = border; _normal = normal; _hover = hover; }
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => _border.SetColor(_hover);
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => _border.SetColor(_normal);
        }

        private PeekerModuleDetailPanel(Canvas canvas, RectTransform overlayLayer, GameObject noSelectionState, GameObject selectionState,
            GameObject captureOverlay, TextMeshProUGUI kicker, TextMeshProUGUI name, TextMeshProUGUI statusBadge,
            UiFactory.BorderHandle statusBorder, TextMeshProUGUI description, TextMeshProUGUI keybindLabel,
            UiFactory.BorderHandle keybindBorder, RectTransform settingsContent, TextMeshProUGUI patchLine)
        {
            _canvas = canvas;
            _overlayLayer = overlayLayer;
            _noSelectionState = noSelectionState;
            _selectionState = selectionState;
            _captureOverlay = captureOverlay;
            _kicker = kicker;
            _name = name;
            _statusBadge = statusBadge;
            _statusBorder = statusBorder;
            _description = description;
            _keybindLabel = keybindLabel;
            _keybindBorder = keybindBorder;
            _settingsContent = settingsContent;
            _patchLine = patchLine;
        }

        public void ShowNoSelection()
        {
            EndCapture();
            _current = null;
            _noSelectionState.SetActive(true);
            _selectionState.SetActive(false);
            Unsubscribe();
        }

        public void ShowModule(Module.Module module)
        {
            EndCapture();
            _current = module;
            _noSelectionState.SetActive(false);
            _selectionState.SetActive(true);

            _kicker.text = module.Category.ToString().ToUpperInvariant() + " MODULE";
            _name.text = module.Name;
            _description.text = module is IDescribedModule described ? described.Description : "";

            bool on = module.Toggled;
            _statusBadge.text = on ? "PATCHED" : "IDLE";
            _statusBadge.color = on ? PeekerColors.Accent : PeekerColors.IdleBadge;
            _statusBorder.SetColor(on ? PeekerColors.Accent : PeekerColors.IdleBadge);

            RefreshKeybindLabel();
            _patchLine.text = BuildPatchLine(module);

            RebuildSettings();
            Subscribe();
        }

        /// <summary>Call after any external change (module toggled, keybind rebound elsewhere) to resync the header.</summary>
        public void Refresh()
        {
            if (_current != null) ShowModule(_current);
        }

        private void RebuildSettings()
        {
            for (int i = _settingsContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_settingsContent.GetChild(i).gameObject);
            _rowSettings.Clear();

            if (_current == null) return;

            var settings = _current.Settings;
            for (int i = 0; i < settings.Count; i++)
            {
                Setting setting = settings[i];
                GameObject row = PeekerSettingRow.Build(_settingsContent, _canvas, _overlayLayer, setting, i, settings.Count, OnSettingChanged);
                _rowSettings[row] = setting;
            }
        }

        private void OnSettingChanged() { /* per-control visuals already refresh themselves */ }

        private void RefreshRowVisibility(Setting _)
        {
            foreach (var kv in _rowSettings)
                if (kv.Key != null) kv.Key.SetActive(kv.Value.IsVisible);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            Setting.AnyValueChanged += RefreshRowVisibility;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            Setting.AnyValueChanged -= RefreshRowVisibility;
            _subscribed = false;
        }

        private void ResetCurrentSettings()
        {
            if (_current == null) return;
            _current.ResetSettings();
            RebuildSettings();
        }

        private void RefreshKeybindLabel()
        {
            if (_current == null) return;
            _keybindLabel.text = _current.Keybind == Key.None ? "UNBOUND" : _current.Keybind.ToString().ToUpperInvariant();
        }

        private void BeginCapture()
        {
            if (_current == null) return;
            _captureOverlay.SetActive(true);
        }

        private void CancelCapture() => EndCapture();

        private void FinishCapture(Key? key)
        {
            if (_current != null)
            {
                // Requires a `public void SetKeybind(Key key)` method on Module (Keybind's
                // setter is private in the base class) — see integration notes.
                _current.SetKeybind(key ?? Key.None);
                RefreshKeybindLabel();
            }
            EndCapture();
        }

        private void EndCapture() => _captureOverlay.SetActive(false);

        private static string BuildPatchLine(Module.Module module)
        {
            Type[] patchTypes = GetPatchTypes(module);
            int count = patchTypes?.Length ?? 0;
            string noun = count == 1 ? "Harmony patch" : "Harmony patches";
            return $"{count} {noun} · dev.px.peeker.module.{module.Name}".ToUpperInvariant();
        }

        private static Type[] GetPatchTypes(Module.Module module)
        {
            PropertyInfo prop = typeof(Module.Module).GetProperty("PatchTypes", BindingFlags.Instance | BindingFlags.NonPublic);
            return prop?.GetValue(module) as Type[];
        }

        /// <summary>Polls every key while active; reports the first press, or null on Escape (unbind).</summary>
        private class KeybindCaptureBehaviour : MonoBehaviour
        {
            public Action<Key?> KeyCaptured;
            private static readonly Key[] AllKeys = (Key[])Enum.GetValues(typeof(Key));

            private void Update()
            {
                var kb = Keyboard.current;
                if (kb == null) return;

                if (kb[Key.Escape].wasPressedThisFrame)
                {
                    KeyCaptured?.Invoke(null);
                    return;
                }

                foreach (Key key in AllKeys)
                {
                    if (key == Key.None || key == Key.Escape) continue;
                    if (kb[key].wasPressedThisFrame)
                    {
                        KeyCaptured?.Invoke(key);
                        return;
                    }
                }
            }
        }
    }
}
