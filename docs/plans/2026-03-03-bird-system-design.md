# Bird System Design

## Overview

Birds are random visitors that land on unoccupied hex tiles and leave behind seeds when collected. They provide a passive seed income stream that rewards players for checking in regularly and scales with flame level progression.

## Data Model

### SeedData Addition

Add `int tier` field (1-9) to `SeedData`. Tier determines which seeds are available at each flame level (`tier <= flameLevel`) and scales bird drop quantity.

| Tier | Seeds |
|------|-------|
| 1 | Basil, Chamomile |
| 2 | Snowdrop, Marigold |
| 3 | Mint, Pansy |
| 4 | Lavender |
| 5 | Poppy |
| 6 | Jasmine |
| 7 | Rosemary |
| 8 | Dahlia |
| 9 | Moonflower |

### SaveData Additions

```
BirdSave { gridX, gridY, seedName, seedCount }
SaveData.birds: List<BirdSave>
SaveData.lastBirdCheckHourUtc: string (ISO 8601, hour boundary)
```

## BirdManager (MonoBehaviour Singleton)

### Hourly Check Logic

Runs in `Update()`. Processes on-the-hour UTC boundaries (1:00, 2:00, etc.):

1. Truncate `GameTime.UtcNow` to the hour
2. If `lastBirdCheckHourUtc` is null, initialize to current hour and return
3. Walk from `(lastBirdCheckHour + 1h)` to `currentHour`, for each hour:
   - `effectiveChance = 0.33 * pow(0.5, currentBirdCount)`
   - Roll random. On success + free tiles exist: pick random free tile, roll seed, create BirdSave, increment currentBirdCount
4. Update `lastBirdCheckHourUtc` to current hour
5. Save if any birds were placed

Supports full offline catch-up: all missed hours are processed sequentially on app open.

### Seed Rolling

1. Load all SeedData from `Resources/Seeds`
2. Filter to `seed.tier <= flameLevel`
3. Pick uniformly random from eligible seeds
4. Quantity: `baseCount = max(1, flameLevel - seed.tier + 1)`, then `Random.Range(max(1, baseCount - 1), baseCount + 2)` for slight randomization

### Free Tile Detection

Build occupied set from: flame (0,0), plots, vases, gardens, mallumHouses, apotheke, existing birds. Enumerate all hex coords within grid radius. Free = valid coords not in occupied set.

### Bird Collection

`CollectBird(int index)`:
1. Get BirdSave at index
2. Call `ApothekeManager.Instance.AddSeed(seedName, seedCount)`
3. Remove BirdSave from list
4. Fire `OnBirdCollected` event
5. Save

Static helper `CollectBirdStatic(SaveData, int)` for testability.

## UI Integration

- Add `CampBuildingType.Bird` to enum
- `CampsiteViewUI.RebuildGrid()` includes birds in occupied dictionary
- Bird tiles render with bird icon + seed info label
- Click in Normal mode calls `BirdManager.Instance.CollectBird(index)`
- Birds do NOT count toward entity cap
- Bird tiles are NOT valid placement targets (occupied)

## Edge Cases

- No free tiles: bird doesn't appear, hour still marked as processed
- Bird blocks building placement: intentional, player must collect first
- Moving buildings onto bird tiles: prevented by occupied check
- Very long offline periods: all hours processed, but bird count halving means diminishing returns naturally (e.g., after 24 hours offline with no collection, chance per hour approaches ~0 after 5-6 birds)
