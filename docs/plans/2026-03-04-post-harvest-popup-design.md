# Post-Harvest Results Popup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Show a rich post-harvest popup with seed icon, drop count, recipe match tier, and per-axis breakdown of ideal vs actual growing conditions.

**Architecture:** Extend `HarvestResult` to carry snapshot/recipe data, add `GrowthRecipe.EvaluatePerAxis()` for per-axis scoring, fix the `RebuildGrid` bug that hides the panel, and revamp `ShowHarvestResult()` in CampsiteViewUI.

**Tech Stack:** Unity UI Toolkit (C#, USS)

---

### Task 1: Add `EvaluatePerAxis()` to GrowthRecipe

**Files:**
- Modify: `Assets/Scripts/Data/GrowthRecipe.cs`
- Test: `Assets/Tests/EditMode/TestGrowthRecipe.cs`

**Step 1: Write the failing tests**

Add to `Assets/Tests/EditMode/TestGrowthRecipe.cs`:

```csharp
[Test]
public void EvaluatePerAxis_HeatOnly_ReturnsOneResult()
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
        sumTemp = 100f // avg 25
    };
    var results = recipe.EvaluatePerAxis(snapshots, 0);
    Assert.AreEqual(1, results.Count);
    Assert.AreEqual("Heat", results[0].axisName);
    Assert.AreEqual(25f, results[0].actual, 0.1f);
    Assert.AreEqual(1f, results[0].score, 0.001f);
}

[Test]
public void EvaluatePerAxis_TwoAxes_ReturnsBoth()
{
    var recipe = new GrowthRecipe
    {
        useHeat = true,
        idealTempMin = 20f, idealTempMax = 30f,
        heatTolerance = 10f, heatWeight = 1f,
        useWaterings = true,
        idealWateringsMin = 2, idealWateringsMax = 4,
        wateringsTolerance = 2f, wateringsWeight = 1f
    };
    var snapshots = new GrowthSnapshots { snapshotCount = 4, sumTemp = 100f };
    var results = recipe.EvaluatePerAxis(snapshots, 5);
    Assert.AreEqual(2, results.Count);
    Assert.AreEqual("Heat", results[0].axisName);
    Assert.AreEqual(1f, results[0].score, 0.001f);
    Assert.AreEqual("Waterings", results[1].axisName);
    Assert.AreEqual(0.5f, results[1].score, 0.001f); // 5 is 1 past max 4, tol 2
}

[Test]
public void EvaluatePerAxis_NoAxesEnabled_ReturnsEmpty()
{
    var recipe = new GrowthRecipe();
    var snapshots = new GrowthSnapshots { snapshotCount = 5 };
    var results = recipe.EvaluatePerAxis(snapshots, 0);
    Assert.AreEqual(0, results.Count);
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with mode `EditMode`, test_names `["Garden.Tests.TestGrowthRecipe.EvaluatePerAxis_HeatOnly_ReturnsOneResult","Garden.Tests.TestGrowthRecipe.EvaluatePerAxis_TwoAxes_ReturnsBoth","Garden.Tests.TestGrowthRecipe.EvaluatePerAxis_NoAxesEnabled_ReturnsEmpty"]`
Expected: FAIL — `EvaluatePerAxis` does not exist

**Step 3: Implement `AxisResult` and `EvaluatePerAxis()`**

Add to `Assets/Scripts/Data/GrowthRecipe.cs`, inside the `Garden` namespace (before the closing brace of the file), add `AxisResult` class. Inside `GrowthRecipe` class, add `EvaluatePerAxis` method:

```csharp
// Add at top of file
using System.Collections.Generic;

// Add inside GrowthRecipe class, after Evaluate():
public List<AxisResult> EvaluatePerAxis(GrowthSnapshots snapshots, int waterCount)
{
    var results = new List<AxisResult>();
    if (snapshots.snapshotCount <= 0) return results;

    if (useHeat)
    {
        float avg = snapshots.sumTemp / snapshots.snapshotCount;
        results.Add(new AxisResult
        {
            axisName = "Heat",
            actual = avg,
            idealMin = idealTempMin,
            idealMax = idealTempMax,
            unit = "\u00b0C",
            score = ScoreRange(avg, idealTempMin, idealTempMax, heatTolerance)
        });
    }
    if (useWind)
    {
        float avg = snapshots.sumWind / snapshots.snapshotCount;
        results.Add(new AxisResult
        {
            axisName = "Wind",
            actual = avg,
            idealMin = idealWindMin,
            idealMax = idealWindMax,
            unit = "m/s",
            score = ScoreRange(avg, idealWindMin, idealWindMax, windTolerance)
        });
    }
    if (useHumidity)
    {
        float avg = snapshots.sumHumidity / snapshots.snapshotCount;
        results.Add(new AxisResult
        {
            axisName = "Humidity",
            actual = avg,
            idealMin = idealHumidityMin,
            idealMax = idealHumidityMax,
            unit = "%",
            score = ScoreRange(avg, idealHumidityMin, idealHumidityMax, humidityTolerance)
        });
    }
    if (useSunlight)
    {
        float avg = snapshots.sumSunlight / snapshots.snapshotCount;
        results.Add(new AxisResult
        {
            axisName = "Sunlight",
            actual = avg,
            idealMin = idealSunlightMin,
            idealMax = idealSunlightMax,
            unit = "%",
            score = ScoreRange(avg, idealSunlightMin, idealSunlightMax, sunlightTolerance)
        });
    }
    if (useRain)
    {
        float fraction = (float)snapshots.rainSnapshots / snapshots.snapshotCount;
        results.Add(new AxisResult
        {
            axisName = "Rain",
            actual = fraction * 100f,
            idealMin = idealRainMin * 100f,
            idealMax = idealRainMax * 100f,
            unit = "%",
            score = ScoreRange(fraction, idealRainMin, idealRainMax, rainTolerance)
        });
    }
    if (useMoon)
    {
        float fraction = 0f;
        if (snapshots.moonPhaseSnapshots != null && snapshots.moonPhaseSnapshots.Length > (int)requiredMoonPhase)
            fraction = (float)snapshots.moonPhaseSnapshots[(int)requiredMoonPhase] / snapshots.snapshotCount;
        results.Add(new AxisResult
        {
            axisName = "Moon",
            actual = fraction * 100f,
            idealMin = -1f, // special: moon uses phase name, not range
            idealMax = -1f,
            unit = "% " + requiredMoonPhase,
            score = fraction
        });
    }
    if (useWaterings)
    {
        results.Add(new AxisResult
        {
            axisName = "Waterings",
            actual = waterCount,
            idealMin = idealWateringsMin,
            idealMax = idealWateringsMax,
            unit = "x",
            score = ScoreRange(waterCount, idealWateringsMin, idealWateringsMax, wateringsTolerance)
        });
    }

    return results;
}
```

Add `AxisResult` class inside `Garden` namespace (outside `GrowthRecipe`):

```csharp
public class AxisResult
{
    public string axisName;
    public float actual;
    public float idealMin;
    public float idealMax;
    public string unit;
    public float score;
}
```

**Step 4: Run tests to verify they pass**

Run: same test names as Step 2
Expected: PASS

**Step 5: Commit**

```
feat: add GrowthRecipe.EvaluatePerAxis for per-axis harvest scoring
```

---

### Task 2: Extend `HarvestResult` and capture data in `Harvest()`

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs:443-448` (HarvestResult class)
- Modify: `Assets/Scripts/Managers/PlotManager.cs:251-292` (Harvest method)

**Step 1: Extend `HarvestResult`**

In `Assets/Scripts/Managers/PlotManager.cs`, change the `HarvestResult` class (line 443):

```csharp
[Serializable]
public class HarvestResult
{
    public string seedName;
    public int drops;
    public float recipeScore;
    public GrowthSnapshots snapshots;
    public int waterCount;
    public GrowthRecipe recipe;
}
```

**Step 2: Capture data before clearing plot**

In `Harvest()` method (line 269), update the result construction to capture snapshot data before the plot is cleared. Change the result creation block to:

```csharp
var result = new HarvestResult
{
    seedName = seed.name,
    drops = drops,
    recipeScore = score,
    snapshots = plot.snapshots ?? new GrowthSnapshots(),
    waterCount = plot.waterCount,
    recipe = seed.recipe
};
```

This must come before the plot clearing lines (line 276+).

**Step 3: Run existing tests**

Run: `run_tests` with mode `EditMode`
Expected: All existing tests still pass (HarvestResult changes are additive)

**Step 4: Commit**

```
feat: extend HarvestResult with snapshot and recipe data
```

---

### Task 3: Fix RebuildGrid bug and revamp ShowHarvestResult UI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs:1148-1163` (harvest button callback)
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs:1175-1195` (ShowHarvestResult method)
- Modify: `Assets/UI/Styles/Interaction.uss` (add harvest result styles)

**Step 1: Fix RebuildGrid ordering in harvest button callback**

In `Assets/Scripts/UI/CampsiteViewUI.cs`, replace the harvest button callback (lines 1148-1162):

```csharp
var harvestBtn = new Button(() =>
{
    suppressRebuild = true;
    var result = PlotManager.Instance.Harvest(index);
    suppressRebuild = false;
    if (result != null)
    {
        RebuildGrid();
        ShowHarvestResult(result);
        ShowInteractionPanel();
    }
    else
        CloseInteractionPanel();
}) { text = "Harvest" };
```

Key change: `RebuildGrid()` now runs first (which closes the panel and rebuilds the grid), then `ShowHarvestResult()` populates it, then `ShowInteractionPanel()` re-opens it.

**Step 2: Revamp `ShowHarvestResult()` method**

Replace the entire `ShowHarvestResult` method (lines 1175-1195):

```csharp
private void ShowHarvestResult(HarvestResult result)
{
    interactionBody.Clear();
    interactionActions.Clear();

    interactionTitle.text = "Harvested!";

    // Seed icon + yield row
    var yieldRow = new VisualElement();
    yieldRow.AddToClassList("harvest-yield-row");
    var seed = Resources.Load<SeedData>("Seeds/" + result.seedName);
    if (seed != null && seed.icon != null)
    {
        var iconEl = new VisualElement();
        iconEl.AddToClassList("harvest-seed-icon");
        iconEl.style.backgroundImage = new StyleBackground(seed.icon);
        yieldRow.Add(iconEl);
    }
    var yieldLabel = new Label($"{PlotManager.GetSeedDisplayName(result.seedName)} x{result.drops}");
    yieldLabel.AddToClassList("harvest-yield-label");
    yieldRow.Add(yieldLabel);
    interactionBody.Add(yieldRow);

    // Recipe match tier
    string matchText = result.recipeScore >= 0.8f ? "Perfect Match"
        : result.recipeScore >= 0.5f ? "Good Match"
        : "Weak Match";
    string matchClass = result.recipeScore >= 0.8f ? "harvest-match--perfect"
        : result.recipeScore >= 0.5f ? "harvest-match--good"
        : "harvest-match--weak";
    int pct = Mathf.RoundToInt(result.recipeScore * 100f);
    var matchLabel = new Label($"{matchText} ({pct}%)");
    matchLabel.AddToClassList("harvest-match-badge");
    matchLabel.AddToClassList(matchClass);
    interactionBody.Add(matchLabel);

    // Per-axis breakdown
    if (result.recipe != null)
    {
        var axisResults = result.recipe.EvaluatePerAxis(result.snapshots, result.waterCount);
        if (axisResults.Count > 0)
        {
            var header = new Label("Recipe Breakdown");
            header.AddToClassList("interaction-section-header");
            interactionBody.Add(header);

            foreach (var axis in axisResults)
            {
                var row = new VisualElement();
                row.AddToClassList("harvest-axis-row");

                var nameEl = new Label(axis.axisName);
                nameEl.AddToClassList("harvest-axis-name");
                row.Add(nameEl);

                string actualStr;
                string idealStr;
                if (axis.axisName == "Moon")
                {
                    actualStr = $"{Mathf.RoundToInt(axis.actual)}%";
                    idealStr = axis.unit.Replace("% ", "");
                }
                else if (axis.axisName == "Waterings")
                {
                    actualStr = $"{Mathf.RoundToInt(axis.actual)}{axis.unit}";
                    idealStr = axis.idealMin == axis.idealMax
                        ? $"{Mathf.RoundToInt(axis.idealMin)}{axis.unit}"
                        : $"{Mathf.RoundToInt(axis.idealMin)}-{Mathf.RoundToInt(axis.idealMax)}{axis.unit}";
                }
                else
                {
                    actualStr = $"{axis.actual:F0}{axis.unit}";
                    idealStr = $"{axis.idealMin:F0}-{axis.idealMax:F0}{axis.unit}";
                }

                var actualEl = new Label(actualStr);
                actualEl.AddToClassList("harvest-axis-actual");
                row.Add(actualEl);

                var idealEl = new Label($"({idealStr})");
                idealEl.AddToClassList("harvest-axis-ideal");
                row.Add(idealEl);

                var statusEl = new Label(axis.score >= 0.5f ? "+" : "-");
                statusEl.AddToClassList(axis.score >= 0.5f ? "harvest-axis-pass" : "harvest-axis-fail");
                row.Add(statusEl);

                interactionBody.Add(row);
            }
        }
    }

    AddCloseButton();
}
```

**Step 3: Add USS styles**

Append to `Assets/UI/Styles/Interaction.uss`:

```css
/* ── Harvest Result ── */

.harvest-yield-row {
    flex-direction: row;
    align-items: center;
    justify-content: center;
    margin-bottom: var(--spacing-sm);
}

.harvest-seed-icon {
    width: 64px;
    height: 64px;
    margin-right: var(--spacing-sm);
    -unity-background-scale-mode: scale-to-fit;
}

.harvest-yield-label {
    font-size: 32px;
    color: rgb(240, 220, 180);
    -unity-font-style: bold;
}

.harvest-match-badge {
    font-size: 28px;
    -unity-font-style: bold;
    -unity-text-align: upper-center;
    margin-bottom: var(--spacing-sm);
    padding: 4px 16px;
    border-radius: 8px;
}

.harvest-match--perfect {
    color: rgb(120, 200, 80);
    background-color: rgba(120, 200, 80, 0.15);
}

.harvest-match--good {
    color: rgb(220, 180, 60);
    background-color: rgba(220, 180, 60, 0.15);
}

.harvest-match--weak {
    color: rgb(220, 120, 70);
    background-color: rgba(220, 120, 70, 0.15);
}

.harvest-axis-row {
    flex-direction: row;
    align-items: center;
    padding: 4px var(--spacing-sm);
    margin-bottom: 2px;
    background-color: rgba(50, 35, 18, 0.4);
    border-radius: 4px;
    align-self: stretch;
}

.harvest-axis-name {
    font-size: 22px;
    color: rgb(180, 160, 130);
    width: 25%;
}

.harvest-axis-actual {
    font-size: 22px;
    color: rgb(220, 200, 160);
    -unity-font-style: bold;
    width: 25%;
    -unity-text-align: upper-center;
}

.harvest-axis-ideal {
    font-size: 20px;
    color: rgb(140, 120, 90);
    width: 35%;
    -unity-text-align: upper-center;
}

.harvest-axis-pass {
    font-size: 24px;
    color: rgb(120, 200, 80);
    -unity-font-style: bold;
    width: 15%;
    -unity-text-align: upper-center;
}

.harvest-axis-fail {
    font-size: 24px;
    color: rgb(220, 90, 70);
    -unity-font-style: bold;
    width: 15%;
    -unity-text-align: upper-center;
}
```

**Step 4: Verify compilation**

Run: `read_console` filtering for errors after Unity recompiles.
Expected: No compilation errors.

**Step 5: Run all tests**

Run: `run_tests` with mode `EditMode`
Expected: All tests pass.

**Step 6: Commit**

```
feat: add post-harvest results popup with recipe breakdown
```
