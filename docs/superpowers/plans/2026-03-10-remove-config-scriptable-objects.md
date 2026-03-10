# Remove Config ScriptableObjects — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate ScriptableObject config assets; ConfigService DTOs become the sole client-side data store for all game configuration, with the server as the single source of truth.

**Architecture:** ConfigService fetches JSON from the server and stores it in typed DTO classes. DTOs gain helper methods that currently live on SOs (e.g., `GetMaxEntities(level)`). Managers read from ConfigService directly. SO classes, `.asset` files, and all reflection-based patching are deleted. Plain serializable types (`FlameIngredient`, `FlameUpgradeRecipe`, `BuildingCost`, `HarvestCost`, `GrowthRecipe`, `GardenCostTier`) survive as data classes — they are not SOs.

**Tech Stack:** Unity 6 / C#, Elixir/Phoenix server

---

## File Structure

### New/Modified Files

| File | Role |
|------|------|
| `Assets/Scripts/Services/ConfigService.cs` | Expanded DTOs with methods; all config data lives here |
| `Assets/Scripts/Managers/FlameManager.cs` | Drop `[SerializeField] FlameConfig`; read from ConfigService |
| `Assets/Scripts/Managers/PlotManager.cs` | Drop seed cache + BuildingCostConfig; read from ConfigService |
| `Assets/Scripts/Managers/VaseManager.cs` | Drop VaseConfig + BuildingCostConfig refs; read from ConfigService |
| `Assets/Scripts/Managers/GardenManager.cs` | Drop plant cache + BuildingCostConfig; read from ConfigService |
| `Assets/Scripts/Managers/MallumManager.cs` | Drop QuestData/HouseConfig/BuildingCostConfig; read from ConfigService |
| `Assets/Scripts/Managers/BirdManager.cs` | Drop SeedData list; read from ConfigService |
| `Assets/Scripts/UI/BuildUI.cs` | Update FlameConfig references |
| `Assets/Scripts/UI/QuestUI.cs` | Update QuestData references |
| `Assets/Scripts/UI/CampsiteViewUI.cs` | Update SeedData/GardenPlantData references |
| `Assets/Scripts/UI/ApothekeUI.cs` | Update SeedData references |
| `Assets/Scripts/Managers/GameManager.cs` | Update config references |
| `server/priv/repo/seeds.exs` | Add `mana_caps` to flame_config; add `description` to quests |
| `server/lib/camp_fire_web/live/economy_live.ex` | Add mana_caps to flame config editor |
| All test files referencing SOs | Migrate to use DTOs directly |

### Files to Delete

| File | Reason |
|------|--------|
| `Assets/Scripts/Data/FlameConfig.cs` | Replaced by `ServerFlameConfig` |
| `Assets/Scripts/Data/SeedData.cs` | Replaced by `ServerSeedConfig` |
| `Assets/Scripts/Data/QuestData.cs` | Replaced by `ServerQuestConfig` |
| `Assets/Scripts/Data/GardenPlantData.cs` | Replaced by `ServerGardenConfig` |
| `Assets/Scripts/Data/VaseConfig.cs` | Replaced by `ServerVaseConfig` |
| `Assets/Scripts/Data/MallumConfig.cs` | Unused already |
| `Assets/Scripts/Data/MallumHouseConfig.cs` | Replaced by `ServerMallumHouseConfig` |
| `Assets/Scripts/Data/BuildingCostConfig.cs` | Replaced by ConfigService building cost accessors |
| `Assets/Resources/Config/FlameConfig.asset` + `.meta` | SO asset |
| `Assets/Resources/Config/VaseConfig.asset` + `.meta` | SO asset |
| `Assets/Resources/Config/MallumConfig.asset` + `.meta` | SO asset |
| `Assets/Resources/Config/MallumHouseConfig.asset` + `.meta` | SO asset |
| `Assets/Resources/Config/BuildingCostConfig.asset` + `.meta` | SO asset |
| `Assets/Resources/Seeds/*.asset` + `.meta` (14 files) | SO assets |
| `Assets/Resources/Quests/*.asset` + `.meta` (8 files) | SO assets |
| `Assets/Resources/GardenPlants/*.asset` + `.meta` (2 files) | SO assets |

### Surviving Data Classes (NOT deleted)

These are plain `[Serializable]` classes, not ScriptableObjects:
- `FlameIngredient`, `FlameUpgradeRecipe` — move to ConfigService.cs (or a ConfigTypes.cs)
- `BuildingCost`, `HarvestCost` — move to ConfigService.cs
- `GrowthRecipe` — stays in its own file (complex evaluation logic)
- `GardenCostTier` — move to ConfigService.cs
- `QuestReward` — replaced by `ServerQuestReward` DTO

---

## Chunk 1: Expand DTOs and Add Server Data

### Task 1: Add missing server fields — mana_caps and quest descriptions

**Files:**
- Modify: `server/priv/repo/seeds.exs`
- Modify: `server/lib/camp_fire_web/live/economy_live.ex`

- [ ] **Step 1: Add `mana_caps` to flame_config in seeds.exs**

In `seeds.exs`, inside the `flame_config` value map, add after `mana_rates`:
```elixir
"mana_caps" => [300, 500, 750, 1000, 1500, 2000, 3000, 4000, 5000, 7000, 9000, 12000],
```

- [ ] **Step 2: Add `description` to quest configs in seeds.exs**

Add `description` field to each quest map:
```elixir
%{quest_name: "SwampForage", description: "Forage in the nearby swamp for useful seeds.", duration_minutes: 5, ...},
%{quest_name: "MeadowExpedition", description: "Explore the meadow for wildflowers.", duration_minutes: 15, ...},
%{quest_name: "DeepWoodsTrek", description: "Trek deep into the woods for rare finds.", duration_minutes: 60, ...},
%{quest_name: "HighlandPass", description: "Cross the highland pass to find mountain herbs.", duration_minutes: 120, ...},
%{quest_name: "DeepMarsh", description: "Navigate the deep marsh for exotic plants.", duration_minutes: 240, ...},
%{quest_name: "MountainAscent", description: "Scale the mountain for high-altitude flora.", duration_minutes: 360, ...},
%{quest_name: "MoonlitPath", description: "Follow the moonlit path through enchanted woods.", duration_minutes: 480, ...},
%{quest_name: "AncientGrove", description: "Explore the ancient grove for legendary seeds.", duration_minutes: 720, ...},
```

Also add `description` to the QuestConfig schema if not present, and to the `replace_fields` list for quest upserts.

- [ ] **Step 3: Add `mana_caps` to the flame config admin editor/display**

In `economy_live.ex`, update `render_flame_display` and `render_flame_editor` to show/edit mana caps per level. Add `mana_caps` to the `save_flame` handler alongside `mana_rates`, `entity_caps`, `grid_sizes`.

- [ ] **Step 4: Run server seeds and verify**

```bash
cd server && mix ecto.reset && mix test
```

- [ ] **Step 5: Commit**

```bash
git add server/
git commit -m "feat(server): add mana_caps to flame config and descriptions to quests"
```

---

### Task 2: Expand ConfigService DTOs with methods and missing fields

**Files:**
- Modify: `Assets/Scripts/Services/ConfigService.cs`

The goal is to make each `Server*Config` DTO a complete replacement for its SO counterpart, with the same accessor methods.

- [ ] **Step 1: Expand `ServerFlameConfig`**

Replace the existing DTO with:
```csharp
[Serializable]
public class ServerFlameConfig
{
    public int max_flame_level;
    public List<float> mana_rates = new();
    public List<int> mana_caps = new();
    public List<int> entity_caps = new();
    public List<int> grid_sizes = new();
    public List<FlameUpgradeRecipe> upgradeRecipes = new();

    public float GetManaPerSecond(int flameLevel)
    {
        int index = Mathf.Clamp(flameLevel - 1, 0, mana_rates.Count - 1);
        return mana_rates.Count > 0 ? mana_rates[index] : 0f;
    }

    public float GetManaCap(int flameLevel)
    {
        int index = Mathf.Clamp(flameLevel - 1, 0, mana_caps.Count - 1);
        return mana_caps.Count > 0 ? mana_caps[index] : 1000f;
    }

    public int GetMaxEntities(int flameLevel)
    {
        int index = Mathf.Clamp(flameLevel - 1, 0, entity_caps.Count - 1);
        return entity_caps.Count > 0 ? entity_caps[index] : 6;
    }

    public int GetGridSize(int flameLevel)
    {
        int index = Mathf.Clamp(flameLevel - 1, 0, grid_sizes.Count - 1);
        return grid_sizes.Count > 0 ? grid_sizes[index] : 2;
    }

    public int MaxLevel => upgradeRecipes.Count + 1;

    public FlameUpgradeRecipe GetUpgradeRecipe(int currentLevel)
    {
        int index = currentLevel - 1;
        if (index < 0 || index >= upgradeRecipes.Count) return null;
        return upgradeRecipes[index];
    }
}
```

Remove `base_mana_per_second` and `mana_per_level` fields (server sends per-level `mana_rates` list instead).

- [ ] **Step 2: Expand `ServerSeedConfig`**

Add `GrowthRecipe` field:
```csharp
[Serializable]
public class ServerSeedConfig
{
    public string seedName;
    public float growthDurationHours;
    public int minDrops;
    public int maxDrops;
    public float manaCost;
    public int tier;
    public GrowthRecipe recipe;
}
```

- [ ] **Step 3: Expand `ServerQuestConfig` and add `ServerQuestReward`**

```csharp
[Serializable]
public class ServerQuestReward
{
    public string seedName;
    public float weight = 1f;
    public int minCount = 1;
    public int maxCount = 1;
}

[Serializable]
public class ServerQuestConfig
{
    public string questName;
    public string description;
    public int durationMinutes;
    public int requiredFlameLevel;
    public int rewardRolls;
    public List<ServerQuestReward> rewardPool = new();
}
```

- [ ] **Step 4: Expand `ServerMallumHouseConfig`**

Add helper method:
```csharp
[Serializable]
public class ServerMallumHouseConfig
{
    public int mallums_per_house;
    public List<BuildingCost> houseCosts = new();

    public int MallumsPerHouse => mallums_per_house;

    public int GetMaxMallums(int houseCount)
    {
        return houseCount * mallums_per_house;
    }
}
```

- [ ] **Step 5: Add building cost accessors to ConfigService**

Add these methods to `ConfigService`:
```csharp
public BuildingCost GetPlotCost(int currentCount)
{
    var costs = GetBuildingCostList("plot_costs");
    if (costs == null || costs.Count == 0) return null;
    int index = Mathf.Clamp(currentCount, 0, costs.Count - 1);
    return costs[index];
}

public BuildingCost GetVaseCost(int currentCount)
{
    var costs = GetBuildingCostList("vase_costs");
    if (costs == null || costs.Count == 0) return null;
    int index = Mathf.Clamp(currentCount, 0, costs.Count - 1);
    return costs[index];
}

public BuildingCost GetGardenCost(int currentCount)
{
    var costs = GetBuildingCostList("garden_costs");
    if (costs == null || costs.Count == 0) return null;
    int index = Mathf.Clamp(currentCount, 0, costs.Count - 1);
    return costs[index];
}

public BuildingCost GetHouseCost(int currentCount)
{
    if (_mallumHouseConfig == null) return null;
    var costs = _mallumHouseConfig.houseCosts;
    if (currentCount < 0 || currentCount >= costs.Count) return null;
    return costs[currentCount];
}

public bool CanBuildNextHouse(int currentCount) => GetHouseCost(currentCount) != null;

private List<BuildingCost> GetBuildingCostList(string key)
{
    if (_buildingCosts == null) return null;
    return _buildingCosts.TryGetValue(key, out var list) ? list : null;
}
```

Replace `_buildingCostConfig` (raw dict) with a parsed `Dictionary<string, List<BuildingCost>> _buildingCosts`.

- [ ] **Step 6: Add seed/quest/garden lookup helpers to ConfigService**

```csharp
public List<ServerSeedConfig> GetAllSeeds() => new(_seedConfigs.Values);
public List<ServerQuestConfig> GetAllQuests() => new(_questConfigs.Values);
public List<ServerGardenConfig> GetAllGardens() => new(_gardenConfigs.Values);
```

- [ ] **Step 7: Update `ParseResponse` to populate expanded DTOs**

Key changes:
- Parse `mana_rates` as `GetFloatList(flame, "mana_rates")` instead of `base_mana_per_second`/`mana_per_level`
- Parse `mana_caps` as `GetIntList(flame, "mana_caps")`
- Parse upgrade recipes directly into `List<FlameUpgradeRecipe>` on the DTO
- Parse seed recipes into `ServerSeedConfig.recipe` via `ConvertRecipe()`
- Parse quest reward pools into `List<ServerQuestReward>` on the DTO
- Parse quest `description` field
- Parse building costs into `Dictionary<string, List<BuildingCost>>` using `ParseBuildingCostList` (moved from PlotManager)
- Parse house costs into `ServerMallumHouseConfig.houseCosts`

- [ ] **Step 8: Move `ParseBuildingCostList` from PlotManager to ConfigService**

Move the static method and make it private in ConfigService. Remove from PlotManager.

- [ ] **Step 9: Verify compilation**

Check Unity console for errors.

- [ ] **Step 10: Commit**

```bash
git add Assets/Scripts/Services/ConfigService.cs
git commit -m "feat: expand ConfigService DTOs to replace ScriptableObjects"
```

---

## Chunk 2: Migrate Managers

### Task 3: Migrate FlameManager

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs`
- Modify: `Assets/Scripts/Data/FlameConfig.cs` (keep `FlameIngredient`, `FlameUpgradeRecipe`; delete SO class)
- Modify: `Assets/Scripts/UI/BuildUI.cs` (FlameConfig.CanAffordUpgrade references)

- [ ] **Step 1: Update FlameManager to read from ConfigService**

Remove `[SerializeField] private FlameConfig config` and `public FlameConfig Config => config`.
Remove `ApplyServerFlameConfig()` method entirely.

Replace properties:
```csharp
private ServerFlameConfig Config => ConfigService.Instance.FlameConfig;
public float ManaPerSecond => Config.GetManaPerSecond(Level);
public int MaxEntities => Config.GetMaxEntities(Level);
public float ManaCap => Config.GetManaCap(Level);
public int MaxLevel => Config.MaxLevel;
public FlameUpgradeRecipe GetUpgradeRecipe() => Config.GetUpgradeRecipe(Level);
public int GetGridSize() => Config.GetGridSize(Level);
```

- [ ] **Step 2: Move static helpers from FlameConfig to FlameManager**

Move `CanAffordUpgrade` and `ConsumeIngredients` from `FlameConfig` to `FlameManager` (they are static helpers that don't depend on FlameConfig state). Update all callers:
- `FlameManager.CanUpgrade()` — already in FlameManager
- `FlameManager.UpgradeFlame()` — already in FlameManager
- `BuildUI.cs` references `FlameConfig.CanAffordUpgrade` — update to `FlameManager.CanAffordUpgrade`

- [ ] **Step 3: Update all callers of `FlameManager.Instance.Config.GetGridSize()`**

These files reference `FlameManager.Instance.Config.GetGridSize(level)`:
- `GameManager.cs` — change to `FlameManager.Instance.GetGridSize()` or `ConfigService.Instance.FlameConfig.GetGridSize(level)`
- `BirdManager.cs` — same
- `VisitorManager.cs` — same
- `CampsiteViewUI.cs` — same

- [ ] **Step 4: Update `BuildUI.cs`**

Update the flame upgrade card to use `FlameManager.Instance.GetUpgradeRecipe()` instead of `FlameManager.Instance.Config.GetUpgradeRecipe(level)`.

- [ ] **Step 5: Delete FlameConfig SO class**

In `FlameConfig.cs`, keep `FlameIngredient` and `FlameUpgradeRecipe` (move them to a new `Assets/Scripts/Data/ConfigTypes.cs` or into `ConfigService.cs`). Delete the `FlameConfig : ScriptableObject` class.

- [ ] **Step 6: Delete asset files**

```bash
rm Assets/Resources/Config/FlameConfig.asset Assets/Resources/Config/FlameConfig.asset.meta
```

- [ ] **Step 7: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: migrate FlameManager from FlameConfig SO to ConfigService"
```

---

### Task 4: Migrate PlotManager (seeds + building costs)

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

- [ ] **Step 1: Replace seed cache with ConfigService lookups**

Remove `_seedCache` dictionary and `Resources.LoadAll<SeedData>("Seeds")` from Awake.
Remove `ApplyServerSeedConfigs()` entirely.
Remove `ApplyServerBuildingCostConfig()` entirely.
Remove `LoadBuildingCostConfig()` and `buildingCostConfig` field.
Remove `ParseBuildingCostList()` (already moved to ConfigService in Task 2).

Replace `LoadSeed(string name)` with:
```csharp
private static ServerSeedConfig LoadSeed(string seedName)
{
    return ConfigService.Instance?.GetSeed(seedName);
}
```

- [ ] **Step 2: Update all seed field accesses**

Throughout PlotManager, replace:
- `seed.growthDurationHours` → same (field name matches DTO)
- `seed.recipe.Evaluate(...)` → same (recipe is now on DTO)
- `seed.minDrops`, `seed.maxDrops` → same
- `seed.seedName` → same

Replace `GetNextPlotCost()`:
```csharp
public BuildingCost GetNextPlotCost()
{
    return ConfigService.Instance?.GetPlotCost(SaveManager.Instance.Data.plots.Count);
}
```

- [ ] **Step 3: Update CraftPlot to use ConfigService costs**

Remove `LoadBuildingCostConfig()` call. Use `GetNextPlotCost()` which now reads from ConfigService.

- [ ] **Step 4: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: migrate PlotManager from SeedData/BuildingCostConfig SOs to ConfigService"
```

---

### Task 5: Migrate VaseManager

**Files:**
- Modify: `Assets/Scripts/Managers/VaseManager.cs`

- [ ] **Step 1: Replace VaseConfig SO with ConfigService**

Remove `[SerializeField] private VaseConfig config` field.
Remove `ApplyServerVaseConfig()` entirely.
Remove `LoadBuildingCostConfig()` and `buildingCostConfig` field.

Replace property accesses:
- `config.FillDurationMinutes` → `ConfigService.Instance.VaseConfig.fill_duration_minutes`
- `config.BaseCapacity` → `ConfigService.Instance.VaseConfig.default_capacity`

Replace `GetNextVaseCost()`:
```csharp
public BuildingCost GetNextVaseCost()
{
    return ConfigService.Instance?.GetVaseCost(SaveManager.Instance.Data.vases.Count);
}
```

- [ ] **Step 2: Update callers of `VaseManager.Instance.Config`**

- `MallumManager` references `VaseManager.Instance.Config.FillDurationMinutes`
- `GameManager` references `VaseManager.Instance.Config.BaseCapacity`

Update both to use `ConfigService.Instance.VaseConfig.fill_duration_minutes` / `.default_capacity`.

- [ ] **Step 3: Delete VaseConfig SO**

Delete `Assets/Scripts/Data/VaseConfig.cs`, `Assets/Resources/Config/VaseConfig.asset` + `.meta`.

- [ ] **Step 4: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: migrate VaseManager from VaseConfig SO to ConfigService"
```

---

### Task 6: Migrate GardenManager

**Files:**
- Modify: `Assets/Scripts/Managers/GardenManager.cs`

- [ ] **Step 1: Replace plant cache with ConfigService lookups**

Remove `_plantCache` dictionary and `Resources.LoadAll<GardenPlantData>("GardenPlants")` from Awake.
Remove `ApplyServerGardenConfigs()` entirely.
Remove `LoadBuildingCostConfig()` and `_buildingCostConfig` field.

Replace `LoadPlantData(string name)`:
```csharp
private static ServerGardenConfig LoadPlantData(string plantName)
{
    return ConfigService.Instance?.GetGarden(plantName);
}
```

- [ ] **Step 2: Update all field accesses**

Throughout GardenManager, replace:
- `plantData.growthDurationHours` → same
- `plantData.yieldItem` → same
- `plantData.yieldAmount` → same
- `plantData.yieldIntervalHours` → same
- `plantData.waterRequired` → same
- `plantData.plantName` → same

Replace `GetNextGardenCost()`:
```csharp
public BuildingCost GetNextGardenCost()
{
    return ConfigService.Instance?.GetGardenCost(SaveManager.Instance.Data.gardens.Count);
}
```

- [ ] **Step 3: Delete GardenPlantData SO**

Delete `Assets/Scripts/Data/GardenPlantData.cs`, `Assets/Resources/GardenPlants/*.asset` + `.meta`.

- [ ] **Step 4: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: migrate GardenManager from GardenPlantData SO to ConfigService"
```

---

### Task 7: Migrate MallumManager (quests + house config)

**Files:**
- Modify: `Assets/Scripts/Managers/MallumManager.cs`
- Modify: `Assets/Scripts/UI/QuestUI.cs`

- [ ] **Step 1: Replace quest cache with ConfigService lookups**

Remove `allQuests` array and `Resources.LoadAll<QuestData>("Quests")` from Awake.
Remove `ApplyServerQuestConfigs()` entirely.
Remove `ApplyServerHouseConfig()` entirely.
Remove `LoadBuildingCostConfig()` and `buildingCostConfig` field.
Remove `[SerializeField] private MallumHouseConfig houseConfig`.

Replace quest lookups with ConfigService:
```csharp
private ServerQuestConfig FindQuest(string questName) => ConfigService.Instance?.GetQuest(questName);
```

- [ ] **Step 2: Update `QuestReward` references in reward rolling**

The current `RollRewards` uses `QuestReward.seed` (a SeedData reference). Replace with `ServerQuestReward.seedName` (string). The reward rolling logic adds seed names to inventory, so this is mostly renaming.

- [ ] **Step 3: Update `QuestUI`**

Replace `Resources.LoadAll<QuestData>("Quests")` with `ConfigService.Instance.GetAllQuests()`. Update all `quest.description`, `quest.requiredFlameLevel` etc. to use `ServerQuestConfig` fields. Update reward pool display to use `ServerQuestReward.seedName` instead of `QuestReward.seed.seedName`.

- [ ] **Step 4: Replace house config**

Replace `houseConfig.GetMaxMallums(count)` with `ConfigService.Instance.MallumHouseConfig.GetMaxMallums(count)`.
Replace `houseConfig.MallumsPerHouse` with `ConfigService.Instance.MallumHouseConfig.MallumsPerHouse`.
Replace `GetNextHouseCost()`:
```csharp
public BuildingCost GetNextHouseCost()
{
    return ConfigService.Instance?.GetHouseCost(SaveManager.Instance.Data.mallumHouses.Count);
}
```

- [ ] **Step 5: Update all callers of `MallumManager.Instance.HouseConfig`**

- `GameService.cs` — use `ConfigService.Instance.MallumHouseConfig`
- `GameManager.cs` — same
- `CampsiteViewUI.cs` — same

- [ ] **Step 6: Delete QuestData and MallumHouseConfig SOs**

Delete `Assets/Scripts/Data/QuestData.cs`, `Assets/Scripts/Data/MallumHouseConfig.cs`.
Delete `Assets/Resources/Quests/*.asset` + `.meta`, `Assets/Resources/Config/MallumHouseConfig.asset` + `.meta`.

- [ ] **Step 7: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: migrate MallumManager/QuestUI from QuestData/MallumHouseConfig SOs to ConfigService"
```

---

### Task 8: Migrate BirdManager and remaining consumers

**Files:**
- Modify: `Assets/Scripts/Managers/BirdManager.cs`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`

- [ ] **Step 1: Migrate BirdManager**

Remove `allSeeds` list and `Resources.LoadAll<SeedData>("Seeds")`.
Replace with `ConfigService.Instance.GetAllSeeds()`.
Update `GetEligibleSeeds` and `RollSeedDrop` to take `List<ServerSeedConfig>` instead of `List<SeedData>`.

- [ ] **Step 2: Migrate CampsiteViewUI**

Replace any `Resources.LoadAll<SeedData>` or `Resources.LoadAll<GardenPlantData>` calls with ConfigService lookups. Update field accesses.

- [ ] **Step 3: Migrate ApothekeUI**

Replace `Resources.LoadAll<SeedData>("Seeds")` with ConfigService lookup. Update seed name references.

- [ ] **Step 4: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: migrate BirdManager, CampsiteViewUI, ApothekeUI to ConfigService"
```

---

## Chunk 3: Cleanup

### Task 9: Delete remaining SO files and BuildingCostConfig

**Files:**
- Delete: `Assets/Scripts/Data/SeedData.cs`
- Delete: `Assets/Scripts/Data/BuildingCostConfig.cs`
- Delete: `Assets/Scripts/Data/MallumConfig.cs` (already unused)
- Delete: `Assets/Scripts/Data/FlameConfig.cs` (if not already deleted in Task 3)
- Delete: All `.asset` + `.meta` files listed in File Structure above
- Modify: `Assets/Scripts/Data/ConfigTypes.cs` (new file for surviving types)

- [ ] **Step 1: Create ConfigTypes.cs for surviving serializable types**

Move `FlameIngredient`, `FlameUpgradeRecipe`, `BuildingCost`, `HarvestCost`, `GardenCostTier` into `Assets/Scripts/Data/ConfigTypes.cs`. These are plain `[Serializable]` classes, not SOs.

- [ ] **Step 2: Delete all SO class files**

```bash
rm Assets/Scripts/Data/SeedData.cs Assets/Scripts/Data/SeedData.cs.meta
rm Assets/Scripts/Data/BuildingCostConfig.cs Assets/Scripts/Data/BuildingCostConfig.cs.meta
rm Assets/Scripts/Data/MallumConfig.cs Assets/Scripts/Data/MallumConfig.cs.meta
rm Assets/Scripts/Data/GardenPlantData.cs Assets/Scripts/Data/GardenPlantData.cs.meta
rm Assets/Scripts/Data/VaseConfig.cs Assets/Scripts/Data/VaseConfig.cs.meta
rm Assets/Scripts/Data/MallumHouseConfig.cs Assets/Scripts/Data/MallumHouseConfig.cs.meta
rm Assets/Scripts/Data/FlameConfig.cs Assets/Scripts/Data/FlameConfig.cs.meta
rm Assets/Scripts/Data/QuestData.cs Assets/Scripts/Data/QuestData.cs.meta
```

- [ ] **Step 3: Delete all config .asset files**

```bash
rm Assets/Resources/Config/FlameConfig.asset Assets/Resources/Config/FlameConfig.asset.meta
rm Assets/Resources/Config/VaseConfig.asset Assets/Resources/Config/VaseConfig.asset.meta
rm Assets/Resources/Config/MallumConfig.asset Assets/Resources/Config/MallumConfig.asset.meta
rm Assets/Resources/Config/MallumHouseConfig.asset Assets/Resources/Config/MallumHouseConfig.asset.meta
rm Assets/Resources/Config/BuildingCostConfig.asset Assets/Resources/Config/BuildingCostConfig.asset.meta
rm Assets/Resources/Seeds/*.asset Assets/Resources/Seeds/*.asset.meta
rm Assets/Resources/Quests/*.asset Assets/Resources/Quests/*.asset.meta
rm Assets/Resources/GardenPlants/*.asset Assets/Resources/GardenPlants/*.asset.meta
```

- [ ] **Step 4: Remove the scene references to deleted SOs**

The Unity scene (`Assets/Scenes/Garden.unity`) may have serialized references to FlameConfig, VaseConfig, and MallumHouseConfig on manager GameObjects (via `[SerializeField]`). These references will become `{fileID: 0}` after asset deletion, which is fine — the managers no longer read from them. But clean up any warnings by removing the `[SerializeField]` fields (done in earlier tasks).

- [ ] **Step 5: Verify compilation + commit**

```bash
git add -A && git commit -m "refactor: delete all config ScriptableObject classes and assets"
```

---

### Task 10: Migrate tests

**Files:**
- Modify: `Assets/Tests/EditMode/TestFlameConfig.cs`
- Modify: `Assets/Tests/EditMode/TestMallumData.cs`
- Modify: `Assets/Tests/EditMode/TestSeedData.cs`
- Modify: `Assets/Tests/EditMode/TestMallumManager.cs`
- Modify: `Assets/Tests/EditMode/TestBirdManager.cs`
- Modify: `Assets/Tests/EditMode/TestGardenManager.cs`
- Modify: `Assets/Tests/EditMode/TestMallumHouse.cs`

- [ ] **Step 1: Migrate TestFlameConfig**

Replace `ScriptableObject.CreateInstance<FlameConfig>()` with `new ServerFlameConfig { ... }`. Test the same methods (GetMaxEntities, GetManaCap, etc.) on the DTO. Move `CanAffordUpgrade`/`ConsumeIngredients` tests to use `FlameManager.CanAffordUpgrade`.

- [ ] **Step 2: Migrate TestSeedData**

Replace `ScriptableObject.CreateInstance<SeedData>()` with `new ServerSeedConfig { ... }`. Test that fields are assignable.

- [ ] **Step 3: Migrate TestMallumData**

Replace `ScriptableObject.CreateInstance<MallumConfig>()` with `new ServerMallumHouseConfig { ... }`. (MallumConfig was already unused; test was testing its `GetMaxMallums` method.)

- [ ] **Step 4: Migrate TestMallumManager**

Replace `QuestReward { seed = ScriptableObject.CreateInstance<SeedData>() }` with `ServerQuestReward { seedName = "TestSeed" }`.

- [ ] **Step 5: Migrate TestBirdManager**

Replace `ScriptableObject.CreateInstance<SeedData>()` with `new ServerSeedConfig { ... }` throughout. Update `CreateTestSeeds()` helper. Update method signatures that changed from `List<SeedData>` to `List<ServerSeedConfig>`.

- [ ] **Step 6: Migrate TestGardenManager**

Replace `ScriptableObject.CreateInstance<BuildingCostConfig>()` tests. These tested `GetGardenCost` — rewrite to test `ConfigService.GetGardenCost` or test the DTO accessor directly.

- [ ] **Step 7: Migrate TestMallumHouse**

Replace `ScriptableObject.CreateInstance<MallumHouseConfig>()` with `new ServerMallumHouseConfig { ... }`.
Replace `ScriptableObject.CreateInstance<BuildingCostConfig>()` tests.

- [ ] **Step 8: Run all tests**

```
Unity Test Runner → EditMode → Run All
```

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "test: migrate all tests from ScriptableObjects to ConfigService DTOs"
```

---

### Task 11: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update architecture section**

Remove all references to ScriptableObjects as config containers. Update to document that ConfigService DTOs are the sole data model. Remove the "ScriptableObject Data Model" section and replace with a "ConfigService Data Model" section. Remove the YAML editing guidance for .asset files. Update key file locations to remove deleted asset paths.

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md && git commit -m "docs: update CLAUDE.md to reflect ConfigService-only architecture"
```
