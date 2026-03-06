# Garden Building via Build Menu

## Summary

Add gardens to the build menu so players can build them. Each garden plant type (Oak, BerryBush) gets its own build entry with deterministic per-copy scaling costs (mana + seeds).

## Flow

Player opens Build menu → sees one entry per garden plant type → taps one → enters hex placement mode → taps cell → pays mana + seeds → GardenSave created with plant pre-filled → growth starts immediately (single-step, no empty garden slot).

## Cost Model

Each `GardenPlantData` has a `List<GardenCostTier>` where each tier defines:
- `manaCost` (float) — mana to spend
- `seedCost` (int) — number of the plant's `yieldItem` to consume (e.g. Acorns for Oak)

The tier index matches the number of existing copies of that plant type. If the player already has as many copies as there are tiers, they cannot build more (hard cap).

`GetCost(int existingCount)` returns the tier at that index, or null if capped.

Example (Oak):
- 1st: 200 mana + 1 Acorn
- 2nd: 300 mana + 2 Acorns
- 3rd: 500 mana + 3 Acorns

The old `manaCost` field on GardenPlantData is replaced by this list. `waterRequired` remains separate (consumed when growth starts, not at build time).

Cost scaling is per-plant-type: having 3 Oaks doesn't affect BerryBush pricing.

## Components Changed

1. **GardenPlantData** — Replace `manaCost` with `List<GardenCostTier>` and add `GetCost(int existingCount)` method. Add `[Serializable] public class GardenCostTier { float manaCost; int seedCost; }`.

2. **GardenManager** — Add `CraftGarden(string plantName, int gridX, int gridY)`: checks entity cap, gets cost tier based on existing count of that plant, checks mana + items, spends them, creates GardenSave with plant fields pre-filled (plantName, plantTimeUtc, gridX, gridY), saves, fires OnGardenChanged, notifies server.

3. **BuildUI** — Add one entry per GardenPlantData (loaded from Resources/GardenPlants). Shows current cost for next copy, or "Max reached" if capped. Clicking fires OnRequestPlacement with plant info. Need to pass selected plant type through placement — add a `selectedGardenPlant` field on BuildUI or CampsiteViewUI.

4. **CampsiteViewUI** — Handle `CampBuildingType.Garden` in placement mode, pass selected plant name to `GardenManager.CraftGarden`.

5. **Asset files** — Update Oak.asset and BerryBush.asset with cost tier lists and actual values.

## Entity Cap

Gardens already count toward the shared entity cap in `FlameManager.CurrentEntityCount`. The `CraftGarden` method checks `FlameManager.Instance.CanPlaceEntity` before proceeding, same as plots and vases.
