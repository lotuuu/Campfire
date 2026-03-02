# Seed Recipe System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the binary weather pass/fail harvest system with a multi-dimension recipe scoring system where environmental conditions accumulate during growth and determine harvest yield.

**Architecture:** New `GrowthRecipe` serializable class on `SeedData` replaces `TriggerCondition preferredWeather`. `PlotSave` gains weather snapshot accumulators. `PlotManager` subscribes to `WeatherService.OnWeatherUpdated` to record snapshots for all growing plots. At harvest, a weighted average of per-dimension scores determines drop count.

**Tech Stack:** Unity 6, C#, NUnit (EditMode tests), ScriptableObject YAML assets

---

### Task 1: Create GrowthRecipe data class and scoring logic

**Files:**
- Create: `Assets/Scripts/Data/GrowthRecipe.cs`
- Test: `Assets/Tests/EditMode/TestGrowthRecipe.cs`

**Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/TestGrowthRecipe.cs`:

```csharp
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestGrowthRecipe
    {
        [Test]
        public void ScoreRange_PerfectMatch_Returns1()
        {
            float score = GrowthRecipe.ScoreRange(25f, 20f, 30f, 10f);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void ScoreRange_AtEdge_Returns1()
        {
            float score = GrowthRecipe.ScoreRange(20f, 20f, 30f, 10f);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void ScoreRange_OutsideTolerance_Returns0()
        {
            float score = GrowthRecipe.ScoreRange(5f, 20f, 30f, 10f);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void ScoreRange_HalfwayOutside_Returns0Point5()
        {
            // actual=15, range=[20,30], tolerance=10 -> distance=5, score=1-5/10=0.5
            float score = GrowthRecipe.ScoreRange(15f, 20f, 30f, 10f);
            Assert.AreEqual(0.5f, score, 0.001f);
        }

        [Test]
        public void ScoreWaterings_ExactMatch_Returns1()
        {
            float score = GrowthRecipe.ScoreWaterings(3, 3);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void ScoreWaterings_Off2_Returns0Point5()
        {
            float score = GrowthRecipe.ScoreWaterings(5, 3);
            Assert.AreEqual(0.5f, score, 0.001f);
        }

        [Test]
        public void ScoreWaterings_Off4OrMore_Returns0()
        {
            float score = GrowthRecipe.ScoreWaterings(7, 3);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void Evaluate_NoActiveDimensions_Returns1()
        {
            var recipe = new GrowthRecipe();
            var snapshots = new GrowthSnapshots { snapshotCount = 5 };
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Evaluate_HeatOnly_PerfectMatch()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 4,
                sumTemp = 100f // avg = 25, inside [20,30]
            };
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Evaluate_TwoDimensions_WeightedAverage()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 2f,
                useWaterings = true,
                idealWaterings = 3, wateringsWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 4,
                sumTemp = 100f // avg=25, perfect -> 1.0
            };
            int waterCount = 3; // perfect -> 1.0
            float score = recipe.Evaluate(snapshots, waterCount);
            // (1.0*2 + 1.0*1) / (2+1) = 1.0
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Evaluate_MoonPhase_FractionScoring()
        {
            var recipe = new GrowthRecipe
            {
                useMoon = true,
                requiredMoonPhase = MoonPhase.FullMoon,
                moonWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 10,
                moonPhaseSnapshots = new int[8]
            };
            snapshots.moonPhaseSnapshots[(int)MoonPhase.FullMoon] = 7; // 70%
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(0.7f, score, 0.001f);
        }

        [Test]
        public void Evaluate_ZeroSnapshots_Returns1()
        {
            var recipe = new GrowthRecipe { useHeat = true };
            var snapshots = new GrowthSnapshots { snapshotCount = 0 };
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(1f, score, 0.001f);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: Unity Test Runner EditMode or `run_tests` MCP tool
Expected: FAIL — `GrowthRecipe` and `GrowthSnapshots` don't exist yet

**Step 3: Create GrowthRecipe and GrowthSnapshots**

Create `Assets/Scripts/Data/GrowthRecipe.cs`:

```csharp
using System;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class GrowthRecipe
    {
        [Header("Heat")]
        public bool useHeat;
        public float idealTempMin;
        public float idealTempMax = 30f;
        public float heatTolerance = 10f;
        public float heatWeight = 1f;

        [Header("Wind")]
        public bool useWind;
        public float idealWindMin;
        public float idealWindMax = 10f;
        public float windTolerance = 5f;
        public float windWeight = 1f;

        [Header("Humidity")]
        public bool useHumidity;
        public float idealHumidityMin;
        public float idealHumidityMax = 80f;
        public float humidityTolerance = 20f;
        public float humidityWeight = 1f;

        [Header("Sunlight")]
        public bool useSunlight;
        public float idealSunlightMin;
        public float idealSunlightMax = 100f;
        public float sunlightTolerance = 20f;
        public float sunlightWeight = 1f;

        [Header("Rain")]
        public bool useRain;
        public float idealRainMin;
        public float idealRainMax = 1f;
        public float rainTolerance = 0.3f;
        public float rainWeight = 1f;

        [Header("Moon")]
        public bool useMoon;
        public MoonPhase requiredMoonPhase;
        public float moonWeight = 1f;

        [Header("Waterings")]
        public bool useWaterings;
        public int idealWaterings;
        public float wateringsWeight = 1f;

        public float Evaluate(GrowthSnapshots snapshots, int waterCount)
        {
            if (snapshots.snapshotCount <= 0) return 1f;

            float weightSum = 0f;
            float scoreSum = 0f;

            if (useHeat)
            {
                float avg = snapshots.sumTemp / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealTempMin, idealTempMax, heatTolerance) * heatWeight;
                weightSum += heatWeight;
            }
            if (useWind)
            {
                float avg = snapshots.sumWind / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealWindMin, idealWindMax, windTolerance) * windWeight;
                weightSum += windWeight;
            }
            if (useHumidity)
            {
                float avg = snapshots.sumHumidity / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealHumidityMin, idealHumidityMax, humidityTolerance) * humidityWeight;
                weightSum += humidityWeight;
            }
            if (useSunlight)
            {
                float avg = snapshots.sumSunlight / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealSunlightMin, idealSunlightMax, sunlightTolerance) * sunlightWeight;
                weightSum += sunlightWeight;
            }
            if (useRain)
            {
                float fraction = (float)snapshots.rainSnapshots / snapshots.snapshotCount;
                scoreSum += ScoreRange(fraction, idealRainMin, idealRainMax, rainTolerance) * rainWeight;
                weightSum += rainWeight;
            }
            if (useMoon)
            {
                float fraction = 0f;
                if (snapshots.moonPhaseSnapshots != null && snapshots.moonPhaseSnapshots.Length > (int)requiredMoonPhase)
                    fraction = (float)snapshots.moonPhaseSnapshots[(int)requiredMoonPhase] / snapshots.snapshotCount;
                scoreSum += fraction * moonWeight;
                weightSum += moonWeight;
            }
            if (useWaterings)
            {
                scoreSum += ScoreWaterings(waterCount, idealWaterings) * wateringsWeight;
                weightSum += wateringsWeight;
            }

            if (weightSum <= 0f) return 1f;
            return scoreSum / weightSum;
        }

        public static float ScoreRange(float actual, float min, float max, float tolerance)
        {
            if (actual >= min && actual <= max) return 1f;
            float distance = actual < min ? min - actual : actual - max;
            if (tolerance <= 0f) return 0f;
            return Mathf.Clamp01(1f - distance / tolerance);
        }

        public static float ScoreWaterings(int actual, int ideal)
        {
            int diff = Mathf.Abs(actual - ideal);
            return Mathf.Clamp01(1f - diff * 0.25f);
        }
    }

    [Serializable]
    public class GrowthSnapshots
    {
        public int snapshotCount;
        public float sumTemp;
        public float sumWind;
        public float sumHumidity;
        public float sumSunlight;
        public int rainSnapshots;
        public int[] moonPhaseSnapshots = new int[8];

        public void RecordSnapshot(WeatherData weather)
        {
            snapshotCount++;
            sumTemp += weather.temperature;
            sumWind += weather.windSpeed;
            sumHumidity += weather.humidity;
            sumSunlight += 100f - weather.cloudCover;
            if (weather.condition == WeatherCondition.Rain || weather.condition == WeatherCondition.Storm)
                rainSnapshots++;
            if (moonPhaseSnapshots == null || moonPhaseSnapshots.Length < 8)
                moonPhaseSnapshots = new int[8];
            moonPhaseSnapshots[(int)weather.moonPhase]++;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: Unity Test Runner EditMode
Expected: All 11 `TestGrowthRecipe` tests PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/GrowthRecipe.cs Assets/Tests/EditMode/TestGrowthRecipe.cs
git commit -m "feat: add GrowthRecipe scoring with tests"
```

---

### Task 2: Update SeedData to use GrowthRecipe

**Files:**
- Modify: `Assets/Scripts/Data/SeedData.cs`
- Modify: `Assets/Tests/EditMode/TestSeedData.cs`

**Step 1: Update SeedData**

Replace the entire contents of `Assets/Scripts/Data/SeedData.cs` with:

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

        [Header("Shop")]
        public float manaCost;
    }
}
```

**Step 2: Update TestSeedData**

Replace `Assets/Tests/EditMode/TestSeedData.cs` with:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSeedData
    {
        [Test]
        public void SeedData_HasExpectedFields()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.seedName = "TestSeed";
            seed.growthDurationHours = 4f;
            seed.baseDrops = 3;

            Assert.AreEqual("TestSeed", seed.seedName);
            Assert.AreEqual(4f, seed.growthDurationHours);
            Assert.AreEqual(3, seed.baseDrops);
        }

        [Test]
        public void SeedData_RecipeField_IsAssignable()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f,
                idealTempMax = 30f
            };

            Assert.IsNotNull(seed.recipe);
            Assert.IsTrue(seed.recipe.useHeat);
        }
    }
}
```

**Step 3: Run tests**

Run: Unity Test Runner EditMode
Expected: All tests pass (TestSeedData + TestGrowthRecipe). Some other tests referencing removed fields (like `TestPlotManager.WeatherMatch_BoostsGrowthSpeed`) will fail — that's expected, we fix them in Task 4.

**Step 4: Commit**

```bash
git add Assets/Scripts/Data/SeedData.cs Assets/Tests/EditMode/TestSeedData.cs
git commit -m "feat: replace SeedData weather/yield fields with GrowthRecipe"
```

---

### Task 3: Update PlotSave and PlotState

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs:35-42` (PlotSave)
- Modify: `Assets/Scripts/Data/GameEnums.cs:17` (PlotState)

**Step 1: Update PlotState enum**

In `Assets/Scripts/Data/GameEnums.cs`, change PlotState from:
```csharp
public enum PlotState { Empty, Planted, Watered, Growing, Mature }
```
to:
```csharp
public enum PlotState { Empty, Growing, Mature }
```

**Step 2: Update PlotSave**

In `Assets/Scripts/Data/SaveData.cs`, replace the PlotSave class with:

```csharp
[Serializable]
public class PlotSave
{
    public string seedName;
    public string plantTimeUtc;
    public int waterCount;
    public PlotState state = PlotState.Empty;
    public int gridX;
    public int gridY;
    public GrowthSnapshots snapshots = new();
}
```

**Step 3: Compile check**

Run: Check Unity console for compilation errors. There will be errors in `PlotManager.cs` and `CampsiteViewUI.cs` referencing removed fields (`watered`, `PlotState.Planted`). These are expected and fixed in Tasks 4 and 6.

**Step 4: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/GameEnums.cs
git commit -m "feat: update PlotSave and PlotState for recipe system"
```

---

### Task 4: Rewrite PlotManager for recipe-based growth

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`
- Modify: `Assets/Tests/EditMode/TestPlotManager.cs`

**Step 1: Write updated tests**

Replace `Assets/Tests/EditMode/TestPlotManager.cs`:

```csharp
using System;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestPlotManager
    {
        [Test]
        public void GrowthProgress_CalculatesCorrectly()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                plantTimeUtc = DateTime.UtcNow.AddHours(-2).ToString("o"),
                state = PlotState.Growing
            };

            float growthHours = 4f;
            var plantTime = DateTime.Parse(plot.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - plantTime).TotalHours;
            float progress = Mathf.Clamp01(elapsed / growthHours);

            Assert.AreEqual(0.5f, progress, 0.05f);
        }

        [Test]
        public void Harvest_ClearsPlot()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                state = PlotState.Mature,
                waterCount = 2
            };
            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.waterCount = 0;
            plot.state = PlotState.Empty;

            Assert.AreEqual(PlotState.Empty, plot.state);
            Assert.IsNull(plot.seedName);
            Assert.AreEqual(0, plot.waterCount);
        }

        [Test]
        public void HarvestDrops_PerfectRecipe_ReturnsBaseDrops()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            int baseDrops = 5;

            var snapshots = new GrowthSnapshots { snapshotCount = 10, sumTemp = 250f }; // avg=25
            float score = recipe.Evaluate(snapshots, 0);
            int drops = Mathf.Max(1, Mathf.RoundToInt(baseDrops * score));

            Assert.AreEqual(5, drops);
        }

        [Test]
        public void HarvestDrops_PoorRecipe_ReturnsAtLeast1()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            int baseDrops = 5;

            var snapshots = new GrowthSnapshots { snapshotCount = 10, sumTemp = 0f }; // avg=0, way off
            float score = recipe.Evaluate(snapshots, 0);
            int drops = Mathf.Max(1, Mathf.RoundToInt(baseDrops * score));

            Assert.AreEqual(1, drops);
        }

        [Test]
        public void Water_IncrementsWaterCount()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                state = PlotState.Growing,
                waterCount = 0
            };
            plot.waterCount++;
            Assert.AreEqual(1, plot.waterCount);
        }
    }
}
```

**Step 2: Rewrite PlotManager**

Replace `Assets/Scripts/Managers/PlotManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class PlotManager : MonoBehaviour
    {
        public static PlotManager Instance { get; private set; }

        public event Action<int> OnPlotChanged;
        public event Action<int, HarvestResult> OnHarvested;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
        }

        private void Start()
        {
            // Subscribe if WeatherService wasn't ready during OnEnable
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
        }

        private void Update()
        {
            CheckGrowthCompletion();
        }

        public List<PlotSave> Plots => SaveManager.Instance.Data.plots;

        public bool CraftPlot(int gridX, int gridY)
        {
            if (Plots.Count >= FlameManager.Instance.MaxPlots) return false;
            SaveManager.Instance.Data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = gridX, gridY = gridY });
            SaveManager.Instance.Save();
            return true;
        }

        public bool Plant(int plotIndex, string seedName)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Empty) return false;

            var seedEntry = data.seedInventory.Find(s => s.seedName == seedName);
            if (seedEntry == null || seedEntry.count <= 0) return false;

            seedEntry.count--;
            if (seedEntry.count <= 0) data.seedInventory.Remove(seedEntry);

            plot.seedName = seedName;
            plot.state = PlotState.Growing;
            plot.plantTimeUtc = GameTime.UtcNow.ToString("o");
            plot.waterCount = 0;
            plot.snapshots = new GrowthSnapshots();

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        public bool Water(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing) return false;

            if (!CurrencyManager.Instance.SpendWater(1)) return false;

            plot.waterCount++;

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        public float GetGrowthProgress(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return 0f;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing || string.IsNullOrEmpty(plot.plantTimeUtc))
                return 0f;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return 0f;

            var plantTime = DateTime.Parse(plot.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(GameTime.UtcNow - plantTime).TotalHours;
            return Mathf.Clamp01(elapsed / seed.growthDurationHours);
        }

        public float GetRemainingSeconds(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return 0f;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing || string.IsNullOrEmpty(plot.plantTimeUtc))
                return 0f;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return 0f;

            var plantTime = DateTime.Parse(plot.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsedSeconds = (float)(GameTime.UtcNow - plantTime).TotalSeconds;
            float totalSeconds = seed.growthDurationHours * 3600f;
            return Mathf.Max(0f, totalSeconds - elapsedSeconds);
        }

        public HarvestResult Harvest(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return null;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Mature) return null;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return null;

            float score = 1f;
            if (seed.recipe != null)
                score = seed.recipe.Evaluate(plot.snapshots ?? new GrowthSnapshots(), plot.waterCount);

            int drops = Mathf.Max(1, Mathf.RoundToInt(seed.baseDrops * score));

            AddItem(data, seed.seedName + "_harvest", drops);

            var result = new HarvestResult
            {
                seedName = seed.seedName,
                drops = drops,
                recipeScore = score
            };

            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.waterCount = 0;
            plot.snapshots = new GrowthSnapshots();
            plot.state = PlotState.Empty;

            SaveManager.Instance.Save();

            OnPlotChanged?.Invoke(plotIndex);
            OnHarvested?.Invoke(plotIndex, result);
            return result;
        }

        public bool InstantFinish(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing) return false;
            plot.state = PlotState.Mature;
            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        private void OnWeatherUpdated(WeatherData weather)
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            for (int i = 0; i < data.plots.Count; i++)
            {
                var plot = data.plots[i];
                if (plot.state != PlotState.Growing) continue;
                if (plot.snapshots == null) plot.snapshots = new GrowthSnapshots();
                plot.snapshots.RecordSnapshot(weather);
                changed = true;
            }
            if (changed) SaveManager.Instance.Save();
        }

        private void CheckGrowthCompletion()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            for (int i = 0; i < data.plots.Count; i++)
            {
                var plot = data.plots[i];
                if (plot.state != PlotState.Growing) continue;
                if (GetGrowthProgress(i) >= 1f)
                {
                    plot.state = PlotState.Mature;
                    changed = true;
                    OnPlotChanged?.Invoke(i);
                }
            }
            if (changed) SaveManager.Instance.Save();
        }

        private static void AddItem(SaveData data, string itemName, int count)
        {
            var existing = data.items.Find(i => i.itemName == itemName);
            if (existing != null) existing.count += count;
            else data.items.Add(new InventoryItem { itemName = itemName, count = count });
        }

        private static SeedData LoadSeed(string seedName)
        {
            if (string.IsNullOrEmpty(seedName)) return null;
            var all = Resources.LoadAll<SeedData>("Seeds");
            foreach (var s in all)
                if (s.seedName == seedName) return s;
            return null;
        }
    }

    [Serializable]
    public class HarvestResult
    {
        public string seedName;
        public int drops;
        public float recipeScore;
    }
}
```

**Step 3: Run tests**

Run: Unity Test Runner EditMode
Expected: All `TestPlotManager` and `TestGrowthRecipe` tests pass. Other tests may still reference old fields — check console.

**Step 4: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Tests/EditMode/TestPlotManager.cs
git commit -m "feat: rewrite PlotManager for recipe-based harvest scoring"
```

---

### Task 5: Update seed .asset files with recipe data

**Files:**
- Modify: `Assets/Resources/Seeds/Fern.asset`
- Modify: `Assets/Resources/Seeds/Sunflower.asset`
- Modify: `Assets/Resources/Seeds/Moonvine.asset`

**Step 1: Update Fern.asset**

Fern: likes rain, moderate temp. Replace the serialized fields after `m_EditorClassIdentifier:`:

```yaml
  seedName: Fern
  growthDurationHours: 2
  baseDrops: 2
  recipe:
    useHeat: 1
    idealTempMin: 15
    idealTempMax: 25
    heatTolerance: 10
    heatWeight: 1
    useWind: 0
    idealWindMin: 0
    idealWindMax: 10
    windTolerance: 5
    windWeight: 1
    useHumidity: 1
    idealHumidityMin: 60
    idealHumidityMax: 90
    humidityTolerance: 20
    humidityWeight: 1
    useSunlight: 0
    idealSunlightMin: 0
    idealSunlightMax: 100
    sunlightTolerance: 20
    sunlightWeight: 1
    useRain: 1
    idealRainMin: 0.3
    idealRainMax: 0.8
    rainTolerance: 0.3
    rainWeight: 1.5
    useMoon: 0
    requiredMoonPhase: 0
    moonWeight: 1
    useWaterings: 1
    idealWaterings: 2
    wateringsWeight: 0.5
  icon: {fileID: 0}
  growthSprites: []
  manaCost: 0
```

**Step 2: Update Sunflower.asset**

Sunflower: loves heat, sunlight, dislikes rain.

```yaml
  seedName: Sunflower
  growthDurationHours: 4
  baseDrops: 3
  recipe:
    useHeat: 1
    idealTempMin: 25
    idealTempMax: 40
    heatTolerance: 10
    heatWeight: 2
    useWind: 0
    idealWindMin: 0
    idealWindMax: 10
    windTolerance: 5
    windWeight: 1
    useHumidity: 0
    idealHumidityMin: 0
    idealHumidityMax: 80
    humidityTolerance: 20
    humidityWeight: 1
    useSunlight: 1
    idealSunlightMin: 60
    idealSunlightMax: 100
    sunlightTolerance: 20
    sunlightWeight: 2
    useRain: 1
    idealRainMin: 0
    idealRainMax: 0.2
    rainTolerance: 0.3
    rainWeight: 1
    useMoon: 0
    requiredMoonPhase: 0
    moonWeight: 1
    useWaterings: 1
    idealWaterings: 3
    wateringsWeight: 1
  icon: {fileID: 0}
  growthSprites: []
  manaCost: 25
```

**Step 3: Update Moonvine.asset**

Moonvine: needs full moon, nighttime humidity, light watering.

```yaml
  seedName: Moonvine
  growthDurationHours: 8
  baseDrops: 5
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
    idealHumidityMin: 50
    idealHumidityMax: 90
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
    useMoon: 1
    requiredMoonPhase: 4
    moonWeight: 3
    useWaterings: 1
    idealWaterings: 2
    wateringsWeight: 0.5
  icon: {fileID: 0}
  growthSprites: []
  manaCost: 50
```

**Step 4: Commit**

```bash
git add Assets/Resources/Seeds/Fern.asset Assets/Resources/Seeds/Sunflower.asset Assets/Resources/Seeds/Moonvine.asset
git commit -m "feat: add growth recipes to seed assets"
```

---

### Task 6: Update CampsiteViewUI for new flow

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

The UI changes needed:
1. Remove `PlotState.Planted` case — planting goes directly to Growing
2. Watering mode: target `Growing` plots instead of `Planted`
3. Update harvest result display to show recipe score instead of weather match
4. Add "Water" button to Growing plot interaction panel

**Step 1: Update CampsiteViewUI**

In `CampsiteViewUI.cs`:

a) In `RebuildGrid()`, change the watering mode highlight (line ~196) from:
```csharp
if (plot.state == PlotState.Planted)
    cell.AddToClassList("grid-cell--water-target");
```
to:
```csharp
if (plot.state == PlotState.Growing)
    cell.AddToClassList("grid-cell--water-target");
```

b) In `OnCellTapped()`, change the watering condition (line ~307) from:
```csharp
if (plot.state == PlotState.Planted)
```
to:
```csharp
if (plot.state == PlotState.Growing)
```

c) In `ShowPlotInteraction()`, remove the entire `case PlotState.Planted:` block (lines 509-514) and update the `case PlotState.Growing:` block to add a water button:

```csharp
case PlotState.Growing:
    interactionTitle.text = plot.seedName;
    float remaining = PlotManager.Instance.GetRemainingSeconds(index);
    var progressLabel = new Label($"Growing... {FormatTimeRemaining(remaining)} left");
    progressLabel.AddToClassList("interaction-info");
    interactionBody.Add(progressLabel);

    if (plot.snapshots != null && plot.snapshots.snapshotCount > 0)
    {
        var snapshotLabel = new Label($"Waterings: {plot.waterCount}");
        snapshotLabel.AddToClassList("interaction-info");
        interactionBody.Add(snapshotLabel);
    }

    var finishBtn = new Button(() =>
    {
        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendGems(1))
        {
            PlotManager.Instance.InstantFinish(index);
            CloseInteractionPanel();
        }
    }) { text = "Finish Now (1 Gem)" };
    finishBtn.AddToClassList("interaction-btn-primary");
    interactionActions.Add(finishBtn);
    break;
```

d) In `ShowHarvestResult()`, replace the quality/weather display with recipe score:

```csharp
private void ShowHarvestResult(HarvestResult result)
{
    interactionBody.Clear();
    interactionActions.Clear();

    interactionTitle.text = "Harvested!";

    var yieldLabel = new Label($"{result.seedName} x{result.drops}");
    yieldLabel.AddToClassList("interaction-info");
    interactionBody.Add(yieldLabel);

    string qualityText = result.recipeScore >= 0.8f ? "Excellent"
        : result.recipeScore >= 0.5f ? "Good"
        : "Poor";
    int pct = Mathf.RoundToInt(result.recipeScore * 100f);
    var qualityLabel = new Label($"Recipe match: {qualityText} ({pct}%)");
    qualityLabel.AddToClassList("interaction-info");
    interactionBody.Add(qualityLabel);

    AddCloseButton();
}
```

**Step 2: Run all tests and check console**

Run: Unity Test Runner EditMode + check console for compile errors
Expected: All tests pass, no compile errors

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: update CampsiteViewUI for recipe-based growth flow"
```

---

### Task 7: Update GameManager new-player setup

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs`

**Step 1: Check GameManager for references to old fields**

The `GameManager.Start()` method sets up new player data. Remove any references to `waterRequired` or `watered` fields if present. The new-player grant (50 mana, 2 vases, 1 plot, 3 Fern seeds) should remain unchanged — just ensure plots are created with `PlotState.Empty` (no `watered` field).

**Step 2: Run all tests**

Run: Unity Test Runner EditMode
Expected: All tests pass

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "fix: update GameManager for new PlotSave fields"
```

---

### Task 8: Fix any remaining compilation errors

**Files:**
- Potentially: any file still referencing `preferredWeather`, `waterRequired`, `baseYield`, `qualityMultiplier`, `weatherMatched`, `PlotState.Planted`, `PlotState.Watered`, `plot.watered`

**Step 1: Search for stale references**

Search the codebase for: `preferredWeather`, `waterRequired`, `baseYield`, `WeatherMatchBonus`, `MinQualityMultiplier`, `MaxQualityMultiplier`, `qualityMultiplier`, `weatherMatched`, `PlotState.Planted`, `PlotState.Watered`, `.watered`

**Step 2: Fix each reference**

Update or remove any remaining references found. Common locations:
- `ResourceDisplayUI.cs` if it displays yield info
- `CampFireUI.cs` if it references plot states
- Any other UI file

**Step 3: Run all tests and verify clean compile**

Run: Unity Test Runner EditMode + check console
Expected: 0 compile errors, all tests pass

**Step 4: Commit**

```bash
git add -A
git commit -m "fix: remove all stale references to old harvest system"
```

---

### Task 9: Final verification

**Step 1: Run full test suite**

Run: Unity Test Runner EditMode — all tests
Expected: All tests pass

**Step 2: Enter play mode briefly**

Verify: game loads without errors, plots can be placed, seeds can be planted (go directly to Growing state), game doesn't crash.

**Step 3: Check console**

Expected: No errors or warnings related to the recipe system.
