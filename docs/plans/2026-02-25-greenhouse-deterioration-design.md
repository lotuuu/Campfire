# Greenhouse Plant Deterioration — Design

**Date:** 2026-02-25

## Overview

Greenhouse plants slowly degrade in quality over real time, reducing their sell value and AuraDust output. Once a plant reaches the Withered state it blocks its slot until the player manually trashes it. This creates ongoing tension between harvesting early (lower quality) vs. holding for higher AuraDust output (risking deterioration).

## Decay Schedule

Each tier has a total lifetime from placement to withering. Step duration is derived by subtracting the next tier's total.

| Starting tier | Total time to wither | Step duration (this tier only) |
|---|---|---|
| S | 12 h | 6 h (S → A) |
| A | 6 h | 4 h (A → B) |
| B | 2 h | 1 h (B → C) |
| C | 1 h | 40 min (C → D) |
| D | 20 min | 20 min (D → Withered) |

The D tier acts as a final warning with a tight 20-minute window. Decay uses real clock time (`GameTime.UtcNow`) so it continues while the app is closed.

## Data Model

### `GreenhousePlantSave` (new fields)
- `tierStartTimeUtc` (string) — UTC timestamp of when the current tier began. If missing on load, defaults to `GameTime.UtcNow` (backward-compatible).
- `isWithered` (bool) — true once plant has passed below D. Missing on load defaults to `false`.

### `GreenhousePlant` (runtime, new fields)
- `DateTime tierStartTime`
- `bool isWithered`

### Withered plant rules
- Generates 0 AuraDust
- Cannot be sold for Dewdrops
- Blocks its greenhouse slot until trashed

## Logic — GreenhouseManager

### Update() decay tick
Each frame, for each non-withered plant:
1. `elapsed = GameTime.UtcNow - plant.tierStartTime`
2. Lookup step threshold for `plant.qualityTier`
3. If `elapsed >= threshold`:
   - If tier == D: set `isWithered = true`
   - Else: downgrade tier one step
   - Reset `plant.tierStartTime = GameTime.UtcNow`
   - Save and fire `OnGreenhouseChanged`

Step thresholds (in minutes):
- S: 360, A: 240, B: 60, C: 40, D: 20

### New public methods
- `TrashPlant(int index)` — removes plant, no currency, saves, fires `OnGreenhouseChanged`
- `GetDecayProgress(int index)` — returns `float 0..1` (elapsed / threshold) for UI display

### GetTotalDustPerSecond()
Skip withered plants (they contribute 0).

### DebugAdvanceTime(hours)
Also backdates `tierStartTime` by the specified duration so decay can be tested.

## UI — GreenhouseUI

### Slot display
- Each filled slot shows a thin decay progress indicator (fills as plant approaches next downgrade)
- Color progression: green → yellow → orange → red (at D tier)
- Withered slots: name label shows "Withered", swatch fades to grey, USS class `plant-slot--withered` applied

### Sell bar
- Normal plant: existing "Sell for X Dew" button unchanged
- Withered plant: sell button replaced with "Trash" (no value shown), wired to `TrashPlant()`

### Refresh
`GreenhouseUI` already subscribes to `OnGreenhouseChanged` via `RefreshDisplay()` — no polling needed, tier drops trigger automatic UI updates.

## Backward Compatibility

Existing saves load cleanly:
- `tierStartTimeUtc` missing → set to `GameTime.UtcNow` on load (starts decaying from first open)
- `isWithered` missing → `false` (plant is alive)
- `qualityTier` already saved — no change

## Out of Scope

- Rarity affecting decay rate (quality tier is the only decay driver)
- Salvage sell value for withered plants
- Auto-clearing withered plants
