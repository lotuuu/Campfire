# Post-Harvest Results Popup

## Problem

The harvest result panel never displays. `ShowHarvestResult()` populates the interaction panel, then `RebuildGrid()` immediately calls `CloseInteractionPanel()`, hiding it.

Additionally, the current harvest result is minimal — just seed name, drop count, and a quality percentage. Players can't see how their growing conditions matched the recipe.

## Design

### Bug Fix

Reorder the harvest button callback: call `RebuildGrid()` first (updates grid to show empty plot), then `ShowHarvestResult()` which re-opens the panel with results.

### Data Changes

**Extend `HarvestResult`** (in PlotManager.cs):
- Add `GrowthSnapshots snapshots`, `int waterCount`, `GrowthRecipe recipe` fields
- Capture these in `Harvest()` before the plot is cleared

**Add `GrowthRecipe.EvaluatePerAxis()`**:
- Returns `List<AxisResult>` for each enabled axis
- `AxisResult`: axis name, actual value (float), ideal range (min/max), per-axis score (0-1)
- Reuses existing `ScoreRange()` logic

### UI Layout (in existing interaction panel)

```
┌─────────────────────────────┐
│        Harvested!           │
│                             │
│    [Seed Icon]  Basil x4    │
│                             │
│   Good Match (72%)          │
│                             │
│  ── Recipe Breakdown ──     │
│  Heat    22°C   (20-30°C) ✓ │
│  Wind    15m/s  (0-10m/s) ✗ │
│  Watered  2x    (2-3x)   ✓ │
│                             │
│        [ Close ]            │
└─────────────────────────────┘
```

- **Match tier**: "Perfect Match" (>=80%), "Good Match" (>=50%), "Weak Match" (<50%) — framed as recipe adherence
- **Per-axis rows**: actual average vs ideal range, color-coded pass/fail
- Only axes enabled in the recipe are shown
- If no recipe axes enabled, skip breakdown section
- Seed icon loaded from SeedData

### Files Changed

1. `Assets/Scripts/Data/GrowthRecipe.cs` — add `AxisResult` class and `EvaluatePerAxis()` method
2. `Assets/Scripts/Managers/PlotManager.cs` — extend `HarvestResult`, capture snapshot data before clearing
3. `Assets/Scripts/UI/CampsiteViewUI.cs` — fix RebuildGrid ordering, revamp `ShowHarvestResult()`
4. `Assets/UI/Styles/Interaction.uss` — add styles for harvest result elements (icon row, match badge, axis rows)
