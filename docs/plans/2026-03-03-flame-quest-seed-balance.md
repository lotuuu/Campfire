# Flame-Quest-Seed Balance Rewrite Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace mana-based flame upgrades with harvest-based recipes, expand to 10 flame levels, and add 5 new quests to complete the seed-quest-flame progression loop.

**Architecture:** FlameConfig gains `upgradeRecipes` (list of `FlameUpgradeRecipe`, each with `List<FlameIngredient>`). FlameManager's `CanUpgrade`/`UpgradeFlame` switch from checking/spending mana to checking/consuming items in `SaveData.items`. Static helpers on FlameConfig enable unit testing without MonoBehaviour. UI shows ingredient lists instead of mana costs.

**Tech Stack:** Unity 6, C#, NUnit EditMode tests, UI Toolkit, ScriptableObjects, YAML asset files

**Design Doc:** `docs/plans/2026-03-03-flame-quest-seed-balance-design.md`

**Critical detail:** Harvested items are stored as `seedName + "_harvest"` (see `PlotManager.cs:215`). All recipe `itemName` values use this format (e.g., `"Basil_harvest"`, not `"Basil"`).

---

### Task 1: Add FlameIngredient and FlameUpgradeRecipe data classes

**Files:**
- Modify: `Assets/Scripts/Data/FlameConfig.cs` (add classes at bottom of file, inside namespace)
- Test: `Assets/Tests/EditMode/TestFlameConfig.cs`

**Step 1: Write the failing test**

Add to `Assets/Tests/EditMode/TestFlameConfig.cs`:

```csharp
[Test]
public void FlameUpgradeRecipe_IngredientsDefaultEmpty()
{
    var recipe = new FlameUpgradeRecipe();
    Assert.IsNotNull(recipe.ingredients);
    Assert.AreEqual(0, recipe.ingredients.Count);
}
```

**Step 2: Run test to verify it fails**

Run: EditMode tests
Expected: FAIL — `FlameUpgradeRecipe` does not exist

**Step 3: Write minimal implementation**

Add to `Assets/Scripts/Data/FlameConfig.cs`, inside the `Garden` namespace but outside the `FlameConfig` class, before the class definition:

```csharp
[Serializable]
public class FlameIngredient
{
    public string itemName;
    public int count;
}

[Serializable]
public class FlameUpgradeRecipe
{
    public List<FlameIngredient> ingredients = new();
}
```

Also add `using System;` and `using System.Collections.Generic;` to the top of the file.

**Step 4: Run test to verify it passes**

Run: EditMode tests
Expected: PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/FlameConfig.cs Assets/Tests/EditMode/TestFlameConfig.cs
git commit -m "feat: add FlameIngredient and FlameUpgradeRecipe data classes"
```

---

### Task 2: Add static helpers to FlameConfig (CanAffordUpgrade, ConsumeIngredients)

**Files:**
- Modify: `Assets/Scripts/Data/FlameConfig.cs`
- Test: `Assets/Tests/EditMode/TestFlameConfig.cs`

**Step 1: Write failing tests**

Add to `TestFlameConfig.cs`:

```csharp
[Test]
public void CanAffordUpgrade_ReturnsTrueWhenItemsSufficient()
{
    var recipe = new FlameUpgradeRecipe
    {
        ingredients = new List<FlameIngredient>
        {
            new() { itemName = "Basil_harvest", count = 3 }
        }
    };
    var items = new List<InventoryItem>
    {
        new() { itemName = "Basil_harvest", count = 5 }
    };
    Assert.IsTrue(FlameConfig.CanAffordUpgrade(recipe, items));
}

[Test]
public void CanAffordUpgrade_ReturnsFalseWhenItemsInsufficient()
{
    var recipe = new FlameUpgradeRecipe
    {
        ingredients = new List<FlameIngredient>
        {
            new() { itemName = "Basil_harvest", count = 3 }
        }
    };
    var items = new List<InventoryItem>
    {
        new() { itemName = "Basil_harvest", count = 2 }
    };
    Assert.IsFalse(FlameConfig.CanAffordUpgrade(recipe, items));
}

[Test]
public void CanAffordUpgrade_ReturnsFalseWhenItemMissing()
{
    var recipe = new FlameUpgradeRecipe
    {
        ingredients = new List<FlameIngredient>
        {
            new() { itemName = "Basil_harvest", count = 3 }
        }
    };
    var items = new List<InventoryItem>();
    Assert.IsFalse(FlameConfig.CanAffordUpgrade(recipe, items));
}

[Test]
public void ConsumeIngredients_SubtractsFromInventory()
{
    var recipe = new FlameUpgradeRecipe
    {
        ingredients = new List<FlameIngredient>
        {
            new() { itemName = "Basil_harvest", count = 3 },
            new() { itemName = "Chamomile_harvest", count = 2 }
        }
    };
    var items = new List<InventoryItem>
    {
        new() { itemName = "Basil_harvest", count = 5 },
        new() { itemName = "Chamomile_harvest", count = 4 }
    };
    FlameConfig.ConsumeIngredients(recipe, items);
    Assert.AreEqual(2, items.Find(i => i.itemName == "Basil_harvest").count);
    Assert.AreEqual(2, items.Find(i => i.itemName == "Chamomile_harvest").count);
}
```

**Step 2: Run tests to verify they fail**

Run: EditMode tests
Expected: FAIL — `FlameConfig.CanAffordUpgrade` and `FlameConfig.ConsumeIngredients` do not exist

**Step 3: Write minimal implementation**

Add to `FlameConfig` class:

```csharp
public static bool CanAffordUpgrade(FlameUpgradeRecipe recipe, List<InventoryItem> items)
{
    foreach (var ingredient in recipe.ingredients)
    {
        var item = items.Find(i => i.itemName == ingredient.itemName);
        if (item == null || item.count < ingredient.count)
            return false;
    }
    return true;
}

public static void ConsumeIngredients(FlameUpgradeRecipe recipe, List<InventoryItem> items)
{
    foreach (var ingredient in recipe.ingredients)
    {
        var item = items.Find(i => i.itemName == ingredient.itemName);
        item.count -= ingredient.count;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: EditMode tests
Expected: PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/FlameConfig.cs Assets/Tests/EditMode/TestFlameConfig.cs
git commit -m "feat: add CanAffordUpgrade and ConsumeIngredients static helpers"
```

---

### Task 3: Modify FlameConfig — replace mana costs with recipe system, expand to 10 levels

**Files:**
- Modify: `Assets/Scripts/Data/FlameConfig.cs`
- Modify: `Assets/Tests/EditMode/TestFlameConfig.cs`

**Step 1: Write failing tests**

Replace the existing `GetUpgradeCost_ReturnsCorrectForLevel` test and add:

```csharp
[Test]
public void GetUpgradeRecipe_ReturnsNullAtMaxLevel()
{
    // Default config has empty upgradeRecipes list, so MaxLevel = 1
    Assert.IsNull(config.GetUpgradeRecipe(1));
}

[Test]
public void MaxLevel_EqualsRecipeCountPlusOne()
{
    Assert.AreEqual(1, config.MaxLevel);
}

[Test]
public void GetMaxEntities_ClampsToLastEntry_Level10()
{
    // After expansion, verify high levels clamp properly
    Assert.AreEqual(config.GetMaxEntities(99), config.GetMaxEntities(10));
}
```

**Step 2: Run tests to verify they fail**

Run: EditMode tests
Expected: FAIL — `GetUpgradeRecipe` doesn't exist; old `GetUpgradeCost` test may fail

**Step 3: Write implementation**

In `FlameConfig.cs`:

1. **Remove** the `upgradeCosts` field and its header:
```csharp
// DELETE these lines:
[Header("Upgrade Costs (Mana)")]
[SerializeField] private float[] upgradeCosts = { 50f, 150f, 400f, 1000f };
```

2. **Remove** the `GetUpgradeCost` method.

3. **Add** the upgrade recipes field:
```csharp
[Header("Upgrade Recipes")]
[SerializeField] private List<FlameUpgradeRecipe> upgradeRecipes = new();
```

4. **Replace** `MaxLevel` property:
```csharp
public int MaxLevel => upgradeRecipes.Count + 1;
```

5. **Add** `GetUpgradeRecipe` method:
```csharp
public FlameUpgradeRecipe GetUpgradeRecipe(int currentLevel)
{
    int index = currentLevel - 1;
    if (index < 0 || index >= upgradeRecipes.Count) return null;
    return upgradeRecipes[index];
}
```

6. **Expand** code defaults for `maxEntitiesPerLevel` and `gridSizePerLevel` to 10 entries:
```csharp
[SerializeField] private int[] maxEntitiesPerLevel = { 3, 5, 8, 12, 15, 18, 22, 26, 30, 35 };
// ...
[SerializeField] private int[] gridSizePerLevel = { 2, 2, 3, 3, 3, 4, 4, 4, 5, 5 };
```

Note: Code defaults don't override serialized .asset values. The .asset file is updated in Task 6.

7. **Delete** the old `GetUpgradeCost_ReturnsCorrectForLevel` test from TestFlameConfig.cs.

**Step 4: Run tests to verify they pass**

Run: EditMode tests
Expected: ALL PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/FlameConfig.cs Assets/Tests/EditMode/TestFlameConfig.cs
git commit -m "feat: replace mana upgrade costs with recipe system in FlameConfig"
```

---

### Task 4: Modify FlameManager — switch to harvest-based upgrades

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs`

**Step 1: Modify CanUpgrade**

Replace the current `CanUpgrade` method (lines 41-45) with:

```csharp
public bool CanUpgrade()
{
    var recipe = config.GetUpgradeRecipe(Level);
    if (recipe == null) return false;
    return FlameConfig.CanAffordUpgrade(recipe, SaveManager.Instance.Data.items);
}
```

**Step 2: Modify UpgradeFlame**

Replace the current `UpgradeFlame` method (lines 47-55) with:

```csharp
public bool UpgradeFlame()
{
    var recipe = config.GetUpgradeRecipe(Level);
    if (recipe == null) return false;
    if (!FlameConfig.CanAffordUpgrade(recipe, SaveManager.Instance.Data.items)) return false;
    FlameConfig.ConsumeIngredients(recipe, SaveManager.Instance.Data.items);
    SaveManager.Instance.Data.flameLevel++;
    SaveManager.Instance.Save();
    OnFlameUpgraded?.Invoke();
    return true;
}
```

**Step 3: Add GetUpgradeRecipe convenience method**

Add to FlameManager for UI access:

```csharp
public FlameUpgradeRecipe GetUpgradeRecipe() => config.GetUpgradeRecipe(Level);
```

**Step 4: Run tests to verify they pass**

Run: EditMode tests
Expected: ALL PASS (FlameManager methods aren't directly unit tested — they depend on singletons)

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/FlameManager.cs
git commit -m "feat: FlameManager upgrades consume harvest items instead of mana"
```

---

### Task 5: Update UI — show ingredient costs instead of mana

**Files:**
- Modify: `Assets/Scripts/UI/BuildUI.cs` (lines 52-60)
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs` (lines 789-805)

**Step 1: Update BuildUI.Refresh**

Replace the flame upgrade block (lines 52-60) with:

```csharp
if (FlameManager.Instance != null && FlameManager.Instance.Level < FlameManager.Instance.Config.MaxLevel)
{
    var recipe = FlameManager.Instance.GetUpgradeRecipe();
    if (recipe != null)
    {
        string costText = FormatRecipeCost(recipe);
        bool canAfford = FlameManager.Instance.CanUpgrade();
        AddBuildItem("Upgrade Flame", costText, () =>
        {
            if (FlameManager.Instance.CanUpgrade())
            {
                FlameManager.Instance.UpgradeFlame();
                Refresh();
            }
        });
    }
}
```

Add helper method to `BuildUI`:

```csharp
private static string FormatRecipeCost(FlameUpgradeRecipe recipe)
{
    var parts = new System.Collections.Generic.List<string>();
    foreach (var ing in recipe.ingredients)
    {
        string displayName = ing.itemName.Replace("_harvest", "");
        parts.Add($"{ing.count}x {displayName}");
    }
    return string.Join(", ", parts);
}
```

Also add `using Garden;` is already present (namespace is Garden), but you'll need no new usings.

**Step 2: Update CampsiteViewUI flame interaction panel**

Replace the upgrade section (lines 789-805 approximately) with:

```csharp
if (FlameManager.Instance.Level >= FlameManager.Instance.Config.MaxLevel)
{
    var maxLabel = new Label("Max level reached");
    maxLabel.AddToClassList("interaction-info");
    interactionBody.Add(maxLabel);
}
else
{
    var recipe = FlameManager.Instance.GetUpgradeRecipe();
    if (recipe != null)
    {
        var ingredientHeader = new Label("Ingredients needed:");
        ingredientHeader.AddToClassList("interaction-info");
        interactionBody.Add(ingredientHeader);

        var items = SaveManager.Instance.Data.items;
        foreach (var ing in recipe.ingredients)
        {
            string displayName = ing.itemName.Replace("_harvest", "");
            var item = items.Find(i => i.itemName == ing.itemName);
            int have = item != null ? item.count : 0;
            string status = have >= ing.count ? "ok" : "need";
            var label = new Label($"  {displayName}: {have}/{ing.count} ({status})");
            label.AddToClassList("interaction-info");
            interactionBody.Add(label);
        }

        bool canUpgrade = FlameManager.Instance.CanUpgrade();
        var btn = new Button(() =>
        {
            FlameManager.Instance.UpgradeFlame();
            CloseInteractionPanel();
        }) { text = "Level Up" };
        btn.SetEnabled(canUpgrade);
        btn.AddToClassList("interaction-btn-primary");
        interactionActions.Add(btn);
    }
}
```

**Step 3: Run tests to verify nothing broken**

Run: EditMode tests
Expected: ALL PASS

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/BuildUI.cs Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: UI shows harvest ingredient costs for flame upgrades"
```

---

### Task 6: Rewrite FlameConfig.asset with 10-level data and 9 upgrade recipes

**Files:**
- Modify: `Assets/Resources/Config/FlameConfig.asset`

**Step 1: Rewrite the asset YAML**

Replace the contents of `Assets/Resources/Config/FlameConfig.asset` with:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a355c566305e246b6aee973cda299253, type: 3}
  m_Name: FlameConfig
  m_EditorClassIdentifier:
  baseManaPerSecond: 0.5
  manaPerLevel: 0.3
  maxEntitiesPerLevel:
  - 3
  - 5
  - 8
  - 12
  - 15
  - 18
  - 22
  - 26
  - 30
  - 35
  gridSizePerLevel:
  - 2
  - 2
  - 3
  - 3
  - 3
  - 4
  - 4
  - 4
  - 5
  - 5
  upgradeRecipes:
  - ingredients:
    - itemName: Basil_harvest
      count: 3
  - ingredients:
    - itemName: Chamomile_harvest
      count: 5
  - ingredients:
    - itemName: Marigold_harvest
      count: 12
    - itemName: Snowdrop_harvest
      count: 8
    - itemName: Basil_harvest
      count: 8
  - ingredients:
    - itemName: Mint_harvest
      count: 8
    - itemName: Pansy_harvest
      count: 4
    - itemName: Chamomile_harvest
      count: 8
  - ingredients:
    - itemName: Lavender_harvest
      count: 22
    - itemName: Snowdrop_harvest
      count: 24
    - itemName: Basil_harvest
      count: 18
  - ingredients:
    - itemName: Poppy_harvest
      count: 35
    - itemName: Pansy_harvest
      count: 30
    - itemName: Marigold_harvest
      count: 50
  - ingredients:
    - itemName: Jasmine_harvest
      count: 60
    - itemName: Lavender_harvest
      count: 50
    - itemName: Poppy_harvest
      count: 60
  - ingredients:
    - itemName: Rosemary_harvest
      count: 50
    - itemName: Jasmine_harvest
      count: 60
    - itemName: Lavender_harvest
      count: 55
    - itemName: Snowdrop_harvest
      count: 40
  - ingredients:
    - itemName: Dahlia_harvest
      count: 50
    - itemName: Moonflower_harvest
      count: 30
    - itemName: Rosemary_harvest
      count: 60
    - itemName: Poppy_harvest
      count: 80
    - itemName: Basil_harvest
      count: 50
```

**Step 2: Verify in Unity**

Check `read_console` for deserialization errors after Unity refreshes. The `upgradeRecipes` field must serialize correctly as a `List<FlameUpgradeRecipe>` where each recipe has a `List<FlameIngredient>`.

**Step 3: Commit**

```bash
git add Assets/Resources/Config/FlameConfig.asset
git commit -m "data: rewrite FlameConfig with 10-level data and 9 harvest-based upgrade recipes"
```

---

### Task 7: Update 3 existing quest assets

The design doc specifies new reward pools. Existing quests need updated names, durations, rolls, and/or rewards.

**Files:**
- Modify: `Assets/Resources/Quests/SwampForage.asset`
- Modify: `Assets/Resources/Quests/MeadowExpedition.asset`
- Modify: `Assets/Resources/Quests/DeepWoodsTrek.asset` (becomes "Forest Trail")

**Seed asset GUIDs** (from .meta files — use these for reward pool seed references):
- Basil: `a1b2c3d4e5f60718293a4b5c6d7e8f90`
- Chamomile: `b2c3d4e5f6071829a3b4c5d6e7f80912`
- Marigold: `c3d4e5f607182930b4c5d6e7f8091234`
- Mint: `d4e5f6071829304ab5c6d7e8f9012345`
- Snowdrop: `4a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d`
- Pansy: `5b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e`
- Lavender: `e5f607182930415bc6d7e8f901234567`
- Poppy: `f607182930415b6cd7e8f90123456789`
- Jasmine: `07182930415b6c7de8f9012345678abc`
- Rosemary: `182930415b6c7d8ef901234567abcdef`
- Dahlia: `2930415b6c7d8e9f0123456789abcdef`
- Moonflower: `30415b6c7d8e9f01234567890abcdef1`

**QuestData script GUID:** `f2e677276f9604759bb0a82495f41439`

**Step 1: Update SwampForage.asset**

Target: Swamp Forage | Level 1 | 30m | 2 rolls | Basil (w3, 1-2), Chamomile (w2, 1-2)

Read the current file first, then update the `rewardPool` entries to use Basil (w3, min 1, max 2) and Chamomile (w2, min 1, max 2). Keep questName "Swamp Forage", durationMinutes 30, requiredFlameLevel 1, rewardRolls 2.

**Step 2: Update MeadowExpedition.asset**

Target: Meadow Expedition | Level 2 | 2h | 3 rolls | Marigold (w3, 1-2), Snowdrop (w2, 1-2)

Update reward pool to Marigold (w3, min 1, max 2) and Snowdrop (w2, min 1, max 2). Keep questName "Meadow Expedition", durationMinutes 120, requiredFlameLevel 2, rewardRolls 3.

**Step 3: Update DeepWoodsTrek.asset → Forest Trail**

Target: Forest Trail | Level 3 | 4h | 3 rolls | Mint (w3, 1-2), Pansy (w2, 1-1)

Change questName to "Forest Trail", durationMinutes to 240, rewardRolls to 3, and replace reward pool with Mint (w3, min 1, max 2) and Pansy (w2, min 1, max 1). Update description accordingly.

**Step 4: Run tests — ensure no breakage**

Run: EditMode tests
Expected: ALL PASS

**Step 5: Commit**

```bash
git add Assets/Resources/Quests/SwampForage.asset Assets/Resources/Quests/MeadowExpedition.asset Assets/Resources/Quests/DeepWoodsTrek.asset
git commit -m "data: update 3 existing quest assets with balanced reward pools"
```

---

### Task 8: Create 5 new quest assets

**Files:**
- Create: `Assets/Resources/Quests/HighlandPass.asset`
- Create: `Assets/Resources/Quests/DeepMarsh.asset`
- Create: `Assets/Resources/Quests/MountainAscent.asset`
- Create: `Assets/Resources/Quests/MoonlitPath.asset`
- Create: `Assets/Resources/Quests/AncientGrove.asset`

Each quest asset is a QuestData ScriptableObject. Use GUID `f2e677276f9604759bb0a82495f41439` for the m_Script reference.

**Quest specifications from design doc:**

| Quest | questName | durationMinutes | requiredFlameLevel | rewardRolls | Reward Pool |
|-------|-----------|----------------|--------------------|-------------|-------------|
| HighlandPass | Highland Pass | 360 | 4 | 3 | Lavender (w3, 1-2), Marigold (w1, 1-1) |
| DeepMarsh | Deep Marsh | 480 | 5 | 4 | Poppy (w3, 1-2), Mint (w1, 1-1) |
| MountainAscent | Mountain Ascent | 720 | 6 | 4 | Jasmine (w3, 1-2), Lavender (w1, 1-1) |
| MoonlitPath | Moonlit Path | 960 | 7 | 4 | Rosemary (w3, 1-2), Pansy (w1, 1-1) |
| AncientGrove | Ancient Grove | 1200 | 8 | 5 | Dahlia (w3, 1-2), Moonflower (w1, 1-1), Rosemary (w1, 1-1) |

**Step 1: Create each quest asset YAML**

For each quest, create a `.asset` file following the existing SwampForage format. Each needs a unique fileID in line 3 (use `&11400000`). Each also needs a `.meta` file with a unique GUID — generate one for each, or let Unity auto-generate by creating via the Unity MCP `manage_asset` tool.

Recommended approach: Create via filesystem (Write tool) with the YAML content, then let Unity generate meta files on refresh.

**Step 2: Add descriptions for each quest**

Use short thematic descriptions, e.g.:
- Highland Pass: "Climb through windswept highland trails."
- Deep Marsh: "Navigate treacherous deep marshland."
- Mountain Ascent: "Scale the misty mountain peaks."
- Moonlit Path: "Follow the silver moonlit forest path."
- Ancient Grove: "Explore the ancient sacred grove."

**Step 3: Verify in Unity**

Use `read_console` and `manage_asset(action="search", path="Assets/Resources/Quests")` to verify all 8 quest assets load correctly.

**Step 4: Commit**

```bash
git add Assets/Resources/Quests/HighlandPass.asset Assets/Resources/Quests/HighlandPass.asset.meta
git add Assets/Resources/Quests/DeepMarsh.asset Assets/Resources/Quests/DeepMarsh.asset.meta
git add Assets/Resources/Quests/MountainAscent.asset Assets/Resources/Quests/MountainAscent.asset.meta
git add Assets/Resources/Quests/MoonlitPath.asset Assets/Resources/Quests/MoonlitPath.asset.meta
git add Assets/Resources/Quests/AncientGrove.asset Assets/Resources/Quests/AncientGrove.asset.meta
git commit -m "data: add 5 new quest assets (Highland Pass through Ancient Grove)"
```

---

### Task 9: Update TestFlameConfig for new 10-level config

**Files:**
- Modify: `Assets/Tests/EditMode/TestFlameConfig.cs`

**Step 1: Update test expectations**

The `SetUp` creates a `FlameConfig` via `ScriptableObject.CreateInstance<FlameConfig>()` which uses code defaults (not asset values). After our changes, code defaults have 10-entry arrays and empty `upgradeRecipes`.

Verify existing tests still make sense with new code defaults:
- `GetMaxEntities_ReturnsCorrectForLevel`: Existing test checks level 1→3, 2→5, 4→12. With new defaults these are correct.
- `GetMaxEntities_ClampsToLastEntry`: Should now clamp to 35 (last of 10 entries).
- `GetManaRate_ScalesWithLevel`: Unchanged.

**Step 2: Update clamping test**

Change `GetMaxEntities_ClampsToLastEntry`:
```csharp
[Test]
public void GetMaxEntities_ClampsToLastEntry()
{
    Assert.AreEqual(35, config.GetMaxEntities(99));
}
```

**Step 3: Run all tests**

Run: EditMode tests
Expected: ALL PASS

**Step 4: Commit**

```bash
git add Assets/Tests/EditMode/TestFlameConfig.cs
git commit -m "test: update TestFlameConfig for 10-level expansion"
```

---

### Task 10: Final integration verification

**Step 1: Run all EditMode tests**

Run: EditMode tests
Expected: ALL PASS (should be 58+ tests)

**Step 2: Check Unity console for errors**

Use `read_console` with type filter for errors.

**Step 3: Verify quest assets load**

Use `manage_asset(action="search", path="Assets/Resources/Quests")` — should show 8 quest assets.

**Step 4: Verify FlameConfig loads**

Use `manage_asset(action="get_info", path="Assets/Resources/Config/FlameConfig.asset")` — should show the asset with no errors.

**Step 5: Spot-check SaveData round-trip**

Existing tests cover InventoryItem serialization. The recipe system uses the same SaveData.items field that PlotManager.Harvest already writes to, so no new save fields are needed.

**Step 6: Final commit if any fixups needed**

If any issues found, fix and commit with descriptive message.
