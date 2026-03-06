# Garden Building Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow players to build gardens from the build menu with deterministic per-plant-type scaling costs (mana + yield items).

**Architecture:** Add `GardenCostTier` data to `GardenPlantData`, a `CraftGarden` static method on `GardenManager`, a garden section in `BuildUI`, and a placement case in `CampsiteViewUI`. Single-step flow: pick plant → place on grid → pay costs → garden starts growing.

**Tech Stack:** Unity C#, ScriptableObjects, UI Toolkit

---

### Task 1: Add GardenCostTier and update GardenPlantData

**Files:**
- Modify: `Assets/Scripts/Data/GardenPlantData.cs`

**Step 1: Write the failing test**

Add to `Assets/Tests/EditMode/TestGardenManager.cs`:

```csharp
[Test]
public void GardenPlantData_GetCost_ReturnsCorrectTier()
{
    var data = ScriptableObject.CreateInstance<GardenPlantData>();
    data.costTiers = new System.Collections.Generic.List<GardenCostTier>
    {
        new GardenCostTier { manaCost = 200, seedCost = 1 },
        new GardenCostTier { manaCost = 300, seedCost = 2 },
    };
    Assert.AreEqual(200, data.GetCost(0).manaCost);
    Assert.AreEqual(2, data.GetCost(1).seedCost);
    Assert.IsNull(data.GetCost(2));
}
```

**Step 2: Run test to verify it fails**

Run via Unity MCP `run_tests` with `test_names: ["Garden.Tests.TestGardenManager.GardenPlantData_GetCost_ReturnsCorrectTier"]`
Expected: FAIL — `GardenCostTier` doesn't exist yet.

**Step 3: Write minimal implementation**

In `Assets/Scripts/Data/GardenPlantData.cs`, add the `GardenCostTier` class and update `GardenPlantData`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class GardenCostTier
    {
        public float manaCost;
        public int seedCost;
    }

    [CreateAssetMenu(fileName = "NewGardenPlant", menuName = "CampFire/Garden Plant Data")]
    public class GardenPlantData : ScriptableObject
    {
        public string plantName;
        public float growthDurationHours = 24f;
        public string yieldItem;
        public int yieldAmount = 1;
        public float yieldIntervalHours = 12f;
        public int waterRequired = 3;

        [Header("Building Costs")]
        public List<GardenCostTier> costTiers = new();

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;
        public Sprite matureSprite;

        public GardenCostTier GetCost(int existingCount)
        {
            if (existingCount < 0 || existingCount >= costTiers.Count) return null;
            return costTiers[existingCount];
        }
    }
}
```

Note: This removes the old `manaCost` field. The `GardenManager.Plant()` method references `plantData.manaCost` — that will be removed in Task 3 since `CraftGarden` replaces the need for a separate plant step. Also update `ApplyServerGardenConfigs` to stop setting `manaCost`.

**Step 4: Run test to verify it passes**

Run via Unity MCP `run_tests` with `test_names: ["Garden.Tests.TestGardenManager.GardenPlantData_GetCost_ReturnsCorrectTier"]`
Expected: PASS

**Step 5: Commit**

```
git add Assets/Scripts/Data/GardenPlantData.cs Assets/Tests/EditMode/TestGardenManager.cs
git commit -m "feat: add GardenCostTier with deterministic per-copy costs"
```

---

### Task 2: Add CraftGarden static method to GardenManager

**Files:**
- Modify: `Assets/Scripts/Managers/GardenManager.cs`
- Modify: `Assets/Tests/EditMode/TestGardenManager.cs`

**Step 1: Write the failing test**

Add to `Assets/Tests/EditMode/TestGardenManager.cs`:

```csharp
[Test]
public void CraftGarden_Success_CreatesGardenAndSpendsCosts()
{
    var data = new SaveData();
    data.items.Add(new InventoryItem { itemName = "Acorn", count = 5 });
    data.mana = 500f;

    var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
    plantData.plantName = "Oak";
    plantData.yieldItem = "Acorn";
    plantData.waterRequired = 3;
    plantData.growthDurationHours = 48;
    plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
    {
        new GardenCostTier { manaCost = 200, seedCost = 1 },
        new GardenCostTier { manaCost = 300, seedCost = 2 },
    };

    // First garden
    bool result = GardenManager.TryCraftGarden(data, plantData, 2, 3);
    Assert.IsTrue(result);
    Assert.AreEqual(1, data.gardens.Count);
    Assert.AreEqual("Oak", data.gardens[0].plantName);
    Assert.AreEqual(2, data.gardens[0].gridX);
    Assert.AreEqual(3, data.gardens[0].gridY);
    Assert.AreEqual(300f, data.mana, 0.01f); // 500 - 200
    Assert.AreEqual(4, data.items[0].count);  // 5 - 1
}

[Test]
public void CraftGarden_ScalingCost_SecondCostsMore()
{
    var data = new SaveData();
    data.items.Add(new InventoryItem { itemName = "Acorn", count = 10 });
    data.mana = 1000f;

    var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
    plantData.plantName = "Oak";
    plantData.yieldItem = "Acorn";
    plantData.waterRequired = 0;
    plantData.growthDurationHours = 48;
    plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
    {
        new GardenCostTier { manaCost = 200, seedCost = 1 },
        new GardenCostTier { manaCost = 300, seedCost = 2 },
    };

    GardenManager.TryCraftGarden(data, plantData, 0, 0);
    GardenManager.TryCraftGarden(data, plantData, 1, 0);

    Assert.AreEqual(2, data.gardens.Count);
    Assert.AreEqual(500f, data.mana, 0.01f); // 1000 - 200 - 300
    Assert.AreEqual(7, data.items[0].count);  // 10 - 1 - 2
}

[Test]
public void CraftGarden_AtCap_ReturnsFalse()
{
    var data = new SaveData();
    data.items.Add(new InventoryItem { itemName = "Acorn", count = 10 });
    data.mana = 1000f;

    var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
    plantData.plantName = "Oak";
    plantData.yieldItem = "Acorn";
    plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
    {
        new GardenCostTier { manaCost = 100, seedCost = 1 },
    };

    GardenManager.TryCraftGarden(data, plantData, 0, 0);
    bool result = GardenManager.TryCraftGarden(data, plantData, 1, 0);

    Assert.IsFalse(result);
    Assert.AreEqual(1, data.gardens.Count);
}

[Test]
public void CraftGarden_CantAffordMana_ReturnsFalse()
{
    var data = new SaveData();
    data.items.Add(new InventoryItem { itemName = "Acorn", count = 5 });
    data.mana = 50f;

    var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
    plantData.plantName = "Oak";
    plantData.yieldItem = "Acorn";
    plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
    {
        new GardenCostTier { manaCost = 200, seedCost = 1 },
    };

    bool result = GardenManager.TryCraftGarden(data, plantData, 0, 0);
    Assert.IsFalse(result);
    Assert.AreEqual(0, data.gardens.Count);
    Assert.AreEqual(50f, data.mana, 0.01f);
}

[Test]
public void CraftGarden_CantAffordItems_ReturnsFalse()
{
    var data = new SaveData();
    data.mana = 1000f;
    // No acorns in inventory

    var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
    plantData.plantName = "Oak";
    plantData.yieldItem = "Acorn";
    plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
    {
        new GardenCostTier { manaCost = 200, seedCost = 1 },
    };

    bool result = GardenManager.TryCraftGarden(data, plantData, 0, 0);
    Assert.IsFalse(result);
    Assert.AreEqual(0, data.gardens.Count);
}
```

**Step 2: Run tests to verify they fail**

Run via Unity MCP `run_tests` with `test_names: ["Garden.Tests.TestGardenManager"]`
Expected: FAIL — `TryCraftGarden` doesn't exist.

**Step 3: Write minimal implementation**

Add to `Assets/Scripts/Managers/GardenManager.cs` a static method (testable without MonoBehaviour):

```csharp
/// <summary>
/// Static helper for testability. Checks costs, spends mana + items, creates GardenSave.
/// Does NOT check entity cap (caller must check FlameManager.CanPlaceEntity).
/// Does NOT notify server or trigger save (caller handles that).
/// </summary>
public static bool TryCraftGarden(SaveData data, GardenPlantData plantData, int gridX, int gridY)
{
    int existingCount = 0;
    foreach (var g in data.gardens)
        if (g.plantName == plantData.plantName) existingCount++;

    var cost = plantData.GetCost(existingCount);
    if (cost == null) return false;

    // Check mana
    if (data.mana < cost.manaCost) return false;

    // Check items
    if (cost.seedCost > 0)
    {
        var item = data.items.Find(it => it.itemName == plantData.yieldItem);
        if (item == null || item.count < cost.seedCost) return false;
    }

    // Spend mana
    data.mana -= cost.manaCost;

    // Spend items
    if (cost.seedCost > 0)
    {
        var item = data.items.Find(it => it.itemName == plantData.yieldItem);
        item.count -= cost.seedCost;
    }

    // Create garden
    var garden = new GardenSave
    {
        plantName = plantData.plantName,
        plantTimeUtc = GameTime.UtcNow.ToString("o"),
        mature = false,
        gridX = gridX,
        gridY = gridY
    };
    data.gardens.Add(garden);

    return true;
}
```

Then add the instance method that wraps it (handles entity cap, save, events, server):

```csharp
public bool CraftGarden(string plantName, int gridX, int gridY)
{
    if (FlameManager.Instance != null && !FlameManager.Instance.CanPlaceEntity) return false;

    var plantData = LoadPlantData(plantName);
    if (plantData == null) return false;

    var data = SaveManager.Instance.Data;
    if (!TryCraftGarden(data, plantData, gridX, gridY)) return false;

    SaveManager.Instance.Save();
    int index = data.gardens.Count - 1;
    OnGardenChanged?.Invoke(index);

    // Notify server
    if (GameService.Instance != null && GameService.Instance.IsOnline)
    {
        _ = NotifyServerAsync(data.gardens[index], plantName, gridX, gridY);
    }

    return true;
}

private async Task NotifyServerAsync(GardenSave garden, string plantName, int gridX, int gridY)
{
    var result = await GameService.Instance.PlantGarden(plantName, gridX, gridY);
    if (result != null) garden.serverId = result.id;
}
```

Also remove the old `Plant()` method's `manaCost` check (line 70-71 in current file) since `manaCost` field is gone. Replace with just the water check. The `Plant()` method itself can stay for now (backward compat with any code that creates empty garden slots), but remove the mana spending from it.

**Step 4: Run tests to verify they pass**

Run via Unity MCP `run_tests` with `test_names: ["Garden.Tests.TestGardenManager"]`
Expected: All PASS

**Step 5: Commit**

```
git add Assets/Scripts/Managers/GardenManager.cs Assets/Tests/EditMode/TestGardenManager.cs
git commit -m "feat: add GardenManager.CraftGarden with per-type scaling costs"
```

---

### Task 3: Clean up GardenManager.Plant() and ApplyServerGardenConfigs

**Files:**
- Modify: `Assets/Scripts/Managers/GardenManager.cs`

**Step 1: Remove `manaCost` references from `Plant()` method**

In `GardenManager.Plant()`, remove lines 70-71:
```csharp
// DELETE these lines:
if (plantData.manaCost > 0 && !CurrencyManager.Instance.SpendMana(plantData.manaCost))
    return false;
```

**Step 2: Update `ApplyServerGardenConfigs` to stop setting `manaCost`**

In `ApplyServerGardenConfigs()`, remove line 49:
```csharp
// DELETE this line:
plant.manaCost = serverGarden.manaCost;
```

**Step 3: Verify compilation**

Run Unity MCP `read_console` to check for errors. Should compile cleanly.

**Step 4: Run all garden tests**

Run via Unity MCP `run_tests` with `test_names: ["Garden.Tests.TestGardenManager"]`
Expected: All PASS

**Step 5: Commit**

```
git add Assets/Scripts/Managers/GardenManager.cs
git commit -m "refactor: remove manaCost from Plant(), costs now handled by CraftGarden"
```

---

### Task 4: Add garden entries to BuildUI

**Files:**
- Modify: `Assets/Scripts/UI/BuildUI.cs`

**Step 1: Add a field to track selected garden plant**

Add a public field and update the event to pass plant info:

```csharp
public string SelectedGardenPlant { get; private set; }
```

**Step 2: Add garden entries in `Refresh()` after the Mallum House block**

Insert before the flame upgrade block:

```csharp
// Garden entries — one per plant type
if (GardenManager.Instance != null && FlameManager.Instance != null)
{
    bool canPlace = FlameManager.Instance.CanPlaceEntity;
    string capText = $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}";
    var data = SaveManager.Instance.Data;

    foreach (var plantData in Resources.LoadAll<GardenPlantData>("GardenPlants"))
    {
        int existingCount = 0;
        foreach (var g in data.gardens)
            if (g.plantName == plantData.plantName) existingCount++;

        var cost = plantData.GetCost(existingCount);
        if (cost == null)
        {
            AddBuildItem(plantData.plantName, "Max reached", () => { });
            continue;
        }

        string costText = $"{cost.manaCost:F0} Mana";
        if (cost.seedCost > 0)
            costText += $" + {cost.seedCost} {plantData.yieldItem}";

        var item = data.items.Find(it => it.itemName == plantData.yieldItem);
        int haveItems = item?.count ?? 0;
        bool canAfford = canPlace
            && data.mana >= cost.manaCost
            && haveItems >= cost.seedCost;

        string displayCost = canPlace ? $"{costText} ({capText})" : $"Cap reached ({capText})";
        string pName = plantData.plantName; // capture for lambda
        AddBuildItem(pName, displayCost, () =>
        {
            if (canAfford)
            {
                SelectedGardenPlant = pName;
                OnRequestPlacement?.Invoke(CampBuildingType.Garden);
            }
        });
    }
}
```

**Step 3: Verify compilation**

Run Unity MCP `read_console` to check for errors.

**Step 4: Commit**

```
git add Assets/Scripts/UI/BuildUI.cs
git commit -m "feat: add garden plant entries to build menu with scaling costs"
```

---

### Task 5: Handle Garden placement in CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

**Step 1: Add Garden case to `OnEmptyCellTapped` placement switch**

In `OnEmptyCellTapped` (around line 543), add after the MallumHouse case:

```csharp
case CampBuildingType.Garden:
    var buildUI = GetComponent<BuildUI>();
    if (buildUI != null && !string.IsNullOrEmpty(buildUI.SelectedGardenPlant))
        success = GardenManager.Instance.CraftGarden(buildUI.SelectedGardenPlant, gridX, gridY);
    break;
```

**Step 2: Verify compilation**

Run Unity MCP `read_console` to check for errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: handle garden placement on hex grid"
```

---

### Task 6: Update asset files with cost tiers

**Files:**
- Modify: `Assets/Resources/GardenPlants/Oak.asset`
- Modify: `Assets/Resources/GardenPlants/BerryBush.asset`

**Step 1: Update Oak.asset**

Add `costTiers` to the YAML and remove `manaCost`. The serialized field name is `costTiers` which is a `List<GardenCostTier>`. Each tier has `manaCost` (float) and `seedCost` (int).

Oak tiers (3 copies max):
- Tier 0: 200 mana, 1 Acorn
- Tier 1: 350 mana, 2 Acorns
- Tier 2: 500 mana, 3 Acorns

Add after `waterRequired: 5`:
```yaml
  costTiers:
  - manaCost: 200
    seedCost: 1
  - manaCost: 350
    seedCost: 2
  - manaCost: 500
    seedCost: 3
```

Remove the `manaCost: 0` line (it was the old standalone field, now gone from the script).

**Step 2: Update BerryBush.asset**

BerryBush tiers (3 copies max):
- Tier 0: 100 mana, 2 Berries
- Tier 1: 200 mana, 4 Berries
- Tier 2: 350 mana, 6 Berries

Add after `waterRequired: 3`:
```yaml
  costTiers:
  - manaCost: 100
    seedCost: 2
  - manaCost: 200
    seedCost: 4
  - manaCost: 350
    seedCost: 6
```

Remove the `manaCost: 0` line.

**Step 3: Verify in Unity**

Use Unity MCP `read_console` to verify no serialization errors.

**Step 4: Commit**

```
git add Assets/Resources/GardenPlants/Oak.asset Assets/Resources/GardenPlants/BerryBush.asset
git commit -m "data: set garden cost tiers for Oak and BerryBush"
```

---

### Task 7: Run all tests and verify

**Step 1: Run all EditMode tests**

Run via Unity MCP `run_tests` with `mode: "EditMode"`
Expected: All PASS

**Step 2: Take a screenshot to visually verify the build menu**

Use Unity MCP `manage_editor` action `play`, then `manage_scene` action `screenshot`.

**Step 3: Final commit if any fixups needed**
