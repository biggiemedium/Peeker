# Peeker Menu UI (Peeker.UI)

Code-only uGUI implementation of the "Peeker Menu" design (category tabs, module
list, settings panel with bool/number/color/enum controls, keybind capture).
`HorizontalLayoutGroup`/`VerticalLayoutGroup` + TextMeshPro — no prefabs, no
external assets, no references beyond the DLLs already in the Lethal Company
install.

## Wiring

`Peeker`'s constructor calls `PeekerMenuController.Create(ModuleManager)`.
`Create` is **idempotent** — it returns the existing controller instead of
building a second one, because two controllers meant two canvases and two
EventSystems fighting over every click.

Hotkeys (all handled by the controller):

| Key | Action |
| --- | --- |
| `Right Arrow` (`PeekerMenuController.ToggleKey`) | open / close |
| `Escape` | close (or clear the binding while capturing) |
| `Right Shift` | fires `HudEditorRequested` |

Module hotkeys are suppressed while the menu is open (`ModuleManager.Update`
checks `Peeker.MenuOpen`), so rebinding a key doesn't also toggle its module.

## What the menu can do

- Click a **tab** to switch category; the menu opens on the first category that
  actually has modules.
- Click a **row** to select it; click its **switch** or **right-click the row**
  to toggle the module. The detail header has its own switch too.
- Click the **keybind button** to enter capture: the next key becomes the
  binding, `Escape` clears it, `CANCEL` backs out.
- **Settings** are live-editable — drag sliders, flip switches, pick enum
  segments, and open the colour popover (swatches, SV field, hue, alpha).
  Settings with a visibility predicate show/hide as their dependency changes.
- **RESET** restores every setting on the module to its default.
- **SAVE CONFIG** writes `BepInEx/config/Peeker.json` — every module's toggle
  state, keybind and setting values — and reports the result in the status bar.
  The file is read back once while `Peeker` is being constructed. See
  `Settings/PeekerConfig.cs`; the serializer is `Settings/MiniJson.cs`,
  hand-rolled so no extra assembly reference is needed.

## Layout rules (read before editing)

Three mistakes collapse this UI to an invisible window; all three were in the
first version:

1. **A child of a layout group must not carry a `ContentSizeFitter`.** The
   parent already asks it for a preferred size. Give the child its own
   `HRow`/`VCol` and let that report the hug size. `UiFactory.AutoSize` is only
   valid where the parent is *not* a layout group (the colour popover).
2. **A child that should fill leftover space needs `UiFactory.Flexible`.**
   With `childControlWidth = true` and `childForceExpandWidth = false`, a child
   with no preferred width and no flexible weight gets **zero** width. That is
   what made the whole detail panel disappear.
3. **A child of a plain RectTransform starts life 0x0.** It needs
   `UiFactory.StretchAll` or an explicit size. Wrapping a control in a bare
   "slot" node is what made the number slider invisible.

Two more that break interaction rather than layout:

4. **A pointer handler needs a `Graphic` on its own GameObject.** Use
   `UiFactory.HitArea` — a zero-alpha `Image` still raycasts.
5. **`Enum.GetValues(typeof(Key))` includes sentinels that `Keyboard`'s indexer
   throws on.** Enumerate `Keyboard.current.allKeys` instead.

## EventSystem

Peeker builds its own `EventSystem` + `InputSystemUIInputModule`
(`AssignDefaultActions()`), kept **disabled** while the menu is closed. On open
it is enabled and pushed to the front via `EventSystem.current`; Unity only ever
pumps `EventSystem.current`, so borrowing the game's would leave clicks at the
mercy of whichever action map Lethal Company currently has enabled. On close the
previous EventSystem is restored.

The canvas itself is built lazily on the **first open**, not at plugin `Awake` —
at Awake no scene has loaded and TMP's font assets may not resolve yet.

## Still open

- **HUD layout editor** — `HudEditorRequested` is a hook with no screen behind it.
- **Module descriptions** — implement `Peeker.UI.IDescribedModule` on a module
  to replace the placeholder body text.
- Config is only written when SAVE CONFIG is clicked; there is no autosave on quit.
- Fonts use TMP's default asset (resolved via `UiFactory.ResolveFont`), not
  Barlow Condensed / IBM Plex Mono, so weights and metrics won't match the
  design pixel-for-pixel. `UiFactory.Sanitize` swaps out any glyph that asset
  lacks (`✕ → → — – ·` all render as hollow boxes in LiberationSans SDF).
- Dashed borders on the two empty-state icons are drawn as solid 1px borders.

## File map

- `PeekerMenuController.cs` — MonoBehaviour entry point (hotkeys, canvas, EventSystem, cursor).
- `PeekerMenu.cs` — header (brand/tabs/status/close), body, footer bar; owns selection state.
- `PeekerModuleSidebar.cs` — **left** module-list column + empty state.
- `PeekerModuleDetailPanel.cs` — selected module header, keybind row, settings list, capture overlay.
- `PeekerSettingRow.cs` — dispatches one `Setting` to the right control.
- `Controls/PeekerNumberControl.cs`, `PeekerColorControl.cs`, `PeekerEnumControl.cs` — setting controls (bool uses `Internal/PeekerToggleSwitch.cs` directly).
- `Internal/` — shared helpers (`UiFactory`, `HoverElement`, `NormalizedDragArea`, `Pulse`, `PeekerToggleSwitch`).
- `PeekerColors.cs` — the Ship-HUD palette.
- `IDescribedModule.cs` — optional module description hook.
