# Mallum House Design

**Date**: 2026-03-03

## Overview

Mallum Houses are hex-grid entities that determine the player's Mallum count. Each house provides 2 Mallums. They replace the current flame-level-based Mallum cap entirely.

## Data Model

### MallumHouseConfig (ScriptableObject)

Replaces the `maxMallumsPerFlameLevel` array in `MallumConfig`:

- `mallumsPerHouse = 2` — Mallums granted per house
- `List<HouseCost> costs` — indexed by house number (0 = first house, 1 = second, etc.)
  - Each `HouseCost`: `manaCost` (float) + `List<SeedCost>` (seed name + count)
- If player tries to build beyond the table length, building is blocked (implicit hard cap via cost table size)

### MallumHouseSave

New save entry following existing pattern:
- `gridX`, `gridY` (axial hex coords)

### SaveData Changes

- Add `List<MallumHouseSave> mallumHouses` field

### CampBuildingType Enum

- Add `MallumHouse` value

## Cap Calculation

- Remove usage of `MallumConfig.GetMaxMallums(flameLevel)`
- New formula: `maxMallums = mallumHouses.Count * config.mallumsPerHouse`
- `MallumManager.GetTotalMallumCount()` reads house count from save data instead of flame level
- `EnsureMallumCount` continues to work — just fed a different target count

## Entity Cap Integration

Mallum Houses share the existing entity cap with plots, vases, and gardens. `FlameManager.CurrentEntityCount` becomes `plots + vases + gardens + mallumHouses`.

## New Player Setup

- Player starts with 1 Mallum House (placed at a default grid position)
- This gives 2 starting Mallums (up from current 1)
- First house is free (granted in `GameManager` init)

## Build UI Integration

Mallum House appears in the BuildUI craft list alongside plots and vases, showing the escalating cost for the next house.

## Cost Escalation

Costs are defined per house index in the `MallumHouseConfig` ScriptableObject table. Example:

| House # | Mana | Seeds |
|---------|------|-------|
| 1 | 0 (free, granted at start) | — |
| 2 | 30 | 2 Basil |
| 3 | 60 | 3 Lavender |
| 4+ | TBD | TBD |

Exact values to be tuned via the ScriptableObject asset.
