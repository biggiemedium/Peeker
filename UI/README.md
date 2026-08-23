# Peeker Menu UI (Peeker.UI)

Code-only uGUI click-GUI: one small floating panel per `ModuleCategory`, each
listing its modules, each module able to drop its settings out underneath.
`HorizontalLayoutGroup`/`VerticalLayoutGroup` + TextMeshPro — no prefabs, no
external assets, no references beyond the DLLs already in the Lethal Company
install. Rounded corners, strokes, shadows, toggle knobs and the collapse arrows
are all textures generated at runtime by `UiFactory` (see *Procedural shapes*).

## Wiring

`Peeker`'s constructor calls `PeekerMenuController.Create(ModuleManager)`.
`Create` is **idempotent** — it returns the existing controller instead of
building a second one, because two controllers meant two canvases and two
EventSystems fighting over every click.

Hotkeys (all handled by the controller):

| Key | Action |
| --- | --- |
| `Right Arrow` (`PeekerMenuController.ToggleKey`) | open / close |
| `Insert` (`AltToggleKey`) | open / close |
| `Escape` | close (or clear the binding while capturing) |
| `Right Shift` | fires `HudEditorRequested` |

Module hotkeys are suppressed while the menu is open (`ModuleManager.Update`
checks `Peeker.MenuOpen`), so rebinding a key doesn't also toggle its module.

## Layout

Nothing is full-screen. The only screen-sized object is the menu root, and it
deliberately carries **no `Image`**, so a click in the gap between two panels
falls through to the game instead of being eaten by an invisible backdrop.

- **Category panels** — 172px wide, positioned in a row from the top-left, and
  free-floating: each one anchors top-left with a `ContentSizeFitter` on the
  vertical axis, so the panel hugs its rows and is never taller than it needs
  to be. **Drag the title bar to move a panel; click it to collapse it.** A
  dragged panel is sent to the front (`SetAsLastSibling`).
- **Module rows** — 22px, with a 2px amber ticker on the left that lights up
  when the module is on. Left-click toggles; the chevron (or a right-click on
  the row) opens the settings tray underneath.
- **Settings tray** — recessed panel holding the keybind line and one row per
  setting: pill switch for bools, slider with a knob for numbers, pressable
  chip for enums, outlined swatch for colours. Left-click advances an
  enum/colour, right-click steps back.
- **Status bar** — floating pill at bottom-centre: brand, hint line, `SAVE`,
  and a close button.

## Procedural shapes

`UiFactory` bakes a handful of small `Texture2D`s once and reuses them through
`Image.Type.Sliced`, which is what gets rounded corners with no art assets:

| Method | Shape |
| --- | --- |
| `RoundedSprite(r)` | filled rounded rect |
| `RoundedTopSprite(r)` | rounded top corners, square bottom — title bars |
| `OutlineSprite(r, t)` | hollow rounded rect, `fillCenter = false` |
| `CircleSprite()` | disc — toggle knobs, slider knob, status dot |
| `TriangleSprite()` | points down; rotate Z by 90° for "collapsed" |

Wrappers: `Rounded`, `RoundedTop`, `RoundedBackground`, `RoundedOutline`,
`Shadow`, `Glyph`.

Two things to keep in mind when using them:

- **A 9-sliced rect must be at least as large as its borders.** The border is
  `radius + 1` per side, so a control of height `h` can only take a radius up
  to `h / 2 - 1` before Unity starts squashing the corners. That is why the
  4px slider track is a plain quad and the 12px pill uses radius 5.
- **A title bar needs `RoundedTopSprite`, not `RoundedSprite`.** A fully
  rounded header laid over a fully rounded panel leaves two notches where the
  body colour shows through the header's bottom corners. When a panel collapses
  the header *becomes* the whole panel, so `PeekerMenu` swaps it back to the
  fully rounded sprite.

## Layout rules (read before editing)

Three mistakes collapse this UI to an invisible window; all three were in the
first version:

1. **A child of a layout group must not carry a `ContentSizeFitter`.** The
   parent already asks it for a preferred size. Give the child its own
   `HRow`/`VCol` and let that report the hug size. `UiFactory.AutoSize` is only
   valid where the parent is *not* a layout group — which is exactly why the
   category panels and the status bar hang off the plain root node.
2. **A child that should fill leftover space needs `UiFactory.Flexible`.**
   With `childControlWidth = true` and `childForceExpandWidth = false`, a child
   with no preferred width and no flexible weight gets **zero** width.
3. **A child of a plain RectTransform starts life 0x0.** It needs
   `UiFactory.StretchAll` or an explicit size. Anything decorative parented to
   a layout group (background, outline, shadow) must also set
   `LayoutElement.ignoreLayout`, or the parent will try to flow it.

Two more that break interaction rather than layout:

4. **A pointer handler needs a `Graphic` on its own GameObject.** Use
   `UiFactory.HitArea` — a zero-alpha `Image` still raycasts.
5. **`Enum.GetValues(typeof(Key))` includes sentinels that `Keyboard`'s indexer
   throws on.** Enumerate `Keyboard.current.allKeys` instead.

And one specific to dragging:

6. **uGUI still fires `IPointerClickHandler` after a drag** when the same
   GameObject is both the press and the drag target. `DragHandle` therefore
   does its own click detection on pointer-up rather than implementing
   `IPointerClickHandler`, which is what stops a panel collapsing every time
   you finish moving it.

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
- **Module descriptions** — `Peeker.UI.IDescribedModule` has no UI behind it either;
  the rows have no room for a description and no tooltip layer exists yet.
- Colour settings cycle a preset list; there is no hue/SV picker.
- Panel positions and collapsed state are not persisted — they reset each launch.
- Config is only written when SAVE is clicked; there is no autosave on quit.
- Fonts use TMP's default asset (resolved via `UiFactory.ResolveFont`), so weights
  and metrics won't match a designed mock pixel-for-pixel. `UiFactory.Sanitize`
  swaps out any glyph that asset lacks (`✕ → — – ·` all render as hollow boxes in
  LiberationSans SDF), which is why the arrows are drawn as sprites rather than text.

## File map

- `PeekerMenuController.cs` — MonoBehaviour entry point (hotkeys, canvas, EventSystem, cursor).
- `PeekerMenu.cs` — builds the status bar and every category panel; owns keybind capture.
- `PeekerSettingRow.cs` — dispatches one `Setting` to the right control.
- `PeekerColors.cs` — the palette.
- `IDescribedModule.cs` — optional module description hook.
- `Internal/UiFactory.cs` — layout + procedural shape builder.
- `Internal/HoverElement.cs` — hover colour swaps and left/right click reporting.
- `Internal/DragHandle.cs` — title-bar drag-to-move, click-to-collapse.
- `Internal/NormalizedDragArea.cs` — 0..1 drag reporting for sliders.
