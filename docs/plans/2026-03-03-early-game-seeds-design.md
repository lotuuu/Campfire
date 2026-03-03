# Early Game Seeds Design

## Goal

Add two very fast-growing seeds to create an immediate gameplay loop for new players, replacing the current 1-hour Basil wait.

## New Seeds

### Sprouts (Bean Sprouts) — 30 seconds

- **Growth**: 0.00833 hours (30 seconds)
- **Base drops**: 1
- **Tier**: 0
- **Mana cost**: 0
- **Recipe**:
  - Humidity: 40–80% ideal, ±20 tolerance, weight 1
  - Waterings: 1–1 ideal, ±1 tolerance, weight 0.5

### Cress (Garden Cress) — 5 minutes

- **Growth**: 0.08333 hours (5 minutes)
- **Base drops**: 1
- **Tier**: 0
- **Recipe**:
  - Heat: 10–25°C ideal, ±15 tolerance, weight 1
  - Humidity: 50–85% ideal, ±15 tolerance, weight 1

## Flame Level Changes

Two new levels inserted at the start. All existing levels shift up by 2. The new levels share entity cap and grid size with the first existing tiers (no extra capacity until old level 2).

### Upgrade Recipes (new levels 1→2 and 2→3)

| Transition | Recipe |
|------------|--------|
| 1 → 2 | 1x Sprouts_harvest |
| 2 → 3 | 5x Sprouts_harvest + 2x Cress_harvest |
| 3 → 4 | 3x Basil_harvest (was 1→2) |
| 4 → 5 | 5x Chamomile_harvest (was 2→3) |
| ... | (all subsequent shifted +2) |

### Max Entities Per Level (12 levels now, was 10)

```
6, 6, 8, 8, 12, 15, 18, 22, 26, 30, 35, 40
```

Levels 1–2 share cap 6, levels 3–4 share cap 8, then original ramp continues.

### Grid Size Per Level (12 levels)

```
2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5
```

## Building Cost Changes

### Plot Costs

| Index | Mana | Harvest Requirement |
|-------|------|---------------------|
| 0 | 150 | 1x Sprouts |
| 1 | 200 | 1x Basil |
| 2 | 260 | 2x Basil |
| 3 | 330 | 1x Chamomile |
| 4+ | unchanged | unchanged |

### Vase Costs

| Index | Mana | Harvest Requirement |
|-------|------|---------------------|
| 0 | 100 | 1x Cress |
| 1 | 120 | 2x Basil |
| 2 | 150 | 1x Chamomile |
| 3+ | unchanged | unchanged |

## New Player Setup Changes

In `GameManager.InitializeNewPlayer()`:
- Replace `AddSeed("Basil", 3)` with `AddSeed("Sprouts", 5)` and `AddSeed("Cress", 3)`

## Files to Create

- `Assets/Resources/Seeds/Sprouts.asset` — new SeedData asset
- `Assets/Resources/Seeds/Cress.asset` — new SeedData asset

## Files to Modify

- `Assets/Resources/Config/FlameConfig.asset` — insert 2 new upgrade recipes, extend maxEntities and gridSize arrays
- `Assets/Resources/Config/BuildingCostConfig.asset` — update plot and vase cost entries
- `Assets/Scripts/Managers/GameManager.cs` — change starter seeds in InitializeNewPlayer
