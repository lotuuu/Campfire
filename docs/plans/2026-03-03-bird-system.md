# Bird System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a bird system where birds randomly appear on unoccupied hex tiles each hour and drop seeds when collected by the player.

**Architecture:** New BirdManager singleton (MonoBehaviour) with static helpers for testability. BirdSave entries in SaveData track bird positions and pending seed drops. CampsiteViewUI renders birds as a new CampBuildingType. SeedData gets a `tier` field for level-gated seed selection.

**Tech Stack:** Unity 6, C#, UI Toolkit, NUnit (EditMode tests)

---

### Task 1: Add `tier` field to SeedData and update seed assets

**Files:**
- Modify: `Assets/Scripts/Data/SeedData.cs:17-18`
- Modify: `Assets/Resources/Seeds/Basil.asset` (and all 11 other seed assets)

**Step 1: Add tier field to SeedData**

In `Assets/Scripts/Data/SeedData.cs`, add a `tier` field between the `Visuals` and `Shop` headers. Insert after line 16 (`public Sprite[] growthSprites;`):

```csharp
        [Header("Progression")]
        public int tier = 1;
```

Full file should become:
```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "CampFire/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public float growthDurationHours = 4f;
        public int baseDrops = 1;
        public GrowthRecipe recipe;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;

        [Header("Progression")]
        public int tier = 1;

        [Header("Shop")]
        public float manaCost;
    }
}
```

**Step 2: Update all seed .asset YAML files**

Add `tier: N` line after `manaCost: 0` in each seed asset file. The tier assignments are:

| Seed | Tier | File |
|------|------|------|
| Basil | 1 | `Assets/Resources/Seeds/Basil.asset` |
| Chamomile | 1 | `Assets/Resources/Seeds/Chamomile.asset` |
| Snowdrop | 2 | `Assets/Resources/Seeds/Snowdrop.asset` |
| Marigold | 2 | `Assets/Resources/Seeds/Marigold.asset` |
| Mint | 3 | `Assets/Resources/Seeds/Mint.asset` |
| Pansy | 3 | `Assets/Resources/Seeds/Pansy.asset` |
| Lavender | 4 | `Assets/Resources/Seeds/Lavender.asset` |
| Poppy | 5 | `Assets/Resources/Seeds/Poppy.asset` |
| Jasmine | 6 | `Assets/Resources/Seeds/Jasmine.asset` |
| Rosemary | 7 | `Assets/Resources/Seeds/Rosemary.asset` |
| Dahlia | 8 | `Assets/Resources/Seeds/Dahlia.asset` |
| Moonflower | 9 | `Assets/Resources/Seeds/Moonflower.asset` |

In each `.asset` file, add `tier: N` as the last field (after `manaCost: 0`). Example for Basil:
```yaml
  manaCost: 0
  tier: 1
```

**Step 3: Verify compilation**

Run: check Unity console for errors after script change.

**Step 4: Commit**

```
feat: add tier field to SeedData for progression gating
```

---

### Task 2: Add BirdSave and SaveData fields

**Files:**
- Create: `Assets/Scripts/Data/BirdSave.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs:25`
- Modify: `Assets/Scripts/Data/GameEnums.cs:21`

**Step 1: Create BirdSave class**

Create `Assets/Scripts/Data/BirdSave.cs` following the `MallumHouseSave.cs` pattern:

```csharp
using System;

namespace Garden
{
    [Serializable]
    public class BirdSave
    {
        public int gridX;
        public int gridY;
        public string seedName;
        public int seedCount;
    }
}
```

**Step 2: Add bird fields to SaveData**

In `Assets/Scripts/Data/SaveData.cs`, add after line 25 (`public int apothekeGridY = 0;`):

```csharp
        public List<BirdSave> birds = new();
        public string lastBirdCheckHourUtc;
```

**Step 3: Add Bird to CampBuildingType enum**

In `Assets/Scripts/Data/GameEnums.cs` line 21, change:
```csharp
    public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke, MallumHouse }
```
to:
```csharp
    public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke, MallumHouse, Bird }
```

**Step 4: Verify compilation**

**Step 5: Commit**

```
feat: add BirdSave data model and Bird building type
```

---

### Task 3: Write BirdManager static helpers with tests (TDD)

**Files:**
- Create: `Assets/Scripts/Managers/BirdManager.cs`
- Create: `Assets/Tests/EditMode/TestBirdManager.cs`

This task creates the core logic as `public static` methods (no MonoBehaviour needed) and tests them.

**Step 1: Write failing tests**

Create `Assets/Tests/EditMode/TestBirdManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestBirdManager
    {
        // --- GetFreeTiles ---

        [Test]
        public void GetFreeTiles_ExcludesOccupiedAndFlame()
        {
            var data = new SaveData { flameLevel = 1 }; // radius 2 → 19 tiles
            data.plots.Add(new PlotSave { gridX = 1, gridY = 0 });
            // flame at (0,0) + 1 plot = 2 occupied → 17 free
            var free = BirdManager.GetFreeTiles(data, 2);
            Assert.AreEqual(17, free.Count);
            Assert.IsFalse(free.Contains((0, 0)));
            Assert.IsFalse(free.Contains((1, 0)));
        }

        [Test]
        public void GetFreeTiles_ExcludesBirds()
        {
            var data = new SaveData { flameLevel = 1 };
            data.birds.Add(new BirdSave { gridX = 1, gridY = 0 });
            var free = BirdManager.GetFreeTiles(data, 2);
            Assert.IsFalse(free.Contains((1, 0)));
        }

        [Test]
        public void GetFreeTiles_ReturnsEmpty_WhenAllOccupied()
        {
            var data = new SaveData { flameLevel = 1 };
            // Fill all 19 tiles at radius 2 with plots
            for (int q = -2; q <= 2; q++)
                for (int r = -2; r <= 2; r++)
                    if (HexGridUtil.IsWithinRadius(q, r, 2) && !(q == 0 && r == 0))
                        data.plots.Add(new PlotSave { gridX = q, gridY = r });
            var free = BirdManager.GetFreeTiles(data, 2);
            Assert.AreEqual(0, free.Count);
        }

        // --- GetEligibleSeeds ---

        [Test]
        public void GetEligibleSeeds_FiltersByTier()
        {
            var seeds = CreateTestSeeds();
            var eligible = BirdManager.GetEligibleSeeds(seeds, 2);
            // tier 1 and tier 2 seeds only
            Assert.AreEqual(2, eligible.Count);
            Assert.IsTrue(eligible.Exists(s => s.seedName == "Basil"));
            Assert.IsTrue(eligible.Exists(s => s.seedName == "Snowdrop"));
        }

        [Test]
        public void GetEligibleSeeds_ReturnsAll_AtMaxLevel()
        {
            var seeds = CreateTestSeeds();
            var eligible = BirdManager.GetEligibleSeeds(seeds, 10);
            Assert.AreEqual(3, eligible.Count);
        }

        [Test]
        public void GetEligibleSeeds_ReturnsEmpty_WhenNoSeedsMatch()
        {
            var seeds = CreateTestSeeds();
            // All test seeds are tier >= 1, flame level 0 means none match
            var eligible = BirdManager.GetEligibleSeeds(seeds, 0);
            Assert.AreEqual(0, eligible.Count);
        }

        // --- RollSeedDrop ---

        [Test]
        public void RollSeedDrop_ReturnsValidEntry()
        {
            var seeds = CreateTestSeeds();
            var drop = BirdManager.RollSeedDrop(seeds, 5);
            Assert.IsNotNull(drop);
            Assert.IsTrue(drop.seedCount >= 1);
            Assert.IsFalse(string.IsNullOrEmpty(drop.seedName));
        }

        [Test]
        public void RollSeedDrop_HigherLevelGivesMoreOfLowTierSeeds()
        {
            // Tier 1 seed at flame level 5: baseCount = max(1, 5-1+1) = 5
            // Run many times and check average is reasonable
            var seeds = new List<SeedData>();
            var s = ScriptableObject.CreateInstance<SeedData>();
            s.seedName = "Basil"; s.tier = 1;
            seeds.Add(s);

            int totalCount = 0;
            int trials = 100;
            for (int i = 0; i < trials; i++)
                totalCount += BirdManager.RollSeedDrop(seeds, 5).seedCount;

            float avg = totalCount / (float)trials;
            // baseCount=5, range=[4,7), so average should be ~5.0-5.5
            Assert.Greater(avg, 3f);
            Assert.Less(avg, 8f);
        }

        // --- ProcessHourlyChecks ---

        [Test]
        public void ProcessHourlyChecks_InitializesLastCheck_WhenNull()
        {
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();
            var now = new DateTime(2026, 3, 3, 14, 30, 0, DateTimeKind.Utc);

            bool changed = BirdManager.ProcessHourlyChecks(data, seeds, 2, now);
            Assert.IsFalse(changed); // just initializes, no birds placed
            Assert.AreEqual("2026-03-03T14:00:00.0000000Z", data.lastBirdCheckHourUtc);
        }

        [Test]
        public void ProcessHourlyChecks_PlacesBird_WhenRollSucceeds()
        {
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();
            // Set last check to 1 hour ago so exactly 1 check runs
            var now = new DateTime(2026, 3, 3, 15, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");

            // Run many times — with 33% chance, should get at least one bird in 50 tries
            bool gotBird = false;
            for (int i = 0; i < 50; i++)
            {
                data.birds.Clear();
                data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");
                BirdManager.ProcessHourlyChecks(data, seeds, 2, now);
                if (data.birds.Count > 0) { gotBird = true; break; }
            }
            Assert.IsTrue(gotBird, "Expected at least one bird in 50 trials at 33% chance");
        }

        [Test]
        public void ProcessHourlyChecks_ExistingBirdsHalveChance()
        {
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();
            // Pre-place 5 birds — chance = 0.33 * 0.5^5 = ~1%, very unlikely to get another
            for (int i = 0; i < 5; i++)
                data.birds.Add(new BirdSave { gridX = i - 2, gridY = -2, seedName = "Basil", seedCount = 1 });

            var now = new DateTime(2026, 3, 3, 15, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");

            int extraBirds = 0;
            for (int i = 0; i < 100; i++)
            {
                // Reset to 5 birds each trial
                while (data.birds.Count > 5) data.birds.RemoveAt(data.birds.Count - 1);
                data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");
                BirdManager.ProcessHourlyChecks(data, seeds, 2, now);
                if (data.birds.Count > 5) extraBirds++;
            }
            // ~1% chance per trial, expect 0-3 out of 100
            Assert.Less(extraBirds, 10, "With 5 existing birds, new bird chance should be very low");
        }

        [Test]
        public void ProcessHourlyChecks_CatchesUpMultipleHours()
        {
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();
            // 6 hours ago
            var now = new DateTime(2026, 3, 3, 20, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");

            // Run many times — 6 hours of 33% chance, should usually get at least one bird
            bool gotBird = false;
            for (int i = 0; i < 20; i++)
            {
                data.birds.Clear();
                data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");
                BirdManager.ProcessHourlyChecks(data, seeds, 2, now);
                if (data.birds.Count > 0) { gotBird = true; break; }
            }
            Assert.IsTrue(gotBird, "Expected at least one bird after 6 hours of catch-up");
            // Also verify last check is updated to current hour
            Assert.AreEqual("2026-03-03T20:00:00.0000000Z", data.lastBirdCheckHourUtc);
        }

        [Test]
        public void ProcessHourlyChecks_NoChange_WhenSameHour()
        {
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();
            var now = new DateTime(2026, 3, 3, 14, 30, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = new DateTime(2026, 3, 3, 14, 0, 0, DateTimeKind.Utc).ToString("o");

            bool changed = BirdManager.ProcessHourlyChecks(data, seeds, 2, now);
            Assert.IsFalse(changed);
            Assert.AreEqual(0, data.birds.Count);
        }

        // --- CollectBird ---

        [Test]
        public void CollectBird_RemovesBirdAndReturnsDrop()
        {
            var data = new SaveData();
            data.birds.Add(new BirdSave { gridX = 1, gridY = 0, seedName = "Basil", seedCount = 3 });

            var drop = BirdManager.CollectBird(data, 0);
            Assert.AreEqual("Basil", drop.seedName);
            Assert.AreEqual(3, drop.seedCount);
            Assert.AreEqual(0, data.birds.Count);
        }

        [Test]
        public void CollectBird_ReturnsNull_WhenIndexOutOfRange()
        {
            var data = new SaveData();
            var drop = BirdManager.CollectBird(data, 0);
            Assert.IsNull(drop);
        }

        // --- Helper ---

        private List<SeedData> CreateTestSeeds()
        {
            var seeds = new List<SeedData>();

            var basil = ScriptableObject.CreateInstance<SeedData>();
            basil.seedName = "Basil"; basil.tier = 1;
            seeds.Add(basil);

            var snowdrop = ScriptableObject.CreateInstance<SeedData>();
            snowdrop.seedName = "Snowdrop"; snowdrop.tier = 2;
            seeds.Add(snowdrop);

            var mint = ScriptableObject.CreateInstance<SeedData>();
            mint.seedName = "Mint"; mint.tier = 3;
            seeds.Add(mint);

            return seeds;
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with mode EditMode. Expected: all TestBirdManager tests fail (BirdManager class doesn't exist).

**Step 3: Implement BirdManager static helpers**

Create `Assets/Scripts/Managers/BirdManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class BirdManager : MonoBehaviour
    {
        public static BirdManager Instance { get; private set; }

        public event Action OnBirdPlaced;
        public event Action<BirdSave> OnBirdCollected;

        private static readonly float BaseChance = 0.33f;
        private static readonly float HalvingFactor = 0.5f;

        private List<SeedData> allSeeds;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allSeeds = new List<SeedData>(Resources.LoadAll<SeedData>("Seeds"));
        }

        private void Update()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            int gridRadius = FlameManager.Instance != null
                ? FlameManager.Instance.Config.GetGridSize(data.flameLevel)
                : 2;

            if (ProcessHourlyChecks(data, allSeeds, gridRadius, GameTime.UtcNow))
            {
                SaveManager.Instance.Save();
                OnBirdPlaced?.Invoke();
            }
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static List<(int q, int r)> GetFreeTiles(SaveData data, int gridRadius)
        {
            var occupied = new HashSet<(int, int)>();
            occupied.Add((0, 0)); // flame

            foreach (var p in data.plots) occupied.Add((p.gridX, p.gridY));
            foreach (var v in data.vases) occupied.Add((v.gridX, v.gridY));
            foreach (var g in data.gardens) occupied.Add((g.gridX, g.gridY));
            foreach (var h in data.mallumHouses) occupied.Add((h.gridX, h.gridY));
            foreach (var b in data.birds) occupied.Add((b.gridX, b.gridY));
            occupied.Add((data.apothekeGridX, data.apothekeGridY));

            var free = new List<(int q, int r)>();
            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                for (int r = -gridRadius; r <= gridRadius; r++)
                {
                    if (!HexGridUtil.IsWithinRadius(q, r, gridRadius)) continue;
                    if (occupied.Contains((q, r))) continue;
                    free.Add((q, r));
                }
            }
            return free;
        }

        public static List<SeedData> GetEligibleSeeds(List<SeedData> allSeeds, int flameLevel)
        {
            var result = new List<SeedData>();
            foreach (var seed in allSeeds)
            {
                if (seed.tier <= flameLevel)
                    result.Add(seed);
            }
            return result;
        }

        public static BirdSave RollSeedDrop(List<SeedData> eligibleSeeds, int flameLevel)
        {
            if (eligibleSeeds.Count == 0) return null;

            var seed = eligibleSeeds[UnityEngine.Random.Range(0, eligibleSeeds.Count)];
            int baseCount = Mathf.Max(1, flameLevel - seed.tier + 1);
            int min = Mathf.Max(1, baseCount - 1);
            int max = baseCount + 2; // exclusive upper bound
            int count = UnityEngine.Random.Range(min, max);

            return new BirdSave
            {
                seedName = seed.seedName,
                seedCount = count
            };
        }

        public static bool ProcessHourlyChecks(SaveData data, List<SeedData> allSeeds, int gridRadius, DateTime utcNow)
        {
            var currentHour = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc);

            if (string.IsNullOrEmpty(data.lastBirdCheckHourUtc))
            {
                data.lastBirdCheckHourUtc = currentHour.ToString("o");
                return false;
            }

            var lastCheck = DateTime.Parse(data.lastBirdCheckHourUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (currentHour <= lastCheck) return false;

            var eligible = GetEligibleSeeds(allSeeds, data.flameLevel);
            if (eligible.Count == 0)
            {
                data.lastBirdCheckHourUtc = currentHour.ToString("o");
                return false;
            }

            bool anyPlaced = false;
            var checkTime = lastCheck.AddHours(1);

            while (checkTime <= currentHour)
            {
                int birdCount = data.birds.Count;
                float chance = BaseChance * Mathf.Pow(HalvingFactor, birdCount);

                if (UnityEngine.Random.value < chance)
                {
                    var freeTiles = GetFreeTiles(data, gridRadius);
                    if (freeTiles.Count > 0)
                    {
                        var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];
                        var drop = RollSeedDrop(eligible, data.flameLevel);
                        drop.gridX = tile.q;
                        drop.gridY = tile.r;
                        data.birds.Add(drop);
                        anyPlaced = true;
                    }
                }

                checkTime = checkTime.AddHours(1);
            }

            data.lastBirdCheckHourUtc = currentHour.ToString("o");
            return anyPlaced;
        }

        public static BirdSave CollectBird(SaveData data, int index)
        {
            if (index < 0 || index >= data.birds.Count) return null;
            var bird = data.birds[index];
            data.birds.RemoveAt(index);
            return bird;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `run_tests` with mode EditMode, filter to TestBirdManager tests.
Expected: all pass.

**Step 5: Commit**

```
feat: add BirdManager with static helpers and tests
```

---

### Task 4: Integrate birds into CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

**Step 1: Subscribe to BirdManager events**

In `CampsiteViewUI.Initialize()` (around line 100, after the MallumManager subscription), add:

```csharp
            if (BirdManager.Instance != null)
            {
                BirdManager.Instance.OnBirdPlaced += RebuildGrid;
                BirdManager.Instance.OnBirdCollected += _ => RebuildGrid();
            }
```

**Step 2: Add birds to occupied dictionary in RebuildGrid**

In `RebuildGrid()`, after line 217 (the mallumHouses loop) and before line 219 (apotheke), add:

```csharp
            for (int i = 0; i < data.birds.Count; i++)
                occupied[(data.birds[i].gridX, data.birds[i].gridY)] = (CampBuildingType.Bird, i);
```

**Step 3: Add Bird case to PopulateOccupiedCell**

In `PopulateOccupiedCell()`, add a new case after the MallumHouse case (after line 388):

```csharp
                case CampBuildingType.Bird:
                    cell.AddToClassList("grid-cell--bird");
                    var bird = SaveManager.Instance.Data.birds[index];
                    if (label != null) label.text = "Bird";
                    if (status != null) status.text = $"{bird.seedCount}x {bird.seedName}";
                    break;
```

**Step 4: Add Bird case to OnCellTapped / ShowInteraction**

In `ShowInteraction()` (around line 848), add a new case:

```csharp
                case CampBuildingType.Bird:
                    ShowBirdInteraction(index);
                    break;
```

Then add the `ShowBirdInteraction` method (after the existing Show*Interaction methods):

```csharp
        private void ShowBirdInteraction(int index)
        {
            var data = SaveManager.Instance.Data;
            if (index < 0 || index >= data.birds.Count) return;

            var bird = data.birds[index];
            interactionTitle.text = "Bird";

            var info = new Label($"A bird has brought you {bird.seedCount}x {bird.seedName}!");
            info.AddToClassList("interaction-info");
            interactionBody.Add(info);

            var collectBtn = new Button(() =>
            {
                var drop = BirdManager.CollectBird(data, index);
                if (drop != null)
                {
                    ApothekeManager.Instance?.AddSeed(drop.seedName, drop.seedCount);
                    SaveManager.Instance.Save();
                    BirdManager.Instance?.OnBirdCollected?.Invoke(drop);
                }
                CloseInteractionPanel();
            }) { text = "Collect Seeds" };
            collectBtn.AddToClassList("interaction-btn-primary");
            interactionActions.Add(collectBtn);

            AddCloseButton();
        }
```

**NOTE on event invocation:** The `OnBirdCollected` event is invoked from the UI here because collection happens via UI interaction, not inside BirdManager's `Update()`. We need to make the event publicly invocable. In `BirdManager.cs`, change the event to use a public method instead:

Add to BirdManager:
```csharp
        public void NotifyBirdCollected(BirdSave bird)
        {
            OnBirdCollected?.Invoke(bird);
        }
```

And in `ShowBirdInteraction`, replace `BirdManager.Instance?.OnBirdCollected?.Invoke(drop)` with `BirdManager.Instance?.NotifyBirdCollected(drop)`.

**Step 5: Handle bird in MoveBuilding (prevent moving birds)**

Birds should NOT be movable via drag. The long-press drag is only enabled for `mode == CampsiteMode.Normal` and `cellType != CampBuildingType.Flame`. We also want to exclude birds. In `RebuildGrid()` around line 265, change:

```csharp
                        bool isMovable = cellType != CampBuildingType.Flame;
```
to:
```csharp
                        bool isMovable = cellType != CampBuildingType.Flame && cellType != CampBuildingType.Bird;
```

**Step 6: Verify compilation and visual rendering**

Check Unity console for errors. Enter play mode and verify birds can appear (may need to advance time or wait).

**Step 7: Commit**

```
feat: integrate birds into campsite grid UI with collect interaction
```

---

### Task 5: Add bird tile USS styling

**Files:**
- Modify: `Assets/UI/Styles/Campsite.uss` (or whichever USS file contains `.grid-cell--*` styles)

**Step 1: Find the correct USS file**

Search for `.grid-cell--flame` or `.grid-cell--plot` in USS files to find where tile styles are defined.

**Step 2: Add bird tile style**

Add after the other grid-cell variant styles:

```css
.grid-cell--bird {
    --hex-fill: rgba(180, 220, 255, 0.4);
    --hex-border: rgb(100, 160, 220);
}
```

Use a light blue color to differentiate birds from other buildings. Adjust the exact colors to match the game's visual style after seeing it in context.

**Step 3: Commit**

```
feat: add bird tile styling
```

---

### Task 6: Add BirdManager to scene

**Files:**
- Modify: Unity scene (add BirdManager component to the Managers GameObject)

**Step 1: Find the Managers GameObject**

Look for the existing managers in the scene. They're typically on a shared GameObject or individual GameObjects in the scene hierarchy.

**Step 2: Add BirdManager component**

Add `BirdManager` component to the same GameObject that has `VisitorSystem` (or the main managers object). This can be done via Unity MCP `manage_components` tool:

```
action: add
target: [managers GameObject name]
component_type: BirdManager
```

**Step 3: Verify in play mode**

Enter play mode, advance time, and verify:
- Birds appear on empty tiles
- Clicking a bird shows the interaction panel with seed info
- Collecting a bird adds seeds to inventory and removes the bird from the grid

**Step 4: Commit**

```
feat: wire BirdManager into scene
```

---

### Summary of all files touched:

| Action | File |
|--------|------|
| Modify | `Assets/Scripts/Data/SeedData.cs` |
| Modify | All 12 `Assets/Resources/Seeds/*.asset` files |
| Create | `Assets/Scripts/Data/BirdSave.cs` |
| Modify | `Assets/Scripts/Data/SaveData.cs` |
| Modify | `Assets/Scripts/Data/GameEnums.cs` |
| Create | `Assets/Scripts/Managers/BirdManager.cs` |
| Create | `Assets/Tests/EditMode/TestBirdManager.cs` |
| Modify | `Assets/Scripts/UI/CampsiteViewUI.cs` |
| Modify | USS stylesheet (grid cell styles) |
| Modify | Scene (add component) |
