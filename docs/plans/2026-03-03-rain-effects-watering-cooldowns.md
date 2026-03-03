# Rain Effects & Watering Cooldowns Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** After 15 minutes of continuous rain, fill all vases and water all growing plants (free). Add watering cooldowns: 2h manual, 6h rain, shared timestamp.

**Architecture:** Add `lastWateredUtc` to `PlotSave` for cooldown tracking, and `rainStartTimeUtc`/`lastRainEffectTimeUtc` to `SaveData` for rain event detection. New static helpers `RainFillAllVases` and `RainWaterAllPlots` on existing managers for testability. `PlotManager.OnWeatherUpdated` gains rain event detection logic.

**Tech Stack:** Unity 6 C# / NUnit EditMode tests

---

### Task 1: Add data fields to SaveData and PlotSave

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`

**Step 1: Add rain tracking fields to SaveData and PlotSave**

In `SaveData`, add after the `mallums` field:

```csharp
public string rainStartTimeUtc;
public string lastRainEffectTimeUtc;
```

In `PlotSave`, add after the `snapshots` field:

```csharp
public string lastWateredUtc;
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs
git commit -m "feat: add rain tracking and watering cooldown fields to SaveData/PlotSave"
```

---

### Task 2: Add watering cooldown constants and helper

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

**Step 1: Write failing test for manual watering cooldown check**

Create tests in `Assets/Tests/EditMode/TestRainAndWatering.cs`:

```csharp
using System;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestRainAndWatering
    {
        [Test]
        public void CanWater_NeverWatered_ReturnsTrue()
        {
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = null };
            bool result = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            Assert.IsTrue(result);
        }

        [Test]
        public void CanWater_WateredRecently_ReturnsFalse()
        {
            string recent = DateTime.UtcNow.AddMinutes(-30).ToString("o");
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = recent };
            bool result = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            Assert.IsFalse(result);
        }

        [Test]
        public void CanWater_WateredOverTwoHoursAgo_ReturnsTrue()
        {
            string old = DateTime.UtcNow.AddHours(-3).ToString("o");
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = old };
            bool result = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            Assert.IsTrue(result);
        }

        [Test]
        public void CanWater_RainCooldown_SixHours()
        {
            string threeHoursAgo = DateTime.UtcNow.AddHours(-3).ToString("o");
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = threeHoursAgo };
            bool manual = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            bool rain = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.RainWaterCooldownHours);
            Assert.IsTrue(manual);  // 3h > 2h
            Assert.IsFalse(rain);   // 3h < 6h
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: FAIL — `PlotManager.CanWaterPlot` does not exist

**Step 3: Add constants and static helper to PlotManager**

Add at the top of the `PlotManager` class (after the singleton fields):

```csharp
public static readonly float ManualWaterCooldownHours = 2f;
public static readonly float RainWaterCooldownHours = 6f;
public static readonly float RainTriggerMinutes = 15f;

public static bool CanWaterPlot(PlotSave plot, DateTime utcNow, float cooldownHours)
{
    if (plot.state != PlotState.Growing) return false;
    if (string.IsNullOrEmpty(plot.lastWateredUtc)) return true;
    var lastWatered = DateTime.Parse(plot.lastWateredUtc, null,
        System.Globalization.DateTimeStyles.RoundtripKind);
    return (utcNow - lastWatered).TotalHours >= cooldownHours;
}
```

**Step 4: Run tests to verify they pass**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: PASS (all 4)

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Tests/EditMode/TestRainAndWatering.cs
git commit -m "feat: add CanWaterPlot static helper with cooldown check"
```

---

### Task 3: Add cooldown enforcement to PlotManager.Water()

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

**Step 1: Write failing test for cooldown rejection**

Add to `TestRainAndWatering.cs`:

```csharp
[Test]
public void Water_SetsLastWateredUtc()
{
    var plot = new PlotSave { state = PlotState.Growing, waterCount = 0, lastWateredUtc = null };
    string now = DateTime.UtcNow.ToString("o");
    PlotManager.ApplyWatering(plot, now);
    Assert.AreEqual(1, plot.waterCount);
    Assert.AreEqual(now, plot.lastWateredUtc);
}

[Test]
public void ApplyWatering_IncrementsWaterCount()
{
    var plot = new PlotSave { state = PlotState.Growing, waterCount = 2, lastWateredUtc = null };
    string now = DateTime.UtcNow.ToString("o");
    PlotManager.ApplyWatering(plot, now);
    Assert.AreEqual(3, plot.waterCount);
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: FAIL — `PlotManager.ApplyWatering` does not exist

**Step 3: Add ApplyWatering static helper and update Water()**

Add to `PlotManager`:

```csharp
public static void ApplyWatering(PlotSave plot, string utcNow)
{
    plot.waterCount++;
    plot.lastWateredUtc = utcNow;
}
```

Update the existing `Water(int plotIndex)` method to use cooldown check and set `lastWateredUtc`:

```csharp
public bool Water(int plotIndex)
{
    var data = SaveManager.Instance.Data;
    if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
    var plot = data.plots[plotIndex];
    if (!CanWaterPlot(plot, GameTime.UtcNow, ManualWaterCooldownHours)) return false;

    if (!CurrencyManager.Instance.SpendWater(1)) return false;

    ApplyWatering(plot, GameTime.UtcNow.ToString("o"));

    SaveManager.Instance.Save();
    OnPlotChanged?.Invoke(plotIndex);
    return true;
}
```

Also update `Harvest()` to clear `lastWateredUtc` during plot reset. In `Harvest()`, after the line `plot.snapshots = new GrowthSnapshots();`, add:

```csharp
plot.lastWateredUtc = null;
```

And in `Plant()`, after `plot.snapshots = new GrowthSnapshots();`, add:

```csharp
plot.lastWateredUtc = null;
```

**Step 4: Run tests to verify they pass**

Run: `run_tests` with `mode: "EditMode"`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Tests/EditMode/TestRainAndWatering.cs
git commit -m "feat: enforce 2h manual watering cooldown with lastWateredUtc"
```

---

### Task 4: Add RainFillAllVases static helper

**Files:**
- Modify: `Assets/Scripts/Managers/VaseManager.cs`

**Step 1: Write failing test for rain-filling all vases**

Add to `TestRainAndWatering.cs`:

```csharp
[Test]
public void RainFillAllVases_FillsEmptyAndFillingVases()
{
    var data = new SaveData();
    data.vases.Add(new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Empty });
    data.vases.Add(new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Filling, fillStartTimeUtc = DateTime.UtcNow.ToString("o") });
    data.vases.Add(new VaseSave { capacity = 5, currentWater = 5, state = VaseState.Full });

    VaseManager.RainFillAllVases(data.vases);

    Assert.AreEqual(VaseState.Full, data.vases[0].state);
    Assert.AreEqual(5, data.vases[0].currentWater);
    Assert.AreEqual(VaseState.Full, data.vases[1].state);
    Assert.AreEqual(5, data.vases[1].currentWater);
    Assert.IsNull(data.vases[1].fillStartTimeUtc);
    Assert.AreEqual(VaseState.Full, data.vases[2].state); // already full, stays full
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: FAIL — `VaseManager.RainFillAllVases` does not exist

**Step 3: Add static helper to VaseManager**

Add to `VaseManager`:

```csharp
public static void RainFillAllVases(List<VaseSave> vases)
{
    foreach (var vase in vases)
    {
        vase.currentWater = vase.capacity;
        vase.state = VaseState.Full;
        vase.fillStartTimeUtc = null;
    }
}
```

Add `using System.Collections.Generic;` to VaseManager imports if not already present.

**Step 4: Run tests to verify they pass**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/VaseManager.cs Assets/Tests/EditMode/TestRainAndWatering.cs
git commit -m "feat: add RainFillAllVases static helper"
```

---

### Task 5: Add RainWaterAllPlots static helper

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

**Step 1: Write failing test for rain-watering all plots**

Add to `TestRainAndWatering.cs`:

```csharp
[Test]
public void RainWaterAllPlots_WatersGrowingPlotsWithExpiredCooldown()
{
    string now = DateTime.UtcNow.ToString("o");
    var plots = new System.Collections.Generic.List<PlotSave>
    {
        new PlotSave { state = PlotState.Growing, waterCount = 0, lastWateredUtc = null },
        new PlotSave { state = PlotState.Growing, waterCount = 1, lastWateredUtc = DateTime.UtcNow.AddHours(-7).ToString("o") },
        new PlotSave { state = PlotState.Growing, waterCount = 1, lastWateredUtc = DateTime.UtcNow.AddHours(-3).ToString("o") },
        new PlotSave { state = PlotState.Empty, waterCount = 0, lastWateredUtc = null },
        new PlotSave { state = PlotState.Mature, waterCount = 2, lastWateredUtc = null },
    };

    int watered = PlotManager.RainWaterAllPlots(plots, DateTime.UtcNow);

    Assert.AreEqual(2, watered); // plots[0] and plots[1]
    Assert.AreEqual(1, plots[0].waterCount);
    Assert.IsNotNull(plots[0].lastWateredUtc);
    Assert.AreEqual(2, plots[1].waterCount);
    Assert.AreEqual(1, plots[2].waterCount); // still on cooldown (3h < 6h)
    Assert.AreEqual(0, plots[3].waterCount); // Empty — not watered
    Assert.AreEqual(2, plots[4].waterCount); // Mature — not watered
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: FAIL — `PlotManager.RainWaterAllPlots` does not exist

**Step 3: Add static helper to PlotManager**

Add to `PlotManager`:

```csharp
public static int RainWaterAllPlots(List<PlotSave> plots, DateTime utcNow)
{
    int count = 0;
    string nowStr = utcNow.ToString("o");
    foreach (var plot in plots)
    {
        if (CanWaterPlot(plot, utcNow, RainWaterCooldownHours))
        {
            ApplyWatering(plot, nowStr);
            count++;
        }
    }
    return count;
}
```

**Step 4: Run tests to verify they pass**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Tests/EditMode/TestRainAndWatering.cs
git commit -m "feat: add RainWaterAllPlots static helper"
```

---

### Task 6: Add rain event detection logic

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

**Step 1: Write failing tests for rain event detection**

Add to `TestRainAndWatering.cs`:

```csharp
[Test]
public void ShouldTriggerRainEffect_FirstRainPoll_ReturnsFalse()
{
    var data = new SaveData();
    bool triggered = PlotManager.CheckRainEvent(data,
        WeatherCondition.Rain, DateTime.UtcNow);
    Assert.IsFalse(triggered);
    Assert.IsNotNull(data.rainStartTimeUtc); // timer started
}

[Test]
public void ShouldTriggerRainEffect_RainFor15Min_ReturnsTrue()
{
    var data = new SaveData();
    data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-16).ToString("o");
    bool triggered = PlotManager.CheckRainEvent(data,
        WeatherCondition.Rain, DateTime.UtcNow);
    Assert.IsTrue(triggered);
    Assert.IsNotNull(data.lastRainEffectTimeUtc);
}

[Test]
public void ShouldTriggerRainEffect_AlreadyTriggeredThisRain_ReturnsFalse()
{
    var data = new SaveData();
    data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-30).ToString("o");
    data.lastRainEffectTimeUtc = DateTime.UtcNow.AddMinutes(-15).ToString("o");
    bool triggered = PlotManager.CheckRainEvent(data,
        WeatherCondition.Rain, DateTime.UtcNow);
    Assert.IsFalse(triggered);
}

[Test]
public void ShouldTriggerRainEffect_ClearWeather_ClearsTimer()
{
    var data = new SaveData();
    data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-10).ToString("o");
    bool triggered = PlotManager.CheckRainEvent(data,
        WeatherCondition.Clear, DateTime.UtcNow);
    Assert.IsFalse(triggered);
    Assert.IsNull(data.rainStartTimeUtc);
}

[Test]
public void ShouldTriggerRainEffect_StormCountsAsRain()
{
    var data = new SaveData();
    data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-20).ToString("o");
    bool triggered = PlotManager.CheckRainEvent(data,
        WeatherCondition.Storm, DateTime.UtcNow);
    Assert.IsTrue(triggered);
}

[Test]
public void ShouldTriggerRainEffect_NewRainAfterClear_TriggersAgain()
{
    var data = new SaveData();
    // Previous rain already triggered and then stopped
    data.lastRainEffectTimeUtc = DateTime.UtcNow.AddHours(-2).ToString("o");
    data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-20).ToString("o");
    bool triggered = PlotManager.CheckRainEvent(data,
        WeatherCondition.Rain, DateTime.UtcNow);
    Assert.IsTrue(triggered); // new rain window, should trigger again
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: FAIL — `PlotManager.CheckRainEvent` does not exist

**Step 3: Add CheckRainEvent static method to PlotManager**

Add to `PlotManager`:

```csharp
public static bool CheckRainEvent(SaveData data, WeatherCondition condition, DateTime utcNow)
{
    bool isRaining = condition == WeatherCondition.Rain || condition == WeatherCondition.Storm;

    if (!isRaining)
    {
        data.rainStartTimeUtc = null;
        return false;
    }

    // First rain poll — start the timer
    if (string.IsNullOrEmpty(data.rainStartTimeUtc))
    {
        data.rainStartTimeUtc = utcNow.ToString("o");
        return false;
    }

    // Check if enough time has passed
    var rainStart = DateTime.Parse(data.rainStartTimeUtc, null,
        System.Globalization.DateTimeStyles.RoundtripKind);
    if ((utcNow - rainStart).TotalMinutes < RainTriggerMinutes)
        return false;

    // Check if we already triggered in this rain window
    if (!string.IsNullOrEmpty(data.lastRainEffectTimeUtc))
    {
        var lastEffect = DateTime.Parse(data.lastRainEffectTimeUtc, null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        if (lastEffect >= rainStart)
            return false;
    }

    data.lastRainEffectTimeUtc = utcNow.ToString("o");
    return true;
}
```

**Step 4: Run tests to verify they pass**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: PASS (all 12 tests)

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Tests/EditMode/TestRainAndWatering.cs
git commit -m "feat: add CheckRainEvent static helper for rain duration tracking"
```

---

### Task 7: Wire rain effects into OnWeatherUpdated

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

**Step 1: Update OnWeatherUpdated to call rain event detection and effects**

Replace the existing `OnWeatherUpdated` method in `PlotManager.cs`:

```csharp
private void OnWeatherUpdated(WeatherData weather)
{
    var data = SaveManager.Instance.Data;
    bool changed = false;

    // Record weather snapshots for all growing plots
    for (int i = 0; i < data.plots.Count; i++)
    {
        var plot = data.plots[i];
        if (plot.state != PlotState.Growing) continue;
        if (plot.snapshots == null) plot.snapshots = new GrowthSnapshots();
        plot.snapshots.RecordSnapshot(weather);
        changed = true;
    }

    // Check for rain event
    if (CheckRainEvent(data, weather.condition, GameTime.UtcNow))
    {
        // Fill all vases
        VaseManager.RainFillAllVases(data.vases);

        // Free any Mallums that were fetching water
        foreach (var mallum in data.mallums)
        {
            if (mallum.state == MallumState.FetchingWater)
                MallumManager.FreeMallumFromWater(mallum);
        }

        // Water all growing plots (6h cooldown, free)
        RainWaterAllPlots(data.plots, GameTime.UtcNow);

        changed = true;

        VaseManager.Instance?.OnVasesChanged?.Invoke();
        MallumManager.Instance?.OnMallumsChanged?.Invoke();
    }

    if (changed) SaveManager.Instance.Save();
}
```

Note: `VaseManager.OnVasesChanged` and `MallumManager.OnMallumsChanged` need to be accessible. Check that `OnVasesChanged` is public on `VaseManager` (it is: `public event Action OnVasesChanged`) and `OnMallumsChanged` is public on `MallumManager` (it is: `public event Action OnMallumsChanged`). However, events can only be invoked from within their declaring class. So instead, add a public method to each manager to fire the event.

Add to `VaseManager`:

```csharp
public void NotifyChanged() => OnVasesChanged?.Invoke();
```

Add to `MallumManager`:

```csharp
public void NotifyChanged() => OnMallumsChanged?.Invoke();
```

Then in `OnWeatherUpdated`, replace the direct invocations with:

```csharp
VaseManager.Instance?.NotifyChanged();
MallumManager.Instance?.NotifyChanged();
```

**Step 2: Run all tests to verify nothing is broken**

Run: `run_tests` with `mode: "EditMode"`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Scripts/Managers/VaseManager.cs Assets/Scripts/Managers/MallumManager.cs
git commit -m "feat: wire rain effects into OnWeatherUpdated — fill vases, water plots, free mallums"
```

---

### Task 8: Add integration test for full rain event flow

**Files:**
- Modify: `Assets/Tests/EditMode/TestRainAndWatering.cs`

**Step 1: Write an integration test that exercises the full static flow**

Add to `TestRainAndWatering.cs`:

```csharp
[Test]
public void FullRainEvent_FillsVasesFreeMallumsWatersPlots()
{
    var data = new SaveData();
    // Set up rain timer 20 min ago
    data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-20).ToString("o");

    // Vases: one empty, one filling
    data.vases.Add(new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Empty });
    data.vases.Add(new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Filling,
        fillStartTimeUtc = DateTime.UtcNow.ToString("o") });

    // Mallum fetching water for vase 1
    data.mallums.Add(new MallumSave { state = MallumState.FetchingWater, assignedVaseIndex = 1 });
    data.mallums.Add(new MallumSave { state = MallumState.Idle });

    // Plots: one growable, one on cooldown
    data.plots.Add(new PlotSave { state = PlotState.Growing, waterCount = 0 });
    data.plots.Add(new PlotSave { state = PlotState.Growing, waterCount = 1,
        lastWateredUtc = DateTime.UtcNow.AddHours(-1).ToString("o") }); // on cooldown

    // Trigger rain event
    var now = DateTime.UtcNow;
    bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Rain, now);
    Assert.IsTrue(triggered);

    VaseManager.RainFillAllVases(data.vases);
    foreach (var m in data.mallums)
        if (m.state == MallumState.FetchingWater)
            MallumManager.FreeMallumFromWater(m);
    int watered = PlotManager.RainWaterAllPlots(data.plots, now);

    // Verify vases
    Assert.AreEqual(VaseState.Full, data.vases[0].state);
    Assert.AreEqual(5, data.vases[0].currentWater);
    Assert.AreEqual(VaseState.Full, data.vases[1].state);

    // Verify mallum freed
    Assert.AreEqual(MallumState.Idle, data.mallums[0].state);
    Assert.AreEqual(-1, data.mallums[0].assignedVaseIndex);

    // Verify plots
    Assert.AreEqual(1, watered); // only first plot (second on cooldown)
    Assert.AreEqual(1, data.plots[0].waterCount);
    Assert.AreEqual(1, data.plots[1].waterCount); // unchanged
}
```

**Step 2: Run tests to verify they pass**

Run: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestRainAndWatering"]`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add Assets/Tests/EditMode/TestRainAndWatering.cs
git commit -m "test: add integration test for full rain event flow"
```
