# Consumable Picker: Expand-Downward Redesign

## Overview

Replace the current consumable picker (circular button + text-row dropdown to the left) with a single pill-shaped container that expands downward, showing an icon for each consumable in stock.

## Structure

Single `VisualElement` container (`consumable-picker`) replaces the current two-element approach:

```
consumable-picker              ← outer container, position:absolute, right:8px
  consumable-picker-btn        ← herb icon button (always visible, top of pill)
  consumable-picker-icons      ← icon list, clipped when collapsed
    consumable-icon-btn        ← one per consumable in stock
      icon image
      count badge ("x2")
```

## States

**Collapsed:**
- Container: `width: 44px`, `max-height: 44px`, `overflow: hidden`, `border-radius: 22px`
- Looks identical to the current circular button

**Expanded (`.consumable-picker--open`):**
- Container: `max-height: 300px`, USS `transition: max-height 0.25s ease-out`
- Border-radius: top `22px`, bottom `12px` (becomes a rounded rectangle)
- Each icon slot: `44×44px`, centered icon, count badge in bottom-right corner

## Behavior

- Tapping the herb button toggles `.consumable-picker--open` on the container
- Tapping an icon: immediate apply for env-scoped consumables; enter slot-select mode for slot-scoped
- Entering slot-select mode collapses the picker
- `RefreshIcons()` rebuilds icon buttons from current inventory on each open

## Files Changed

1. **`Assets/Scripts/UI/BackyardViewUI.cs`** — rewrite `BuildConsumablePicker()` and `RefreshDropdown()` (renamed `RefreshIcons()`); update `ToggleDropdown` and `OnConsumableRowTapped`
2. **`Assets/UI/Styles/Backyard.uss`** — replace dropdown/row classes with new picker container and icon button classes; add `max-height` transition
