# Seed Roster Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace 3 placeholder seeds with 10 real-world flower/herb seeds with realistic growth conditions.

**Architecture:** Delete old SeedData assets, create 10 new ones as Unity YAML `.asset` files with GrowthRecipe values from the design doc. Update code references (GameManager starter grant, VisitorSystem gift, tests).

**Tech Stack:** Unity ScriptableObject YAML assets, C# test updates

---

### Task 1: Delete old seed assets

**Files:**
- Delete: `Assets/Resources/Seeds/Fern.asset` + `.meta`
- Delete: `Assets/Resources/Seeds/Sunflower.asset` + `.meta`
- Delete: `Assets/Resources/Seeds/Moonvine.asset` + `.meta`

**Step 1: Delete the files**

```bash
rm Assets/Resources/Seeds/Fern.asset Assets/Resources/Seeds/Fern.asset.meta
rm Assets/Resources/Seeds/Sunflower.asset Assets/Resources/Seeds/Sunflower.asset.meta
rm Assets/Resources/Seeds/Moonvine.asset Assets/Resources/Seeds/Moonvine.asset.meta
```

**Step 2: Commit**

```bash
git add -A Assets/Resources/Seeds/
git commit -m "chore: remove old placeholder seed assets"
```

---

### Task 2: Create all 10 seed assets

**Files:**
- Create: `Assets/Resources/Seeds/Basil.asset`
- Create: `Assets/Resources/Seeds/Chamomile.asset`
- Create: `Assets/Resources/Seeds/Marigold.asset`
- Create: `Assets/Resources/Seeds/Mint.asset`
- Create: `Assets/Resources/Seeds/Lavender.asset`
- Create: `Assets/Resources/Seeds/Poppy.asset`
- Create: `Assets/Resources/Seeds/Jasmine.asset`
- Create: `Assets/Resources/Seeds/Rosemary.asset`
- Create: `Assets/Resources/Seeds/Dahlia.asset`
- Create: `Assets/Resources/Seeds/Moonflower.asset`

Each asset is a Unity YAML file referencing the SeedData script GUID `4b1ea091be0f647ae96e7e3f22d8a36c`. Use a unique `fileID` per asset (Unity convention: `&11400000`). Each `.meta` file needs a unique GUID.

**Step 1: Create all 10 `.asset` files**

Use the values from the design doc (`docs/plans/2026-03-03-seed-roster-design.md`) section "GrowthRecipe Details". Template structure for each:

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
  m_Name: <SEED_NAME>
  m_EditorClassIdentifier:
  seedName: <SEED_NAME>
  growthDurationHours: <HOURS>
  baseDrops: <DROPS>
  recipe:
    useHeat: <0|1>
    idealTempMin: <MIN>
    idealTempMax: <MAX>
    heatTolerance: 10
    heatWeight: <WEIGHT>
    useWind: <0|1>
    idealWindMin: <MIN>
    idealWindMax: <MAX>
    windTolerance: 5
    windWeight: <WEIGHT>
    useHumidity: <0|1>
    idealHumidityMin: <MIN>
    idealHumidityMax: <MAX>
    humidityTolerance: 20
    humidityWeight: <WEIGHT>
    useSunlight: <0|1>
    idealSunlightMin: <MIN>
    idealSunlightMax: <MAX>
    sunlightTolerance: 20
    sunlightWeight: <WEIGHT>
    useRain: <0|1>
    idealRainMin: <MIN>
    idealRainMax: <MAX>
    rainTolerance: 0.3
    rainWeight: <WEIGHT>
    useMoon: <0|1>
    requiredMoonPhase: <0-7>
    moonWeight: <WEIGHT>
    useWaterings: <0|1>
    idealWaterings: <COUNT>
    wateringsWeight: <WEIGHT>
  icon: {fileID: 0}
  growthSprites: []
  manaCost: <COST>
```

Seed values (from design doc):

| Seed | Hours | Drops | Cost | Heat | Wind | Humidity | Sunlight | Rain | Moon | Waterings |
|------|-------|-------|------|------|------|----------|----------|------|------|-----------|
| Basil | 1 | 1 | 0 | 20-30, w1 | - | - | - | - | - | 1, w0.5 |
| Chamomile | 1.5 | 2 | 0 | 15-25, w1 | - | - | 50-90, w1 | - | - | - |
| Marigold | 2 | 2 | 10 | 20-35, w1 | - | - | 60-100, w1 | - | - | 2, w0.5 |
| Mint | 3 | 3 | 15 | - | - | 50-80, w1 | - | 0.2-0.6, w1.5 | - | 2, w0.5 |
| Lavender | 5 | 3 | 25 | 25-35, w1.5 | 5-15, w1 | - | 70-100, w1.5 | - | - | - |
| Poppy | 8 | 4 | 40 | 15-25, w1 | - | 40-75, w1 | - | 0.3-0.7, w1.5 | - | - |
| Jasmine | 12 | 4 | 60 | 20-30, w1 | - | 60-90, w1.5 | - | - | - | 3, w1 |
| Rosemary | 18 | 5 | 80 | 20-35, w1 | 5-20, w1 | - | 60-100, w1.5 | - | - | - |
| Dahlia | 30 | 6 | 120 | 18-28, tol8, w1 | - | 50-80, w1 | 50-90, w1 | - | - | 4, w1 |
| Moonflower | 48 | 8 | 200 | - | - | 60-90, w1 | - | - | phase4, w3 | 3, w1 |

Inactive dimensions use defaults: `useX: 0`, `idealXMin: 0`, `idealXMax: <default>`, default tolerances/weights.

**Step 2: Generate .meta files**

Each `.meta` file needs a unique GUID. Generate 10 unique GUIDs and write the meta files.

**Step 3: Commit**

```bash
git add Assets/Resources/Seeds/
git commit -m "feat: add 10 real-world seed assets (Basil through Moonflower)"
```

---

### Task 3: Update GameManager starter grant

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs:46`

**Step 1: Change starter seed from Fern to Basil**

Line 46, change:
```csharp
ApothekeManager.Instance.AddSeed("Fern", 3);
```
to:
```csharp
ApothekeManager.Instance.AddSeed("Basil", 3);
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "fix: update starter seed grant to Basil"
```

---

### Task 4: Update VisitorSystem gift seed

**Files:**
- Modify: `Assets/Scripts/Managers/VisitorSystem.cs:50`

**Step 1: Change gift seed from Fern to Chamomile**

Line 50, change:
```csharp
return new VisitorGift { type = VisitorGiftType.Seed, seedName = "Fern", amount = 1 };
```
to:
```csharp
return new VisitorGift { type = VisitorGiftType.Seed, seedName = "Chamomile", amount = 1 };
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Managers/VisitorSystem.cs
git commit -m "fix: update visitor gift seed to Chamomile"
```

---

### Task 5: Update test references

**Files:**
- Modify: `Assets/Tests/EditMode/TestPlotManager.cs` — lines 14, 33, 88: "Fern" → "Basil"
- Modify: `Assets/Tests/EditMode/TestVillageSnapshot.cs` — line 15, 30: "Fern" → "Basil"; line 45, 53: "Sunflower" → "Lavender"; line 59, 61: "Moonvine" → "Moonflower"; line 79: "Fern" → "Basil"
- Modify: `Assets/Tests/EditMode/TestSaveData.cs` — lines 32, 34, 44: "Fern" → "Basil"

**Step 1: Replace all seed name references in tests**

These are just placeholder names in test data — they don't load actual assets, so any valid name works. Use new roster names for consistency.

In `TestPlotManager.cs`:
- Replace all `"Fern"` with `"Basil"` (3 occurrences, lines 14, 33, 88)

In `TestVillageSnapshot.cs`:
- Replace `"Fern"` with `"Basil"` (lines 15, 30, 79)
- Replace `"Sunflower"` with `"Lavender"` (lines 45, 53)
- Replace `"Moonvine"` with `"Moonflower"` (lines 59, 61)

In `TestSaveData.cs`:
- Replace all `"Fern"` with `"Basil"` (lines 32, 34, 44)

**Step 2: Run tests**

Use Unity MCP `run_tests` with `mode: "EditMode"`. All 18 tests should pass.

**Step 3: Commit**

```bash
git add Assets/Tests/EditMode/
git commit -m "test: update seed name references to new roster"
```
