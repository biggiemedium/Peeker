# Peeker Menu UI (Peeker.UI)

Drop-in uGUI implementation of the "Peeker Menu" design (category tabs, module
list, settings panel with bool/number/color/enum controls, keybind capture).
Built with code-only uGUI (`HorizontalLayoutGroup`/`VerticalLayoutGroup` +
TextMeshPro) — no prefabs, no external assets, no `_ds`/design-tool files
needed at runtime.

## 1. One required change to your existing code

`Module.Keybind` only has a private setter, so there's no way for the UI to
rebind a key at runtime. Add this one method to `Peeker.Module.Module`:

```csharp
public void SetKeybind(Key key) => Keybind = key;
```

Everything else in this drop targets your classes exactly as pasted in chat
(`Module`, `ModuleManager`, `SettingsHolder`, `Setting`/`Setting<T>`).

## 2. Wiring it up

From your plugin's `Awake()` (or wherever you construct your `ModuleManager`):

```csharp
using Peeker.UI;

// _moduleManager is your existing ModuleManager instance
PeekerMenuController.Create(_moduleManager);
```

That's it — the controller creates its own Canvas/EventSystem, listens for
`Insert` (open/close), `Escape` (close, unless a keybind capture is in
progress), and `Right Shift` (fires `HudEditorRequested` — see below), and
un/locks the cursor while open.

## 3. Things you'll likely want to hook up next

- **HUD layout editor.** The design's "HUD LAYOUT →" link and the Right Shift
  hint both point at a layout-editor screen that wasn't part of this handoff
  (the chat only designed the module menu). Subscribe to
  `PeekerMenuController.Create(...).` → keep the returned controller and wire
  its `_menu.HudEditorRequested` (or add your own public passthrough) to
  whatever you build for `HudElement` layout editing.
- **Save Config.** `PeekerModuleDetailPanel.SaveConfigRequested` fires when
  "SAVE CONFIG" is clicked. Nothing in the pasted classes described how you
  persist settings (BepInEx `ConfigFile`, JSON, etc.), so this is left as a
  hook rather than guessed at. Wire it from `PeekerMenu`/`PeekerMenuController`
  if you want it to do something.
- **Module descriptions.** `Module` has no description field, so the detail
  panel shows blank body text unless a module implements the optional
  `Peeker.UI.IDescribedModule` interface (`string Description { get; }`).

## 4. Assumptions made without a chance to confirm

- `Peeker.Module.ModuleCategory` is an enum with members `Visual`, `Movement`,
  `Combat`, `Notification` (matches the comments in the pasted
  `ModuleManager`). Tabs are generated from `Enum.GetValues(typeof(ModuleCategory))`,
  so extra categories "just work" as long as the enum itself is right.
- Fonts: per your choice, this uses TextMeshPro's default/built-in font rather
  than bundling Barlow Condensed / IBM Plex Mono — sizes, letter-spacing and
  weights follow the design, but the exact typeface won't match pixel-for-pixel.
- Dashed borders (the two empty-state icons) are drawn as solid 1px borders —
  a dashed uGUI border needs a tiling sprite/material, which felt like overkill
  for a decorative icon.
- Assumes the project uses Unity's new Input System package (matches
  `UnityEngine.InputSystem.Key` already used in `Module`/`ModuleManager`) and
  creates an `InputSystemUIInputModule` if no `EventSystem` exists yet.

## 5. File map

- `PeekerMenuController.cs` — MonoBehaviour entry point (hotkeys, canvas, cursor).
- `PeekerMenu.cs` — header (brand/tabs/status/close), body, footer bar; owns selection state.
- `PeekerModuleSidebar.cs` — module list column + empty state.
- `PeekerModuleDetailPanel.cs` — selected module header, keybind row, settings list, capture overlay.
- `PeekerSettingRow.cs` — dispatches one `Setting` to the right control.
- `Controls/PeekerNumberControl.cs`, `PeekerColorControl.cs`, `PeekerEnumControl.cs` — the four setting types (bool uses `Internal/PeekerToggleSwitch.cs` directly).
- `Internal/` — shared low-level helpers (`UiFactory`, `HoverElement`, `NormalizedDragArea`, `Pulse`, `PeekerToggleSwitch`).
- `PeekerColors.cs` — the Ship-HUD palette.
- `IDescribedModule.cs` — optional module description hook.
