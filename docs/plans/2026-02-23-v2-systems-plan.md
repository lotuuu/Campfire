# V2.0 Systems Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement all v2.0 gameplay systems: Bloom Roll quality tiers, Sync Shield, multi-slot terrarium, seed shop, 60 variants, and harvest sell/keep choice.

**Architecture:** Extend existing ScriptableObject data layer with QualityTier enum and SeedData fields. Add HarvestEngine static service for probability rolls. Refactor PlantManager from single-plant to multi-slot via PlantSlot class. Add EnvironmentManager and SeedShopManager singletons. Replace Greenhouse panel with Terrarium view. Add Shop panel and Harvest popup.

**Tech Stack:** Unity 6 / C# / UI Toolkit (UXML+USS) / ScriptableObjects / NUnit EditMode tests

---

## Task 1: Add QualityTier Enum and HarvestResult Data

**Files:**
- Modify: `Assets/Scripts/Data/GameEnums.cs`
- Create: `Assets/Scripts/Data/HarvestResult.cs`

**Step 1: Add QualityTier enum to GameEnums.cs**

Add after the `PlantState` enum at the end of `GameEnums.cs`:

```csharp
public enum QualityTier { D, C, B, A, S }
```

**Step 2: Create HarvestResult.cs**

```csharp
namespace Garden
{
    public struct HarvestResult
    {
        public QualityTier tier;
        public float valueMultiplier;
        public bool syncShieldActive;
        public int dewdropValue;
        public VariantData variant;
        public SeedData seed;
    }
}
```

**Step 3: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 4: Commit**

```bash
git add Assets/Scripts/Data/GameEnums.cs Assets/Scripts/Data/HarvestResult.cs Assets/Scripts/Data/HarvestResult.cs.meta
git commit -m "feat: add QualityTier enum and HarvestResult struct"
```

---

## Task 2: Extend SeedData with Shop and Sync Shield Fields

**Files:**
- Modify: `Assets/Scripts/Data/SeedData.cs`

**Step 1: Add new fields to SeedData**

Add fields after the existing `variants` field. Also add `SeedSpecialCondition` as a serializable class in the same file:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "Garden/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public Sprite icon;
        [TextArea] public string description;
        [Range(0.01f, 72f)] public float baseGrowthHours = 24f;
        public List<VariantData> variants = new();

        [Header("Shop")]
        public int buyPrice;
        public int baseSellPrice = 120;

        [Header("Sync Shield")]
        public WeatherCondition preferredWeather = WeatherCondition.Clear;

        [Header("Special Conditions")]
        public List<SeedSpecialCondition> specialConditions = new();
    }

    [Serializable]
    public class SeedSpecialCondition
    {
        public QualityTier targetTier;
        [Range(0f, 1f)] public float bonusPercent = 0.1f;
        public TriggerCondition condition;
    }
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors. Existing Astra.asset will gain new fields with defaults.

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/SeedData.cs
git commit -m "feat: add shop pricing, Sync Shield, and special conditions to SeedData"
```

---

## Task 3: Extend SaveData for Multi-Slot and QualityTier

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`

**Step 1: Add PlantSlotSave, extend GreenhousePlantSave, add environment tracking**

Replace the full file:

```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int dewdrops;
        public int sunShards;
        public int auraDust;

        // v2: multi-slot replaces single activePlant
        public ActivePlantSave activePlant;
        public List<PlantSlotSave> activeSlots = new();
        public List<GreenhousePlantSave> greenhousePlants = new();
        public List<string> discoveredVariants = new();
        public List<SeedInventoryEntry> seedInventory = new();
        public int greenhouseSlots = 6;

        // v2: terrarium environments
        public List<string> unlockedEnvironments = new();
    }

    [Serializable]
    public class ActivePlantSave
    {
        public string seedName;
        public string variantName;
        public string plantTimeUtc;
        public float growthSpeedMultiplier = 1f;
        public bool isActive;
    }

    [Serializable]
    public class PlantSlotSave
    {
        public int environmentIndex;
        public int slotIndex;
        public string seedName;
        public string variantName;
        public string plantTimeUtc;
        public float growthSpeedMultiplier = 1f;
    }

    [Serializable]
    public class GreenhousePlantSave
    {
        public string seedName;
        public string variantName;
        public string harvestTimeUtc;
        public QualityTier qualityTier;
    }

    [Serializable]
    public class SeedInventoryEntry
    {
        public string seedName;
        public int count;
    }
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs
git commit -m "feat: add PlantSlotSave and QualityTier to save data"
```

---

## Task 4: Create EnvironmentData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/EnvironmentData.cs`

**Step 1: Create EnvironmentData.cs**

```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewEnvironment", menuName = "Garden/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        public string environmentName;
        public int slotCount = 2;
        public int unlockCostDewdrops;

        [Header("Growth Bonus")]
        [Range(0f, 0.5f)] public float growthSpeedBonus;
        public TriggerCondition bonusCondition;

        [Header("Features")]
        public bool allowsCrossPollination;
    }
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/EnvironmentData.cs Assets/Scripts/Data/EnvironmentData.cs.meta
git commit -m "feat: add EnvironmentData ScriptableObject"
```

---

## Task 5: Add QualityTier Utilities to CurrencyConfig

**Files:**
- Modify: `Assets/Scripts/Data/CurrencyConfig.cs`

**Step 1: Add quality tier methods to CurrencyConfig**

Add after the existing `GetDustPerHourForRarity` method:

```csharp
public static float GetQualityMultiplier(QualityTier tier) => tier switch
{
    QualityTier.D => 0.8f,
    QualityTier.C => 1.0f,
    QualityTier.B => 1.5f,
    QualityTier.A => 2.2f,
    QualityTier.S => 3.5f,
    _ => 1.0f
};

public static string GetQualityLabel(QualityTier tier) => tier switch
{
    QualityTier.D => "Faded",
    QualityTier.C => "Stable",
    QualityTier.B => "Vibrant",
    QualityTier.A => "Radiant",
    QualityTier.S => "Eternal",
    _ => "Unknown"
};

public int GetSellValue(int baseSellPrice, QualityTier tier)
{
    return Mathf.RoundToInt(baseSellPrice * GetQualityMultiplier(tier));
}

public float GetDustPerHourForPlant(Rarity rarity, QualityTier tier)
{
    return GetDustPerHourForRarity(rarity) * GetQualityMultiplier(tier);
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/CurrencyConfig.cs
git commit -m "feat: add quality tier multipliers and sell value to CurrencyConfig"
```

---

## Task 6: Write HarvestEngine Tests

**Files:**
- Create: `Assets/Tests/EditMode/TestHarvestEngine.cs`

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestHarvestEngine
    {
        private SeedData CreateTestSeed(WeatherCondition preferred = WeatherCondition.Clear)
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.seedName = "TestSeed";
            seed.baseSellPrice = 100;
            seed.preferredWeather = preferred;
            seed.specialConditions = new();
            return seed;
        }

        private VariantData CreateTestVariant()
        {
            var variant = ScriptableObject.CreateInstance<VariantData>();
            variant.variantName = "TestVariant";
            variant.rarity = Rarity.Common;
            return variant;
        }

        [Test]
        public void Roll_ReturnsValidQualityTier()
        {
            var seed = CreateTestSeed();
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Cloudy };

            var result = HarvestEngine.Roll(seed, variant, weather);

            Assert.That(result.tier, Is.AnyOf(
                QualityTier.D, QualityTier.C, QualityTier.B,
                QualityTier.A, QualityTier.S));
            Assert.AreEqual(seed, result.seed);
            Assert.AreEqual(variant, result.variant);
        }

        [Test]
        public void Roll_NoSyncShield_DewdropValueUsesBaseSellPrice()
        {
            var seed = CreateTestSeed(WeatherCondition.Rain);
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Clear };

            var result = HarvestEngine.Roll(seed, variant, weather);

            float expectedMultiplier = CurrencyConfig.GetQualityMultiplier(result.tier);
            int expected = Mathf.RoundToInt(100 * expectedMultiplier);
            Assert.AreEqual(expected, result.dewdropValue);
            Assert.IsFalse(result.syncShieldActive);
        }

        [Test]
        public void Roll_SyncShieldActive_WhenWeatherMatchesPreferred()
        {
            var seed = CreateTestSeed(WeatherCondition.Storm);
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Storm };

            var result = HarvestEngine.Roll(seed, variant, weather);

            Assert.IsTrue(result.syncShieldActive);
            // With sync shield, D tier should never appear
            Assert.AreNotEqual(QualityTier.D, result.tier);
        }

        [Test]
        public void Roll_SyncShieldActive_NeverReturnsD()
        {
            var seed = CreateTestSeed(WeatherCondition.Rain);
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Rain };

            // Roll 200 times to statistically verify D never appears
            for (int i = 0; i < 200; i++)
            {
                var result = HarvestEngine.Roll(seed, variant, weather);
                Assert.AreNotEqual(QualityTier.D, result.tier,
                    $"D tier appeared on roll {i} with sync shield active");
            }
        }

        [Test]
        public void GetBaseProbabilities_ReturnsCorrectValues()
        {
            var probs = HarvestEngine.GetBaseProbabilities();

            Assert.AreEqual(0.15f, probs[QualityTier.D], 0.001f);
            Assert.AreEqual(0.55f, probs[QualityTier.C], 0.001f);
            Assert.AreEqual(0.20f, probs[QualityTier.B], 0.001f);
            Assert.AreEqual(0.08f, probs[QualityTier.A], 0.001f);
            Assert.AreEqual(0.02f, probs[QualityTier.S], 0.001f);
        }

        [Test]
        public void GetSyncShieldProbabilities_ReturnsCorrectValues()
        {
            var probs = HarvestEngine.GetSyncShieldProbabilities();

            Assert.AreEqual(0f, probs[QualityTier.D], 0.001f);
            Assert.AreEqual(0.50f, probs[QualityTier.C], 0.001f);
            Assert.AreEqual(0.30f, probs[QualityTier.B], 0.001f);
            Assert.AreEqual(0.15f, probs[QualityTier.A], 0.001f);
            Assert.AreEqual(0.05f, probs[QualityTier.S], 0.001f);
        }

        [Test]
        public void Roll_SpecialCondition_ModifiesProbabilities()
        {
            var seed = CreateTestSeed(WeatherCondition.Clear);
            seed.specialConditions.Add(new SeedSpecialCondition
            {
                targetTier = QualityTier.S,
                bonusPercent = 0.10f,
                condition = new TriggerCondition
                {
                    useTemperature = true,
                    minTemp = 25f,
                    maxTemp = 60f
                }
            });
            var variant = CreateTestVariant();
            // Hot weather triggers the special condition
            var weather = new WeatherData { temperature = 30f, condition = WeatherCondition.Cloudy };

            // Roll many times - with +10% S-tier, we should see more S results
            int sCount = 0;
            int totalRolls = 1000;
            for (int i = 0; i < totalRolls; i++)
            {
                var result = HarvestEngine.Roll(seed, variant, weather);
                if (result.tier == QualityTier.S) sCount++;
            }

            // Base S rate is 2%, with +10% it should be ~12%
            // With 1000 rolls, expect ~120. Allow wide margin.
            Assert.Greater(sCount, 50, "S-tier should appear more often with +10% bonus");
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: Unity EditMode tests
Expected: FAIL — `HarvestEngine` does not exist yet.

**Step 3: Commit failing tests**

```bash
git add Assets/Tests/EditMode/TestHarvestEngine.cs Assets/Tests/EditMode/TestHarvestEngine.cs.meta
git commit -m "test: add failing HarvestEngine tests"
```

---

## Task 7: Implement HarvestEngine

**Files:**
- Create: `Assets/Scripts/Services/HarvestEngine.cs`

**Step 1: Implement HarvestEngine**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public static class HarvestEngine
    {
        public static Dictionary<QualityTier, float> GetBaseProbabilities()
        {
            return new Dictionary<QualityTier, float>
            {
                { QualityTier.D, 0.15f },
                { QualityTier.C, 0.55f },
                { QualityTier.B, 0.20f },
                { QualityTier.A, 0.08f },
                { QualityTier.S, 0.02f }
            };
        }

        public static Dictionary<QualityTier, float> GetSyncShieldProbabilities()
        {
            return new Dictionary<QualityTier, float>
            {
                { QualityTier.D, 0f },
                { QualityTier.C, 0.50f },
                { QualityTier.B, 0.30f },
                { QualityTier.A, 0.15f },
                { QualityTier.S, 0.05f }
            };
        }

        public static HarvestResult Roll(SeedData seed, VariantData variant, WeatherData weather)
        {
            bool syncShield = weather.condition == seed.preferredWeather;
            var probs = syncShield ? GetSyncShieldProbabilities() : GetBaseProbabilities();

            ApplySpecialConditions(seed, weather, probs);
            NormalizeProbabilities(probs);

            QualityTier tier = RollTier(probs);
            float multiplier = CurrencyConfig.GetQualityMultiplier(tier);

            return new HarvestResult
            {
                tier = tier,
                valueMultiplier = multiplier,
                syncShieldActive = syncShield,
                dewdropValue = Mathf.RoundToInt(seed.baseSellPrice * multiplier),
                variant = variant,
                seed = seed
            };
        }

        private static void ApplySpecialConditions(
            SeedData seed, WeatherData weather, Dictionary<QualityTier, float> probs)
        {
            if (seed.specialConditions == null) return;

            foreach (var sc in seed.specialConditions)
            {
                if (sc.condition == null || !sc.condition.Evaluate(weather)) continue;

                float bonus = sc.bonusPercent;
                probs[sc.targetTier] += bonus;

                // Subtract proportionally from other tiers
                float otherTotal = 0f;
                foreach (var kv in probs)
                    if (kv.Key != sc.targetTier) otherTotal += kv.Value;

                if (otherTotal <= 0f) continue;

                var keys = new List<QualityTier>(probs.Keys);
                foreach (var key in keys)
                {
                    if (key == sc.targetTier) continue;
                    probs[key] -= bonus * (probs[key] / otherTotal);
                    if (probs[key] < 0f) probs[key] = 0f;
                }
            }
        }

        private static void NormalizeProbabilities(Dictionary<QualityTier, float> probs)
        {
            float total = 0f;
            foreach (var kv in probs) total += kv.Value;
            if (total <= 0f || Mathf.Approximately(total, 1f)) return;

            var keys = new List<QualityTier>(probs.Keys);
            foreach (var key in keys)
                probs[key] /= total;
        }

        private static QualityTier RollTier(Dictionary<QualityTier, float> probs)
        {
            float roll = Random.value;
            float cumulative = 0f;

            // Process in order D, C, B, A, S
            QualityTier[] order = { QualityTier.D, QualityTier.C, QualityTier.B, QualityTier.A, QualityTier.S };
            foreach (var tier in order)
            {
                cumulative += probs[tier];
                if (roll < cumulative) return tier;
            }

            return QualityTier.C; // fallback
        }
    }
}
```

**Step 2: Run tests to verify they pass**

Run: Unity EditMode tests
Expected: All 7 HarvestEngine tests PASS.

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/HarvestEngine.cs Assets/Scripts/Services/HarvestEngine.cs.meta
git commit -m "feat: implement HarvestEngine with Sync Shield and special conditions"
```

---

## Task 8: Implement EnvironmentManager

**Files:**
- Create: `Assets/Scripts/Managers/EnvironmentManager.cs`

**Step 1: Create EnvironmentManager.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        private List<EnvironmentData> environments = new();

        public event Action<int> OnEnvironmentUnlocked;

        public IReadOnlyList<EnvironmentData> Environments => environments;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var loaded = Resources.LoadAll<EnvironmentData>("Config/Environments");
            environments.AddRange(loaded);
            // Sort by unlock cost to ensure consistent ordering
            environments.Sort((a, b) => a.unlockCostDewdrops.CompareTo(b.unlockCostDewdrops));
        }

        public bool IsUnlocked(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            if (env.unlockCostDewdrops == 0) return true; // Free = always unlocked
            return SaveManager.Instance.Data.unlockedEnvironments.Contains(env.environmentName);
        }

        public bool Unlock(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            if (IsUnlocked(envIndex)) return false;

            var env = environments[envIndex];
            if (!CurrencyManager.Instance.Spend(CurrencyType.Dewdrops, env.unlockCostDewdrops))
                return false;

            SaveManager.Instance.Data.unlockedEnvironments.Add(env.environmentName);
            SaveManager.Instance.Save();
            OnEnvironmentUnlocked?.Invoke(envIndex);
            return true;
        }

        public float GetGrowthBonus(int envIndex, WeatherData weather)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0f;
            var env = environments[envIndex];
            if (env.bonusCondition == null) return 0f;
            return env.bonusCondition.Evaluate(weather) ? env.growthSpeedBonus : 0f;
        }

        public int GetTotalUnlockedSlots()
        {
            int total = 0;
            for (int i = 0; i < environments.Count; i++)
            {
                if (IsUnlocked(i))
                    total += environments[i].slotCount;
            }
            return total;
        }

        public int GetSlotCount(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0;
            return environments[envIndex].slotCount;
        }
    }
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/EnvironmentManager.cs Assets/Scripts/Managers/EnvironmentManager.cs.meta
git commit -m "feat: add EnvironmentManager for terrarium unlock and bonuses"
```

---

## Task 9: Implement SeedShopManager

**Files:**
- Create: `Assets/Scripts/Managers/SeedShopManager.cs`

**Step 1: Create SeedShopManager.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class SeedShopManager : MonoBehaviour
    {
        public static SeedShopManager Instance { get; private set; }

        public event Action<string> OnSeedPurchased;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public List<SeedData> GetShopSeeds()
        {
            var result = new List<SeedData>();
            foreach (var seed in SeedRegistry.Instance.AllSeeds)
                result.Add(seed);
            return result;
        }

        public bool CanBuy(string seedName)
        {
            var seed = SeedRegistry.Instance.GetSeed(seedName);
            if (seed == null) return false;
            return CurrencyManager.Instance.CanAfford(CurrencyType.Dewdrops, seed.buyPrice);
        }

        public bool BuySeed(string seedName)
        {
            var seed = SeedRegistry.Instance.GetSeed(seedName);
            if (seed == null) return false;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Dewdrops, seed.buyPrice))
                return false;

            SeedRegistry.Instance.AddSeed(seedName);
            OnSeedPurchased?.Invoke(seedName);
            return true;
        }
    }
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/SeedShopManager.cs Assets/Scripts/Managers/SeedShopManager.cs.meta
git commit -m "feat: add SeedShopManager for buying seeds"
```

---

## Task 10: Refactor PlantManager to Multi-Slot

This is the largest single change. PlantManager goes from managing one plant to managing up to 12 across terrarium environments.

**Files:**
- Modify: `Assets/Scripts/Managers/PlantManager.cs`
- Create: `Assets/Scripts/Data/PlantSlot.cs`

**Step 1: Create PlantSlot.cs**

```csharp
using System;

namespace Garden
{
    public class PlantSlot
    {
        public int environmentIndex;
        public int slotIndex;
        public PlantState state = PlantState.Empty;
        public SeedData seed;
        public VariantData variant;
        public DateTime plantTime;
        public float growthSpeedMultiplier = 1f;
        public float growthProgress;
    }
}
```

**Step 2: Rewrite PlantManager for multi-slot**

Replace the full file with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class PlantManager : MonoBehaviour
    {
        public static PlantManager Instance { get; private set; }

        private List<PlantSlot> slots = new();

        // Legacy single-plant compatibility
        public PlantState State => GetFirstSlotInState(PlantState.Mature)?.state
            ?? GetFirstSlotInState(PlantState.Growing)?.state
            ?? PlantState.Empty;
        public SeedData CurrentSeed => GetFeaturedSlot()?.seed;
        public VariantData CurrentVariant => GetFeaturedSlot()?.variant;
        public float GrowthProgress => GetFeaturedSlot()?.growthProgress ?? 0f;
        public float GrowthSpeedMultiplier => GetFeaturedSlot()?.growthSpeedMultiplier ?? 1f;
        public DateTime PlantTime => GetFeaturedSlot()?.plantTime ?? DateTime.MinValue;

        public event Action OnPlantStateChanged;
        public event Action<float> OnGrowthUpdated;
        public event Action<int, int, PlantState> OnSlotStateChanged;
        public event Action<int, int, float> OnSlotGrowthUpdated;

        public IReadOnlyList<PlantSlot> Slots => slots;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            InitializeSlots();
            RestoreFromSave();
        }

        private void Update()
        {
            bool anyUpdated = false;
            foreach (var slot in slots)
            {
                if (slot.state != PlantState.Growing) continue;

                float envBonus = 0f;
                if (EnvironmentManager.Instance != null && WeatherService.Instance != null)
                    envBonus = EnvironmentManager.Instance.GetGrowthBonus(
                        slot.environmentIndex, WeatherService.Instance.CurrentWeather);

                float totalMultiplier = slot.growthSpeedMultiplier + envBonus;
                float totalHours = slot.seed.baseGrowthHours / totalMultiplier;
                float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
                slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);

                // Re-check trigger for dynamic speed
                if (WeatherService.Instance != null && slot.variant.trigger != null)
                {
                    slot.growthSpeedMultiplier = slot.variant.trigger.Evaluate(
                        WeatherService.Instance.CurrentWeather) ? 1.25f : 1f;
                }

                OnSlotGrowthUpdated?.Invoke(slot.environmentIndex, slot.slotIndex, slot.growthProgress);
                anyUpdated = true;

                if (slot.growthProgress >= 1f)
                {
                    slot.state = PlantState.Mature;
                    OnSlotStateChanged?.Invoke(slot.environmentIndex, slot.slotIndex, PlantState.Mature);
                    OnPlantStateChanged?.Invoke();
                    SaveState();
                }
            }

            if (anyUpdated)
            {
                var featured = GetFeaturedSlot();
                if (featured != null)
                    OnGrowthUpdated?.Invoke(featured.growthProgress);
            }
        }

        public PlantSlot GetSlot(int envIndex, int slotIndex)
        {
            return slots.Find(s => s.environmentIndex == envIndex && s.slotIndex == slotIndex);
        }

        public List<PlantSlot> GetSlotsForEnvironment(int envIndex)
        {
            return slots.FindAll(s => s.environmentIndex == envIndex);
        }

        public bool Plant(SeedData seed, int environmentIndex, int slotIndex)
        {
            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Empty) return false;

            var weather = WeatherService.Instance.CurrentWeather;
            var result = GeneticsEngine.Resolve(seed, weather);

            slot.seed = seed;
            slot.variant = result.variant;
            slot.growthSpeedMultiplier = result.growthSpeedMultiplier;
            slot.plantTime = GameTime.UtcNow;
            slot.growthProgress = 0f;
            slot.state = PlantState.Growing;

            var save = SaveManager.Instance.Data;
            if (!save.discoveredVariants.Contains(result.variant.variantName))
                save.discoveredVariants.Add(result.variant.variantName);

            var entry = save.seedInventory.Find(e => e.seedName == seed.seedName);
            if (entry != null) entry.count--;

            OnSlotStateChanged?.Invoke(environmentIndex, slotIndex, PlantState.Growing);
            OnPlantStateChanged?.Invoke();
            SaveState();
            return true;
        }

        // Legacy single-slot plant for backward compat (uses first empty slot)
        public void Plant(SeedData seed)
        {
            foreach (var slot in slots)
            {
                if (slot.state == PlantState.Empty)
                {
                    Plant(seed, slot.environmentIndex, slot.slotIndex);
                    return;
                }
            }
        }

        public HarvestResult Harvest(int environmentIndex, int slotIndex)
        {
            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Mature)
                return default;

            var weather = WeatherService.Instance.CurrentWeather;
            var result = HarvestEngine.Roll(slot.seed, slot.variant, weather);

            // Don't auto-sell or auto-greenhouse — caller decides
            ClearSlot(slot);
            return result;
        }

        // Legacy single-slot harvest (harvests first mature slot, auto-sells)
        public void Harvest()
        {
            var slot = GetFirstSlotInState(PlantState.Mature);
            if (slot == null) return;

            var result = Harvest(slot.environmentIndex, slot.slotIndex);
            // Legacy behavior: auto-add to greenhouse and give dewdrops
            GreenhouseManager.Instance.AddPlant(result.seed, result.variant, result.tier);
            CurrencyManager.Instance.Add(CurrencyType.Dewdrops, result.dewdropValue);
        }

        public void SellHarvest(HarvestResult result)
        {
            CurrencyManager.Instance.Add(CurrencyType.Dewdrops, result.dewdropValue);
        }

        public void KeepHarvest(HarvestResult result)
        {
            GreenhouseManager.Instance.AddPlant(result.seed, result.variant, result.tier);
        }

        public void DebugAdvanceTime(float hours)
        {
            foreach (var slot in slots)
            {
                if (slot.state == PlantState.Growing)
                    slot.plantTime = slot.plantTime.AddHours(-hours);
            }
            SaveState();
        }

        public float GetRemainingHours()
        {
            var slot = GetFeaturedSlot();
            if (slot == null || slot.state != PlantState.Growing) return 0f;
            float totalHours = slot.seed.baseGrowthHours / slot.growthSpeedMultiplier;
            float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }

        public float GetRemainingHours(int envIndex, int slotIndex)
        {
            var slot = GetSlot(envIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Growing) return 0f;
            float totalHours = slot.seed.baseGrowthHours / slot.growthSpeedMultiplier;
            float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }

        public int GetMatureCount()
        {
            int count = 0;
            foreach (var slot in slots)
                if (slot.state == PlantState.Mature) count++;
            return count;
        }

        public int GetGrowingCount()
        {
            int count = 0;
            foreach (var slot in slots)
                if (slot.state == PlantState.Growing) count++;
            return count;
        }

        public PlantSlot GetFirstSlotInState(PlantState state)
        {
            foreach (var slot in slots)
                if (slot.state == state) return slot;
            return null;
        }

        // "Featured" slot = first mature, or first growing, or null
        private PlantSlot GetFeaturedSlot()
        {
            return GetFirstSlotInState(PlantState.Mature)
                ?? GetFirstSlotInState(PlantState.Growing);
        }

        private void InitializeSlots()
        {
            slots.Clear();
            if (EnvironmentManager.Instance == null) return;

            var envs = EnvironmentManager.Instance.Environments;
            for (int e = 0; e < envs.Count; e++)
            {
                for (int s = 0; s < envs[e].slotCount; s++)
                {
                    slots.Add(new PlantSlot
                    {
                        environmentIndex = e,
                        slotIndex = s
                    });
                }
            }
        }

        private void ClearSlot(PlantSlot slot)
        {
            slot.seed = null;
            slot.variant = null;
            slot.growthProgress = 0f;
            slot.state = PlantState.Empty;

            OnSlotStateChanged?.Invoke(slot.environmentIndex, slot.slotIndex, PlantState.Empty);
            OnPlantStateChanged?.Invoke();
            SaveState();
        }

        private void SaveState()
        {
            var save = SaveManager.Instance.Data;
            save.activeSlots.Clear();
            foreach (var slot in slots)
            {
                if (slot.state == PlantState.Empty) continue;
                save.activeSlots.Add(new PlantSlotSave
                {
                    environmentIndex = slot.environmentIndex,
                    slotIndex = slot.slotIndex,
                    seedName = slot.seed.seedName,
                    variantName = slot.variant.variantName,
                    plantTimeUtc = slot.plantTime.ToString("O"),
                    growthSpeedMultiplier = slot.growthSpeedMultiplier
                });
            }
            // Keep legacy activePlant in sync for old save compat
            var featured = GetFeaturedSlot();
            save.activePlant = featured != null
                ? new ActivePlantSave
                {
                    isActive = true,
                    seedName = featured.seed.seedName,
                    variantName = featured.variant.variantName,
                    plantTimeUtc = featured.plantTime.ToString("O"),
                    growthSpeedMultiplier = featured.growthSpeedMultiplier
                }
                : new ActivePlantSave { isActive = false };

            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;

            // Try v2 multi-slot first
            if (save.activeSlots != null && save.activeSlots.Count > 0)
            {
                foreach (var ps in save.activeSlots)
                {
                    var slot = GetSlot(ps.environmentIndex, ps.slotIndex);
                    if (slot == null) continue;

                    var seed = SeedRegistry.Instance.GetSeed(ps.seedName);
                    if (seed == null) continue;
                    VariantData variant = null;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName == ps.variantName) { variant = v; break; }
                    }
                    if (variant == null) continue;

                    slot.seed = seed;
                    slot.variant = variant;
                    slot.plantTime = DateTime.Parse(ps.plantTimeUtc).ToUniversalTime();
                    slot.growthSpeedMultiplier = ps.growthSpeedMultiplier;

                    float totalHours = seed.baseGrowthHours / slot.growthSpeedMultiplier;
                    float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
                    slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);
                    slot.state = slot.growthProgress >= 1f ? PlantState.Mature : PlantState.Growing;
                }
                OnPlantStateChanged?.Invoke();
                return;
            }

            // Fallback: legacy single-plant restore into first slot
            if (save.activePlant != null && save.activePlant.isActive && slots.Count > 0)
            {
                var slot = slots[0];
                var seeds = Resources.LoadAll<SeedData>("Seeds");
                foreach (var seed in seeds)
                {
                    if (seed.seedName != save.activePlant.seedName) continue;
                    slot.seed = seed;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName == save.activePlant.variantName)
                        { slot.variant = v; break; }
                    }
                    break;
                }
                if (slot.seed != null && slot.variant != null)
                {
                    slot.plantTime = DateTime.Parse(save.activePlant.plantTimeUtc).ToUniversalTime();
                    slot.growthSpeedMultiplier = save.activePlant.growthSpeedMultiplier;
                    float totalHours = slot.seed.baseGrowthHours / slot.growthSpeedMultiplier;
                    float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
                    slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);
                    slot.state = slot.growthProgress >= 1f ? PlantState.Mature : PlantState.Growing;
                }
                OnPlantStateChanged?.Invoke();
            }
        }
    }
}
```

**Step 3: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 4: Commit**

```bash
git add Assets/Scripts/Data/PlantSlot.cs Assets/Scripts/Data/PlantSlot.cs.meta Assets/Scripts/Managers/PlantManager.cs
git commit -m "feat: refactor PlantManager to multi-slot with PlantSlot model"
```

---

## Task 11: Extend GreenhouseManager for QualityTier

**Files:**
- Modify: `Assets/Scripts/Managers/GreenhouseManager.cs`

**Step 1: Update GreenhouseManager**

Key changes:
- `AddPlant` gains `QualityTier` parameter
- `GreenhousePlant` gains `QualityTier` field
- Dust calculation uses quality multiplier
- Add `SellPlant` method
- Save/Restore handles qualityTier

Replace the full file:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class GreenhouseManager : MonoBehaviour
    {
        public static GreenhouseManager Instance { get; private set; }

        public List<GreenhousePlant> Plants { get; private set; } = new();
        public int MaxSlots => SaveManager.Instance.Data.greenhouseSlots;

        public event Action OnGreenhouseChanged;

        private float dustAccumulator;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            RestoreFromSave();
        }

        private void Update()
        {
            if (Plants.Count == 0) return;

            dustAccumulator += Time.deltaTime;
            if (dustAccumulator >= 3600f)
            {
                dustAccumulator -= 3600f;
                int totalDust = Mathf.RoundToInt(GetTotalDustPerHour());
                if (totalDust > 0)
                    CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);
            }
        }

        public bool AddPlant(SeedData seed, VariantData variant, QualityTier tier = QualityTier.C)
        {
            if (Plants.Count >= MaxSlots) return false;

            Plants.Add(new GreenhousePlant
            {
                seedName = seed.seedName,
                variantName = variant.variantName,
                rarity = variant.rarity,
                qualityTier = tier,
                primaryColor = variant.primaryColor,
                harvestTime = GameTime.UtcNow
            });

            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        // Legacy overload for backward compatibility
        public bool AddPlant(SeedData seed, VariantData variant)
        {
            return AddPlant(seed, variant, QualityTier.C);
        }

        public int SellPlant(int index)
        {
            if (index < 0 || index >= Plants.Count) return 0;

            var plant = Plants[index];
            // Look up seed for baseSellPrice
            var seed = SeedRegistry.Instance.GetSeed(plant.seedName);
            int baseSell = seed != null ? seed.baseSellPrice : 100;
            int value = CurrencyManager.Instance.Config.GetSellValue(baseSell, plant.qualityTier);

            Plants.RemoveAt(index);
            CurrencyManager.Instance.Add(CurrencyType.Dewdrops, value);

            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
            return value;
        }

        public bool ExpandSlots()
        {
            var config = CurrencyManager.Instance.Config;
            if (!CurrencyManager.Instance.Spend(CurrencyType.SunShards, config.slotCostSunShards))
                return false;
            SaveManager.Instance.Data.greenhouseSlots++;
            SaveManager.Instance.Save();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        public float GetTotalDustPerHour()
        {
            float total = 0;
            var config = CurrencyManager.Instance.Config;
            foreach (var p in Plants)
                total += config.GetDustPerHourForPlant(p.rarity, p.qualityTier);
            return total;
        }

        public void DebugAdvanceTime(float hours)
        {
            if (Plants.Count == 0) return;
            int totalDust = Mathf.RoundToInt(GetTotalDustPerHour() * hours);
            if (totalDust > 0)
                CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);
        }

        private void SaveGreenhouse()
        {
            var save = SaveManager.Instance.Data;
            save.greenhousePlants.Clear();
            foreach (var p in Plants)
            {
                save.greenhousePlants.Add(new GreenhousePlantSave
                {
                    seedName = p.seedName,
                    variantName = p.variantName,
                    harvestTimeUtc = p.harvestTime.ToString("O"),
                    qualityTier = p.qualityTier
                });
            }
            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;
            Plants.Clear();

            var allSeeds = Resources.LoadAll<SeedData>("Seeds");
            foreach (var ps in save.greenhousePlants)
            {
                Rarity rarity = Rarity.Common;
                Color color = Color.green;
                foreach (var seed in allSeeds)
                {
                    if (seed.seedName != ps.seedName) continue;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName != ps.variantName) continue;
                        rarity = v.rarity;
                        color = v.primaryColor;
                        break;
                    }
                    break;
                }

                Plants.Add(new GreenhousePlant
                {
                    seedName = ps.seedName,
                    variantName = ps.variantName,
                    rarity = rarity,
                    qualityTier = ps.qualityTier,
                    primaryColor = color,
                    harvestTime = DateTime.Parse(ps.harvestTimeUtc).ToUniversalTime()
                });
            }
        }
    }

    public class GreenhousePlant
    {
        public string seedName;
        public string variantName;
        public Rarity rarity;
        public QualityTier qualityTier;
        public Color primaryColor;
        public DateTime harvestTime;
    }
}
```

**Step 2: Verify compilation**

Run Unity console check. Expected: no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/GreenhouseManager.cs
git commit -m "feat: extend GreenhouseManager with QualityTier and SellPlant"
```

---

## Task 12: Update GameManager for New Starter Flow

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs`

**Step 1: Update GameManager to unlock Hearth by default and give starter seeds**

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
        }

        private void Start()
        {
            if (SaveManager.Instance.Data.seedInventory.Count == 0)
            {
                SeedRegistry.Instance.AddSeed("Astra", 5);
                SaveManager.Instance.Data.sunShards = 10;
                SaveManager.Instance.Data.dewdrops = 200;
                SaveManager.Instance.Save();
            }
        }
    }
}
```

**Step 2: Verify compilation and commit**

```bash
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: update GameManager starter resources"
```

---

## Task 13: Create Environment Data Assets

**Files:**
- Create: `Assets/Resources/Config/Environments/` (folder)
- Create: 4 EnvironmentData `.asset` files via Unity MCP

**Step 1: Create the Environments folder**

Use Unity MCP `manage_asset` to create the folder, then create 4 EnvironmentData assets:

1. **The Hearth** — 2 slots, 0 cost, +10% growth in temperate (15-25°C)
2. **The Balcony** — 2 slots, 5000 cost, +0% (wind/rain boost handled at seed level)
3. **The Wild Patch** — 4 slots, 15000 cost, +0%
4. **Deep Conservatory** — 4 slots, 25000 cost, allows cross-pollination

Create these via Unity's `manage_asset(action="create")` or via a setup script. Configure each asset's fields in the Unity Inspector or via `manage_scriptable_object`.

**Step 2: Commit**

```bash
git add Assets/Resources/Config/Environments/
git commit -m "feat: add 4 terrarium environment data assets"
```

---

## Task 14: Create 4 New Seed Data Assets

**Files:**
- Create: `Assets/Resources/Seeds/CinderFern.asset`
- Create: `Assets/Resources/Seeds/MistVine.asset`
- Create: `Assets/Resources/Seeds/LunaPetal.asset`
- Create: `Assets/Resources/Seeds/StormRoot.asset`

**Step 1: Create SeedData assets via Unity MCP**

For each seed, create a ScriptableObject of type SeedData with these values:

| Seed | seedName | buyPrice | baseSellPrice | baseGrowthHours | preferredWeather |
|------|----------|----------|---------------|-----------------|------------------|
| Cinder-Fern | Cinder-Fern | 450 | 550 | 12 | Clear |
| Mist-Vine | Mist-Vine | 800 | 1000 | 18 | Rain |
| Luna-Petal | Luna-Petal | 1500 | 1900 | 24 | Clear |
| Storm-Root | Storm-Root | 3000 | 4000 | 36 | Storm |

Also update the existing Astra asset: `buyPrice=100`, `baseSellPrice=120`.

Special conditions:
- Cinder-Fern: +10% S-Tier when Temp > 25°C
- Mist-Vine: +10% S-Tier when Humidity > 70%
- Luna-Petal: (only grows at night — enforce via all variants having night trigger or growth check)
- Storm-Root: +20% S-Tier during Storm/Rain weather

**Step 2: Commit**

```bash
git add Assets/Resources/Seeds/
git commit -m "feat: add 4 new seed data assets with shop pricing"
```

---

## Task 15: Create 48 New Variant Data Assets

**Files:**
- Create: `Assets/Resources/Variants/CinderFern/` (12 variants)
- Create: `Assets/Resources/Variants/MistVine/` (12 variants)
- Create: `Assets/Resources/Variants/LunaPetal/` (12 variants)
- Create: `Assets/Resources/Variants/StormRoot/` (12 variants)

Each seed gets 12 variants following the Astra pattern:

| # | Trigger | Priority | Rarity |
|---|---------|----------|--------|
| 1 | Base (default) | 4 | Common |
| 2 | Frost (Temp < 5°C) | 2 | Rare |
| 3 | Desert (Temp > 38°C) | 2 | Rare |
| 4 | Dew (Humidity > 80%) | 3 | Uncommon |
| 5 | Tempest (High Wind > 15 m/s) | 2 | Epic |
| 6 | Lunar (Night + Clear) | 3 | Uncommon |
| 7 | Solar (Day + Clear) | 3 | Common |
| 8 | Celestial (Equinox/Eclipse) | 1 | Legendary |
| 9 | Biolume (New Moon) | 3 | Uncommon |
| 10 | Nebula (Golden Hour) | 3 | Uncommon |
| 11 | Static (Thunderstorm) | 2 | Epic |
| 12 | Void (New Moon + Night) | 2 | Epic |

**Naming convention per seed:**
- Cinder-Fern: "Ember-Glass Fern", "Magma Fern", "Inferno Fern", etc.
- Mist-Vine: "Fog-Weave Vine", "Dew-Drop Vine", "Tempest Vine", etc.
- Luna-Petal: "Frost-Moon Petal", "Blaze Petal", "Eclipse Petal", etc.
- Storm-Root: "Frozen Root", "Scorched Root", "Thunder Root", etc.

Create via Unity MCP `manage_asset(action="create", asset_type="VariantData")` or via a batch creation script. Configure trigger conditions, colors (themed per seed), and link variants to their seed's variant list.

**Step 2: Link variants to SeedData assets**

Each seed's `variants` list must reference its 12 VariantData assets.

**Step 3: Commit**

```bash
git add Assets/Resources/Variants/
git commit -m "feat: add 48 variant assets for 4 new seeds"
```

---

## Task 16: Create UI Templates and Styles

**Files:**
- Create: `Assets/Resources/UI/Templates/SeedShopCard.uxml`
- Create: `Assets/Resources/UI/Templates/HarvestResultPopup.uxml`
- Create: `Assets/Resources/UI/Templates/EnvironmentSection.uxml`
- Create: `Assets/Resources/UI/Templates/TerrariumSlot.uxml`
- Create: `Assets/UI/Styles/SeedShop.uss`
- Create: `Assets/UI/Styles/HarvestResult.uss`
- Create: `Assets/UI/Styles/Terrarium.uss`

**Step 1: Create SeedShopCard.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="grid-item shop-card">
        <ui:VisualElement class="shop-icon" />
        <ui:Label class="shop-seed-name" />
        <ui:Label class="shop-price" />
        <ui:Label class="shop-condition" />
        <ui:Button text="Buy" class="btn shop-buy-btn" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 2: Create HarvestResultPopup.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="harvest-overlay">
        <ui:VisualElement class="harvest-card">
            <ui:VisualElement class="harvest-swatch" />
            <ui:Label class="harvest-variant-name" />
            <ui:Label class="harvest-tier-label" />
            <ui:Label class="harvest-sync-badge" />
            <ui:Label class="harvest-dewdrops" />
            <ui:VisualElement class="harvest-buttons">
                <ui:Button text="Sell" class="btn harvest-sell-btn" />
                <ui:Button text="Keep" class="btn harvest-keep-btn" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 3: Create EnvironmentSection.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="environment-section">
        <ui:VisualElement class="environment-header">
            <ui:Label class="environment-name" />
            <ui:Label class="environment-slots-text" />
        </ui:VisualElement>
        <ui:VisualElement class="environment-slot-grid" />
        <ui:VisualElement class="environment-locked">
            <ui:Label class="environment-cost" />
            <ui:Button text="Unlock" class="btn environment-unlock-btn" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 4: Create TerrariumSlot.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:Button class="grid-item terrarium-slot">
        <ui:VisualElement class="terrarium-swatch" />
        <ui:Label class="terrarium-slot-label" />
        <ui:VisualElement class="terrarium-progress-bar">
            <ui:VisualElement class="terrarium-progress-fill" />
        </ui:VisualElement>
    </ui:Button>
</ui:UXML>
```

**Step 5: Create SeedShop.uss**

```css
.shop-card {
    width: 45%;
    padding: var(--spacing-md);
    align-items: center;
}

.shop-icon {
    width: 96px;
    height: 96px;
    border-radius: var(--radius-sm);
    background-color: var(--color-bg-slot);
    margin-bottom: var(--spacing-sm);
}

.shop-seed-name {
    font-size: var(--font-md);
    color: var(--color-text-bright);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-xs);
}

.shop-price {
    font-size: var(--font-sm);
    color: var(--color-text-accent);
    margin-bottom: var(--spacing-xs);
}

.shop-condition {
    font-size: var(--font-xs);
    color: var(--color-text-dim);
    -unity-text-align: middle-center;
    margin-bottom: var(--spacing-sm);
    white-space: normal;
}

.shop-buy-btn {
    min-height: 64px;
    width: 100%;
}
```

**Step 6: Create HarvestResult.uss**

```css
.harvest-overlay {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.7);
    align-items: center;
    justify-content: center;
}

.harvest-card {
    background-color: var(--color-bg-panel);
    border-width: 2px;
    border-color: var(--color-border-accent);
    border-radius: var(--radius-lg);
    padding: var(--spacing-xl);
    align-items: center;
    min-width: 500px;
}

.harvest-swatch {
    width: 160px;
    height: 160px;
    border-radius: var(--radius-md);
    margin-bottom: var(--spacing-md);
}

.harvest-variant-name {
    font-size: var(--font-lg);
    color: var(--color-text-bright);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-sm);
}

.harvest-tier-label {
    font-size: var(--font-xxl);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-sm);
}

.harvest-sync-badge {
    font-size: var(--font-sm);
    color: var(--color-text-accent);
    margin-bottom: var(--spacing-sm);
}

.harvest-dewdrops {
    font-size: var(--font-lg);
    color: var(--color-highlight);
    margin-bottom: var(--spacing-lg);
}

.harvest-buttons {
    flex-direction: row;
    justify-content: center;
}

.harvest-sell-btn {
    margin-right: var(--spacing-md);
    min-width: 200px;
}

.harvest-keep-btn {
    min-width: 200px;
}

/* Quality tier colors */
.tier-d { color: rgb(150, 150, 150); }
.tier-c { color: rgb(100, 200, 120); }
.tier-b { color: rgb(80, 160, 255); }
.tier-a { color: rgb(255, 200, 60); }
.tier-s { color: rgb(200, 100, 255); }
```

**Step 7: Create Terrarium.uss**

```css
.environment-section {
    margin-bottom: var(--spacing-lg);
    padding: var(--spacing-md);
    background-color: var(--color-bg-slot);
    border-radius: var(--radius-md);
    border-width: 1px;
    border-color: var(--color-border);
}

.environment-header {
    flex-direction: row;
    justify-content: space-between;
    margin-bottom: var(--spacing-sm);
}

.environment-name {
    font-size: var(--font-md);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

.environment-slots-text {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
}

.environment-slot-grid {
    flex-direction: row;
    flex-wrap: wrap;
}

.environment-locked {
    align-items: center;
    padding: var(--spacing-md);
}

.environment-cost {
    font-size: var(--font-sm);
    color: var(--color-text-accent);
    margin-bottom: var(--spacing-sm);
}

.environment-unlock-btn {
    min-width: 200px;
}

.terrarium-slot {
    width: 23%;
    aspect-ratio: 1;
    align-items: center;
    justify-content: center;
    padding: var(--spacing-xs);
}

.terrarium-swatch {
    width: 64px;
    height: 64px;
    border-radius: var(--radius-sm);
    margin-bottom: var(--spacing-xs);
}

.terrarium-slot-label {
    font-size: var(--font-xs);
    color: var(--color-text);
    -unity-text-align: middle-center;
}

.terrarium-progress-bar {
    width: 100%;
    height: 8px;
    background-color: var(--color-bg-slot);
    border-radius: 4px;
    margin-top: var(--spacing-xs);
}

.terrarium-progress-fill {
    height: 100%;
    background-color: var(--color-text-accent);
    border-radius: 4px;
    width: 0%;
}
```

**Step 8: Commit**

```bash
git add Assets/Resources/UI/Templates/SeedShopCard.uxml Assets/Resources/UI/Templates/SeedShopCard.uxml.meta \
    Assets/Resources/UI/Templates/HarvestResultPopup.uxml Assets/Resources/UI/Templates/HarvestResultPopup.uxml.meta \
    Assets/Resources/UI/Templates/EnvironmentSection.uxml Assets/Resources/UI/Templates/EnvironmentSection.uxml.meta \
    Assets/Resources/UI/Templates/TerrariumSlot.uxml Assets/Resources/UI/Templates/TerrariumSlot.uxml.meta \
    Assets/UI/Styles/SeedShop.uss Assets/UI/Styles/SeedShop.uss.meta \
    Assets/UI/Styles/HarvestResult.uss Assets/UI/Styles/HarvestResult.uss.meta \
    Assets/UI/Styles/Terrarium.uss Assets/UI/Styles/Terrarium.uss.meta
git commit -m "feat: add UI templates and styles for shop, harvest, and terrarium"
```

---

## Task 17: Update GardenRoot.uxml with New Panels

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`

**Step 1: Add style imports for new USS files**

Add after the existing Style imports (line 10):

```xml
<Style src="../Styles/SeedShop.uss" />
<Style src="../Styles/HarvestResult.uss" />
<Style src="../Styles/Terrarium.uss" />
```

**Step 2: Add Shop nav button**

Add to the nav-bar element, after the greenhouse-button:

```xml
<ui:Button name="shop-button" text="Shop" class="nav-btn" />
```

**Step 3: Add Shop Panel**

Add after the Greenhouse panel:

```xml
<!-- Shop Panel (hidden by default) -->
<ui:VisualElement name="shop-panel" class="panel" style="display: none;">
    <ui:Label text="Seed Shop" class="panel-header" />
    <ui:Button name="shop-close" text="X" class="btn btn-close" />
    <ui:ScrollView name="shop-grid" class="scroll-view" />
</ui:VisualElement>
```

**Step 4: Replace Greenhouse Panel with Terrarium Panel**

Replace the greenhouse-panel section with:

```xml
<!-- Terrarium Panel (replaces Greenhouse, hidden by default) -->
<ui:VisualElement name="terrarium-panel" class="panel" style="display: none;">
    <ui:Label text="Terrarium" class="panel-header" />
    <ui:Button name="terrarium-close" text="X" class="btn btn-close" />
    <ui:VisualElement name="terrarium-header">
        <ui:Label name="dust-rate" text="+0 Aura Dust/hr" />
        <ui:Label name="terrarium-slots-text" text="0 / 0" />
    </ui:VisualElement>
    <ui:ScrollView name="terrarium-scroll" class="scroll-view" />
</ui:VisualElement>
```

**Step 5: Add Harvest Popup container**

Add at the very end, before the closing `</ui:UXML>`:

```xml
<!-- Harvest Result Popup (hidden by default) -->
<ui:VisualElement name="harvest-popup" style="display: none;" />
```

**Step 6: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml
git commit -m "feat: update GardenRoot.uxml with shop, terrarium, and harvest popup"
```

---

## Task 18: Implement SeedShopUI

**Files:**
- Create: `Assets/Scripts/UI/SeedShopUI.cs`

**Step 1: Create SeedShopUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SeedShopUI : MonoBehaviour
    {
        private VisualTreeAsset shopCardTemplate;

        private VisualElement panel;
        private ScrollView shopGrid;
        private Button closeButton;

        public void Initialize(VisualElement root)
        {
            shopCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedShopCard");

            panel = root.Q<VisualElement>("shop-panel");
            shopGrid = root.Q<ScrollView>("shop-grid");
            closeButton = root.Q<Button>("shop-close");

            closeButton.clicked += Hide;
        }

        public void Show()
        {
            panel.style.display = DisplayStyle.Flex;
            RefreshDisplay();
        }

        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        private void RefreshDisplay()
        {
            shopGrid.Clear();

            var seeds = SeedShopManager.Instance.GetShopSeeds();
            foreach (var seed in seeds)
            {
                var card = shopCardTemplate.CloneTree();

                var nameLabel = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var conditionLabel = card.Q<Label>(className: "shop-condition");
                var icon = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn = card.Q<Button>(className: "shop-buy-btn");

                if (nameLabel != null) nameLabel.text = seed.seedName;
                if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dew";
                if (conditionLabel != null) conditionLabel.text = seed.description ?? "";
                if (icon != null && seed.icon != null)
                    icon.style.backgroundImage = new StyleBackground(seed.icon);

                int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                if (nameLabel != null) nameLabel.text = $"{seed.seedName} (x{owned})";

                if (buyBtn != null)
                {
                    bool canBuy = SeedShopManager.Instance.CanBuy(seed.seedName);
                    buyBtn.SetEnabled(canBuy);
                    buyBtn.text = $"Buy ({seed.buyPrice})";

                    var seedName = seed.seedName;
                    buyBtn.clicked += () =>
                    {
                        if (SeedShopManager.Instance.BuySeed(seedName))
                            RefreshDisplay();
                    };
                }

                shopGrid.Add(card);
            }
        }
    }
}
```

**Step 2: Verify compilation and commit**

```bash
git add Assets/Scripts/UI/SeedShopUI.cs Assets/Scripts/UI/SeedShopUI.cs.meta
git commit -m "feat: add SeedShopUI controller"
```

---

## Task 19: Implement HarvestResultUI

**Files:**
- Create: `Assets/Scripts/UI/HarvestResultUI.cs`

**Step 1: Create HarvestResultUI.cs**

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HarvestResultUI : MonoBehaviour
    {
        private VisualTreeAsset popupTemplate;

        private VisualElement popupContainer;
        private VisualElement root;

        public event Action OnDismissed;

        private HarvestResult currentResult;

        public void Initialize(VisualElement root)
        {
            this.root = root;
            popupTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/HarvestResultPopup");
            popupContainer = root.Q<VisualElement>("harvest-popup");
        }

        public void Show(HarvestResult result)
        {
            currentResult = result;
            popupContainer.Clear();

            var popup = popupTemplate.CloneTree();

            var swatch = popup.Q<VisualElement>(className: "harvest-swatch");
            var variantName = popup.Q<Label>(className: "harvest-variant-name");
            var tierLabel = popup.Q<Label>(className: "harvest-tier-label");
            var syncBadge = popup.Q<Label>(className: "harvest-sync-badge");
            var dewdropsLabel = popup.Q<Label>(className: "harvest-dewdrops");
            var sellBtn = popup.Q<Button>(className: "harvest-sell-btn");
            var keepBtn = popup.Q<Button>(className: "harvest-keep-btn");

            if (swatch != null && result.variant != null)
                swatch.style.backgroundColor = result.variant.primaryColor;

            if (variantName != null)
                variantName.text = result.variant?.variantName ?? "Unknown";

            if (tierLabel != null)
            {
                tierLabel.text = $"{result.tier} - {CurrencyConfig.GetQualityLabel(result.tier)}";
                tierLabel.RemoveFromClassList("tier-d");
                tierLabel.RemoveFromClassList("tier-c");
                tierLabel.RemoveFromClassList("tier-b");
                tierLabel.RemoveFromClassList("tier-a");
                tierLabel.RemoveFromClassList("tier-s");
                tierLabel.AddToClassList($"tier-{result.tier.ToString().ToLower()}");
            }

            if (syncBadge != null)
            {
                syncBadge.text = result.syncShieldActive ? "Weather Sync!" : "";
                syncBadge.style.display = result.syncShieldActive
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (dewdropsLabel != null)
                dewdropsLabel.text = $"+{result.dewdropValue} Dewdrops";

            if (sellBtn != null)
            {
                sellBtn.clicked += () =>
                {
                    PlantManager.Instance.SellHarvest(currentResult);
                    Dismiss();
                };
            }

            if (keepBtn != null)
            {
                bool canKeep = GreenhouseManager.Instance.Plants.Count
                    < GreenhouseManager.Instance.MaxSlots;
                keepBtn.SetEnabled(canKeep);
                if (!canKeep) keepBtn.text = "Full";

                keepBtn.clicked += () =>
                {
                    PlantManager.Instance.KeepHarvest(currentResult);
                    Dismiss();
                };
            }

            popupContainer.Add(popup);
            popupContainer.style.display = DisplayStyle.Flex;
        }

        private void Dismiss()
        {
            popupContainer.Clear();
            popupContainer.style.display = DisplayStyle.None;
            OnDismissed?.Invoke();
        }
    }
}
```

**Step 2: Verify compilation and commit**

```bash
git add Assets/Scripts/UI/HarvestResultUI.cs Assets/Scripts/UI/HarvestResultUI.cs.meta
git commit -m "feat: add HarvestResultUI with sell/keep choice"
```

---

## Task 20: Implement TerrariumUI (Replaces GreenhouseUI)

**Files:**
- Modify: `Assets/Scripts/UI/GreenhouseUI.cs` → Repurpose as TerrariumUI
- Or Create: `Assets/Scripts/UI/TerrariumUI.cs`

**Step 1: Create TerrariumUI.cs**

This replaces the old greenhouse panel with the terrarium view showing 4 environment sections.

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class TerrariumUI : MonoBehaviour
    {
        private VisualTreeAsset environmentTemplate;
        private VisualTreeAsset slotTemplate;

        private VisualElement panel;
        private ScrollView terrariumScroll;
        private Label dustRateText;
        private Label slotsText;
        private Button closeButton;

        // Callback for when user taps an empty slot (opens satchel targeting that slot)
        public System.Action<int, int> OnEmptySlotTapped;
        // Callback for when a mature slot is tapped
        public System.Action<int, int> OnMatureSlotTapped;

        public void Initialize(VisualElement root)
        {
            environmentTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/EnvironmentSection");
            slotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/TerrariumSlot");

            panel = root.Q<VisualElement>("terrarium-panel");
            terrariumScroll = root.Q<ScrollView>("terrarium-scroll");
            dustRateText = root.Q<Label>("dust-rate");
            slotsText = root.Q<Label>("terrarium-slots-text");
            closeButton = root.Q<Button>("terrarium-close");

            closeButton.clicked += Hide;
        }

        public void Show()
        {
            panel.style.display = DisplayStyle.Flex;
            RefreshDisplay();
        }

        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        public void RefreshDisplay()
        {
            terrariumScroll.Clear();

            var em = EnvironmentManager.Instance;
            var pm = PlantManager.Instance;
            var gm = GreenhouseManager.Instance;

            if (em == null || pm == null) return;

            dustRateText.text = gm != null
                ? $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr"
                : "+0 Aura Dust/hr";

            int totalSlots = em.GetTotalUnlockedSlots();
            int usedSlots = pm.GetGrowingCount() + pm.GetMatureCount();
            slotsText.text = $"{usedSlots} / {totalSlots}";

            for (int e = 0; e < em.Environments.Count; e++)
            {
                var env = em.Environments[e];
                var section = environmentTemplate.CloneTree();

                var nameLabel = section.Q<Label>(className: "environment-name");
                var envSlotsText = section.Q<Label>(className: "environment-slots-text");
                var slotGrid = section.Q<VisualElement>(className: "environment-slot-grid");
                var lockedSection = section.Q<VisualElement>(className: "environment-locked");
                var costLabel = section.Q<Label>(className: "environment-cost");
                var unlockBtn = section.Q<Button>(className: "environment-unlock-btn");

                if (nameLabel != null) nameLabel.text = env.environmentName;

                bool unlocked = em.IsUnlocked(e);

                if (unlocked)
                {
                    if (lockedSection != null) lockedSection.style.display = DisplayStyle.None;

                    var envSlots = pm.GetSlotsForEnvironment(e);
                    int active = 0;
                    foreach (var s in envSlots)
                        if (s.state != PlantState.Empty) active++;
                    if (envSlotsText != null) envSlotsText.text = $"{active} / {env.slotCount}";

                    foreach (var slot in envSlots)
                    {
                        var slotEl = slotTemplate.CloneTree();
                        var swatch = slotEl.Q<VisualElement>(className: "terrarium-swatch");
                        var label = slotEl.Q<Label>(className: "terrarium-slot-label");
                        var progressFill = slotEl.Q<VisualElement>(className: "terrarium-progress-fill");
                        var btn = slotEl.Q<Button>(className: "terrarium-slot");

                        int envIdx = slot.environmentIndex;
                        int slotIdx = slot.slotIndex;

                        switch (slot.state)
                        {
                            case PlantState.Empty:
                                if (swatch != null) swatch.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                                if (label != null) label.text = "Empty";
                                if (progressFill != null) progressFill.style.width = new Length(0, LengthUnit.Percent);
                                if (btn != null) btn.clicked += () => OnEmptySlotTapped?.Invoke(envIdx, slotIdx);
                                break;

                            case PlantState.Growing:
                                if (swatch != null && slot.variant != null)
                                    swatch.style.backgroundColor = slot.variant.primaryColor;
                                float hours = pm.GetRemainingHours(envIdx, slotIdx);
                                if (label != null)
                                    label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                                if (progressFill != null)
                                    progressFill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                                break;

                            case PlantState.Mature:
                                if (swatch != null && slot.variant != null)
                                    swatch.style.backgroundColor = slot.variant.primaryColor;
                                if (label != null) label.text = "Harvest!";
                                if (progressFill != null) progressFill.style.width = new Length(100, LengthUnit.Percent);
                                if (btn != null) btn.clicked += () => OnMatureSlotTapped?.Invoke(envIdx, slotIdx);
                                break;
                        }

                        slotGrid.Add(slotEl);
                    }
                }
                else
                {
                    if (slotGrid != null) slotGrid.style.display = DisplayStyle.None;
                    if (envSlotsText != null) envSlotsText.text = "Locked";
                    if (costLabel != null) costLabel.text = $"{env.unlockCostDewdrops} Dewdrops to unlock";

                    int envIndex = e;
                    if (unlockBtn != null)
                    {
                        unlockBtn.SetEnabled(CurrencyManager.Instance.CanAfford(
                            CurrencyType.Dewdrops, env.unlockCostDewdrops));
                        unlockBtn.clicked += () =>
                        {
                            if (em.Unlock(envIndex))
                                RefreshDisplay();
                        };
                    }
                }

                terrariumScroll.Add(section);
            }
        }
    }
}
```

**Step 2: Verify compilation and commit**

```bash
git add Assets/Scripts/UI/TerrariumUI.cs Assets/Scripts/UI/TerrariumUI.cs.meta
git commit -m "feat: add TerrariumUI with environment sections and slot management"
```

---

## Task 21: Update SatchelUI for Slot Targeting

**Files:**
- Modify: `Assets/Scripts/UI/SatchelUI.cs`

**Step 1: Add target slot tracking**

The satchel needs to know which slot the player wants to plant into. Add target slot fields and modify `OnPlant` to use the multi-slot API:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SatchelUI : MonoBehaviour
    {
        private VisualTreeAsset seedSlotTemplate;
        private VisualTreeAsset probabilityEntryTemplate;

        private VisualElement panel;
        private ScrollView seedGrid;
        private VisualElement probabilityPanel;
        private ScrollView probabilityGrid;
        private Button plantButton;
        private Button closeButton;
        private Label selectedSeedName;

        private SeedData selectedSeed;

        // Target slot for multi-slot planting
        private int targetEnvIndex = -1;
        private int targetSlotIndex = -1;

        public void Initialize(VisualElement root)
        {
            seedSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedSlot");
            probabilityEntryTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/ProbabilityEntry");

            panel = root.Q<VisualElement>("satchel-panel");
            seedGrid = root.Q<ScrollView>("seed-grid");
            probabilityPanel = root.Q<VisualElement>("probability-panel");
            probabilityGrid = root.Q<ScrollView>("probability-grid");
            plantButton = root.Q<Button>("plant-button");
            closeButton = root.Q<Button>("satchel-close");
            selectedSeedName = root.Q<Label>("selected-seed-name");

            plantButton.clicked += OnPlant;
            closeButton.clicked += Hide;
        }

        public void Show()
        {
            Show(-1, -1);
        }

        public void Show(int envIndex, int slotIndex)
        {
            targetEnvIndex = envIndex;
            targetSlotIndex = slotIndex;
            panel.style.display = DisplayStyle.Flex;
            RefreshGrid();
            probabilityPanel.style.display = DisplayStyle.None;
            plantButton.SetEnabled(false);
            selectedSeed = null;
        }

        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        private void RefreshGrid()
        {
            seedGrid.Clear();

            var seeds = SeedRegistry.Instance.GetOwnedSeeds();
            foreach (var seed in seeds)
            {
                int count = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                var slot = SeedSlotUI.Create(seedSlotTemplate, seed, count, OnSeedSelected);
                seedGrid.Add(slot);
            }
        }

        private void OnSeedSelected(SeedData seed)
        {
            selectedSeed = seed;
            selectedSeedName.text = seed.seedName;
            plantButton.SetEnabled(true);
            ShowProbabilities(seed);
        }

        private void ShowProbabilities(SeedData seed)
        {
            probabilityPanel.style.display = DisplayStyle.Flex;
            probabilityGrid.Clear();

            var weather = WeatherService.Instance.CurrentWeather;
            var probs = GeneticsEngine.GetProbabilities(seed, weather);

            foreach (var (variant, isHigh) in probs)
            {
                var entry = probabilityEntryTemplate.CloneTree();
                var nameLabel = entry.Q<Label>(className: "probability-name");
                if (nameLabel != null)
                {
                    nameLabel.text = variant.variantName;
                    nameLabel.style.color = isHigh ? Color.yellow : Color.gray;
                }
                probabilityGrid.Add(entry);
            }
        }

        private void OnPlant()
        {
            if (selectedSeed == null) return;

            if (targetEnvIndex >= 0 && targetSlotIndex >= 0)
            {
                PlantManager.Instance.Plant(selectedSeed, targetEnvIndex, targetSlotIndex);
            }
            else
            {
                PlantManager.Instance.Plant(selectedSeed);
            }
            Hide();
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/SatchelUI.cs
git commit -m "feat: update SatchelUI with target slot for multi-slot planting"
```

---

## Task 22: Update PulseButton for Multi-Slot

**Files:**
- Modify: `Assets/Scripts/UI/PulseButton.cs`

**Step 1: Update PulseButton to show multi-slot status**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class PulseButton : MonoBehaviour
    {
        public event System.Action OnPulse;

        private Button button;

        public void Initialize(VisualElement root)
        {
            button = root.Q<Button>("pulse-button");
            button.clicked += HandleClick;
        }

        private void Start()
        {
            UpdateState();
            if (PlantManager.Instance != null)
                PlantManager.Instance.OnPlantStateChanged += UpdateState;
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
                PlantManager.Instance.OnPlantStateChanged -= UpdateState;
        }

        private void Update()
        {
            if (button == null || PlantManager.Instance == null) return;

            int mature = PlantManager.Instance.GetMatureCount();
            int growing = PlantManager.Instance.GetGrowingCount();

            if (mature > 0)
            {
                button.text = $"{mature} plant{(mature > 1 ? "s" : "")} ready!";
            }
            else if (growing > 0)
            {
                float hours = PlantManager.Instance.GetRemainingHours();
                if (hours > 1f)
                    button.text = $"{growing} growing \u2022 {hours:F1}h";
                else
                    button.text = $"{growing} growing \u2022 {hours * 60f:F0}m";
            }
        }

        private void HandleClick()
        {
            var pm = PlantManager.Instance;
            if (pm == null) return;

            int mature = pm.GetMatureCount();
            if (mature > 0 || pm.GetGrowingCount() > 0)
            {
                // Open terrarium to manage slots
                OnPulse?.Invoke();
            }
            else
            {
                // Nothing growing - open satchel
                OnPulse?.Invoke();
            }
        }

        private void UpdateState()
        {
            if (button == null || PlantManager.Instance == null) return;

            var pm = PlantManager.Instance;
            int mature = pm.GetMatureCount();
            int growing = pm.GetGrowingCount();

            if (mature > 0)
                button.text = $"{mature} plant{(mature > 1 ? "s" : "")} ready!";
            else if (growing > 0)
                button.text = "Growing...";
            else
                button.text = "Plant a Seed";
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/PulseButton.cs
git commit -m "feat: update PulseButton for multi-slot status display"
```

---

## Task 23: Update HortusUI to Wire New Panels

**Files:**
- Modify: `Assets/Scripts/UI/HortusUI.cs`

**Step 1: Wire new controllers into HortusUI**

Add SeedShopUI, TerrariumUI, and HarvestResultUI as sub-controllers. Replace greenhouse references with terrarium. Wire the harvest flow.

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlantVisual plantVisual;
        [SerializeField] private UIDocument uiDocument;

        // Sub-controllers
        private PulseButton pulseButton;
        private ResonanceBar resonanceBar;
        private CurrencyDisplay currencyDisplay;
        private SatchelUI satchelUI;
        private CodexUI codexUI;
        private TerrariumUI terrariumUI;
        private SeedShopUI seedShopUI;
        private HarvestResultUI harvestResultUI;
        private DebugWeatherPanel debugPanel;

        // Panel roots
        private VisualElement satchelPanel;
        private VisualElement codexPanel;
        private VisualElement terrariumPanel;
        private VisualElement shopPanel;
        private VisualElement debugPanelRoot;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            pulseButton = GetComponent<PulseButton>();
            resonanceBar = GetComponent<ResonanceBar>();
            currencyDisplay = GetComponent<CurrencyDisplay>();
            satchelUI = GetComponent<SatchelUI>();
            codexUI = GetComponent<CodexUI>();
            terrariumUI = GetComponent<TerrariumUI>();
            seedShopUI = GetComponent<SeedShopUI>();
            harvestResultUI = GetComponent<HarvestResultUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            pulseButton?.Initialize(root);
            resonanceBar?.Initialize(root);
            currencyDisplay?.Initialize(root);
            satchelUI?.Initialize(root);
            codexUI?.Initialize(root);
            terrariumUI?.Initialize(root);
            seedShopUI?.Initialize(root);
            harvestResultUI?.Initialize(root);
            debugPanel?.Initialize(root);

            satchelPanel = root.Q<VisualElement>("satchel-panel");
            codexPanel = root.Q<VisualElement>("codex-panel");
            terrariumPanel = root.Q<VisualElement>("terrarium-panel");
            shopPanel = root.Q<VisualElement>("shop-panel");
            debugPanelRoot = root.Q<VisualElement>("debug-panel");

            // Nav wiring
            pulseButton.OnPulse += () =>
            {
                var pm = PlantManager.Instance;
                if (pm.GetMatureCount() > 0 || pm.GetGrowingCount() > 0)
                    OpenTerrarium();
                else
                    OpenSatchel();
            };

            root.Q<Button>("codex-button").clicked += () => TogglePanel(codexPanel, codexUI);
            root.Q<Button>("greenhouse-button").clicked += () => TogglePanel(terrariumPanel, terrariumUI);
            root.Q<Button>("shop-button").clicked += () => TogglePanel(shopPanel, seedShopUI);
            root.Q<Button>("debug-button").clicked += () => TogglePanel(debugPanelRoot, debugPanel);

            // Terrarium callbacks
            if (terrariumUI != null)
            {
                terrariumUI.OnEmptySlotTapped += (envIdx, slotIdx) =>
                {
                    CloseAllPanels();
                    satchelUI?.Show(envIdx, slotIdx);
                };

                terrariumUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                    {
                        harvestResultUI?.Show(result);
                    }
                    terrariumUI?.RefreshDisplay();
                };
            }

            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    terrariumUI?.RefreshDisplay();
                    RefreshPlantVisual();
                };
            }

            // Plant state
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged += RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated += OnGrowth;
                RefreshPlantVisual();
            }

            CloseAllPanels();
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged -= RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated -= OnGrowth;
            }
        }

        private void RefreshPlantVisual()
        {
            var pm = PlantManager.Instance;
            var featured = pm.CurrentVariant;
            if (featured == null)
            {
                plantVisual.Clear();
            }
            else
            {
                plantVisual.SetVariant(featured);
                plantVisual.SetGrowth(pm.GrowthProgress);
            }
        }

        private void OnGrowth(float progress)
        {
            plantVisual.SetGrowth(progress);
        }

        private void OpenSatchel()
        {
            CloseAllPanels();
            satchelUI?.Show();
        }

        private void OpenTerrarium()
        {
            CloseAllPanels();
            terrariumUI?.Show();
        }

        private void TogglePanel(VisualElement panel, object controller)
        {
            bool wasVisible = panel != null && panel.resolvedStyle.display == DisplayStyle.Flex;
            CloseAllPanels();
            if (!wasVisible)
            {
                if (controller is SatchelUI s) s.Show();
                else if (controller is CodexUI c) c.Show();
                else if (controller is TerrariumUI t) t.Show();
                else if (controller is SeedShopUI sh) sh.Show();
                else if (controller is DebugWeatherPanel d) d.Show();
            }
        }

        private void CloseAllPanels()
        {
            satchelUI?.Hide();
            codexUI?.Hide();
            terrariumUI?.Hide();
            seedShopUI?.Hide();
            debugPanel?.Hide();
        }
    }
}
```

**Step 2: Verify compilation and commit**

```bash
git add Assets/Scripts/UI/HortusUI.cs
git commit -m "feat: wire shop, terrarium, and harvest popup into HortusUI"
```

---

## Task 24: Update Existing Tests for API Changes

**Files:**
- Modify: `Assets/Tests/EditMode/TestGeneticsEngine.cs`

**Step 1: Verify existing tests still pass**

The GeneticsEngine API hasn't changed, so tests should still pass. Run all EditMode tests.

Expected: All existing tests PASS. If any fail due to SeedData new required fields, update test SeedData creation to include defaults:

```csharp
seed.baseSellPrice = 100;
seed.preferredWeather = WeatherCondition.Clear;
seed.specialConditions = new();
```

**Step 2: Run tests**

Run: Unity EditMode tests
Expected: All tests PASS.

**Step 3: Commit if changes needed**

```bash
git add Assets/Tests/EditMode/
git commit -m "test: update existing tests for SeedData field changes"
```

---

## Task 25: Add New Components to Scene GameObject

**Files:**
- Modify: Unity Scene (via Unity MCP)

**Step 1: Add new MonoBehaviour components to the "--- UI ---" GameObject**

The existing scene has components on the "--- UI ---" GameObject. Add:
- `EnvironmentManager`
- `SeedShopManager`
- `TerrariumUI`
- `SeedShopUI`
- `HarvestResultUI`

Use Unity MCP `manage_components(action="add")` to add each component.

**Step 2: Verify all singletons initialize**

Enter Play mode briefly via `manage_editor(action="play")`, then check console for errors.

**Step 3: Commit scene**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: add new manager and UI components to scene"
```

---

## Task 26: Create Environment Data Assets via Unity MCP

**Step 1: Create assets folder and 4 environment assets**

Use Unity MCP to:
1. Create folder: `Assets/Resources/Config/Environments`
2. Create 4 `EnvironmentData` ScriptableObject assets:
   - `Hearth.asset`: name="The Hearth", slotCount=2, unlockCost=0, growthSpeedBonus=0.10, bonusCondition with useTemperature=true minTemp=15 maxTemp=25
   - `Balcony.asset`: name="The Balcony", slotCount=2, unlockCost=5000, growthSpeedBonus=0
   - `WildPatch.asset`: name="The Wild Patch", slotCount=4, unlockCost=15000, growthSpeedBonus=0
   - `Conservatory.asset`: name="Deep Conservatory", slotCount=4, unlockCost=25000, growthSpeedBonus=0, allowsCrossPollination=true

**Step 2: Commit**

```bash
git add Assets/Resources/Config/Environments/
git commit -m "feat: create 4 terrarium environment data assets"
```

---

## Task 27: Create 4 New Seed Assets and Update Astra

**Step 1: Update Astra.asset**

Via Unity MCP, modify the existing Astra asset to set:
- buyPrice = 100
- baseSellPrice = 120
- preferredWeather = Clear

**Step 2: Create 4 new SeedData assets**

Create via Unity MCP's `manage_scriptable_object` or `manage_asset`:

- `CinderFern.asset` in `Assets/Resources/Seeds/`: seedName="Cinder-Fern", buyPrice=450, baseSellPrice=550, baseGrowthHours=12, preferredWeather=Clear, specialConditions=[{targetTier=S, bonusPercent=0.10, condition={useTemperature=true, minTemp=25, maxTemp=60}}]
- `MistVine.asset`: seedName="Mist-Vine", buyPrice=800, baseSellPrice=1000, baseGrowthHours=18, preferredWeather=Rain, specialConditions=[{targetTier=S, bonusPercent=0.10, condition={useHumidity=true, minHumidity=70}}]
- `LunaPetal.asset`: seedName="Luna-Petal", buyPrice=1500, baseSellPrice=1900, baseGrowthHours=24, preferredWeather=Clear, specialConditions=[]
- `StormRoot.asset`: seedName="Storm-Root", buyPrice=3000, baseSellPrice=4000, baseGrowthHours=36, preferredWeather=Storm, specialConditions=[{targetTier=S, bonusPercent=0.20, condition={useWeatherCondition=true, requiredConditions=[Storm, Rain]}}]

**Step 3: Commit**

```bash
git add Assets/Resources/Seeds/
git commit -m "feat: add 4 new seed data assets and update Astra pricing"
```

---

## Task 28: Create 48 Variant Assets (Batch)

This is the largest asset creation task. Create 12 variants per new seed (48 total). Each follows the same trigger pattern as Astra's 12 variants but with seed-themed names and colors.

**Step 1: Create variant folders**

```
Assets/Resources/Variants/CinderFern/
Assets/Resources/Variants/MistVine/
Assets/Resources/Variants/LunaPetal/
Assets/Resources/Variants/StormRoot/
```

**Step 2: Create all 48 variant assets**

Use Unity MCP batch operations or a helper script. Each variant needs:
- Unique variantName (seed-themed)
- Trigger condition matching the pattern (Base, Frost, Desert, Dew, Tempest, Lunar, Solar, Celestial, Biolume, Nebula, Static, Void)
- Priority (1-4) matching the Astra pattern
- Rarity matching the Astra pattern
- Primary/secondary colors themed to the seed
- discoveryHint text

**Cinder-Fern Variants:**
1. Cinder-Fern Base (P4, Common) — Orange stems, red tips
2. Ember-Glass Fern (P2, Rare, Temp<5) — Crystalline orange, ice blue edges
3. Magma Fern (P2, Rare, Temp>38) — Molten red, glowing veins
4. Steam Fern (P3, Uncommon, Humidity>80) — Orange-gray, misty
5. Inferno Fern (P2, Epic, Wind>15) — White-hot orange, sparks
6. Ember-Moon Fern (P3, Uncommon, Night+Clear) — Deep red, moonlit glow
7. Blaze Fern (P3, Common, Day+Clear) — Bright orange, sun-warmed
8. Solstice Fern (P1, Legendary, Equinox) — Gold-orange, celestial rings
9. Ash-Glow Fern (P3, Uncommon, NewMoon) — Dark ash, faint orange glow
10. Sunset Fern (P3, Uncommon, GoldenHour) — Red-orange gradient
11. Thunder-Fern (P2, Epic, Storm) — Electric orange, crackling
12. Void-Ember Fern (P2, Epic, NewMoon+Night) — Black with orange outline

**Mist-Vine Variants:**
1. Mist-Vine Base (P4, Common) — Pale green, misty tendrils
2. Frost-Weave Vine (P2, Rare, Temp<5) — Ice-blue vine, frozen droplets
3. Mirage Vine (P2, Rare, Temp>38) — Shimmering green, heat haze
4. Dew-Drop Vine (P3, Uncommon, Humidity>80) — Deep green, heavy droplets
5. Tempest Vine (P2, Epic, Wind>15) — Gray-green, whipping tendrils
6. Moon-Mist Vine (P3, Uncommon, Night+Clear) — Silver-green, luminous
7. Sun-Mist Vine (P3, Common, Day+Clear) — Bright green, light mist
8. Equinox Vine (P1, Legendary, Equinox) — Iridescent green, cosmic
9. Dark-Mist Vine (P3, Uncommon, NewMoon) — Near-black green, faint glow
10. Twilight Vine (P3, Uncommon, GoldenHour) — Pink-green gradient
11. Storm-Weave Vine (P2, Epic, Storm) — Dark green, electric tendrils
12. Void-Mist Vine (P2, Epic, NewMoon+Night) — Transparent with green outline

**Luna-Petal Variants:**
1. Luna-Petal Base (P4, Common) — Pale lavender petals
2. Frost-Moon Petal (P2, Rare, Temp<5) — Ice-white petals, crystalline
3. Heat-Moon Petal (P2, Rare, Temp>38) — Warm pink, heat shimmer
4. Rain-Moon Petal (P3, Uncommon, Humidity>80) — Deep purple, wet shine
5. Wind-Moon Petal (P2, Epic, Wind>15) — Silver petals, floating
6. Full-Moon Petal (P3, Uncommon, Night+Clear) — Bright white, glowing
7. Daybreak Petal (P3, Common, Day+Clear) — Light pink, subtle
8. Eclipse Petal (P1, Legendary, Eclipse) — Dark purple, corona glow
9. New-Moon Petal (P3, Uncommon, NewMoon) — Deep indigo, hidden
10. Dusk Petal (P3, Uncommon, GoldenHour) — Rose-gold gradient
11. Storm-Moon Petal (P2, Epic, Storm) — Electric purple, sparking
12. Void Petal (P2, Epic, NewMoon+Night) — Near-invisible, outline only

**Storm-Root Variants:**
1. Storm-Root Base (P4, Common) — Dark brown, thick roots
2. Frozen Root (P2, Rare, Temp<5) — Ice-blue bark, frosted
3. Scorched Root (P2, Rare, Temp>38) — Charred black, red cracks
4. Mud Root (P3, Uncommon, Humidity>80) — Dark brown, muddy
5. Gale Root (P2, Epic, Wind>15) — Twisted gray, windswept
6. Night Root (P3, Uncommon, Night+Clear) — Dark bark, moonlit moss
7. Sun Root (P3, Common, Day+Clear) — Warm brown, green moss
8. Equinox Root (P1, Legendary, Equinox) — Gold bark, cosmic energy
9. Shadow Root (P3, Uncommon, NewMoon) — Black bark, faint pulse
10. Amber Root (P3, Uncommon, GoldenHour) — Amber-toned bark
11. Thunder Root (P2, Epic, Storm) — Gray with lightning scars
12. Void Root (P2, Epic, NewMoon+Night) — Invisible roots, glowing tips

**Step 3: Link all variants to their seed's variant list**

**Step 4: Commit**

```bash
git add Assets/Resources/Variants/
git commit -m "feat: add 48 variant assets for all new seeds"
```

---

## Task 29: Integration Testing and Polish

**Step 1: Enter Play mode and test the full flow**

1. Verify Hearth (2 slots) is unlocked by default
2. Open Shop → buy a Cinder-Fern
3. Open Terrarium → tap empty Hearth slot → Satchel opens
4. Select Cinder-Fern → Plant
5. Debug advance time to mature the plant
6. Terrarium shows "Harvest!" on that slot
7. Tap → Harvest popup shows with quality tier, Dewdrops, sell/keep buttons
8. Sell → Dewdrops increase
9. Repeat with Keep → plant appears in Greenhouse section
10. Verify dust rate accounts for quality tier
11. Unlock The Balcony for 5000 Dewdrops
12. Verify new slots appear

**Step 2: Fix any issues found**

**Step 3: Run all EditMode tests**

Expected: All tests PASS.

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete v2.0 systems integration"
```
