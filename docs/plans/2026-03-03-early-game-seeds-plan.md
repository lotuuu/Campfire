# Early Game Seeds Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add two fast-growing tutorial seeds (Sprouts 30s, Cress 5min) and insert two new flame levels at the start of the progression chain.

**Architecture:** Create two new SeedData `.asset` files following the exact YAML format of existing seeds. Modify FlameConfig, BuildingCostConfig, and GameManager serialized assets/code directly. No new scripts needed.

**Tech Stack:** Unity 6 YAML asset files, C# MonoBehaviour

---

### Task 1: Create Sprouts seed asset

**Files:**
- Create: `Assets/Resources/Seeds/Sprouts.asset`

**Step 1: Create the Sprouts.asset YAML file**

Write this file (uses same script GUID as Basil: `4b1ea091be0f647ae96e7e3f22d8a36c`):

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
  m_Script: {fileID: 11500000, guid: 4b1ea091be0f647ae96e7e3f22d8a36c, type: 3}
  m_Name: Sprouts
  m_EditorClassIdentifier:
  seedName: Sprouts Seed
  growthDurationHours: 0.00833
  baseDrops: 1
  recipe:
    useHeat: 0
    idealTempMin: 0
    idealTempMax: 30
    heatTolerance: 10
    heatWeight: 1
    useWind: 0
    idealWindMin: 0
    idealWindMax: 10
    windTolerance: 5
    windWeight: 1
    useHumidity: 1
    idealHumidityMin: 40
    idealHumidityMax: 80
    humidityTolerance: 20
    humidityWeight: 1
    useSunlight: 0
    idealSunlightMin: 0
    idealSunlightMax: 100
    sunlightTolerance: 20
    sunlightWeight: 1
    useRain: 0
    idealRainMin: 0
    idealRainMax: 1
    rainTolerance: 0.3
    rainWeight: 1
    useMoon: 0
    requiredMoonPhase: 0
    moonWeight: 1
    useWaterings: 1
    idealWateringsMin: 1
    idealWateringsMax: 1
    wateringsTolerance: 1
    wateringsWeight: 0.5
  icon: {fileID: 0}
  growthSprites: []
  tier: 0
  manaCost: 0
```

**Step 2: Generate .meta file**

Run: `uuidgen | tr -d '-' | tr 'A-F' 'a-f' | head -c 32` to get a GUID, then create `Assets/Resources/Seeds/Sprouts.asset.meta` with standard Unity meta format.

---

### Task 2: Create Cress seed asset

**Files:**
- Create: `Assets/Resources/Seeds/Cress.asset`

**Step 1: Create the Cress.asset YAML file**

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
  m_Script: {fileID: 11500000, guid: 4b1ea091be0f647ae96e7e3f22d8a36c, type: 3}
  m_Name: Cress
  m_EditorClassIdentifier:
  seedName: Cress Seed
  growthDurationHours: 0.08333
  baseDrops: 1
  recipe:
    useHeat: 1
    idealTempMin: 10
    idealTempMax: 25
    heatTolerance: 15
    heatWeight: 1
    useWind: 0
    idealWindMin: 0
    idealWindMax: 10
    windTolerance: 5
    windWeight: 1
    useHumidity: 1
    idealHumidityMin: 50
    idealHumidityMax: 85
    humidityTolerance: 15
    humidityWeight: 1
    useSunlight: 0
    idealSunlightMin: 0
    idealSunlightMax: 100
    sunlightTolerance: 20
    sunlightWeight: 1
    useRain: 0
    idealRainMin: 0
    idealRainMax: 1
    rainTolerance: 0.3
    rainWeight: 1
    useMoon: 0
    requiredMoonPhase: 0
    moonWeight: 1
    useWaterings: 0
    idealWateringsMin: 0
    idealWateringsMax: 0
    wateringsTolerance: 2
    wateringsWeight: 1
  icon: {fileID: 0}
  growthSprites: []
  tier: 0
  manaCost: 0
```

**Step 2: Generate .meta file**

Same approach as Task 1.

---

### Task 3: Update FlameConfig.asset

**Files:**
- Modify: `Assets/Resources/Config/FlameConfig.asset`

**Step 1: Insert two new upgrade recipes at the start of the upgradeRecipes list**

Prepend these two entries before the existing Basil_harvest recipe:

```yaml
  - ingredients:
    - itemName: Sprouts_harvest
      count: 1
  - ingredients:
    - itemName: Sprouts_harvest
      count: 5
    - itemName: Cress_harvest
      count: 2
```

**Step 2: Extend maxEntitiesPerLevel from 10 to 12 entries**

Insert `6` at index 1 and `8` at index 3 (duplicate the first two tiers):

```yaml
  maxEntitiesPerLevel:
  - 6
  - 6
  - 8
  - 8
  - 12
  - 15
  - 18
  - 22
  - 26
  - 30
  - 35
  - 40
```

**Step 3: Extend gridSizePerLevel from 10 to 12 entries**

Insert `2` at index 1 and `2` at index 3:

```yaml
  gridSizePerLevel:
  - 2
  - 2
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
```

---

### Task 4: Update BuildingCostConfig.asset

**Files:**
- Modify: `Assets/Resources/Config/BuildingCostConfig.asset`

**Step 1: Update plotCosts**

Replace the first 4 entries (indices 0-3). The rest remain unchanged.

```yaml
  plotCosts:
  - manaCost: 150
    seedCosts:
    - seedName: Sprouts
      count: 1
  - manaCost: 200
    seedCosts:
    - seedName: Basil
      count: 1
  - manaCost: 260
    seedCosts:
    - seedName: Basil
      count: 2
  - manaCost: 330
    seedCosts:
    - seedName: Chamomile
      count: 1
```

Entries at index 4+ (420 Chamomile, 520 Chamomile+Basil, etc.) stay exactly as they are.

**Step 2: Update vaseCosts**

Replace the first 3 entries (indices 0-2). The rest remain unchanged.

```yaml
  vaseCosts:
  - manaCost: 100
    seedCosts:
    - seedName: Cress
      count: 1
  - manaCost: 120
    seedCosts:
    - seedName: Basil
      count: 2
  - manaCost: 150
    seedCosts:
    - seedName: Chamomile
      count: 1
```

Entries at index 3+ (180 Chamomile, 220 Chamomile+Basil, etc.) stay exactly as they are.

---

### Task 5: Update GameManager starter seeds

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs:48`

**Step 1: Replace the Basil starter seeds**

Change line 48 from:

```csharp
            ApothekeManager.Instance.AddSeed("Basil", 3);
```

To:

```csharp
            ApothekeManager.Instance.AddSeed("Sprouts", 5);
            ApothekeManager.Instance.AddSeed("Cress", 3);
```

---

### Task 6: Run tests and verify

**Step 1: Run all EditMode tests**

Use Unity MCP `run_tests` with `mode: "EditMode"`.

Expected: All tests pass. The FlameConfig tests use `CreateInstance<FlameConfig>()` which creates default (empty) config, so they don't depend on the serialized asset values.

**Step 2: Check Unity console for errors**

Use `read_console` to verify no compilation errors or asset loading issues after the new seed assets are picked up.

---

### Task 7: Commit

**Step 1: Commit all changes**

```bash
git add Assets/Resources/Seeds/Sprouts.asset Assets/Resources/Seeds/Sprouts.asset.meta \
       Assets/Resources/Seeds/Cress.asset Assets/Resources/Seeds/Cress.asset.meta \
       Assets/Resources/Config/FlameConfig.asset \
       Assets/Resources/Config/BuildingCostConfig.asset \
       Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: add Sprouts (30s) and Cress (5min) early-game seeds

- Create Sprouts seed: 30-second growth, humidity + waterings recipe
- Create Cress seed: 5-minute growth, heat + humidity recipe
- Insert 2 new flame levels at start (Sprouts_harvest, then Sprouts+Cress)
- Update plot costs: first plot requires 1x Sprouts harvest
- Update vase costs: first vase requires 1x Cress harvest
- Change new-player starter seeds from 3x Basil to 5x Sprouts + 3x Cress"
```
