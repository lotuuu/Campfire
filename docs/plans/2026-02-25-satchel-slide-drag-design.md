# Satchel Slide Animation & Drag-to-Close Design

## Overview

Two fixes to the Satchel bottom sheet:
1. Slide-in animation fires correctly on open
2. Drag-to-close works reliably from the top of the panel

## Fix 1: Slide Animation

**Root cause:** When `display: none`, the panel has no height, so `Translate(0, Length.Percent(100))` evaluates to `0px`. The panel starts visible at position 0; `schedule.Execute` sets it to 0 again — nothing to animate.

**Approach:** Remove `display: none` toggling entirely. The panel is `position: absolute` so it doesn't affect flow layout. A `bottom-sheet--hidden` USS class holds `translate: 0 100%`. Since the panel is always laid out, `100%` always resolves to the panel's real height and the CSS transition fires correctly every time.

- **Show:** `panel.RemoveFromClassList("bottom-sheet--hidden")`
- **Hide:** `panel.AddToClassList("bottom-sheet--hidden")`, then hide scrim after `TransitionEndEvent`
- **Initial state:** Panel starts with `bottom-sheet--hidden` class (UXML inline style `display:none` removed; class applied instead)
- **Scrim** still uses `display: none / flex` as before

## Fix 2: Drag-to-Close Hit Area

**Root cause:** `satchel-handle` is 4px tall — too small to reliably initiate a drag.

**Approach:** Replace the single handle element with a two-level structure in UXML:
- `satchel-drag-zone` — full-width, 44px tall transparent container; receives `PointerDownEvent`
- `bottom-sheet-handle-pip` child — the 40×4px visual pill, centered within the zone

CSS changes:
- Remove visual styling from `bottom-sheet-handle`; add `bottom-sheet-drag-zone` (full-width, 44px, transparent) and `bottom-sheet-handle-pip` (40×4px pill)

C# change: query `satchel-drag-zone` instead of `satchel-handle` for `PointerDownEvent` registration.

## Files Changed

1. **`Assets/UI/Documents/GardenRoot.uxml`** — replace `satchel-handle` with `satchel-drag-zone` + `bottom-sheet-handle-pip` child
2. **`Assets/UI/Styles/Common.uss`** — add `bottom-sheet--hidden` class; update handle styles; add drag-zone + pip styles; remove `display:none` initial state from bottom-sheet
3. **`Assets/Scripts/UI/SatchelUI.cs`** — remove `display` toggling on panel; use `bottom-sheet--hidden` class; update handle query to `satchel-drag-zone`
