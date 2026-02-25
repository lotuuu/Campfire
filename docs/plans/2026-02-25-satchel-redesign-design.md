# Satchel Redesign Design

**Date:** 2026-02-25
**Status:** Approved

## Overview

Redesign the Satchel bottom sheet to use a vertical list of horizontal seed cards. Tapping a card plants the seed directly (no Plant button). The sheet slides up smoothly and can be dismissed by swiping down.

## Sheet Animation

- **Show:** set `display:flex` + `translate(0, 100%)` in the same frame, then schedule one frame later to set `translate(0, 0)` — triggers the existing 250ms CSS ease-out transition
- **Dismiss:** animate `translate(0, 100%)`, listen for `TransitionEndEvent` → set `display:none`
- **Scrim:** fades in/out alongside the panel; click still closes

## Swipe-to-Dismiss Gesture

Register `PointerDown/Move/Up` on the full panel:
- **Down:** record `startY`, add `no-transition` USS class (kills CSS transition for responsive drag)
- **Move:** clamp delta to positive only (can't drag panel upward); apply `translate(0, deltaYpx)` inline
- **Up:** if delta > 80px → complete dismiss animation; else snap back (remove `no-transition`, set translate to 0)

## Seed Card Layout

Full-width tappable card. Tap anywhere = plant immediately + close sheet.

```
┌─────────────────────────────────────────────┐
│  [icon]  Astra Seed                    ×2   │
│  64×64   12h · ☀ Clear                      │
│          ◆ Celestial  ◆ Aurora  ◆ ?????     │
└─────────────────────────────────────────────┘
```

- **Left:** 64×64px seed icon
- **Center column:**
  - Row 1: seed name (bold)
  - Row 2: grow time (e.g. "12h") + preferred weather condition (dim text)
  - Row 3: variant chips — small colored pills per variant; discovered = variant name colored by rarity via `.rarity-*` classes; undiscovered = "?????" in dim gray
- **Right:** count badge (`×N` or `∞`)
- Subtle pressed state on tap

## Code Changes

### `GardenRoot.uxml`
- Remove `probability-panel`, `plant-button`, `selected-seed-name` elements
- Rename `seed-grid` → `seed-list`

### `SeedSlot.uxml`
Complete rewrite as horizontal card:
- `seed-row` Button (full width)
- `seed-icon` (64px)
- `seed-info` column: `seed-name`, `seed-meta` (grow time + weather), `seed-variants` (flex-row chip container)
- `seed-count` label (right-aligned)

### `SatchelUI.cs`
- Remove: `selectedSeed`, `probabilityPanel`, `probabilityGrid`, `plantButton`, `selectedSeedName`
- Remove: `OnSeedSelected`, `ShowProbabilities`, `OnPlant`
- Add: animated `Show()`/`Hide()` using scheduled translate toggle + `TransitionEndEvent`
- Add: swipe gesture with pointer events + `no-transition` class toggle
- `RefreshGrid` → `RefreshList`, callback = direct plant call

### `SeedSlotUI.cs`
- Add variant chip population (create pill VisualElements per variant, apply rarity class or "?????" if undiscovered)

### `Satchel.uss`
- Restyle `.seed-slot` as full-width horizontal card
- Add `.seed-info`, `.seed-meta`, `.seed-variants`, `.variant-chip` styles
- Add `.no-transition` utility (overrides `transition-duration: 0ms`)
- Remove grid sizing and Plant button styles
