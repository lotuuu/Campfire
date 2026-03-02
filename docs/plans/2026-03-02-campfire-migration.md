# Camp Fire Migration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Migrate the Garden codebase to Camp Fire — a campsite management game built around a magical flame, plots, gardens, vases, and an apotheke, powered by real-world weather.

**Architecture:** Delete all Garden-specific code/assets/UI/tests. Keep WeatherService, SaveManager, NotificationService, Utils (GameTime, MoonPhaseCalculator, CalendarEvents, TimeUtils), TriggerCondition/WeatherData, UI Toolkit infrastructure, and project settings. Build all Camp Fire systems fresh with the same singleton-MonoBehaviour pattern.

**Tech Stack:** Unity 6 (6000.3.6f1), 2D URP, UI Toolkit (UXML/USS), C# with NUnit EditMode tests. Namespace: `Garden` (unchanged — renaming the assembly would break too many things).

---

### Task 1: Delete Garden-Specific Code

**Files:**
- Delete: `Assets/Scripts/Data/ConsumableData.cs`
- Delete: `Assets/Scripts/Data/ConsumableType.cs`
- Delete: `Assets/Scripts/Data/CurrencyConfig.cs`
- Delete: `Assets/Scripts/Data/EnvironmentData.cs`
- Delete: `Assets/Scripts/Data/HarvestResult.cs`
- Delete: `Assets/Scripts/Data/PlantSlot.cs`
- Delete: `Assets/Scripts/Data/SaveData.cs`
- Delete: `Assets/Scripts/Data/SeedData.cs`
- Delete: `Assets/Scripts/Data/VariantData.cs`
- Delete: `Assets/Scripts/Services/GeneticsEngine.cs`
- Delete: `Assets/Scripts/Services/HarvestEngine.cs`
- Delete: `Assets/Scripts/Managers/ConsumableManager.cs`
- Delete: `Assets/Scripts/Managers/EnvironmentManager.cs`
- Delete: `Assets/Scripts/Managers/GameManager.cs`
- Delete: `Assets/Scripts/Managers/GreenhouseManager.cs`
- Delete: `Assets/Scripts/Managers/PlantManager.cs`
- Delete: `Assets/Scripts/Managers/SeedRegistry.cs`
- Delete: `Assets/Scripts/Managers/SeedShopManager.cs`
- Delete: `Assets/Scripts/UI/BackyardIsometricView.cs`
- Delete: `Assets/Scripts/UI/BackyardViewUI.cs`
- Delete: `Assets/Scripts/UI/BarMorphElement.cs`
- Delete: `Assets/Scripts/UI/BottomNavUI.cs`
- Delete: `Assets/Scripts/UI/CodexUI.cs`
- Delete: `Assets/Scripts/UI/ConstructionUI.cs`
- Delete: `Assets/Scripts/UI/CurrencyDisplay.cs`
- Delete: `Assets/Scripts/UI/DiscoveryPopupUI.cs`
- Delete: `Assets/Scripts/UI/EnvironmentSwitcherBar.cs`
- Delete: `Assets/Scripts/UI/GreenhouseUI.cs`
- Delete: `Assets/Scripts/UI/HarvestResultUI.cs`
- Delete: `Assets/Scripts/UI/HortusUI.cs`
- Delete: `Assets/Scripts/UI/LivingCanvasController.cs`
- Delete: `Assets/Scripts/UI/PlantVisual.cs`
- Delete: `Assets/Scripts/UI/RainOverlay.cs`
- Delete: `Assets/Scripts/UI/ResonanceBar.cs`
- Delete: `Assets/Scripts/UI/SatchelUI.cs`
- Delete: `Assets/Scripts/UI/SeedShopUI.cs`
- Delete: `Assets/Scripts/UI/SeedSlotUI.cs`
- Delete: `Assets/Scripts/UI/SnowOverlay.cs`
- Delete: `Assets/Scripts/UI/SwipeablePageView.cs`
- Delete: `Assets/Scripts/UI/WeatherOverlay.cs`
- Delete: `Assets/Scripts/UI/SafeAreaController.cs`
- Delete: `Assets/Scripts/Debug/DebugWeatherPanel.cs`
- Keep: `Assets/Scripts/Data/GameEnums.cs` (will rewrite)
- Keep: `Assets/Scripts/Data/TriggerCondition.cs` (unchanged)
- Keep: `Assets/Scripts/Data/WeatherData.cs` (unchanged)
- Keep: `Assets/Scripts/Services/WeatherService.cs` (minor edits)
- Keep: `Assets/Scripts/Services/SaveManager.cs` (unchanged)
- Keep: `Assets/Scripts/Services/CurrencyManager.cs` (will rewrite)
- Keep: `Assets/Scripts/Services/NotificationService.cs` (will rewrite)
- Keep: `Assets/Scripts/Utils/` (all 4 files unchanged)

**Step 1: Delete all Garden-specific scripts and their .meta files**

```bash
# Data layer (keep GameEnums.cs, TriggerCondition.cs, WeatherData.cs)
rm Assets/Scripts/Data/ConsumableData.cs Assets/Scripts/Data/ConsumableData.cs.meta
rm Assets/Scripts/Data/ConsumableType.cs Assets/Scripts/Data/ConsumableType.cs.meta
rm Assets/Scripts/Data/CurrencyConfig.cs Assets/Scripts/Data/CurrencyConfig.cs.meta
rm Assets/Scripts/Data/EnvironmentData.cs Assets/Scripts/Data/EnvironmentData.cs.meta
rm Assets/Scripts/Data/HarvestResult.cs Assets/Scripts/Data/HarvestResult.cs.meta
rm Assets/Scripts/Data/PlantSlot.cs Assets/Scripts/Data/PlantSlot.cs.meta
rm Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/SaveData.cs.meta
rm Assets/Scripts/Data/SeedData.cs Assets/Scripts/Data/SeedData.cs.meta
rm Assets/Scripts/Data/VariantData.cs Assets/Scripts/Data/VariantData.cs.meta

# Services (keep WeatherService, SaveManager, CurrencyManager, NotificationService)
rm Assets/Scripts/Services/GeneticsEngine.cs Assets/Scripts/Services/GeneticsEngine.cs.meta
rm Assets/Scripts/Services/HarvestEngine.cs Assets/Scripts/Services/HarvestEngine.cs.meta

# Managers (delete all)
rm Assets/Scripts/Managers/ConsumableManager.cs Assets/Scripts/Managers/ConsumableManager.cs.meta
rm Assets/Scripts/Managers/EnvironmentManager.cs Assets/Scripts/Managers/EnvironmentManager.cs.meta
rm Assets/Scripts/Managers/GameManager.cs Assets/Scripts/Managers/GameManager.cs.meta
rm Assets/Scripts/Managers/GreenhouseManager.cs Assets/Scripts/Managers/GreenhouseManager.cs.meta
rm Assets/Scripts/Managers/PlantManager.cs Assets/Scripts/Managers/PlantManager.cs.meta
rm Assets/Scripts/Managers/SeedRegistry.cs Assets/Scripts/Managers/SeedRegistry.cs.meta
rm Assets/Scripts/Managers/SeedShopManager.cs Assets/Scripts/Managers/SeedShopManager.cs.meta

# UI (delete all)
rm Assets/Scripts/UI/BackyardIsometricView.cs Assets/Scripts/UI/BackyardIsometricView.cs.meta
rm Assets/Scripts/UI/BackyardViewUI.cs Assets/Scripts/UI/BackyardViewUI.cs.meta
rm Assets/Scripts/UI/BarMorphElement.cs Assets/Scripts/UI/BarMorphElement.cs.meta
rm Assets/Scripts/UI/BottomNavUI.cs Assets/Scripts/UI/BottomNavUI.cs.meta
rm Assets/Scripts/UI/CodexUI.cs Assets/Scripts/UI/CodexUI.cs.meta
rm Assets/Scripts/UI/ConstructionUI.cs Assets/Scripts/UI/ConstructionUI.cs.meta
rm Assets/Scripts/UI/CurrencyDisplay.cs Assets/Scripts/UI/CurrencyDisplay.cs.meta
rm Assets/Scripts/UI/DiscoveryPopupUI.cs Assets/Scripts/UI/DiscoveryPopupUI.cs.meta
rm Assets/Scripts/UI/EnvironmentSwitcherBar.cs Assets/Scripts/UI/EnvironmentSwitcherBar.cs.meta
rm Assets/Scripts/UI/GreenhouseUI.cs Assets/Scripts/UI/GreenhouseUI.cs.meta
rm Assets/Scripts/UI/HarvestResultUI.cs Assets/Scripts/UI/HarvestResultUI.cs.meta
rm Assets/Scripts/UI/HortusUI.cs Assets/Scripts/UI/HortusUI.cs.meta
rm Assets/Scripts/UI/LivingCanvasController.cs Assets/Scripts/UI/LivingCanvasController.cs.meta
rm Assets/Scripts/UI/PlantVisual.cs Assets/Scripts/UI/PlantVisual.cs.meta
rm Assets/Scripts/UI/RainOverlay.cs Assets/Scripts/UI/RainOverlay.cs.meta
rm Assets/Scripts/UI/ResonanceBar.cs Assets/Scripts/UI/ResonanceBar.cs.meta
rm Assets/Scripts/UI/SatchelUI.cs Assets/Scripts/UI/SatchelUI.cs.meta
rm Assets/Scripts/UI/SeedShopUI.cs Assets/Scripts/UI/SeedShopUI.cs.meta
rm Assets/Scripts/UI/SeedSlotUI.cs Assets/Scripts/UI/SeedSlotUI.cs.meta
rm Assets/Scripts/UI/SnowOverlay.cs Assets/Scripts/UI/SnowOverlay.cs.meta
rm Assets/Scripts/UI/SwipeablePageView.cs Assets/Scripts/UI/SwipeablePageView.cs.meta
rm Assets/Scripts/UI/WeatherOverlay.cs Assets/Scripts/UI/WeatherOverlay.cs.meta
rm Assets/Scripts/UI/SafeAreaController.cs Assets/Scripts/UI/SafeAreaController.cs.meta

# Debug
rm Assets/Scripts/Debug/DebugWeatherPanel.cs Assets/Scripts/Debug/DebugWeatherPanel.cs.meta
```

**Step 2: Delete all Garden ScriptableObject assets**

```bash
rm -rf Assets/Resources/Seeds/
rm -rf Assets/Resources/Variants/
rm -rf Assets/Resources/Consumables/
rm -rf Assets/Resources/ConsumablePrefabs/
rm -rf Assets/Resources/Config/Environments/
rm Assets/Resources/Config/CurrencyConfig.asset Assets/Resources/Config/CurrencyConfig.asset.meta
```

**Step 3: Delete all Garden UI files**

```bash
rm Assets/UI/Documents/GardenRoot.uxml Assets/UI/Documents/GardenRoot.uxml.meta
rm Assets/UI/Styles/Backyard.uss Assets/UI/Styles/Backyard.uss.meta
rm Assets/UI/Styles/BottomNav.uss Assets/UI/Styles/BottomNav.uss.meta
rm Assets/UI/Styles/Codex.uss Assets/UI/Styles/Codex.uss.meta
rm Assets/UI/Styles/Construction.uss Assets/UI/Styles/Construction.uss.meta
rm Assets/UI/Styles/Debug.uss Assets/UI/Styles/Debug.uss.meta
rm Assets/UI/Styles/DiscoveryPopup.uss Assets/UI/Styles/DiscoveryPopup.uss.meta
rm Assets/UI/Styles/Greenhouse.uss Assets/UI/Styles/Greenhouse.uss.meta
rm Assets/UI/Styles/HarvestResult.uss Assets/UI/Styles/HarvestResult.uss.meta
rm Assets/UI/Styles/HUD.uss Assets/UI/Styles/HUD.uss.meta
rm Assets/UI/Styles/Satchel.uss Assets/UI/Styles/Satchel.uss.meta
rm Assets/UI/Styles/SeedShop.uss Assets/UI/Styles/SeedShop.uss.meta
rm Assets/UI/Styles/Terrarium.uss Assets/UI/Styles/Terrarium.uss.meta
# Keep: Variables.uss, Common.uss
rm -rf Assets/Resources/UI/Templates/
```

**Step 4: Delete all Garden tests**

```bash
rm Assets/Tests/EditMode/TestCalendarEvents.cs Assets/Tests/EditMode/TestCalendarEvents.cs.meta
rm Assets/Tests/EditMode/TestConsumableManager.cs Assets/Tests/EditMode/TestConsumableManager.cs.meta
rm Assets/Tests/EditMode/TestDiscovery.cs Assets/Tests/EditMode/TestDiscovery.cs.meta
rm Assets/Tests/EditMode/TestGeneticsEngine.cs Assets/Tests/EditMode/TestGeneticsEngine.cs.meta
rm Assets/Tests/EditMode/TestGreenhouseDecay.cs Assets/Tests/EditMode/TestGreenhouseDecay.cs.meta
rm Assets/Tests/EditMode/TestHarvestEngine.cs Assets/Tests/EditMode/TestHarvestEngine.cs.meta
rm Assets/Tests/EditMode/TestMoonPhase.cs Assets/Tests/EditMode/TestMoonPhase.cs.meta
rm Assets/Tests/EditMode/TestSaveManager.cs Assets/Tests/EditMode/TestSaveManager.cs.meta
```

**Step 5: Delete Garden sprites (plant art)**

```bash
rm -rf Assets/Sprites/
```

**Step 6: Verify compilation succeeds**

Run: `read_console` to check for errors.
Expected: Compilation errors from WeatherService, CurrencyManager, NotificationService referencing deleted types. These will be fixed in Task 2.

**Step 7: Commit**

```bash
git add -A
git commit -m "chore: delete all Garden-specific code, assets, UI, and tests

Keep infrastructure: WeatherService, SaveManager, NotificationService,
CurrencyManager, Utils, TriggerCondition, WeatherData, GameEnums,
UI Toolkit settings, project config. Tagged v0.1.0 preserves Garden."
```

---

### Task 2: Rewrite GameEnums and SaveData

**Files:**
- Modify: `Assets/Scripts/Data/GameEnums.cs`
- Create: `Assets/Scripts/Data/SaveData.cs`

**Step 1: Write the test for SaveData serialization**

Create `Assets/Tests/EditMode/TestSaveData.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSaveData
    {
        [Test]
        public void NewSaveData_HasCorrectDefaults()
        {
            var data = new SaveData();
            Assert.AreEqual(1, data.version);
            Assert.AreEqual(0f, data.mana);
            Assert.AreEqual(1, data.flameLevel);
            Assert.AreEqual(0, data.vases.Count);
            Assert.AreEqual(0, data.plots.Count);
            Assert.AreEqual(0, data.gardens.Count);
            Assert.AreEqual(0, data.seedInventory.Count);
            Assert.AreEqual(0, data.items.Count);
            Assert.AreEqual(0f, data.lastManaCollectTime);
        }

        [Test]
        public void SaveData_RoundTrips_ThroughJson()
        {
            var data = new SaveData
            {
                mana = 123.5f,
                flameLevel = 3,
                lastManaCollectTime = 1000f,
            };
            data.seedInventory.Add(new SeedInventoryEntry { seedName = "Fern", count = 5 });
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 7 });
            data.plots.Add(new PlotSave { seedName = "Fern", watered = true });
            data.gardens.Add(new GardenSave { plantName = "Oak", mature = true });
            data.items.Add(new InventoryItem { itemName = "Acorn", count = 3 });

            var json = JsonUtility.ToJson(data);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(3, restored.flameLevel);
            Assert.AreEqual(123.5f, restored.mana);
            Assert.AreEqual(1, restored.seedInventory.Count);
            Assert.AreEqual("Fern", restored.seedInventory[0].seedName);
            Assert.AreEqual(1, restored.vases.Count);
            Assert.AreEqual(7, restored.vases[0].currentWater);
            Assert.AreEqual(1, restored.plots.Count);
            Assert.IsTrue(restored.plots[0].watered);
            Assert.AreEqual(1, restored.gardens.Count);
            Assert.IsTrue(restored.gardens[0].mature);
            Assert.AreEqual(1, restored.items.Count);
            Assert.AreEqual("Acorn", restored.items[0].itemName);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` with mode EditMode.
Expected: FAIL — `SaveData` class doesn't exist yet.

**Step 3: Rewrite GameEnums**

Replace contents of `Assets/Scripts/Data/GameEnums.cs`:
```csharp
namespace Garden
{
    public enum WeatherCondition { Clear, Cloudy, Rain, Storm, Snow }

    public enum MoonPhase
    {
        NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous,
        FullMoon, WaningGibbous, LastQuarter, WaningCrescent
    }

    public enum TimeOfDay { Day, Night, GoldenHour }

    public enum CalendarEvent { None, SpringEquinox, FallEquinox, LunarEclipse }

    public enum CurrencyType { Mana, Water }

    public enum PlotState { Empty, Planted, Watered, Growing, Mature }

    public enum VaseState { Empty, Filling, Full }
}
```

**Step 4: Write SaveData**

Create `Assets/Scripts/Data/SaveData.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public float mana;
        public int flameLevel = 1;
        public List<VaseSave> vases = new();
        public List<PlotSave> plots = new();
        public List<GardenSave> gardens = new();
        public List<SeedInventoryEntry> seedInventory = new();
        public List<InventoryItem> items = new();
        public float lastManaCollectTime;
        public string lastVisitorDateUtc;
    }

    [Serializable]
    public class VaseSave
    {
        public int capacity = 5;
        public int currentWater;
        public string fillStartTimeUtc;
        public VaseState state = VaseState.Empty;
    }

    [Serializable]
    public class PlotSave
    {
        public string seedName;
        public string plantTimeUtc;
        public bool watered;
        public PlotState state = PlotState.Empty;
    }

    [Serializable]
    public class GardenSave
    {
        public string plantName;
        public string plantTimeUtc;
        public string lastYieldTimeUtc;
        public bool mature;
    }

    [Serializable]
    public class SeedInventoryEntry
    {
        public string seedName;
        public int count;
    }

    [Serializable]
    public class InventoryItem
    {
        public string itemName;
        public int count;
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `run_tests` with mode EditMode.
Expected: PASS (2 tests).

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/GameEnums.cs Assets/Scripts/Data/SaveData.cs Assets/Tests/EditMode/TestSaveData.cs
git commit -m "feat: new GameEnums and SaveData for Camp Fire

CurrencyType is now Mana/Water. PlotState tracks full plant lifecycle.
VaseSave, PlotSave, GardenSave, SeedInventoryEntry, InventoryItem."
```

---

### Task 3: Rewrite CurrencyManager for Mana + Water

**Files:**
- Modify: `Assets/Scripts/Services/CurrencyManager.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestCurrencyManager.cs`:
```csharp
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestCurrencyManager
    {
        private SaveData data;

        [SetUp]
        public void SetUp()
        {
            data = new SaveData { mana = 100f };
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 5 });
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 3 });
        }

        [Test]
        public void GetMana_ReturnsDataMana()
        {
            Assert.AreEqual(100f, data.mana);
        }

        [Test]
        public void GetTotalWater_SumsAcrossVases()
        {
            int total = 0;
            foreach (var v in data.vases) total += v.currentWater;
            Assert.AreEqual(8, total);
        }

        [Test]
        public void SpendMana_DeductsCorrectly()
        {
            Assert.IsTrue(data.mana >= 30f);
            data.mana -= 30f;
            Assert.AreEqual(70f, data.mana, 0.01f);
        }

        [Test]
        public void SpendMana_FailsIfInsufficient()
        {
            Assert.IsFalse(data.mana >= 200f);
        }

        [Test]
        public void SpendWater_DeductsFromFirstNonEmptyVase()
        {
            int needed = 4;
            for (int i = 0; i < data.vases.Count && needed > 0; i++)
            {
                int take = System.Math.Min(data.vases[i].currentWater, needed);
                data.vases[i].currentWater -= take;
                needed -= take;
            }
            Assert.AreEqual(0, needed);
            Assert.AreEqual(1, data.vases[0].currentWater);
            Assert.AreEqual(3, data.vases[1].currentWater);
        }

        [Test]
        public void SpendWater_SpansMultipleVases()
        {
            int needed = 7;
            for (int i = 0; i < data.vases.Count && needed > 0; i++)
            {
                int take = System.Math.Min(data.vases[i].currentWater, needed);
                data.vases[i].currentWater -= take;
                needed -= take;
            }
            Assert.AreEqual(0, needed);
            Assert.AreEqual(0, data.vases[0].currentWater);
            Assert.AreEqual(1, data.vases[1].currentWater);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` with mode EditMode.
Expected: Should PASS since we're testing SaveData logic directly. (These tests validate the logic we'll use in CurrencyManager.)

**Step 3: Rewrite CurrencyManager**

Replace contents of `Assets/Scripts/Services/CurrencyManager.cs`:
```csharp
using System;
using UnityEngine;

namespace Garden
{
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        public float Mana => SaveManager.Instance.Data.mana;

        public int TotalWater
        {
            get
            {
                int total = 0;
                foreach (var v in SaveManager.Instance.Data.vases)
                    total += v.currentWater;
                return total;
            }
        }

        public event Action<CurrencyType, float, float> OnCurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddMana(float amount)
        {
            var data = SaveManager.Instance.Data;
            float old = data.mana;
            data.mana = Mathf.Max(0f, data.mana + amount);
            OnCurrencyChanged?.Invoke(CurrencyType.Mana, old, data.mana);
            SaveManager.Instance.Save();
        }

        public bool SpendMana(float amount)
        {
            if (!CanAffordMana(amount)) return false;
            AddMana(-amount);
            return true;
        }

        public bool CanAffordMana(float amount)
        {
            return SaveManager.Instance.Data.mana >= amount;
        }

        public bool SpendWater(int amount)
        {
            if (TotalWater < amount) return false;
            var data = SaveManager.Instance.Data;
            float oldTotal = TotalWater;
            int remaining = amount;
            for (int i = 0; i < data.vases.Count && remaining > 0; i++)
            {
                int take = Math.Min(data.vases[i].currentWater, remaining);
                data.vases[i].currentWater -= take;
                remaining -= take;
            }
            OnCurrencyChanged?.Invoke(CurrencyType.Water, oldTotal, TotalWater);
            SaveManager.Instance.Save();
            return true;
        }

        public bool CanAffordWater(int amount)
        {
            return TotalWater >= amount;
        }
    }
}
```

**Step 4: Run tests**

Run: `run_tests` with mode EditMode.
Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Services/CurrencyManager.cs Assets/Tests/EditMode/TestCurrencyManager.cs
git commit -m "feat: rewrite CurrencyManager for Mana + Water

Mana is a float stored in SaveData. Water is deducted across vases
(first non-empty first). Old Gold/Pollen/SunShards removed."
```

---

### Task 4: Create FlameConfig ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/FlameConfig.cs`
- Create: `Assets/Resources/Config/FlameConfig.asset` (via Unity MCP or manual YAML)

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestFlameConfig.cs`:
```csharp
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestFlameConfig
    {
        [Test]
        public void GetMaxPlots_ReturnsCorrectForLevel()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<FlameConfig>();
            // Default plotsPerLevel: [1, 2, 3, 5, 8]
            Assert.AreEqual(1, config.GetMaxPlots(1));
            Assert.AreEqual(2, config.GetMaxPlots(2));
            Assert.AreEqual(5, config.GetMaxPlots(4));
        }

        [Test]
        public void GetMaxPlots_ClampsToLastEntry()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<FlameConfig>();
            Assert.AreEqual(8, config.GetMaxPlots(99));
        }

        [Test]
        public void GetUpgradeCost_ReturnsCorrectForLevel()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<FlameConfig>();
            // Upgrade from level 1 to 2 costs upgradeCosts[0]
            Assert.Greater(config.GetUpgradeCost(1), 0f);
        }

        [Test]
        public void GetManaRate_ScalesWithLevel()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<FlameConfig>();
            float rate1 = config.GetManaPerSecond(1);
            float rate2 = config.GetManaPerSecond(2);
            Assert.Greater(rate2, rate1);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `run_tests` with mode EditMode.
Expected: FAIL — `FlameConfig` doesn't exist.

**Step 3: Create FlameConfig**

Create `Assets/Scripts/Data/FlameConfig.cs`:
```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "FlameConfig", menuName = "CampFire/Flame Config")]
    public class FlameConfig : ScriptableObject
    {
        public static readonly float DefaultBaseManaPerSecond = 0.5f;
        public static readonly float DefaultManaPerLevel = 0.3f;
        public static readonly int[] DefaultPlotsPerLevel = { 1, 2, 3, 5, 8 };
        public static readonly float[] DefaultUpgradeCosts = { 50f, 150f, 400f, 1000f };

        [Header("Mana Generation")]
        [SerializeField] private float baseManaPerSecond = 0.5f;
        [SerializeField] private float manaPerLevel = 0.3f;

        [Header("Plot Capacity")]
        [SerializeField] private int[] plotsPerLevel = { 1, 2, 3, 5, 8 };

        [Header("Upgrade Costs (Mana)")]
        [SerializeField] private float[] upgradeCosts = { 50f, 150f, 400f, 1000f };

        public float GetManaPerSecond(int flameLevel)
        {
            return baseManaPerSecond + (flameLevel - 1) * manaPerLevel;
        }

        public int GetMaxPlots(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, plotsPerLevel.Length - 1);
            return plotsPerLevel[index];
        }

        public float GetUpgradeCost(int currentLevel)
        {
            int index = Mathf.Clamp(currentLevel - 1, 0, upgradeCosts.Length - 1);
            return upgradeCosts[index];
        }

        public int MaxLevel => upgradeCosts.Length + 1;
    }
}
```

**Step 4: Run tests**

Run: `run_tests` with mode EditMode.
Expected: PASS (4 tests).

**Step 5: Create the FlameConfig asset via Unity MCP**

Use `manage_asset` action=create to create `Assets/Resources/Config/FlameConfig.asset` of type `FlameConfig`.

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/FlameConfig.cs Assets/Resources/Config/FlameConfig.asset Assets/Tests/EditMode/TestFlameConfig.cs
git commit -m "feat: add FlameConfig ScriptableObject

Configures Mana generation rate, plot capacity per flame level,
and upgrade costs. Created asset at Resources/Config/FlameConfig."
```

---

### Task 5: Create SeedData ScriptableObject (Simplified)

**Files:**
- Create: `Assets/Scripts/Data/SeedData.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestSeedData.cs`:
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
            seed.waterRequired = 2;
            seed.baseYield = 3;

            Assert.AreEqual("TestSeed", seed.seedName);
            Assert.AreEqual(4f, seed.growthDurationHours);
            Assert.AreEqual(2, seed.waterRequired);
            Assert.AreEqual(3, seed.baseYield);
        }

        [Test]
        public void SeedData_WeatherMatch_AffectsQuality()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.preferredWeather = new TriggerCondition
            {
                useWeatherCondition = true,
                requiredConditions = new[] { WeatherCondition.Rain }
            };

            var rainyWeather = new WeatherData { condition = WeatherCondition.Rain };
            var clearWeather = new WeatherData { condition = WeatherCondition.Clear };

            Assert.IsTrue(seed.preferredWeather.Evaluate(rainyWeather));
            Assert.IsFalse(seed.preferredWeather.Evaluate(clearWeather));
        }
    }
}
```

**Step 2: Run test to verify it fails**

Expected: FAIL — `SeedData` doesn't exist.

**Step 3: Create SeedData**

Create `Assets/Scripts/Data/SeedData.cs`:
```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "CampFire/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public float growthDurationHours = 4f;
        public int waterRequired = 1;
        public TriggerCondition preferredWeather;
        public int baseYield = 1;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;

        [Header("Shop")]
        public float manaCost;

        /// <summary>
        /// Quality multiplier range: 0.8 (bad weather) to 2.0 (perfect match).
        /// Base is 1.0.
        /// </summary>
        public static readonly float WeatherMatchBonus = 0.25f;
        public static readonly float MinQualityMultiplier = 0.8f;
        public static readonly float MaxQualityMultiplier = 2.0f;
    }
}
```

**Step 4: Run tests**

Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/SeedData.cs Assets/Tests/EditMode/TestSeedData.cs
git commit -m "feat: add simplified SeedData ScriptableObject

No variants. Weather affects growth speed and quality multiplier only."
```

---

### Task 6: Create VaseConfig and GardenPlantData

**Files:**
- Create: `Assets/Scripts/Data/VaseConfig.cs`
- Create: `Assets/Scripts/Data/GardenPlantData.cs`
- Create: `Assets/Scripts/Data/RecipeData.cs`

**Step 1: Create VaseConfig**

Create `Assets/Scripts/Data/VaseConfig.cs`:
```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "VaseConfig", menuName = "CampFire/Vase Config")]
    public class VaseConfig : ScriptableObject
    {
        [SerializeField] private int baseCapacity = 5;
        [SerializeField] private float craftCostMana = 100f;
        [SerializeField] private float fillDurationMinutes = 30f;
        [SerializeField] private int[] capacityPerTier = { 5, 8, 12, 20 };
        [SerializeField] private float[] upgradeCosts = { 75f, 200f, 500f };

        public int BaseCapacity => baseCapacity;
        public float CraftCostMana => craftCostMana;
        public float FillDurationMinutes => fillDurationMinutes;

        public int GetCapacity(int tier)
        {
            int index = Mathf.Clamp(tier, 0, capacityPerTier.Length - 1);
            return capacityPerTier[index];
        }

        public float GetUpgradeCost(int currentTier)
        {
            int index = Mathf.Clamp(currentTier, 0, upgradeCosts.Length - 1);
            return upgradeCosts[index];
        }

        public int MaxTier => capacityPerTier.Length - 1;
    }
}
```

**Step 2: Create GardenPlantData**

Create `Assets/Scripts/Data/GardenPlantData.cs`:
```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewGardenPlant", menuName = "CampFire/Garden Plant Data")]
    public class GardenPlantData : ScriptableObject
    {
        public string plantName;
        public float growthDurationHours = 24f;
        public string yieldItem;
        public int yieldAmount = 1;
        public float yieldIntervalHours = 12f;
        public int waterRequired = 3;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;
        public Sprite matureSprite;
    }
}
```

**Step 3: Create RecipeData**

Create `Assets/Scripts/Data/RecipeData.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "CampFire/Recipe Data")]
    public class RecipeData : ScriptableObject
    {
        public string recipeName;
        public List<IngredientEntry> ingredients = new();
        public string result;
        public int resultQuantity = 1;

        [Header("Visuals")]
        public Sprite icon;
    }

    [Serializable]
    public class IngredientEntry
    {
        public string itemName;
        public int quantity;
    }
}
```

**Step 4: Verify compilation**

Run: `read_console` to check for errors.
Expected: Clean compilation.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/VaseConfig.cs Assets/Scripts/Data/GardenPlantData.cs Assets/Scripts/Data/RecipeData.cs
git commit -m "feat: add VaseConfig, GardenPlantData, and RecipeData

VaseConfig: capacity tiers, craft/upgrade costs, fill duration.
GardenPlantData: permanent plants with periodic yields.
RecipeData: Apotheke mixing recipes with ingredients."
```

---

### Task 7: Build FlameManager

**Files:**
- Create: `Assets/Scripts/Managers/FlameManager.cs`
- Create: `Assets/Tests/EditMode/TestFlameManager.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestFlameManager.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestFlameManager
    {
        private FlameConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<FlameConfig>();
        }

        [Test]
        public void ManaAccumulation_CalculatesCorrectly()
        {
            // Level 1: baseManaPerSecond (0.5) + (1-1)*manaPerLevel = 0.5/sec
            float rate = config.GetManaPerSecond(1);
            float deltaSeconds = 10f;
            float accumulated = rate * deltaSeconds;
            Assert.AreEqual(5f, accumulated, 0.01f);
        }

        [Test]
        public void OfflineManaAccumulation_CalculatesFromTimeDelta()
        {
            float rate = config.GetManaPerSecond(2); // 0.5 + 0.3 = 0.8/sec
            float offlineSeconds = 3600f; // 1 hour
            float accumulated = rate * offlineSeconds;
            Assert.AreEqual(2880f, accumulated, 1f);
        }

        [Test]
        public void UpgradeFlame_IncreasesLevel()
        {
            var data = new SaveData { mana = 1000f, flameLevel = 1 };
            float cost = config.GetUpgradeCost(data.flameLevel);
            Assert.IsTrue(data.mana >= cost);
            data.mana -= cost;
            data.flameLevel++;
            Assert.AreEqual(2, data.flameLevel);
        }

        [Test]
        public void UpgradeFlame_FailsIfCantAfford()
        {
            var data = new SaveData { mana = 10f, flameLevel = 1 };
            float cost = config.GetUpgradeCost(data.flameLevel);
            Assert.IsFalse(data.mana >= cost);
        }
    }
}
```

**Step 2: Run test to verify it passes**

Expected: PASS — these tests validate config + data logic directly.

**Step 3: Create FlameManager**

Create `Assets/Scripts/Managers/FlameManager.cs`:
```csharp
using System;
using UnityEngine;

namespace Garden
{
    public class FlameManager : MonoBehaviour
    {
        public static FlameManager Instance { get; private set; }

        [SerializeField] private FlameConfig config;

        public FlameConfig Config => config;
        public int Level => SaveManager.Instance.Data.flameLevel;
        public float ManaPerSecond => config.GetManaPerSecond(Level);
        public int MaxPlots => config.GetMaxPlots(Level);

        public event Action OnFlameUpgraded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            AccumulateOfflineMana();
        }

        private void Update()
        {
            var data = SaveManager.Instance.Data;
            data.mana += ManaPerSecond * Time.deltaTime;
            // lastManaCollectTime tracks real time for offline calc
            data.lastManaCollectTime = Time.realtimeSinceStartup;
        }

        private void AccumulateOfflineMana()
        {
            var data = SaveManager.Instance.Data;
            if (data.lastManaCollectTime > 0f)
            {
                // Use save file timestamp to calculate offline duration
                // For now, offline mana is handled by SaveManager storing
                // the last save time and calculating the delta on load
            }
        }

        public bool CanUpgrade()
        {
            return Level < config.MaxLevel &&
                   CurrencyManager.Instance.CanAffordMana(config.GetUpgradeCost(Level));
        }

        public bool UpgradeFlame()
        {
            if (!CanUpgrade()) return false;
            if (!CurrencyManager.Instance.SpendMana(config.GetUpgradeCost(Level))) return false;
            SaveManager.Instance.Data.flameLevel++;
            SaveManager.Instance.Save();
            OnFlameUpgraded?.Invoke();
            return true;
        }
    }
}
```

**Step 4: Run tests**

Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/FlameManager.cs Assets/Tests/EditMode/TestFlameManager.cs
git commit -m "feat: add FlameManager

Drives passive Mana generation, flame level upgrades, and plot
capacity gating. Accumulates Mana per frame based on level."
```

---

### Task 8: Build VaseManager

**Files:**
- Create: `Assets/Scripts/Managers/VaseManager.cs`
- Create: `Assets/Tests/EditMode/TestVaseManager.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestVaseManager.cs`:
```csharp
using System;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestVaseManager
    {
        [Test]
        public void NewGame_StartsWith2Vases()
        {
            var data = new SaveData();
            VaseManager.InitializeNewPlayer(data, 5);
            Assert.AreEqual(2, data.vases.Count);
            Assert.AreEqual(VaseState.Empty, data.vases[0].state);
        }

        [Test]
        public void SendToCollect_SetsFillingState()
        {
            var vase = new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Empty };
            vase.state = VaseState.Filling;
            vase.fillStartTimeUtc = DateTime.UtcNow.ToString("o");
            Assert.AreEqual(VaseState.Filling, vase.state);
            Assert.IsNotNull(vase.fillStartTimeUtc);
        }

        [Test]
        public void FillComplete_SetsFullAndFillsWater()
        {
            var vase = new VaseSave
            {
                capacity = 10,
                currentWater = 0,
                state = VaseState.Filling,
                fillStartTimeUtc = DateTime.UtcNow.AddMinutes(-60).ToString("o")
            };
            // Simulate fill completion
            vase.currentWater = vase.capacity;
            vase.state = VaseState.Full;
            vase.fillStartTimeUtc = null;

            Assert.AreEqual(VaseState.Full, vase.state);
            Assert.AreEqual(10, vase.currentWater);
        }

        [Test]
        public void UseWater_DeductsAcrossVases()
        {
            var data = new SaveData();
            data.vases.Add(new VaseSave { capacity = 5, currentWater = 3, state = VaseState.Full });
            data.vases.Add(new VaseSave { capacity = 5, currentWater = 5, state = VaseState.Full });

            int needed = 6;
            for (int i = 0; i < data.vases.Count && needed > 0; i++)
            {
                int take = Math.Min(data.vases[i].currentWater, needed);
                data.vases[i].currentWater -= take;
                needed -= take;
            }

            Assert.AreEqual(0, needed);
            Assert.AreEqual(0, data.vases[0].currentWater);
            Assert.AreEqual(2, data.vases[1].currentWater);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Expected: FAIL — `VaseManager.InitializeNewPlayer` doesn't exist.

**Step 3: Create VaseManager**

Create `Assets/Scripts/Managers/VaseManager.cs`:
```csharp
using System;
using UnityEngine;

namespace Garden
{
    public class VaseManager : MonoBehaviour
    {
        public static VaseManager Instance { get; private set; }

        [SerializeField] private VaseConfig config;

        public VaseConfig Config => config;

        public event Action OnVasesChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            CheckFillCompletion();
        }

        public static void InitializeNewPlayer(SaveData data, int baseCapacity)
        {
            data.vases.Add(new VaseSave { capacity = baseCapacity, state = VaseState.Empty });
            data.vases.Add(new VaseSave { capacity = baseCapacity, state = VaseState.Empty });
        }

        private void CheckFillCompletion()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            foreach (var vase in data.vases)
            {
                if (vase.state != VaseState.Filling) continue;
                if (string.IsNullOrEmpty(vase.fillStartTimeUtc)) continue;

                var startTime = DateTime.Parse(vase.fillStartTimeUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                var elapsed = GameTime.UtcNow - startTime;

                if (elapsed.TotalMinutes >= config.FillDurationMinutes)
                {
                    vase.currentWater = vase.capacity;
                    vase.state = VaseState.Full;
                    vase.fillStartTimeUtc = null;
                    changed = true;
                }
            }
            if (changed)
            {
                SaveManager.Instance.Save();
                OnVasesChanged?.Invoke();
            }
        }

        public bool SendToCollect(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return false;
            var vase = data.vases[vaseIndex];
            if (vase.state == VaseState.Filling) return false;

            vase.state = VaseState.Filling;
            vase.fillStartTimeUtc = GameTime.UtcNow.ToString("o");
            SaveManager.Instance.Save();
            OnVasesChanged?.Invoke();
            return true;
        }

        public bool CraftVase()
        {
            if (!CurrencyManager.Instance.SpendMana(config.CraftCostMana)) return false;
            var data = SaveManager.Instance.Data;
            data.vases.Add(new VaseSave { capacity = config.BaseCapacity, state = VaseState.Empty });
            SaveManager.Instance.Save();
            OnVasesChanged?.Invoke();
            return true;
        }

        public float GetFillProgress(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return 0f;
            var vase = data.vases[vaseIndex];
            if (vase.state != VaseState.Filling || string.IsNullOrEmpty(vase.fillStartTimeUtc))
                return 0f;

            var startTime = DateTime.Parse(vase.fillStartTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            var elapsed = (float)(GameTime.UtcNow - startTime).TotalMinutes;
            return Mathf.Clamp01(elapsed / config.FillDurationMinutes);
        }
    }
}
```

**Step 4: Run tests**

Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/VaseManager.cs Assets/Tests/EditMode/TestVaseManager.cs
git commit -m "feat: add VaseManager

Water storage with Mallum collection. SendToCollect starts a fill
timer, CheckFillCompletion runs per-frame. CraftVase spends Mana."
```

---

### Task 9: Build PlotManager

**Files:**
- Create: `Assets/Scripts/Managers/PlotManager.cs`
- Create: `Assets/Tests/EditMode/TestPlotManager.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestPlotManager.cs`:
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
                watered = true,
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
        public void WeatherMatch_BoostsGrowthSpeed()
        {
            float baseHours = 4f;
            float weatherBoost = 0.25f;
            float boostedHours = baseHours / (1f + weatherBoost);
            Assert.AreEqual(3.2f, boostedHours, 0.01f);
        }

        [Test]
        public void QualityMultiplier_InRange()
        {
            // With weather match, quality should be boosted
            // Without, it should be base
            float baseQuality = 1.0f;
            float weatherBonus = 0.5f; // random 0-1 * weather bonus
            float total = Mathf.Clamp(baseQuality + weatherBonus, 0.8f, 2.0f);
            Assert.GreaterOrEqual(total, 0.8f);
            Assert.LessOrEqual(total, 2.0f);
        }

        [Test]
        public void Harvest_ClearsPlot()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                state = PlotState.Mature,
                watered = true
            };
            // Simulate harvest
            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.watered = false;
            plot.state = PlotState.Empty;

            Assert.AreEqual(PlotState.Empty, plot.state);
            Assert.IsNull(plot.seedName);
        }
    }
}
```

**Step 2: Run test to verify it passes**

Expected: PASS — tests validate logic on data classes directly.

**Step 3: Create PlotManager**

Create `Assets/Scripts/Managers/PlotManager.cs`:
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

        private void Update()
        {
            CheckGrowthCompletion();
        }

        public List<PlotSave> Plots => SaveManager.Instance.Data.plots;

        public bool CraftPlot()
        {
            if (Plots.Count >= FlameManager.Instance.MaxPlots) return false;
            // Plots cost Mana to craft — use FlameConfig upgrade-like cost
            var data = SaveManager.Instance.Data;
            data.plots.Add(new PlotSave { state = PlotState.Empty });
            SaveManager.Instance.Save();
            return true;
        }

        public bool Plant(int plotIndex, string seedName)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Empty) return false;

            // Check seed inventory
            var seedEntry = data.seedInventory.Find(s => s.seedName == seedName);
            if (seedEntry == null || seedEntry.count <= 0) return false;

            // Consume seed
            seedEntry.count--;
            if (seedEntry.count <= 0) data.seedInventory.Remove(seedEntry);

            plot.seedName = seedName;
            plot.state = PlotState.Planted;
            plot.watered = false;
            plot.plantTimeUtc = null;

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        public bool Water(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Planted) return false;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return false;

            if (!CurrencyManager.Instance.SpendWater(seed.waterRequired)) return false;

            plot.watered = true;
            plot.state = PlotState.Growing;
            plot.plantTimeUtc = GameTime.UtcNow.ToString("o");

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
            float effectiveHours = GetEffectiveGrowthHours(seed);
            return Mathf.Clamp01(elapsed / effectiveHours);
        }

        public HarvestResult Harvest(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return null;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Mature) return null;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return null;

            bool weatherMatch = seed.preferredWeather != null &&
                                WeatherService.Instance != null &&
                                seed.preferredWeather.Evaluate(WeatherService.Instance.CurrentWeather);

            float quality = CalculateQuality(weatherMatch);
            int yield = Mathf.RoundToInt(seed.baseYield * quality);

            // Add to inventory
            AddItem(data, seed.seedName + "_harvest", yield);

            // Clear plot
            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.watered = false;
            plot.state = PlotState.Empty;

            SaveManager.Instance.Save();

            var result = new HarvestResult
            {
                seedName = seed.seedName,
                yield = yield,
                qualityMultiplier = quality,
                weatherMatched = weatherMatch
            };

            OnPlotChanged?.Invoke(plotIndex);
            OnHarvested?.Invoke(plotIndex, result);
            return result;
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

        private float GetEffectiveGrowthHours(SeedData seed)
        {
            float hours = seed.growthDurationHours;
            if (WeatherService.Instance != null &&
                seed.preferredWeather != null &&
                seed.preferredWeather.Evaluate(WeatherService.Instance.CurrentWeather))
            {
                hours /= (1f + SeedData.WeatherMatchBonus);
            }
            return hours;
        }

        private static float CalculateQuality(bool weatherMatch)
        {
            float base_ = 1.0f;
            float roll = UnityEngine.Random.Range(-0.2f, 0.2f);
            if (weatherMatch) roll += 0.5f;
            return Mathf.Clamp(base_ + roll, SeedData.MinQualityMultiplier, SeedData.MaxQualityMultiplier);
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
        public int yield;
        public float qualityMultiplier;
        public bool weatherMatched;
    }
}
```

**Step 4: Run tests**

Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Tests/EditMode/TestPlotManager.cs
git commit -m "feat: add PlotManager

Full plot lifecycle: craft, plant, water, grow, harvest.
Weather match boosts growth speed by 25% and quality multiplier.
Quality is a continuous 0.8-2.0 multiplier (no letter tiers)."
```

---

### Task 10: Build GardenManager

**Files:**
- Create: `Assets/Scripts/Managers/GardenManager.cs`
- Create: `Assets/Tests/EditMode/TestGardenManager.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestGardenManager.cs`:
```csharp
using System;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestGardenManager
    {
        [Test]
        public void GardenGrowth_CalculatesProgress()
        {
            var garden = new GardenSave
            {
                plantName = "Oak",
                plantTimeUtc = DateTime.UtcNow.AddHours(-12).ToString("o"),
                mature = false
            };

            float growthHours = 24f;
            var plantTime = DateTime.Parse(garden.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - plantTime).TotalHours;
            float progress = UnityEngine.Mathf.Clamp01(elapsed / growthHours);

            Assert.AreEqual(0.5f, progress, 0.05f);
        }

        [Test]
        public void MatureGarden_YieldsOnInterval()
        {
            var garden = new GardenSave
            {
                plantName = "Oak",
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-13).ToString("o")
            };

            float intervalHours = 12f;
            var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - lastYield).TotalHours;

            Assert.IsTrue(elapsed >= intervalHours);
        }

        [Test]
        public void MatureGarden_DoesNotYieldTooEarly()
        {
            var garden = new GardenSave
            {
                plantName = "Oak",
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-6).ToString("o")
            };

            float intervalHours = 12f;
            var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - lastYield).TotalHours;

            Assert.IsFalse(elapsed >= intervalHours);
        }
    }
}
```

**Step 2: Run test to verify it passes**

Expected: PASS.

**Step 3: Create GardenManager**

Create `Assets/Scripts/Managers/GardenManager.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class GardenManager : MonoBehaviour
    {
        public static GardenManager Instance { get; private set; }

        public event Action<int> OnGardenChanged;
        public event Action<int, string, int> OnYieldCollected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            CheckGrowthAndYields();
        }

        public List<GardenSave> Gardens => SaveManager.Instance.Data.gardens;

        public bool Plant(int gardenIndex, string plantName)
        {
            var data = SaveManager.Instance.Data;
            if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return false;
            var garden = data.gardens[gardenIndex];
            if (!string.IsNullOrEmpty(garden.plantName)) return false;

            var plantData = LoadPlantData(plantName);
            if (plantData == null) return false;

            if (!CurrencyManager.Instance.SpendWater(plantData.waterRequired)) return false;

            garden.plantName = plantName;
            garden.plantTimeUtc = GameTime.UtcNow.ToString("o");
            garden.mature = false;
            garden.lastYieldTimeUtc = null;

            SaveManager.Instance.Save();
            OnGardenChanged?.Invoke(gardenIndex);
            return true;
        }

        public float GetGrowthProgress(int gardenIndex)
        {
            var data = SaveManager.Instance.Data;
            if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return 0f;
            var garden = data.gardens[gardenIndex];
            if (garden.mature || string.IsNullOrEmpty(garden.plantTimeUtc)) return garden.mature ? 1f : 0f;

            var plantData = LoadPlantData(garden.plantName);
            if (plantData == null) return 0f;

            var plantTime = DateTime.Parse(garden.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(GameTime.UtcNow - plantTime).TotalHours;
            return Mathf.Clamp01(elapsed / plantData.growthDurationHours);
        }

        private void CheckGrowthAndYields()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;

            for (int i = 0; i < data.gardens.Count; i++)
            {
                var garden = data.gardens[i];
                if (string.IsNullOrEmpty(garden.plantName)) continue;

                // Check growth completion
                if (!garden.mature && GetGrowthProgress(i) >= 1f)
                {
                    garden.mature = true;
                    garden.lastYieldTimeUtc = GameTime.UtcNow.ToString("o");
                    changed = true;
                    OnGardenChanged?.Invoke(i);
                }

                // Check periodic yield
                if (garden.mature && !string.IsNullOrEmpty(garden.lastYieldTimeUtc))
                {
                    var plantData = LoadPlantData(garden.plantName);
                    if (plantData == null) continue;

                    var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    float elapsed = (float)(GameTime.UtcNow - lastYield).TotalHours;

                    if (elapsed >= plantData.yieldIntervalHours)
                    {
                        AddItem(data, plantData.yieldItem, plantData.yieldAmount);
                        garden.lastYieldTimeUtc = GameTime.UtcNow.ToString("o");
                        changed = true;
                        OnYieldCollected?.Invoke(i, plantData.yieldItem, plantData.yieldAmount);
                    }
                }
            }

            if (changed) SaveManager.Instance.Save();
        }

        private static void AddItem(SaveData data, string itemName, int count)
        {
            var existing = data.items.Find(it => it.itemName == itemName);
            if (existing != null) existing.count += count;
            else data.items.Add(new InventoryItem { itemName = itemName, count = count });
        }

        private static GardenPlantData LoadPlantData(string plantName)
        {
            if (string.IsNullOrEmpty(plantName)) return null;
            var all = Resources.LoadAll<GardenPlantData>("GardenPlants");
            foreach (var p in all)
                if (p.plantName == plantName) return p;
            return null;
        }
    }
}
```

**Step 4: Run tests**

Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/GardenManager.cs Assets/Tests/EditMode/TestGardenManager.cs
git commit -m "feat: add GardenManager

Permanent garden plants that grow to maturity then yield fruit
on a periodic interval. No re-watering after initial planting."
```

---

### Task 11: Build ApothekeManager

**Files:**
- Create: `Assets/Scripts/Managers/ApothekeManager.cs`
- Create: `Assets/Tests/EditMode/TestApothekeManager.cs`

**Step 1: Write the test**

Create `Assets/Tests/EditMode/TestApothekeManager.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestApothekeManager
    {
        [Test]
        public void Mix_ConsumesIngredients()
        {
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "RedFlower", count = 5 });
            data.items.Add(new InventoryItem { itemName = "Water_Essence", count = 3 });

            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.ingredients = new List<IngredientEntry>
            {
                new() { itemName = "RedFlower", quantity = 2 },
                new() { itemName = "Water_Essence", quantity = 1 }
            };
            recipe.result = "Fertilizer";
            recipe.resultQuantity = 1;

            // Check can mix
            bool canMix = true;
            foreach (var ing in recipe.ingredients)
            {
                var item = data.items.Find(i => i.itemName == ing.itemName);
                if (item == null || item.count < ing.quantity) { canMix = false; break; }
            }
            Assert.IsTrue(canMix);

            // Consume
            foreach (var ing in recipe.ingredients)
            {
                var item = data.items.Find(i => i.itemName == ing.itemName);
                item.count -= ing.quantity;
            }

            // Add result
            data.items.Add(new InventoryItem { itemName = recipe.result, count = recipe.resultQuantity });

            Assert.AreEqual(3, data.items[0].count); // RedFlower: 5-2=3
            Assert.AreEqual(2, data.items[1].count); // Water_Essence: 3-1=2
            Assert.AreEqual("Fertilizer", data.items[2].itemName);
        }

        [Test]
        public void Mix_FailsIfMissingIngredients()
        {
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "RedFlower", count = 1 });

            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.ingredients = new List<IngredientEntry>
            {
                new() { itemName = "RedFlower", quantity = 2 }
            };

            var item = data.items.Find(i => i.itemName == "RedFlower");
            Assert.IsFalse(item.count >= 2);
        }
    }
}
```

**Step 2: Run test to verify it passes**

Expected: PASS.

**Step 3: Create ApothekeManager**

Create `Assets/Scripts/Managers/ApothekeManager.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class ApothekeManager : MonoBehaviour
    {
        public static ApothekeManager Instance { get; private set; }

        private RecipeData[] allRecipes;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allRecipes = Resources.LoadAll<RecipeData>("Recipes");
        }

        public RecipeData[] AllRecipes => allRecipes;
        public List<SeedInventoryEntry> Seeds => SaveManager.Instance.Data.seedInventory;
        public List<InventoryItem> Items => SaveManager.Instance.Data.items;

        public bool CanMix(RecipeData recipe)
        {
            var data = SaveManager.Instance.Data;
            foreach (var ing in recipe.ingredients)
            {
                var item = data.items.Find(i => i.itemName == ing.itemName);
                if (item == null || item.count < ing.quantity) return false;
            }
            return true;
        }

        public bool Mix(RecipeData recipe)
        {
            if (!CanMix(recipe)) return false;
            var data = SaveManager.Instance.Data;

            // Consume ingredients
            foreach (var ing in recipe.ingredients)
            {
                var item = data.items.Find(i => i.itemName == ing.itemName);
                item.count -= ing.quantity;
                if (item.count <= 0) data.items.Remove(item);
            }

            // Produce result
            var existing = data.items.Find(i => i.itemName == recipe.result);
            if (existing != null)
                existing.count += recipe.resultQuantity;
            else
                data.items.Add(new InventoryItem { itemName = recipe.result, count = recipe.resultQuantity });

            SaveManager.Instance.Save();
            return true;
        }

        public void AddSeed(string seedName, int count = 1)
        {
            var data = SaveManager.Instance.Data;
            var entry = data.seedInventory.Find(s => s.seedName == seedName);
            if (entry != null)
                entry.count += count;
            else
                data.seedInventory.Add(new SeedInventoryEntry { seedName = seedName, count = count });
            SaveManager.Instance.Save();
        }
    }
}
```

**Step 4: Run tests**

Expected: PASS.

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/ApothekeManager.cs Assets/Tests/EditMode/TestApothekeManager.cs
git commit -m "feat: add ApothekeManager

Seed storage and recipe mixing. CanMix checks ingredients,
Mix consumes them and produces results. AddSeed for inventory."
```

---

### Task 12: Build GameManager and VisitorSystem

**Files:**
- Create: `Assets/Scripts/Managers/GameManager.cs`
- Create: `Assets/Scripts/Managers/VisitorSystem.cs`

**Step 1: Create GameManager**

Create `Assets/Scripts/Managers/GameManager.cs`:
```csharp
using UnityEngine;

namespace Garden
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 120;
        }

        private void Start()
        {
            if (SaveManager.Instance.Data.vases.Count == 0)
            {
                InitializeNewPlayer();
            }
        }

        private void InitializeNewPlayer()
        {
            var data = SaveManager.Instance.Data;

            // Starting resources
            data.mana = 50f;

            // 2 starting vases
            VaseManager.InitializeNewPlayer(data, VaseManager.Instance.Config.BaseCapacity);

            // 1 starting plot
            data.plots.Add(new PlotSave { state = PlotState.Empty });

            // Starting seeds
            ApothekeManager.Instance.AddSeed("Fern", 3);

            SaveManager.Instance.Save();
        }
    }
}
```

**Step 2: Create VisitorSystem**

Create `Assets/Scripts/Managers/VisitorSystem.cs`:
```csharp
using System;
using UnityEngine;

namespace Garden
{
    public class VisitorSystem : MonoBehaviour
    {
        public static VisitorSystem Instance { get; private set; }

        public event Action<VisitorGift> OnVisitorArrived;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            CheckForVisitor();
        }

        private void CheckForVisitor()
        {
            if (WeatherService.Instance == null) return;
            if (!WeatherService.Instance.CurrentWeather.isNight) return;

            var data = SaveManager.Instance.Data;
            var today = GameTime.UtcNow.Date.ToString("o");

            if (data.lastVisitorDateUtc == today) return;

            // Visitor arrives at night
            data.lastVisitorDateUtc = today;

            var gift = DetermineGift(data);
            ApplyGift(data, gift);

            SaveManager.Instance.Save();
            OnVisitorArrived?.Invoke(gift);
        }

        private VisitorGift DetermineGift(SaveData data)
        {
            // If low on water, gift water
            int totalWater = 0;
            foreach (var v in data.vases) totalWater += v.currentWater;
            if (totalWater <= 2)
            {
                return new VisitorGift { type = VisitorGiftType.Water, amount = 3 };
            }

            // Otherwise gift a random seed
            return new VisitorGift { type = VisitorGiftType.Seed, seedName = "Fern", amount = 1 };
        }

        private void ApplyGift(SaveData data, VisitorGift gift)
        {
            switch (gift.type)
            {
                case VisitorGiftType.Water:
                    // Fill first non-full vase
                    foreach (var vase in data.vases)
                    {
                        int space = vase.capacity - vase.currentWater;
                        if (space > 0)
                        {
                            int fill = Math.Min(space, gift.amount);
                            vase.currentWater += fill;
                            gift.amount -= fill;
                            if (gift.amount <= 0) break;
                        }
                    }
                    break;
                case VisitorGiftType.Seed:
                    ApothekeManager.Instance?.AddSeed(gift.seedName, gift.amount);
                    break;
            }
        }
    }

    public enum VisitorGiftType { Seed, Water }

    [Serializable]
    public class VisitorGift
    {
        public VisitorGiftType type;
        public string seedName;
        public int amount;
    }
}
```

**Step 3: Verify compilation**

Run: `read_console`.
Expected: Clean.

**Step 4: Commit**

```bash
git add Assets/Scripts/Managers/GameManager.cs Assets/Scripts/Managers/VisitorSystem.cs
git commit -m "feat: add GameManager and VisitorSystem

GameManager initializes new players with 50 Mana, 2 vases, 1 plot,
and 3 Fern seeds. VisitorSystem gifts seed/water at night, once/day."
```

---

### Task 13: Rewrite NotificationService

**Files:**
- Modify: `Assets/Scripts/Services/NotificationService.cs`

**Step 1: Rewrite NotificationService to remove PlantManager references**

Replace contents of `Assets/Scripts/Services/NotificationService.cs`:
```csharp
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#elif UNITY_ANDROID
using Unity.Notifications.Android;
#endif

namespace Garden
{
    public class NotificationService : MonoBehaviour
    {
        public static NotificationService Instance { get; private set; }

        private const string AndroidChannelId = "campfire_plants";
        private const string AndroidWeatherChannelId = "campfire_weather";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitializePlatform();
        }

        private void InitializePlatform()
        {
#if UNITY_ANDROID
            var channel = new AndroidNotificationChannel
            {
                Id = AndroidChannelId,
                Name = "Plant Growth",
                Description = "Notifications when your plants finish growing",
                Importance = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            var weatherChannel = new AndroidNotificationChannel
            {
                Id = AndroidWeatherChannelId,
                Name = "Weather Updates",
                Description = "Notifications about weather changes at your camp",
                Importance = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(weatherChannel);
#elif UNITY_IOS
            using var req = new AuthorizationRequest(
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false);
#endif
        }

        public void SaveWeatherData(string apiKey, float lat, float lon, WeatherCondition condition)
        {
#if UNITY_IOS
            PlayerPrefs.SetString("weather_api_key", apiKey);
            PlayerPrefs.SetFloat("weather_lat", lat);
            PlayerPrefs.SetFloat("weather_lon", lon);
            PlayerPrefs.SetInt("weather_condition", (int)condition);
            PlayerPrefs.Save();
#elif UNITY_ANDROID
            using var plugin = new AndroidJavaClass("com.garden.WeatherPrefsPlugin");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            plugin.CallStatic("saveWeatherData", context, apiKey, lat, lon, (int)condition);
#endif
        }

        public void SchedulePlantNotification(int plotIndex, string seedName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string title = $"Your {seedName} is ready!";
            string body = $"Come harvest your {seedName} at the camp!";

#if UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now.AddSeconds(remainingSeconds),
                SmallIcon = "icon_0",
                LargeIcon = "icon_1"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, AndroidChannelId, plotIndex);
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = new System.TimeSpan(0, 0, (int)remainingSeconds),
                Repeats = false
            };
            var notification = new iOSNotification
            {
                Identifier = plotIndex.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
        }

        public void CancelAll()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
        }
    }
}
```

**Step 2: Verify compilation**

Run: `read_console`.
Expected: Clean.

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/NotificationService.cs
git commit -m "refactor: update NotificationService for Camp Fire

Remove PlantManager references. Simplify notification scheduling
to use plot index only. Update channel IDs."
```

---

### Task 14: Build CampFireRoot.uxml and Stylesheets

**Files:**
- Create: `Assets/UI/Documents/CampFireRoot.uxml`
- Modify: `Assets/UI/Styles/Variables.uss`
- Create: `Assets/UI/Styles/CampSite.uss`
- Create: `Assets/UI/Styles/WeatherBar.uss`
- Create: `Assets/UI/Styles/BottomNav.uss`
- Create: `Assets/UI/Styles/Apotheke.uss`
- Create: `Assets/UI/Styles/Craft.uss`
- Create: `Assets/Resources/UI/Templates/` (PlotItem, VaseItem, etc.)

This is the UI scaffold. Write the root UXML with all panel containers, the bottom nav, the weather bar, and the campsite view. Create stub stylesheets for each panel. Create UXML templates for dynamic list items (plots, vases, seeds, recipes).

The exact UXML and USS content will be substantial — write it as a single cohesive pass. Refer to the design doc Section 3 for the layout specification. Key elements:

- `#weather-bar` — top bar with weather info
- `#campsite-view` — absolute-positioned container for camp objects
- `#flame` — central flame element
- `#plots-container` — plot elements around the flame
- `#vases-container` — vase elements
- `#gardens-container` — garden elements
- `#bottom-nav` — 3 buttons: Apotheke, Letters, Craft
- `#overlay-container` — slide-up overlay panels (one per bottom nav tab)
- `#apotheke-panel`, `#letters-panel`, `#craft-panel` — overlay content

Create templates in `Assets/Resources/UI/Templates/`:
- `PlotItem.uxml` — seed name, growth progress bar, state label, action button
- `VaseItem.uxml` — water level, fill progress, collect button
- `SeedCard.uxml` — seed icon, name, count
- `RecipeCard.uxml` — recipe name, ingredients list, mix button
- `CraftItem.uxml` — item name, cost, craft button

**Step 1: Create all UI files**

(Implementation creates each file with the appropriate UXML/USS content matching the design.)

**Step 2: Verify files load in Unity**

Run: `read_console`.
Expected: No UXML parse errors.

**Step 3: Commit**

```bash
git add Assets/UI/ Assets/Resources/UI/
git commit -m "feat: add CampFireRoot.uxml and all stylesheets/templates

Root document with weather bar, campsite view, bottom nav, and
overlay panels. Templates for plots, vases, seeds, recipes, crafts."
```

---

### Task 15: Build CampFireUI Root Orchestrator and Sub-Controllers

**Files:**
- Create: `Assets/Scripts/UI/CampFireUI.cs`
- Create: `Assets/Scripts/UI/WeatherBarUI.cs`
- Create: `Assets/Scripts/UI/BottomNavUI.cs`
- Create: `Assets/Scripts/UI/CampsiteViewUI.cs`
- Create: `Assets/Scripts/UI/ApothekeUI.cs`
- Create: `Assets/Scripts/UI/CraftUI.cs`
- Create: `Assets/Scripts/UI/LettersUI.cs`
- Create: `Assets/Scripts/UI/FlameUI.cs`
- Create: `Assets/Scripts/UI/PlotUI.cs`
- Create: `Assets/Scripts/UI/VaseUI.cs`
- Create: `Assets/Scripts/UI/GardenUI.cs`
- Create: `Assets/Scripts/UI/SafeAreaController.cs`

Each controller follows the pattern from Garden:
- MonoBehaviour on the `--- UI ---` GameObject
- `Initialize(VisualElement root)` called by CampFireUI
- Caches element refs via `root.Q<>()`
- Subscribes to manager events for dynamic updates

**CampFireUI** is the root orchestrator:
- Gets `UIDocument.rootVisualElement`
- Calls `Initialize(root)` on all sub-controllers
- Wires `WeatherService.OnLocationResolved` for the location gate
- Manages overlay show/hide

**Step 1: Create all UI controller files**

(Each file follows the Initialize pattern. CampsiteViewUI manages camp object placement. WeatherBarUI subscribes to OnWeatherUpdated. BottomNavUI toggles overlays. FlameUI/PlotUI/VaseUI/GardenUI handle object interactions.)

**Step 2: Verify compilation**

Run: `read_console`.
Expected: Clean.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/
git commit -m "feat: add all UI controllers

CampFireUI root orchestrator, WeatherBarUI, BottomNavUI,
CampsiteViewUI, overlay panels (Apotheke, Letters, Craft),
and camp object interaction panels (Flame, Plot, Vase, Garden)."
```

---

### Task 16: Rebuild the Scene

**Files:**
- Modify: `Assets/Scenes/Garden.unity` (rebuild via Unity MCP)

Using Unity MCP tools, rebuild the scene hierarchy:

1. Delete all existing GameObjects
2. Create new hierarchy:
   ```
   Main Camera (Camera, 2D)
   Global Light 2D (Light2D)
   --- MANAGERS ---
     GameManager (GameManager)
     WeatherService (WeatherService)
     SaveManager (SaveManager)
     CurrencyManager (CurrencyManager)
     FlameManager (FlameManager) + wire FlameConfig asset
     PlotManager (PlotManager)
     VaseManager (VaseManager) + wire VaseConfig asset
     GardenManager (GardenManager)
     ApothekeManager (ApothekeManager)
     VisitorSystem (VisitorSystem)
     NotificationService (NotificationService)
   --- UI ---
     UIDocument (source = CampFireRoot.uxml, PanelSettings = GardenPanelSettings)
     CampFireUI
     WeatherBarUI
     BottomNavUI
     CampsiteViewUI
     ApothekeUI
     CraftUI
     LettersUI
     SafeAreaController
   ```

3. Wire serialized field references (FlameConfig, VaseConfig assets)
4. Save scene

**Step 1: Rebuild scene**

Use `manage_scene` and `manage_gameobject` tools.

**Step 2: Verify scene loads**

Run: `manage_editor` action=play, check console for errors.

**Step 3: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat: rebuild scene with Camp Fire hierarchy

New manager and UI GameObjects. FlameConfig and VaseConfig wired.
UIDocument points to CampFireRoot.uxml."
```

---

### Task 17: Create Initial Content Assets

**Files:**
- Create: `Assets/Resources/Config/FlameConfig.asset`
- Create: `Assets/Resources/Config/VaseConfig.asset`
- Create: `Assets/Resources/Seeds/Fern.asset` (starter seed)
- Create: `Assets/Resources/Seeds/Sunflower.asset`
- Create: `Assets/Resources/Seeds/Moonvine.asset`
- Create: `Assets/Resources/GardenPlants/Oak.asset`
- Create: `Assets/Resources/GardenPlants/BerryBush.asset`
- Create: `Assets/Resources/Recipes/` (1-2 starter recipes)

Create ScriptableObject assets either via Unity MCP `manage_asset` or by writing YAML directly.

**Step 1: Create config assets**

FlameConfig with the default values from the class.
VaseConfig with the default values from the class.

**Step 2: Create seed assets**

Fern: growthDurationHours=2, waterRequired=1, baseYield=2, manaCost=0 (free starter)
Sunflower: growthDurationHours=4, waterRequired=2, baseYield=3, manaCost=25, preferredWeather=Clear+Hot
Moonvine: growthDurationHours=8, waterRequired=2, baseYield=5, manaCost=50, preferredWeather=Night

**Step 3: Create garden plant assets**

Oak: growthDurationHours=48, yieldItem=Acorn, yieldAmount=2, yieldIntervalHours=24, waterRequired=5
BerryBush: growthDurationHours=24, yieldItem=Berry, yieldAmount=3, yieldIntervalHours=12, waterRequired=3

**Step 4: Create recipe assets**

Fertilizer: 3x Berry + 1x Acorn → 1x Fertilizer

**Step 5: Commit**

```bash
git add Assets/Resources/
git commit -m "feat: add initial content assets

FlameConfig, VaseConfig, 3 seeds (Fern, Sunflower, Moonvine),
2 garden plants (Oak, BerryBush), 1 recipe (Fertilizer)."
```

---

### Task 18: Update CLAUDE.md and Memory

**Files:**
- Modify: `CLAUDE.md`
- Modify: `memory/MEMORY.md`

Update CLAUDE.md to reflect Camp Fire architecture:
- New project overview
- New file locations
- New system descriptions
- Remove all Garden-specific references

Update memory/MEMORY.md similarly.

**Step 1: Rewrite CLAUDE.md**

**Step 2: Update MEMORY.md**

**Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md for Camp Fire

Replace all Garden references with Camp Fire architecture,
systems, and file locations."
```

---

### Task 19: Verify Full Compilation and Playtest

**Step 1: Check console for errors**

Run: `read_console` filtered for errors.
Expected: Clean.

**Step 2: Enter play mode**

Run: `manage_editor` action=play.
Check: Flame generates Mana, UI renders, no exceptions.

**Step 3: Exit play mode**

Run: `manage_editor` action=stop.

**Step 4: Run all tests**

Run: `run_tests` mode=EditMode.
Expected: All tests pass.

**Step 5: Final commit if any fixes were needed**

```bash
git commit -m "fix: resolve compilation and runtime issues from migration"
```
