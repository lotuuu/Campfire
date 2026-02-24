# Greenhouse Sell Feature Design
**Date:** 2026-02-24
**Status:** Approved

## Summary

Allow players to sell plants stored in the greenhouse. Tap a plant card to select it; a contextual info/sell bar appears at the bottom showing the plant's dust rate and sell price. Tap "Sell" to confirm.

---

## Backend

`GreenhouseManager.SellPlant(int index)` already exists and handles the full sell flow (removes plant, adds Dewdrops, saves, fires `OnGreenhouseChanged`). No backend changes needed.

Sell value formula: `CurrencyConfig.GetSellValue(seed.baseSellPrice, plant.qualityTier)`
Dust rate formula: `CurrencyConfig.GetDustPerHourForPlant(plant.rarity, plant.qualityTier)`

---

## UI Design

### Selection State

`GreenhouseUI` gains a `selectedIndex` field (default `-1` = nothing selected).

- Tapping a **filled** slot: sets `selectedIndex`, adds `plant-slot--selected` CSS class to highlight that card, updates the sell bar
- Tapping the **same** card again: deselects (`selectedIndex = -1`), hides sell bar
- Tapping an **empty** slot: no effect
- After a successful sell: selection cleared, `RefreshDisplay()` called

### Sell Info Bar

A `VisualElement` (`greenhouse-sell-bar`) added to the greenhouse page below the scroll view. Hidden by default (`display: none`). Shown when a plant is selected.

```
┌────────────────────────────────────────┐
│  Astra Celestial · Eternal             │
│  +8.4 Dust/hr                          │
│  [Sell for 💧 350]                     │
└────────────────────────────────────────┘
```

Elements inside the bar:
- `greenhouse-sell-name` (Label) — variant name · quality label
- `greenhouse-sell-dust` (Label) — "+X.X Dust/hr"
- `greenhouse-sell-btn` (Button) — "Sell for X Dew"

### Layout

```
greenhouse-page
├── panel-header "Greenhouse"
├── greenhouse-header (dust rate + slot count)
├── greenhouse-grid (ScrollView)        ← existing
└── greenhouse-sell-bar                 ← new, hidden until selection
    ├── greenhouse-sell-name
    ├── greenhouse-sell-dust
    └── greenhouse-sell-btn
```

---

## Files Changed

| File | Change |
|---|---|
| `Assets/UI/Documents/GardenRoot.uxml` | Add `greenhouse-sell-bar` below `greenhouse-grid` |
| `Assets/Scripts/UI/GreenhouseUI.cs` | `selectedIndex` field, tap handling, sell bar wiring |
| `Assets/UI/Styles/Greenhouse.uss` | `.plant-slot--selected` highlight + sell bar styles |
