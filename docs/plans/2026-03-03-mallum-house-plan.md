# Mallum House Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add Mallum House as a hex-grid entity that determines Mallum count (2 per house), replacing the flame-level-based cap.

**Architecture:** New `MallumHouseConfig` ScriptableObject defines costs per house index (escalating mana + seeds). `MallumHouseSave` stores grid position. `MallumManager` reads house count from save data instead of flame level. Houses share the unified entity cap.

**Tech Stack:** Unity 6, C#, NUnit (EditMode tests), UI Toolkit, ScriptableObjects

---

### Task 1: Data — MallumHouseSave and SaveData

**Files:**
- Create: `Assets/Scripts/Data/MallumHouseSave.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs:7-23`
- Modify: `Assets/Scripts/Data/GameEnums.cs:21`

**Step 1: Create MallumHouseSave**

```csharp
// Assets/Scripts/Data/MallumHouseSave.cs
using System;

namespace Garden
{
    [Serializable]
    public class MallumHouseSave
    {
        public int gridX;
        public int gridY;
    }
}
```

**Step 2: Add mallumHouses to SaveData**

In `Assets/Scripts/Data/SaveData.cs`, add after line 20 (`public List<MallumSave> mallums = new();`):

```csharp
public List<MallumHouseSave> mallumHouses = new();
```

**Step 3: Add MallumHouse to CampBuildingType**

In `Assets/Scripts/Data/GameEnums.cs`, change line 21 from:
```csharp
public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke }
```
to:
```csharp
public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke, MallumHouse }
```

**Step 4: Commit**

```
git add Assets/Scripts/Data/MallumHouseSave.cs Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/GameEnums.cs
git commit -m "feat: add MallumHouseSave data model and CampBuildingType.MallumHouse"
```

---

### Task 2: Data — MallumHouseConfig ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/MallumHouseConfig.cs`

**Step 1: Write the failing test**

In `Assets/Tests/EditMode/TestMallumHouse.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMallumHouse
    {
        [Test]
        public void GetMaxMallums_ReturnsHouseCountTimesPerHouse()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.AreEqual(4, config.GetMaxMallums(2));
            Assert.AreEqual(6, config.GetMaxMallums(3));
        }

        [Test]
        public void GetMaxMallums_ReturnsZeroForZeroHouses()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.AreEqual(0, config.GetMaxMallums(0));
        }

        [Test]
        public void GetNextHouseCost_ReturnsCorrectCostByIndex()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            // Default has no costs defined, so first house should return null
            Assert.IsNull(config.GetNextHouseCost(0));
        }

        [Test]
        public void CanBuildNextHouse_ReturnsFalse_WhenNoCostsLeft()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.IsFalse(config.CanBuildNextHouse(0));
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` with mode EditMode, test_names `["Garden.Tests.TestMallumHouse"]`
Expected: FAIL — `MallumHouseConfig` does not exist yet.

**Step 3: Create MallumHouseConfig**

```csharp
// Assets/Scripts/Data/MallumHouseConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "MallumHouseConfig", menuName = "CampFire/Mallum House Config")]
    public class MallumHouseConfig : ScriptableObject
    {
        [SerializeField] private int mallumsPerHouse = 2;

        [SerializeField] private List<HouseCost> houseCosts = new();

        public int MallumsPerHouse => mallumsPerHouse;

        public int GetMaxMallums(int houseCount)
        {
            return houseCount * mallumsPerHouse;
        }

        /// <summary>
        /// Returns the cost for building the next house (0-indexed by current house count).
        /// Returns null if no more houses can be built (beyond the cost table).
        /// </summary>
        public HouseCost GetNextHouseCost(int currentHouseCount)
        {
            if (currentHouseCount < 0 || currentHouseCount >= houseCosts.Count)
                return null;
            return houseCosts[currentHouseCount];
        }

        public bool CanBuildNextHouse(int currentHouseCount)
        {
            return GetNextHouseCost(currentHouseCount) != null;
        }
    }

    [Serializable]
    public class HouseCost
    {
        public float manaCost;
        public List<SeedCost> seedCosts = new();
    }

    [Serializable]
    public class SeedCost
    {
        public string seedName;
        public int count;
    }
}
```

**Step 4: Run test to verify it passes**

Run: `run_tests` with mode EditMode, test_names `["Garden.Tests.TestMallumHouse"]`
Expected: PASS

**Step 5: Commit**

```
git add Assets/Scripts/Data/MallumHouseConfig.cs Assets/Tests/EditMode/TestMallumHouse.cs
git commit -m "feat: add MallumHouseConfig ScriptableObject with escalating costs"
```

---

### Task 3: MallumManager — Replace flame cap with house count

**Files:**
- Modify: `Assets/Scripts/Managers/MallumManager.cs`
- Modify: `Assets/Tests/EditMode/TestMallumHouse.cs`

**Step 1: Write the failing test**

Add to `TestMallumHouse.cs`:

```csharp
[Test]
public void GetMaxMallumsFromHouses_MatchesHouseCount()
{
    // Static helper: given a list of houses and config, returns max mallums
    var houses = new List<MallumHouseSave>
    {
        new() { gridX = 1, gridY = 0 },
        new() { gridX = 0, gridY = 1 }
    };
    var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
    int max = config.GetMaxMallums(houses.Count);
    Assert.AreEqual(4, max); // 2 houses * 2 mallums each
}
```

**Step 2: Run test to verify it passes** (this one should pass already since GetMaxMallums exists)

**Step 3: Modify MallumManager to use house count**

In `Assets/Scripts/Managers/MallumManager.cs`:

1. Add a serialized field for MallumHouseConfig (line 11 area):
```csharp
[SerializeField] private MallumHouseConfig houseConfig;
```

2. Add a public accessor:
```csharp
public MallumHouseConfig HouseConfig => houseConfig;
```

3. Change `Start()` (lines 27-35) — replace flame-level cap with house count:
```csharp
private void Start()
{
    var data = SaveManager.Instance.Data;
    int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
    EnsureMallumCount(data.mallums, max);

    if (FlameManager.Instance != null)
        FlameManager.Instance.OnFlameUpgraded += OnFlameUpgraded;
}
```

4. Change `OnFlameUpgraded()` (lines 43-54) — no longer adjust Mallum count on flame upgrade. Remove the Mallum count logic but keep the method for the event unsubscribe to not break:
```csharp
private void OnFlameUpgraded()
{
    // Mallum count now determined by houses, not flame level
    // Keep method for potential future flame-related Mallum effects
}
```

5. Change `GetTotalMallumCount()` to reflect the house-based max (not just current mallum list count):
```csharp
public int GetTotalMallumCount()
{
    return SaveManager.Instance.Data.mallums.Count;
}

public int GetMaxMallumCount()
{
    return houseConfig.GetMaxMallums(SaveManager.Instance.Data.mallumHouses.Count);
}
```

6. Add `CraftMallumHouse` method — the core crafting logic:
```csharp
public bool CraftMallumHouse(int gridX, int gridY)
{
    if (!FlameManager.Instance.CanPlaceEntity) return false;

    var data = SaveManager.Instance.Data;
    var cost = houseConfig.GetNextHouseCost(data.mallumHouses.Count);
    if (cost == null) return false;

    // Check mana
    if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;

    // Check seeds
    foreach (var seedCost in cost.seedCosts)
    {
        var entry = data.seedInventory.Find(s => s.seedName == seedCost.seedName);
        if (entry == null || entry.count < seedCost.count) return false;
    }

    // Spend mana
    if (!CurrencyManager.Instance.SpendMana(cost.manaCost)) return false;

    // Spend seeds
    foreach (var seedCost in cost.seedCosts)
    {
        var entry = data.seedInventory.Find(s => s.seedName == seedCost.seedName);
        entry.count -= seedCost.count;
        if (entry.count <= 0) data.seedInventory.Remove(entry);
    }

    // Place house
    data.mallumHouses.Add(new MallumHouseSave { gridX = gridX, gridY = gridY });

    // Add new mallums
    int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
    EnsureMallumCount(data.mallums, max);

    SaveManager.Instance.Save();
    OnMallumsChanged?.Invoke();
    return true;
}
```

7. Add a static helper for testability — checking if seeds are affordable:
```csharp
public static bool CanAffordSeeds(List<SeedInventoryEntry> inventory, List<SeedCost> seedCosts)
{
    foreach (var seedCost in seedCosts)
    {
        var entry = inventory.Find(s => s.seedName == seedCost.seedName);
        if (entry == null || entry.count < seedCost.count) return false;
    }
    return true;
}
```

**Step 4: Run all Mallum tests**

Run: `run_tests` with mode EditMode, test_names `["Garden.Tests.TestMallumManager", "Garden.Tests.TestMallumHouse"]`
Expected: PASS

**Step 5: Commit**

```
git add Assets/Scripts/Managers/MallumManager.cs Assets/Tests/EditMode/TestMallumHouse.cs
git commit -m "feat: MallumManager uses house count for Mallum cap instead of flame level"
```

---

### Task 4: Tests — CraftMallumHouse and CanAffordSeeds

**Files:**
- Modify: `Assets/Tests/EditMode/TestMallumHouse.cs`

**Step 1: Write tests for CanAffordSeeds**

Add to `TestMallumHouse.cs`:

```csharp
[Test]
public void CanAffordSeeds_ReturnsTrueWhenEnough()
{
    var inventory = new List<SeedInventoryEntry>
    {
        new() { seedName = "Basil", count = 5 }
    };
    var costs = new List<SeedCost>
    {
        new() { seedName = "Basil", count = 3 }
    };
    Assert.IsTrue(MallumManager.CanAffordSeeds(inventory, costs));
}

[Test]
public void CanAffordSeeds_ReturnsFalseWhenNotEnough()
{
    var inventory = new List<SeedInventoryEntry>
    {
        new() { seedName = "Basil", count = 1 }
    };
    var costs = new List<SeedCost>
    {
        new() { seedName = "Basil", count = 3 }
    };
    Assert.IsFalse(MallumManager.CanAffordSeeds(inventory, costs));
}

[Test]
public void CanAffordSeeds_ReturnsFalseWhenSeedMissing()
{
    var inventory = new List<SeedInventoryEntry>();
    var costs = new List<SeedCost>
    {
        new() { seedName = "Lavender", count = 1 }
    };
    Assert.IsFalse(MallumManager.CanAffordSeeds(inventory, costs));
}

[Test]
public void CanAffordSeeds_ReturnsTrueWhenNoCosts()
{
    var inventory = new List<SeedInventoryEntry>();
    var costs = new List<SeedCost>();
    Assert.IsTrue(MallumManager.CanAffordSeeds(inventory, costs));
}
```

**Step 2: Run tests**

Run: `run_tests` with mode EditMode, test_names `["Garden.Tests.TestMallumHouse"]`
Expected: PASS

**Step 3: Commit**

```
git add Assets/Tests/EditMode/TestMallumHouse.cs
git commit -m "test: add CanAffordSeeds tests for Mallum House crafting"
```

---

### Task 5: Entity cap — Include Mallum Houses

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs:17-24`

**Step 1: Modify CurrentEntityCount to include mallumHouses**

In `Assets/Scripts/Managers/FlameManager.cs`, change `CurrentEntityCount` getter (line 22) from:
```csharp
return data.plots.Count + data.vases.Count + data.gardens.Count;
```
to:
```csharp
return data.plots.Count + data.vases.Count + data.gardens.Count + data.mallumHouses.Count;
```

**Step 2: Check console for compilation**

Run: `read_console` to check for errors.
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/Managers/FlameManager.cs
git commit -m "feat: include Mallum Houses in entity cap count"
```

---

### Task 6: New player setup — Start with 1 Mallum House

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs:35-53`

**Step 1: Update InitializeNewPlayer**

In `Assets/Scripts/Managers/GameManager.cs`, replace the Mallum initialization block (lines 47-51) with house-based initialization. The full new `InitializeNewPlayer` method:

```csharp
private void InitializeNewPlayer()
{
    var data = SaveManager.Instance.Data;
    data.mana = 50f;
    data.gems = 5;
    VaseManager.InitializeNewPlayer(data, VaseManager.Instance.Config.BaseCapacity);
    data.vases[0].gridX = 1;
    data.vases[0].gridY = 0;
    data.vases[1].gridX = 0;
    data.vases[1].gridY = 1;
    data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = -1, gridY = 0 });
    ApothekeManager.Instance.AddSeed("Basil", 3);

    // Start with 1 Mallum House
    data.mallumHouses.Add(new MallumHouseSave { gridX = 1, gridY = -1 });
    if (MallumManager.Instance != null)
    {
        int maxMallums = MallumManager.Instance.HouseConfig.GetMaxMallums(data.mallumHouses.Count);
        MallumManager.EnsureMallumCount(data.mallums, maxMallums);
    }
    SaveManager.Instance.Save();
}
```

**Step 2: Check console**

Run: `read_console` to check for errors.
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: new players start with 1 Mallum House (2 Mallums)"
```

---

### Task 7: UI — CampsiteViewUI hex grid rendering

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

This task adds Mallum House to the grid rendering, occupied cell tracking, and cell population.

**Step 1: Add mallumHouses to occupied grid**

In `CampsiteViewUI.cs`, in the method that builds the `occupied` dictionary (around line 185-196), add after the gardens loop:

```csharp
for (int i = 0; i < data.mallumHouses.Count; i++)
    occupied[(data.mallumHouses[i].gridX, data.mallumHouses[i].gridY)] = (CampBuildingType.MallumHouse, i);
```

Do the same in the visit mode version (around line 589-599) if `VillageSnapshot` has mallumHouses — skip this for now since visit mode doesn't need houses yet.

**Step 2: Add MallumHouse case to PopulateOccupiedCell**

In `PopulateOccupiedCell` method (around line 285-336), add a new case before the closing `}`:

```csharp
case CampBuildingType.MallumHouse:
    cell.AddToClassList("grid-cell--mallum-house");
    if (label != null) label.text = "Mallum House";
    if (status != null)
    {
        int mallumCount = MallumManager.Instance != null
            ? MallumManager.Instance.HouseConfig.MallumsPerHouse
            : 2;
        status.text = $"+{mallumCount} Mallums";
    }
    break;
```

**Step 3: Add MallumHouse case to OnEmptyCellTapped placement switch**

In `OnEmptyCellTapped` (around line 368-384), add to the switch:

```csharp
case CampBuildingType.MallumHouse:
    success = MallumManager.Instance.CraftMallumHouse(gridX, gridY);
    break;
```

**Step 4: Add MallumHouse case to ShowInteraction**

In the `ShowInteraction` method (around line 751-765), add:

```csharp
case CampBuildingType.MallumHouse:
    ShowMallumHouseInteraction(index);
    break;
```

**Step 5: Create ShowMallumHouseInteraction method**

Add a new private method near the other Show*Interaction methods:

```csharp
private void ShowMallumHouseInteraction(int index)
{
    if (MallumManager.Instance == null) return;
    var config = MallumManager.Instance.HouseConfig;
    interactionTitle.text = "Mallum House";

    var infoLabel = new Label($"Houses {config.MallumsPerHouse} Mallums");
    infoLabel.AddToClassList("interaction-info");
    interactionBody.Add(infoLabel);

    AddCloseButton();
}
```

**Step 6: Add MallumHouse to ShowBuildMenu**

In `ShowBuildMenu` (around line 390-439), add a new block for Mallum House after the Vase option:

```csharp
// Mallum House option
if (MallumManager.Instance != null)
{
    var hConfig = MallumManager.Instance.HouseConfig;
    var cost = hConfig.GetNextHouseCost(SaveManager.Instance.Data.mallumHouses.Count);
    if (cost != null)
    {
        string costText = $"{cost.manaCost:F0} Mana";
        foreach (var sc in cost.seedCosts)
            costText += $" + {sc.count} {sc.seedName}";
        bool canAffordHouse = canPlace
            && CurrencyManager.Instance.CanAffordMana(cost.manaCost)
            && MallumManager.CanAffordSeeds(SaveManager.Instance.Data.seedInventory, cost.seedCosts);
        var houseBtn = new Button(() =>
        {
            if (MallumManager.Instance.CraftMallumHouse(gridX, gridY))
                CloseInteractionPanel();
        }) { text = $"Mallum House ({costText})" };
        houseBtn.AddToClassList("interaction-btn-primary");
        houseBtn.SetEnabled(canAffordHouse);
        interactionActions.Add(houseBtn);
    }
}
```

**Step 7: Check console**

Run: `read_console` to check for errors.
Expected: No compilation errors.

**Step 8: Commit**

```
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: render Mallum House on hex grid with build and interaction support"
```

---

### Task 8: UI — BuildUI overlay panel

**Files:**
- Modify: `Assets/Scripts/UI/BuildUI.cs`

**Step 1: Add Mallum House to BuildUI.Refresh()**

In `Assets/Scripts/UI/BuildUI.cs`, add a new block in `Refresh()` after the Vase block (after line 50) and before the Flame upgrade block (line 52):

```csharp
if (MallumManager.Instance != null)
{
    var hConfig = MallumManager.Instance.HouseConfig;
    var nextCost = hConfig.GetNextHouseCost(SaveManager.Instance.Data.mallumHouses.Count);
    if (nextCost != null)
    {
        bool canPlace = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;
        string capText = FlameManager.Instance != null
            ? $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}"
            : "";
        string costText = $"{nextCost.manaCost:F0} Mana";
        foreach (var sc in nextCost.seedCosts)
            costText += $" + {sc.count} {sc.seedName}";
        bool canAfford = canPlace
            && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
            && MallumManager.CanAffordSeeds(SaveManager.Instance.Data.seedInventory, nextCost.seedCosts);
        AddBuildItem("Mallum House", canPlace ? $"{costText} ({capText})" : $"Cap reached ({capText})", () =>
        {
            if (canAfford)
                OnRequestPlacement?.Invoke(CampBuildingType.MallumHouse);
        });
    }
}
```

**Step 2: Check console**

Run: `read_console` to check for errors.
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/BuildUI.cs
git commit -m "feat: add Mallum House to BuildUI craft list with escalating costs"
```

---

### Task 9: USS — Grid cell style for Mallum House

**Files:**
- Modify: appropriate USS file (find the one with `.grid-cell--plot`, `.grid-cell--vase` etc.)

**Step 1: Find the USS file**

Search for `grid-cell--plot` in USS files to find the right stylesheet.

**Step 2: Add `.grid-cell--mallum-house` style**

Follow the same pattern as other grid cell styles. Use a distinct color (e.g. warm brown/orange) to differentiate from plots (green), vases (blue), gardens (green), flame (orange/red):

```css
.grid-cell--mallum-house {
    background-color: rgb(139, 90, 43);
}
```

**Step 3: Commit**

```
git add <uss-file>
git commit -m "style: add grid cell style for Mallum House"
```

---

### Task 10: Asset — Create MallumHouseConfig.asset

**Files:**
- Create: `Assets/Resources/Config/MallumHouseConfig.asset`

**Step 1: Create the ScriptableObject asset**

Create the asset YAML file directly at `Assets/Resources/Config/MallumHouseConfig.asset` with initial cost table:

- House 1: 0 mana, no seeds (free — granted at game start)
- House 2: 30 mana + 2 Basil
- House 3: 60 mana + 3 Lavender
- House 4: 100 mana + 2 Chamomile + 2 Mint

Note: Need to find the GUID of the MallumHouseConfig script first via its .meta file.

**Step 2: Wire the asset**

The `MallumManager` prefab/scene object needs its `houseConfig` field set to this asset. Check how the existing `config` (MallumConfig) field is wired and follow the same pattern — likely in the Garden.unity scene on the MallumManager component.

**Step 3: Commit**

```
git add Assets/Resources/Config/MallumHouseConfig.asset Assets/Resources/Config/MallumHouseConfig.asset.meta
git commit -m "asset: create MallumHouseConfig with initial cost table"
```

---

### Task 11: Cleanup — Remove unused MallumConfig.GetMaxMallums references

**Files:**
- Modify: `Assets/Scripts/Data/MallumConfig.cs` (optional: keep for reference or remove)

**Step 1: Search for remaining GetMaxMallums(flameLevel) calls**

Grep for `GetMaxMallums` across the codebase. At this point, `MallumManager.Start()` and `GameManager.InitializeNewPlayer()` should already be updated. Remove or deprecate any remaining references.

**Step 2: Run full test suite**

Run: `run_tests` with mode EditMode
Expected: ALL PASS

**Step 3: Commit**

```
git commit -m "chore: clean up unused flame-level Mallum cap references"
```

---

### Task 12: Final verification

**Step 1: Run full test suite**

Run: `run_tests` with mode EditMode
Expected: ALL PASS

**Step 2: Check console for any warnings/errors**

Run: `read_console`
Expected: No errors

**Step 3: Enter play mode and verify**

Run: `manage_editor` action play, verify no runtime errors via `read_console`.
