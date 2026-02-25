# Greenhouse Plant Deterioration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Greenhouse plants degrade in quality over real time, producing less AuraDust and sell value as they age, until they wither and must be manually trashed.

**Architecture:** A `tierStartTime` UTC timestamp stored per plant drives decay. `GreenhouseManager.Update()` compares elapsed time against per-tier step thresholds and downgrades quality, eventually setting `isWithered = true`. `GreenhouseUI` subscribes to `OnGreenhouseChanged` and renders a decay progress bar + withered state per slot.

**Tech Stack:** Unity 6, C# NUnit EditMode tests, UI Toolkit (UXML/USS), `GameTime.UtcNow` for time, `JsonUtility` save system.

---

## Decay Schedule Reference

Step thresholds (time spent at each tier before downgrading):

| Tier | Step duration | Total to wither |
|---|---|---|
| S | 360 min (6 h) | 720 min (12 h) |
| A | 240 min (4 h) | 360 min (6 h) |
| B | 60 min (1 h) | 120 min (2 h) |
| C | 40 min | 60 min (1 h) |
| D | 20 min | 20 min |

---

### Task 1: Add decay fields to data model

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs` — `GreenhousePlantSave` class (line 57)
- Modify: `Assets/Scripts/Managers/GreenhouseManager.cs` — `GreenhousePlant` class (line 164)

**Step 1: Add fields to `GreenhousePlantSave`**

In `SaveData.cs`, update `GreenhousePlantSave` to:
```csharp
[Serializable]
public class GreenhousePlantSave
{
    public string seedName;
    public string variantName;
    public string harvestTimeUtc;
    public QualityTier qualityTier;
    public string tierStartTimeUtc;  // new: when current tier began decaying
    public bool isWithered;          // new: true once plant has passed below D
}
```

**Step 2: Add fields to `GreenhousePlant`**

In `GreenhouseManager.cs`, update `GreenhousePlant` to:
```csharp
public class GreenhousePlant
{
    public string seedName;
    public string variantName;
    public Rarity rarity;
    public QualityTier qualityTier;
    public Color primaryColor;
    public DateTime harvestTime;
    public DateTime tierStartTime;  // new
    public bool isWithered;         // new
}
```

**Step 3: Commit**
```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Managers/GreenhouseManager.cs
git commit -m "feat: add decay fields to GreenhousePlant data model"
```

---

### Task 2: Add decay schedule helpers + tests

**Files:**
- Modify: `Assets/Scripts/Managers/GreenhouseManager.cs` — add two public static methods after `GetTotalDustPerSecond()`
- Create: `Assets/Tests/EditMode/TestGreenhouseDecay.cs`

**Step 1: Write failing tests**

Create `Assets/Tests/EditMode/TestGreenhouseDecay.cs`:
```csharp
using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestGreenhouseDecay
    {
        [Test] public void GetStepMinutes_D_Returns20() =>
            Assert.AreEqual(20f, GreenhouseManager.GetStepMinutes(QualityTier.D));
        [Test] public void GetStepMinutes_C_Returns40() =>
            Assert.AreEqual(40f, GreenhouseManager.GetStepMinutes(QualityTier.C));
        [Test] public void GetStepMinutes_B_Returns60() =>
            Assert.AreEqual(60f, GreenhouseManager.GetStepMinutes(QualityTier.B));
        [Test] public void GetStepMinutes_A_Returns240() =>
            Assert.AreEqual(240f, GreenhouseManager.GetStepMinutes(QualityTier.A));
        [Test] public void GetStepMinutes_S_Returns360() =>
            Assert.AreEqual(360f, GreenhouseManager.GetStepMinutes(QualityTier.S));

        [Test]
        public void ComputeDecayProgress_HalfwayThroughS_Returns0Point5()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddMinutes(-180); // 180 of 360 min elapsed
            float progress = GreenhouseManager.ComputeDecayProgress(start, QualityTier.S, now);
            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        [Test]
        public void ComputeDecayProgress_AtStart_Returns0()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            float progress = GreenhouseManager.ComputeDecayProgress(now, QualityTier.D, now);
            Assert.AreEqual(0f, progress, 0.001f);
        }

        [Test]
        public void ComputeDecayProgress_PastThreshold_ReturnsAbove1()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddMinutes(-30); // 30 of 20 min elapsed → >1
            float progress = GreenhouseManager.ComputeDecayProgress(start, QualityTier.D, now);
            Assert.Greater(progress, 1f);
        }
    }
}
```

**Step 2: Run tests — expect compile failure (methods don't exist yet)**

In Unity: Window > General > Test Runner > EditMode > Run All. Expected: compile errors referencing `GetStepMinutes` and `ComputeDecayProgress`.

**Step 3: Add static helpers to `GreenhouseManager`**

After the `GetTotalDustPerSecond()` method, add:
```csharp
public static float GetStepMinutes(QualityTier tier) => tier switch
{
    QualityTier.S => 360f,
    QualityTier.A => 240f,
    QualityTier.B => 60f,
    QualityTier.C => 40f,
    QualityTier.D => 20f,
    _ => 20f
};

public static float ComputeDecayProgress(DateTime tierStartTime, QualityTier tier, DateTime now)
{
    float elapsedMinutes = (float)(now - tierStartTime).TotalMinutes;
    return elapsedMinutes / GetStepMinutes(tier);
}
```

**Step 4: Run tests — expect all 8 to pass**

**Step 5: Commit**
```bash
git add Assets/Scripts/Managers/GreenhouseManager.cs Assets/Tests/EditMode/TestGreenhouseDecay.cs
git commit -m "feat: add decay schedule helpers with tests"
```

---

### Task 3: Wire decay into GreenhouseManager lifecycle

**Files:**
- Modify: `Assets/Scripts/Managers/GreenhouseManager.cs`

Touch these methods in order:

**Step 1: Update `AddPlant()` to initialize `tierStartTime`**

In both `AddPlant` overloads, set `tierStartTime = GameTime.UtcNow` when constructing a new `GreenhousePlant`:
```csharp
Plants.Add(new GreenhousePlant
{
    seedName = seed.seedName,
    variantName = variant.variantName,
    rarity = variant.rarity,
    qualityTier = tier,
    primaryColor = variant.primaryColor,
    harvestTime = GameTime.UtcNow,
    tierStartTime = GameTime.UtcNow,  // add this
    isWithered = false                 // add this
});
```

**Step 2: Update `GetTotalDustPerSecond()` to skip withered plants**

Change the foreach loop body from:
```csharp
total += config.GetDustPerSecondForPlant(p.rarity, p.qualityTier);
```
to:
```csharp
if (!p.isWithered)
    total += config.GetDustPerSecondForPlant(p.rarity, p.qualityTier);
```

**Step 3: Add `TrashPlant(int index)`**

After `SellPlant`, add:
```csharp
public void TrashPlant(int index)
{
    if (index < 0 || index >= Plants.Count) return;
    Plants.RemoveAt(index);
    SaveGreenhouse();
    OnGreenhouseChanged?.Invoke();
}
```

**Step 4: Add `GetDecayProgress(int index)`**

After `TrashPlant`, add:
```csharp
public float GetDecayProgress(int index)
{
    if (index < 0 || index >= Plants.Count) return 0f;
    var p = Plants[index];
    if (p.isWithered) return 1f;
    return Mathf.Clamp01(ComputeDecayProgress(p.tierStartTime, p.qualityTier, GameTime.UtcNow));
}
```

**Step 5: Add decay tick to `Update()`**

Add a second loop after the dust accumulation block:
```csharp
for (int i = 0; i < Plants.Count; i++)
{
    var p = Plants[i];
    if (p.isWithered) continue;

    float progress = ComputeDecayProgress(p.tierStartTime, p.qualityTier, GameTime.UtcNow);
    if (progress < 1f) continue;

    // Advance to next state
    if (p.qualityTier == QualityTier.D)
    {
        p.isWithered = true;
    }
    else
    {
        p.qualityTier = p.qualityTier - 1; // enum order: S=4 A=3 B=2 C=1 D=0
        p.tierStartTime = GameTime.UtcNow;
    }

    SaveGreenhouse();
    OnGreenhouseChanged?.Invoke();
}
```

Note: `QualityTier` enum values are `D=0, C=1, B=2, A=3, S=4`. Subtracting 1 steps down correctly (S→A→B→C→D).

**Step 6: Update `DebugAdvanceTime()` to also advance decay**

Replace the current body with:
```csharp
public void DebugAdvanceTime(float hours)
{
    if (Plants.Count == 0) return;
    int totalDust = Mathf.RoundToInt(GetTotalDustPerSecond() * hours * 3600f);
    if (totalDust > 0)
        CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);

    // Also advance decay timestamps
    foreach (var p in Plants)
    {
        if (!p.isWithered)
            p.tierStartTime = p.tierStartTime.AddHours(-hours);
    }
}
```

**Step 7: Update `SaveGreenhouse()` to write new fields**

In the `GreenhousePlantSave` construction block inside `SaveGreenhouse()`, add:
```csharp
save.greenhousePlants.Add(new GreenhousePlantSave
{
    seedName = p.seedName,
    variantName = p.variantName,
    harvestTimeUtc = p.harvestTime.ToString("O"),
    qualityTier = p.qualityTier,
    tierStartTimeUtc = p.tierStartTime.ToString("O"),  // new
    isWithered = p.isWithered                           // new
});
```

**Step 8: Update `RestoreFromSave()` to read new fields with defaults**

After setting `harvestTime` in the `Plants.Add(new GreenhousePlant { ... })` block, add:
```csharp
tierStartTime = string.IsNullOrEmpty(ps.tierStartTimeUtc)
    ? GameTime.UtcNow
    : DateTime.Parse(ps.tierStartTimeUtc).ToUniversalTime(),
isWithered = ps.isWithered
```

**Step 9: Run all EditMode tests — all 8 should still pass**

**Step 10: Commit**
```bash
git add Assets/Scripts/Managers/GreenhouseManager.cs
git commit -m "feat: wire decay tick, TrashPlant, and GetDecayProgress into GreenhouseManager"
```

---

### Task 4: Add decay bar to PlantSlot UXML template

**Files:**
- Modify: `Assets/Resources/UI/Templates/PlantSlot.uxml`

**Step 1: Add decay bar elements**

Replace the current content with:
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="grid-item plant-slot">
        <ui:VisualElement class="plant-swatch" />
        <ui:Label class="plant-name" />
        <ui:VisualElement class="plant-decay-bar-bg">
            <ui:VisualElement class="plant-decay-bar-fill" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 2: Commit**
```bash
git add Assets/Resources/UI/Templates/PlantSlot.uxml
git commit -m "feat: add decay bar elements to PlantSlot template"
```

---

### Task 5: Add decay bar and withered styles to Greenhouse.uss

**Files:**
- Modify: `Assets/UI/Styles/Greenhouse.uss`

**Step 1: Append new rules at end of file**

```css
/* Decay bar */
#greenhouse-page .plant-decay-bar-bg {
    width: 80%;
    height: 8px;
    background-color: rgba(255, 255, 255, 0.1);
    border-radius: 4px;
    margin-top: var(--spacing-xs);
    overflow: hidden;
}

#greenhouse-page .plant-decay-bar-fill {
    height: 100%;
    width: 0%;
    border-radius: 4px;
    background-color: rgb(80, 220, 120);
    transition-property: width;
    transition-duration: 0.3s;
}

#greenhouse-page .plant-decay-bar-fill--warning {
    background-color: rgb(255, 180, 50);
}

#greenhouse-page .plant-decay-bar-fill--critical {
    background-color: rgb(255, 70, 60);
}

/* Withered state */
#greenhouse-page .plant-slot--withered .plant-swatch {
    background-color: rgba(60, 50, 40, 0.6) !important;
}

#greenhouse-page .plant-slot--withered .plant-name {
    color: rgb(140, 110, 90);
}

#greenhouse-page .plant-slot--withered .plant-decay-bar-bg {
    display: none;
}
```

**Step 2: Commit**
```bash
git add Assets/UI/Styles/Greenhouse.uss
git commit -m "feat: add decay bar and withered slot styles"
```

---

### Task 6: Wire decay display and Trash action in GreenhouseUI

**Files:**
- Modify: `Assets/Scripts/UI/GreenhouseUI.cs`

**Step 1: Subscribe to `OnGreenhouseChanged` in `Initialize()`**

At the end of `Initialize()`, after wiring `sellButton.clicked`, add:
```csharp
GreenhouseManager.Instance.OnGreenhouseChanged += RefreshDisplay;
```

**Step 2: Update `RefreshDisplay()` to set decay bar per slot**

Inside the `for (int i = 0; i < gm.Plants.Count; i++)` loop, after setting `nameLabel.text` and `swatch` color, add:

```csharp
var decayBarFill = slot.Q<VisualElement>(className: "plant-decay-bar-fill");
if (decayBarFill != null)
{
    float progress = gm.GetDecayProgress(i);
    decayBarFill.style.width = Length.Percent(progress * 100f);

    decayBarFill.RemoveFromClassList("plant-decay-bar-fill--warning");
    decayBarFill.RemoveFromClassList("plant-decay-bar-fill--critical");

    var p = gm.Plants[i];
    if (p.qualityTier == QualityTier.D)
        decayBarFill.AddToClassList("plant-decay-bar-fill--critical");
    else if (p.qualityTier == QualityTier.C)
        decayBarFill.AddToClassList("plant-decay-bar-fill--warning");
}

if (gm.Plants[i].isWithered && slotRoot != null)
    slotRoot.AddToClassList("plant-slot--withered");
```

Also update `nameLabel.text` to show "Withered" for dead plants:
```csharp
if (nameLabel != null)
    nameLabel.text = gm.Plants[i].isWithered ? "Withered" : plant.variantName;
```

**Step 3: Update `UpdateSellBar()` to handle withered plants**

Replace the button-wiring section at the end of `UpdateSellBar()`:
```csharp
if (plant.isWithered)
{
    if (sellNameLabel != null) sellNameLabel.text = plant.variantName + " · Withered";
    if (sellDustLabel != null) sellDustLabel.text = "No value remaining";
    if (sellButton != null) sellButton.text = "Trash";
    if (sellBar != null) sellBar.style.display = DisplayStyle.Flex;
}
else
{
    if (sellNameLabel != null) sellNameLabel.text = $"{plant.variantName} · {qualityLabel}";
    if (sellDustLabel != null) sellDustLabel.text = $"+{dustRate:F1} Dust/hr";
    if (sellButton != null) sellButton.text = $"Sell for {sellValue} Dew";
    if (sellBar != null) sellBar.style.display = DisplayStyle.Flex;
}
```

**Step 4: Update `OnSell()` to dispatch to `TrashPlant` when withered**

Replace `OnSell()` with:
```csharp
private void OnSell()
{
    if (selectedIndex < 0) return;
    var plant = GreenhouseManager.Instance.Plants[selectedIndex];
    if (plant.isWithered)
        GreenhouseManager.Instance.TrashPlant(selectedIndex);
    else
        GreenhouseManager.Instance.SellPlant(selectedIndex);
    RefreshDisplay();
}
```

**Step 5: Run all EditMode tests — all 8 should still pass**

**Step 6: Commit**
```bash
git add Assets/Scripts/UI/GreenhouseUI.cs
git commit -m "feat: render decay progress bar and withered state in GreenhouseUI"
```

---

## Manual Verification Checklist

After all tasks:
1. Place a plant in the greenhouse — confirm decay bar appears at 0%
2. Use `DebugAdvanceTime(1)` via the debug panel — confirm bar fills and/or tier drops
3. Advance past D tier — confirm slot shows "Withered" with grey swatch
4. Tap a withered slot — confirm sell bar shows "Trash" with no Dew value
5. Tap Trash — confirm slot clears and total count decreases
6. Restart app — confirm withered state and tier persist across sessions
7. Place fresh S plant, advance 12+ hours — confirm full decay path S→A→B→C→D→Withered
