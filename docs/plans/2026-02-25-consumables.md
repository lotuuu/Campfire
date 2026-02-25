# Consumables Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add 6 single-use consumable items (Fertilizer, Quality Dirt, Fan, Igloo, Heater, Cloud) purchasable with Gold in the Shop. Fertilizer and Quality Dirt are slot-scoped. Fan, Igloo, Heater, Cloud are environment-scoped (apply to the whole Backyard).

**Architecture:** `ConsumableData` ScriptableObjects with `isEnvironmentScoped` flag. `ConsumableManager` owns inventory + env-level application (save-backed). Slot-scoped consumables are applied per-slot via `PlantManager.ApplyConsumable`. Env-scoped consumables go through `ConsumableManager.ApplyToEnvironment`, which triggers `PlantManager.ForceRefreshMultipliers()`. Per-slot effective weather = global weather + env-scoped overrides only. `BackyardViewUI` circle button + dropdown — env-scoped tap applies immediately; slot-scoped tap enters per-slot apply mode. `BackyardIsometricView` spawns slot prefabs as tile children and env prefabs at fixed positions.

**Tech Stack:** Unity 6, UI Toolkit, ScriptableObject data, `WeatherData` struct (copy-by-value), NUnit EditMode tests.

---

### Task 1: Data types — ConsumableType, ConsumableData, SaveData additions, PlantSlot additions

**Files:**
- Create: `Assets/Scripts/Data/ConsumableType.cs`
- Create: `Assets/Scripts/Data/ConsumableData.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Scripts/Data/PlantSlot.cs`

**Step 1: Create ConsumableType enum**

Create `Assets/Scripts/Data/ConsumableType.cs`:

```csharp
namespace Garden
{
    public enum ConsumableType
    {
        Fertilizer,
        QualityDirt,
        Fan,
        Igloo,
        Heater,
        Cloud
    }
}
```

**Step 2: Create ConsumableData ScriptableObject**

Create `Assets/Scripts/Data/ConsumableData.cs`:

```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewConsumable", menuName = "Garden/Consumable Data")]
    public class ConsumableData : ScriptableObject
    {
        public ConsumableType type;
        public string displayName;
        public Sprite icon;
        public int buyPrice;           // Gold
        public float magnitude;        // Fan: m/s added; Igloo/Heater: °C delta; unused for others
        public bool isEnvironmentScoped; // Fan, Igloo, Heater, Cloud = true
        [TextArea] public string description;
    }
}
```

**Step 3: Add save types to SaveData.cs**

In `Assets/Scripts/Data/SaveData.cs`:

a) Add to `SaveData` class body (after `seedInventory` line):

```csharp
        public List<ConsumableInventoryEntry> consumableInventory = new();
        public List<EnvironmentConsumableSave> environmentConsumables = new();
```

b) Add to `PlantSlotSave` class body (after `growthSpeedMultiplier` line):

```csharp
        public List<string> appliedConsumables = new(); // ConsumableType.ToString() — slot-scoped only
```

c) Add these two new classes at the bottom of the file (after `SeedInventoryEntry`):

```csharp
    [Serializable]
    public class ConsumableInventoryEntry
    {
        public ConsumableType consumableType;
        public int count;
    }

    [Serializable]
    public class EnvironmentConsumableSave
    {
        public int envIndex;
        public string consumableType; // ConsumableType.ToString()
    }
```

**Step 4: Add appliedConsumables to PlantSlot**

In `Assets/Scripts/Data/PlantSlot.cs`, add after `cachedEnvBonus`:

```csharp
        // Slot-scoped consumables (Fertilizer, QualityDirt); cleared on harvest
        public List<ConsumableData> appliedConsumables = new();
```

Add `using System.Collections.Generic;` at the top if not already present.

**Step 5: Compile check**

Save all files and confirm Unity compiles without errors (check Console, no red entries).

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/ConsumableType.cs \
        Assets/Scripts/Data/ConsumableType.cs.meta \
        Assets/Scripts/Data/ConsumableData.cs \
        Assets/Scripts/Data/ConsumableData.cs.meta \
        Assets/Scripts/Data/SaveData.cs \
        Assets/Scripts/Data/PlantSlot.cs
git commit -m "feat: ConsumableType enum, ConsumableData SO, SaveData + PlantSlot additions"
```

---

### Task 2: ConsumableManager singleton

**Files:**
- Create: `Assets/Scripts/Managers/ConsumableManager.cs`
- Test: `Assets/Tests/EditMode/TestConsumableManager.cs`

**Step 1: Write failing test**

Create `Assets/Tests/EditMode/TestConsumableManager.cs`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using Garden;

namespace Garden.Tests
{
    public class TestConsumableManager
    {
        [Test]
        public void ConsumableInventoryEntry_AddAndSpend()
        {
            var data = new List<ConsumableInventoryEntry>();
            data.Add(new ConsumableInventoryEntry { consumableType = ConsumableType.Fan, count = 3 });
            var entry = data.Find(e => e.consumableType == ConsumableType.Fan);
            Assert.AreEqual(3, entry.count);
            entry.count--;
            Assert.AreEqual(2, entry.count);
        }

        [Test]
        public void ConsumableInventoryEntry_NoStackSameType()
        {
            var applied = new List<ConsumableType>();
            applied.Add(ConsumableType.Fertilizer);
            Assert.IsTrue(applied.Contains(ConsumableType.Fertilizer));
            Assert.IsTrue(!applied.Contains(ConsumableType.Fan));
        }

        [Test]
        public void EnvironmentConsumableSave_NoDuplicateType()
        {
            var envList = new List<EnvironmentConsumableSave>();
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

            // Simulate "no stacking" logic: remove existing before adding
            envList.RemoveAll(e => e.envIndex == 0 && e.consumableType == "Fan");
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

            int fanCount = envList.FindAll(e => e.consumableType == "Fan").Count;
            Assert.AreEqual(1, fanCount);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run EditMode tests via Window > General > Test Runner. Tests that reference `ConsumableInventoryEntry`/`EnvironmentConsumableSave` fail until Task 1 is done.

**Step 3: Create ConsumableManager**

Create `Assets/Scripts/Managers/ConsumableManager.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class ConsumableManager : MonoBehaviour
    {
        public static ConsumableManager Instance { get; private set; }

        private readonly List<ConsumableData> _allConsumables = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _allConsumables.AddRange(Resources.LoadAll<ConsumableData>("Consumables"));
        }

        public IReadOnlyList<ConsumableData> AllConsumables => _allConsumables;

        public ConsumableData GetConsumableData(ConsumableType type)
            => _allConsumables.Find(c => c.type == type);

        // ── Inventory ──────────────────────────────────────────────────

        public int GetCount(ConsumableType type)
        {
            var entry = SaveManager.Instance.Data.consumableInventory
                .Find(e => e.consumableType == type);
            return entry?.count ?? 0;
        }

        public void Add(ConsumableType type, int count = 1)
        {
            var inv = SaveManager.Instance.Data.consumableInventory;
            var entry = inv.Find(e => e.consumableType == type);
            if (entry != null)
                entry.count += count;
            else
                inv.Add(new ConsumableInventoryEntry { consumableType = type, count = count });
            SaveManager.Instance.Save();
        }

        public bool Spend(ConsumableType type)
        {
            var entry = SaveManager.Instance.Data.consumableInventory
                .Find(e => e.consumableType == type);
            if (entry == null || entry.count <= 0) return false;
            entry.count--;
            SaveManager.Instance.Save();
            return true;
        }

        public bool CanBuy(ConsumableData c)
            => CurrencyManager.Instance.CanAfford(CurrencyType.Gold, c.buyPrice);

        public bool Buy(ConsumableData c)
        {
            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, c.buyPrice)) return false;
            Add(c.type);
            return true;
        }

        // ── Environment-scoped consumables ─────────────────────────────

        /// <summary>
        /// Returns all ConsumableData currently applied to the given environment.
        /// </summary>
        public List<ConsumableData> GetEnvConsumables(int envIndex)
        {
            var result = new List<ConsumableData>();
            var envList = SaveManager.Instance.Data.environmentConsumables;
            foreach (var save in envList)
            {
                if (save.envIndex != envIndex) continue;
                if (System.Enum.TryParse<ConsumableType>(save.consumableType, out var ctype))
                {
                    var cd = GetConsumableData(ctype);
                    if (cd != null) result.Add(cd);
                }
            }
            return result;
        }

        /// <summary>
        /// Spends one consumable from inventory and applies it to the environment.
        /// Replaces any existing consumable of the same type. Only env-scoped types allowed.
        /// Returns false if out of stock or consumable is not env-scoped.
        /// </summary>
        public bool ApplyToEnvironment(ConsumableType type, int envIndex)
        {
            var cd = GetConsumableData(type);
            if (cd == null || !cd.isEnvironmentScoped) return false;
            if (!Spend(type)) return false;

            var envList = SaveManager.Instance.Data.environmentConsumables;
            // Replace same type (no stacking)
            envList.RemoveAll(e => e.envIndex == envIndex && e.consumableType == type.ToString());
            envList.Add(new EnvironmentConsumableSave
            {
                envIndex = envIndex,
                consumableType = type.ToString()
            });
            SaveManager.Instance.Save();

            // Immediately re-evaluate growth for all slots in this environment
            PlantManager.Instance?.ForceRefreshMultipliers();
            return true;
        }
    }
}
```

**Step 4: Run tests**

All three tests in `TestConsumableManager` should pass.

**Step 5: Commit**

```bash
git add Assets/Scripts/Managers/ConsumableManager.cs \
        Assets/Scripts/Managers/ConsumableManager.cs.meta \
        Assets/Tests/EditMode/TestConsumableManager.cs \
        Assets/Tests/EditMode/TestConsumableManager.cs.meta
git commit -m "feat: ConsumableManager with inventory and environment-scoped apply"
```

---

### Task 3: HarvestEngine — qualityBoosted parameter

**Files:**
- Modify: `Assets/Scripts/Services/HarvestEngine.cs`
- Modify: `Assets/Tests/EditMode/TestHarvestEngine.cs`

**Step 1: Add failing test**

Open `Assets/Tests/EditMode/TestHarvestEngine.cs`. Add this test inside the existing test class:

```csharp
[Test]
public void Roll_QualityBoosted_NeverRollsD()
{
    var seed = ScriptableObject.CreateInstance<SeedData>();
    seed.baseSellPrice = 100;
    seed.preferredWeather = WeatherCondition.Clear;
    var variant = ScriptableObject.CreateInstance<VariantData>();
    var weather = new WeatherData { condition = WeatherCondition.Rain };

    int dCount = 0;
    for (int i = 0; i < 1000; i++)
    {
        var result = HarvestEngine.Roll(seed, variant, weather, qualityBoosted: true);
        if (result.tier == QualityTier.D) dCount++;
    }
    Assert.AreEqual(0, dCount, "Quality Dirt should prevent D-tier rolls");
}
```

**Step 2: Run test to verify it fails**

`Roll_QualityBoosted_NeverRollsD` fails because `Roll` doesn't have `qualityBoosted` parameter yet.

**Step 3: Modify HarvestEngine.Roll**

In `Assets/Scripts/Services/HarvestEngine.cs`, change the `Roll` method signature from:
```csharp
public static HarvestResult Roll(SeedData seed, VariantData variant, WeatherData weather)
```
to:
```csharp
public static HarvestResult Roll(SeedData seed, VariantData variant, WeatherData weather, bool qualityBoosted = false)
```

Also update the `syncShield` line inside the method from:
```csharp
bool syncShield = weather.condition == seed.preferredWeather;
```
to:
```csharp
bool syncShield = qualityBoosted || weather.condition == seed.preferredWeather;
```

**Step 4: Run tests**

All HarvestEngine tests pass.

**Step 5: Commit**

```bash
git add Assets/Scripts/Services/HarvestEngine.cs \
        Assets/Tests/EditMode/TestHarvestEngine.cs
git commit -m "feat: HarvestEngine.Roll accepts qualityBoosted flag for Quality Dirt"
```

---

### Task 4: PlantManager — effective weather + consumable apply + save/restore

**Files:**
- Modify: `Assets/Scripts/Managers/PlantManager.cs`

**Step 1: Add static ApplyConsumableOverrides helper**

At the bottom of the `PlantManager` class, before the final closing brace, add:

```csharp
        /// <summary>
        /// Returns a copy of globalWeather with consumable overrides applied.
        /// Only Fan/Igloo/Heater/Cloud modify weather. Fertilizer/QualityDirt are ignored here.
        /// WeatherData is a struct so assignment gives a copy.
        /// </summary>
        internal static WeatherData ApplyConsumableOverrides(
            List<ConsumableData> consumables, WeatherData globalWeather)
        {
            var w = globalWeather;
            foreach (var c in consumables)
            {
                switch (c.type)
                {
                    case ConsumableType.Fan:    w.windSpeed   += c.magnitude; break;
                    case ConsumableType.Igloo:  w.temperature -= c.magnitude; break;
                    case ConsumableType.Heater: w.temperature += c.magnitude; break;
                    case ConsumableType.Cloud:  w.condition    = WeatherCondition.Rain; break;
                }
            }
            return w;
        }
```

**Step 2: Update RefreshMultipliers to use env-level effective weather**

Replace the existing `RefreshMultipliers` method:

```csharp
        private void RefreshMultipliers(WeatherData weather)
        {
            foreach (var slot in slots)
            {
                if (slot.state != PlantState.Growing) continue;
                // Env-scoped consumables (Fan, Igloo, Heater, Cloud) modify weather for all slots in env
                var envConsumables = ConsumableManager.Instance != null
                    ? ConsumableManager.Instance.GetEnvConsumables(slot.environmentIndex)
                    : new List<ConsumableData>();
                var effective = ApplyConsumableOverrides(envConsumables, weather);
                slot.growthSpeedMultiplier = (slot.variant?.trigger != null
                    && slot.variant.trigger.Evaluate(effective)) ? 1.25f : 1f;
                slot.cachedEnvBonus = EnvironmentManager.Instance != null
                    ? EnvironmentManager.Instance.GetGrowthBonus(slot.environmentIndex, effective)
                    : 0f;
            }
        }
```

**Step 3: Add ForceRefreshMultipliers public method**

Add after `RefreshMultipliers`:

```csharp
        public void ForceRefreshMultipliers()
        {
            if (WeatherService.Instance != null)
                RefreshMultipliers(WeatherService.Instance.CurrentWeather);
        }
```

**Step 4: Update Update() to add Fertilizer bonus**

Replace line 90 (the `float totalMultiplier` line inside Update's foreach):

```csharp
                bool hasFertilizer = slot.appliedConsumables != null &&
                    slot.appliedConsumables.Exists(c => c.type == ConsumableType.Fertilizer);
                float fertilizerBonus = hasFertilizer ? 1f : 0f;
                float totalMultiplier = Mathf.Max(
                    slot.growthSpeedMultiplier + slot.cachedEnvBonus + fertilizerBonus, 0.01f);
```

Also update `GetRemainingHours(int envIndex, int slotIndex)` to include Fertilizer:

```csharp
        public float GetRemainingHours(int envIndex, int slotIndex)
        {
            var slot = GetSlot(envIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Growing) return 0f;
            bool hasFertilizer = slot.appliedConsumables != null &&
                slot.appliedConsumables.Exists(c => c.type == ConsumableType.Fertilizer);
            float fertilizerBonus = hasFertilizer ? 1f : 0f;
            float totalMultiplier = Mathf.Max(
                slot.growthSpeedMultiplier + slot.cachedEnvBonus + fertilizerBonus, 0.01f);
            float totalHours = slot.seed.baseGrowthHours / totalMultiplier;
            float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }
```

**Step 5: Update Harvest() to use effective weather + QualityDirt**

Replace the `Harvest(int environmentIndex, int slotIndex)` method:

```csharp
        public HarvestResult Harvest(int environmentIndex, int slotIndex)
        {
            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Mature)
                return default;

            var globalWeather = WeatherService.Instance.CurrentWeather;
            // Env-scoped consumables affect harvest quality (Cloud → Rain can trigger sync shield)
            var envConsumables = ConsumableManager.Instance != null
                ? ConsumableManager.Instance.GetEnvConsumables(environmentIndex)
                : new List<ConsumableData>();
            var effectiveWeather = ApplyConsumableOverrides(envConsumables, globalWeather);
            bool qualityBoosted = slot.appliedConsumables != null &&
                slot.appliedConsumables.Exists(c => c.type == ConsumableType.QualityDirt);
            var result = HarvestEngine.Roll(slot.seed, slot.variant, effectiveWeather, qualityBoosted);

            ClearSlot(slot);
            return result;
        }
```

**Step 6: Add ApplyConsumable method (slot-scoped only)**

Add after `AddSlot` method:

```csharp
        /// <summary>
        /// Spends one slot-scoped consumable from inventory and applies it to the slot.
        /// Returns false if consumable is env-scoped, slot is empty, already has this type, or out of stock.
        /// </summary>
        public bool ApplyConsumable(ConsumableType type, int environmentIndex, int slotIndex)
        {
            var consumableData = ConsumableManager.Instance?.GetConsumableData(type);
            if (consumableData == null || consumableData.isEnvironmentScoped) return false;

            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state == PlantState.Empty) return false;
            if (slot.appliedConsumables.Exists(c => c.type == type)) return false;

            if (!ConsumableManager.Instance.Spend(type)) return false;

            slot.appliedConsumables.Add(consumableData);
            SaveState();
            return true;
        }
```

**Step 7: Update ClearSlot to clear appliedConsumables**

In `ClearSlot`, after `slot.state = PlantState.Empty;`, add:

```csharp
            slot.appliedConsumables.Clear();
```

**Step 8: Update SaveState to persist slot appliedConsumables**

In `SaveState`, update the `PlantSlotSave` initializer:

```csharp
                save.activeSlots.Add(new PlantSlotSave
                {
                    environmentIndex = slot.environmentIndex,
                    slotIndex = slot.slotIndex,
                    seedName = slot.seed.seedName,
                    variantName = slot.variant.variantName,
                    plantTimeUtc = slot.plantTime.ToString("O"),
                    growthSpeedMultiplier = slot.growthSpeedMultiplier,
                    appliedConsumables = slot.appliedConsumables
                        .ConvertAll(c => c.type.ToString())
                });
```

**Step 9: Update RestoreFromSave to restore slot appliedConsumables**

In `RestoreFromSave`, after `slot.growthSpeedMultiplier = ps.growthSpeedMultiplier;`, add:

```csharp
                    if (ps.appliedConsumables != null && ConsumableManager.Instance != null)
                    {
                        foreach (var typeName in ps.appliedConsumables)
                        {
                            if (System.Enum.TryParse<ConsumableType>(typeName, out var ctype))
                            {
                                var cd = ConsumableManager.Instance.GetConsumableData(ctype);
                                if (cd != null) slot.appliedConsumables.Add(cd);
                            }
                        }
                    }
```

**Step 10: Verify compile + run tests**

Check Unity Console — no errors. Run EditMode tests — all pass.

**Step 11: Commit**

```bash
git add Assets/Scripts/Managers/PlantManager.cs
git commit -m "feat: PlantManager env-level effective weather, Fertilizer bonus, slot-scoped consumable apply"
```

---

### Task 5: ConsumableData assets

**Files:**
- Create: `Assets/Resources/Consumables/Fertilizer.asset`
- Create: `Assets/Resources/Consumables/QualityDirt.asset`
- Create: `Assets/Resources/Consumables/Fan.asset`
- Create: `Assets/Resources/Consumables/Igloo.asset`
- Create: `Assets/Resources/Consumables/Heater.asset`
- Create: `Assets/Resources/Consumables/Cloud.asset`

**Step 1: Find ConsumableData script GUID**

```bash
grep "guid:" Assets/Scripts/Data/ConsumableData.cs.meta | head -1
```

Note the GUID value — needed for all 6 assets.

**Step 2: Create the 6 asset files**

First, create the directory:
```bash
mkdir -p Assets/Resources/Consumables
```

Create `Assets/Resources/Consumables/Fertilizer.asset` using the GUID from Step 1:

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
  m_Script: {fileID: 11500000, guid: REPLACE_WITH_CONSUMABLEDATA_GUID, type: 3}
  m_Name: Fertilizer
  m_EditorClassIdentifier: Garden::Garden.ConsumableData
  type: 0
  displayName: Fertilizer
  icon: {fileID: 0}
  buyPrice: 200
  magnitude: 0
  isEnvironmentScoped: 0
  description: Accelerates growth to double speed until harvest.
```

Create `Assets/Resources/Consumables/QualityDirt.asset` (same header, change these fields):
```yaml
  type: 1
  displayName: Quality Dirt
  buyPrice: 300
  magnitude: 0
  isEnvironmentScoped: 0
  description: Guarantees boosted quality odds at harvest.
```

Create `Assets/Resources/Consumables/Fan.asset`:
```yaml
  type: 2
  displayName: Fan
  buyPrice: 150
  magnitude: 5
  isEnvironmentScoped: 1
  description: Adds 5 m/s wind to the Backyard's effective weather.
```

Create `Assets/Resources/Consumables/Igloo.asset`:
```yaml
  type: 3
  displayName: Igloo
  buyPrice: 150
  magnitude: 10
  isEnvironmentScoped: 1
  description: Lowers the Backyard's effective temperature by 10°C.
```

Create `Assets/Resources/Consumables/Heater.asset`:
```yaml
  type: 4
  displayName: Heater
  buyPrice: 150
  magnitude: 10
  isEnvironmentScoped: 1
  description: Raises the Backyard's effective temperature by 10°C.
```

Create `Assets/Resources/Consumables/Cloud.asset`:
```yaml
  type: 5
  displayName: Cloud
  buyPrice: 200
  magnitude: 0
  isEnvironmentScoped: 1
  description: Forces rain over the Backyard regardless of actual weather.
```

For Fan/Igloo/Heater/Cloud use the full YAML block from Fertilizer.asset as the template, substituting only the varying fields listed above.

**Step 3: Verify in Unity**

After Unity reimports, confirm 6 ConsumableData assets appear under `Assets/Resources/Consumables/`. Check `isEnvironmentScoped` is unchecked for Fertilizer/QualityDirt and checked for the other four.

**Step 4: Commit**

```bash
git add Assets/Resources/Consumables/
git commit -m "feat: 6 ConsumableData assets with env-scoped flags"
```

---

### Task 6: Shop UI — consumables section

**Files:**
- Modify: `Assets/Scripts/UI/SeedShopUI.cs`
- Modify: `Assets/UI/Styles/SeedShop.uss`

**Step 1: Read SeedShopUI.cs first**, then replace it entirely with:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SeedShopUI : MonoBehaviour
    {
        private VisualTreeAsset shopCardTemplate;
        private ScrollView shopGrid;

        public void Initialize(VisualElement root)
        {
            shopCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedShopCard");
            shopGrid = root.Q<ScrollView>("shop-grid");
            shopGrid.contentContainer.style.flexDirection = FlexDirection.Column;
            shopGrid.contentContainer.style.flexWrap = Wrap.NoWrap;
            shopGrid.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        public void Show() => RefreshDisplay();

        private void RefreshDisplay()
        {
            shopGrid.Clear();
            AddSeedSection();
            AddConsumableSection();
        }

        private void AddSeedSection()
        {
            var seeds = SeedShopManager.Instance.GetShopSeeds();
            seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));

            foreach (var seed in seeds)
            {
                var card = shopCardTemplate.CloneTree();
                card.style.flexGrow = 1;
                card.style.flexShrink = 0;

                var nameLabel  = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var condLabel  = card.Q<Label>(className: "shop-condition");
                var icon       = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn     = card.Q<Button>(className: "shop-buy-btn");

                int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                if (nameLabel  != null) nameLabel.text  = $"{seed.seedName} (x{owned})";
                if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dust";
                if (condLabel  != null) condLabel.text  = seed.description ?? "";
                if (icon != null && seed.icon != null)
                    icon.style.backgroundImage = new StyleBackground(seed.icon);

                if (buyBtn != null)
                {
                    buyBtn.SetEnabled(SeedShopManager.Instance.CanBuy(seed.seedName));
                    buyBtn.text = $"Buy ({seed.buyPrice} Dust)";
                    var seedName = seed.seedName;
                    buyBtn.clicked += () => { if (SeedShopManager.Instance.BuySeed(seedName)) RefreshDisplay(); };
                }
                shopGrid.Add(card);
            }
        }

        private void AddConsumableSection()
        {
            if (ConsumableManager.Instance == null) return;

            var header = new Label("Consumables");
            header.AddToClassList("shop-section-header");
            shopGrid.Add(header);

            var consumables = new System.Collections.Generic.List<ConsumableData>(
                ConsumableManager.Instance.AllConsumables);
            consumables.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));

            foreach (var c in consumables)
            {
                var card = shopCardTemplate.CloneTree();
                card.style.flexGrow = 1;
                card.style.flexShrink = 0;

                var nameLabel  = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var condLabel  = card.Q<Label>(className: "shop-condition");
                var icon       = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn     = card.Q<Button>(className: "shop-buy-btn");

                int owned = ConsumableManager.Instance.GetCount(c.type);
                if (nameLabel  != null) nameLabel.text  = $"{c.displayName} (x{owned})";
                if (priceLabel != null) priceLabel.text = $"{c.buyPrice} Gold";
                if (condLabel  != null) condLabel.text  = c.description ?? "";
                if (icon != null && c.icon != null)
                    icon.style.backgroundImage = new StyleBackground(c.icon);

                if (buyBtn != null)
                {
                    buyBtn.SetEnabled(ConsumableManager.Instance.CanBuy(c));
                    buyBtn.text = $"Buy ({c.buyPrice} Gold)";
                    var consumable = c;
                    buyBtn.clicked += () => { if (ConsumableManager.Instance.Buy(consumable)) RefreshDisplay(); };
                }
                shopGrid.Add(card);
            }
        }
    }
}
```

**Step 2: Add section header style to SeedShop.uss**

Open `Assets/UI/Styles/SeedShop.uss`. Read it first, then add at the bottom:

```css
.shop-section-header {
    color: var(--color-text-dim);
    font-size: var(--font-sm);
    margin-top: var(--spacing-md);
    margin-left: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    -unity-font-style: bold;
}
```

**Step 3: Verify**

Enter Play mode, open Shop page. "Consumables" section appears below seeds with 6 cards. Use "Max Currency" in debug panel to get Gold and verify Buy works.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/SeedShopUI.cs Assets/UI/Styles/SeedShop.uss
git commit -m "feat: shop consumables section with Gold pricing"
```

---

### Task 7: BackyardViewUI — consumable picker + split apply flow

**Files:**
- Modify: `Assets/Scripts/UI/BackyardViewUI.cs`
- Modify: `Assets/UI/Styles/Backyard.uss`

**Step 1: Add CSS to Backyard.uss**

Read `Assets/UI/Styles/Backyard.uss` first, then append at the bottom:

```css
.consumable-picker-btn {
    position: absolute;
    right: 8px;
    top: 50%;
    width: 44px;
    height: 44px;
    border-radius: 22px;
    background-color: rgba(20, 35, 50, 0.75);
    border-width: 1px;
    border-color: var(--color-highlight);
    color: var(--color-text-primary);
    font-size: var(--font-sm);
    -unity-text-align: middle-center;
    translate: 0 -22px;
}

.consumable-dropdown {
    position: absolute;
    right: 60px;
    top: 50%;
    width: 180px;
    background-color: rgba(12, 20, 30, 0.92);
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: var(--color-highlight);
    padding: var(--spacing-xs);
    translate: 0 -44px;
}

.consumable-row {
    flex-direction: row;
    align-items: center;
    padding: 6px var(--spacing-xs);
    background-color: rgba(0,0,0,0);
    border-width: 0;
    margin-bottom: 2px;
}

.consumable-row:hover {
    background-color: rgba(255,255,255,0.08);
    border-radius: var(--radius-sm);
}

.consumable-row-name {
    flex-grow: 1;
    color: var(--color-text-primary);
    font-size: var(--font-xs);
}

.consumable-row-count {
    color: var(--color-text-dim);
    font-size: var(--font-xs);
}

.backyard-slot-apply-mode {
    border-color: rgba(255, 220, 80, 0.8);
    border-width: 2px;
    border-radius: var(--radius-sm);
}
```

**Step 2: Replace BackyardViewUI.cs**

Read `Assets/Scripts/UI/BackyardViewUI.cs` first, then replace entirely:

```csharp
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BackyardViewUI : MonoBehaviour
    {
        private const int BackyardEnvIndex = 0;

        [SerializeField] private BackyardIsometricView isometricView;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        private readonly List<Button> slotButtons = new();
        private readonly List<Label> labels = new();
        private readonly List<VisualElement> progressFills = new();
        private readonly List<string> _lastLabelText = new();

        private Button _pickerBtn;
        private VisualElement _dropdown;
        private ConsumableType? _pendingType; // only set for slot-scoped apply mode

        private bool initialized;
        private bool pageActive;

        public void SetPageActive(bool active) => pageActive = active;

        public void Initialize(VisualElement root)
        {
            terrariumPage = root.Q<VisualElement>("terrarium-page");

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
            }

            if (EnvironmentManager.Instance != null)
            {
                int count = EnvironmentManager.Instance.GetActiveSlotCount(BackyardEnvIndex);
                for (int i = 0; i < count; i++)
                    AddSlotButton(i);
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            }

            BuildConsumablePicker();

            // Restore slot-scoped consumable visuals for any already-planted slots
            if (PlantManager.Instance != null && isometricView != null)
            {
                foreach (var slot in PlantManager.Instance.Slots)
                {
                    if (slot.environmentIndex != BackyardEnvIndex) continue;
                    foreach (var c in slot.appliedConsumables)
                        isometricView.SpawnSlotConsumableVisual(slot.slotIndex, c.type);
                }
            }

            // Restore env-scoped consumable visuals
            if (ConsumableManager.Instance != null && isometricView != null)
            {
                foreach (var c in ConsumableManager.Instance.GetEnvConsumables(BackyardEnvIndex))
                    isometricView.SpawnEnvConsumableVisual(c.type);
            }

            initialized = true;
            RefreshAllSlots();
        }

        private void BuildConsumablePicker()
        {
            _pickerBtn = new Button(ToggleDropdown);
            _pickerBtn.text = "🌿";
            _pickerBtn.AddToClassList("consumable-picker-btn");
            terrariumPage.Add(_pickerBtn);

            _dropdown = new VisualElement();
            _dropdown.AddToClassList("consumable-dropdown");
            _dropdown.style.display = DisplayStyle.None;
            terrariumPage.Add(_dropdown);
        }

        private void ToggleDropdown()
        {
            if (_pendingType.HasValue)
            {
                CancelApplyMode();
                return;
            }

            bool showing = _dropdown.style.display == DisplayStyle.Flex;
            if (showing)
            {
                _dropdown.style.display = DisplayStyle.None;
                return;
            }

            RefreshDropdown();
            _dropdown.style.display = DisplayStyle.Flex;
        }

        private void RefreshDropdown()
        {
            _dropdown.Clear();
            if (ConsumableManager.Instance == null) return;

            foreach (var c in ConsumableManager.Instance.AllConsumables)
            {
                int count = ConsumableManager.Instance.GetCount(c.type);
                if (count <= 0) continue;

                var row = new Button();
                row.AddToClassList("consumable-row");

                var nameLabel = new Label(c.displayName);
                nameLabel.AddToClassList("consumable-row-name");

                var countLabel = new Label($"x{count}");
                countLabel.AddToClassList("consumable-row-count");

                row.Add(nameLabel);
                row.Add(countLabel);

                var capturedType = c.type;
                var capturedIsEnvScoped = c.isEnvironmentScoped;
                row.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

                _dropdown.Add(row);
            }

            if (_dropdown.childCount == 0)
            {
                var empty = new Label("No consumables owned");
                empty.AddToClassList("consumable-row-name");
                empty.style.padding = new StyleLength(8);
                _dropdown.Add(empty);
            }
        }

        private void OnConsumableRowTapped(ConsumableType type, bool isEnvironmentScoped)
        {
            _dropdown.style.display = DisplayStyle.None;

            if (isEnvironmentScoped)
            {
                // Apply immediately to entire Backyard — no slot selection needed
                if (ConsumableManager.Instance != null &&
                    ConsumableManager.Instance.ApplyToEnvironment(type, BackyardEnvIndex))
                {
                    isometricView?.SpawnEnvConsumableVisual(type);
                }
                return;
            }

            // Slot-scoped: enter apply mode so player can tap a slot
            _pendingType = type;
            foreach (var btn in slotButtons)
                btn.AddToClassList("backyard-slot-apply-mode");
        }

        private void CancelApplyMode()
        {
            _pendingType = null;
            foreach (var btn in slotButtons)
                btn.RemoveFromClassList("backyard-slot-apply-mode");
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated -= OnSlotGrowthUpdated;
            }
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        }

        private void AddSlotButton(int slotIndex)
        {
            var btn = new Button();
            btn.AddToClassList("backyard-slot-overlay");
            btn.style.position = Position.Absolute;

            var label = new Label("Tap to Plant");
            label.AddToClassList("backyard-slot-label");
            btn.Add(label);

            var progressBar = new VisualElement();
            progressBar.AddToClassList("backyard-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("backyard-progress-fill");
            progressBar.Add(fill);
            btn.Add(progressBar);

            int idx = slotIndex;
            btn.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));

            terrariumPage.Add(btn);
            slotButtons.Add(btn);
            labels.Add(label);
            _lastLabelText.Add(null);
            progressFills.Add(fill);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != BackyardEnvIndex) return;
            AddSlotButton(slotButtons.Count);
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || !pageActive || isometricView == null || terrariumPage == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
                PositionButton(i);

            if (PlantManager.Instance == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
            {
                var slot = PlantManager.Instance.GetSlot(BackyardEnvIndex, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(BackyardEnvIndex, i);
                    string text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < labels.Count && labels[i] != null && text != _lastLabelText[i])
                    {
                        labels[i].text = text;
                        _lastLabelText[i] = text;
                    }
                }
                else if (slot.state == PlantState.Mature)
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
                    isometricView.SetPlantScale(i, pulse);
                }
            }
        }

        private void PositionButton(int i)
        {
            if (i >= slotButtons.Count || terrariumPage?.panel == null) return;

            var screenRect = isometricView.GetTileScreenBounds(i);
            var panel = terrariumPage.panel;

            var bl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.x, screenRect.y));
            var tr = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.xMax, screenRect.yMax));

            float panelLeft   = Mathf.Min(bl.x, tr.x);
            float panelTop    = Mathf.Min(bl.y, tr.y);
            float panelWidth  = Mathf.Abs(tr.x - bl.x);
            float panelHeight = Mathf.Abs(bl.y - tr.y);

            var pageOrigin = terrariumPage.worldBound;
            if (pageOrigin.width <= 0) return;
            slotButtons[i].style.left   = panelLeft   - pageOrigin.x;
            slotButtons[i].style.top    = panelTop    - pageOrigin.y;
            slotButtons[i].style.width  = panelWidth;
            slotButtons[i].style.height = panelHeight;
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < slotButtons.Count; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int i)
        {
            if (PlantManager.Instance == null || i >= slotButtons.Count) return;

            var slot = PlantManager.Instance.GetSlot(BackyardEnvIndex, i);
            if (slot == null) return;

            var label = i < labels.Count ? labels[i] : null;
            var fill  = i < progressFills.Count ? progressFills[i] : null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    if (label != null) label.text = "Tap to Plant";
                    if (fill  != null) fill.style.width = new Length(0, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    isometricView?.ClearSlotConsumableVisuals(i);
                    break;

                case PlantState.Growing:
                    float hours = PlantManager.Instance.GetRemainingHours(BackyardEnvIndex, i);
                    if (label != null)
                        label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < _lastLabelText.Count) _lastLabelText[i] = null;
                    if (fill != null)
                        fill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Growing,
                        slot.variant?.primaryColor ?? Color.green);
                    break;

                case PlantState.Mature:
                    if (label != null) label.text = "Harvest!";
                    if (fill  != null) fill.style.width = new Length(100, LengthUnit.Percent);
                    slotButtons[i].AddToClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Mature,
                        slot.variant?.primaryColor ?? Color.green);
                    break;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            // Slot-scoped apply mode: apply consumable to this slot
            if (_pendingType.HasValue)
            {
                var type = _pendingType.Value;
                CancelApplyMode();
                if (PlantManager.Instance != null &&
                    PlantManager.Instance.ApplyConsumable(type, BackyardEnvIndex, slotIndex))
                {
                    isometricView?.SpawnSlotConsumableVisual(slotIndex, type);
                }
                return;
            }

            // Normal interaction
            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(BackyardEnvIndex, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty:  OnEmptySlotTapped?.Invoke(BackyardEnvIndex, slotIndex);  break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(BackyardEnvIndex, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != BackyardEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < slotButtons.Count)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != BackyardEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < progressFills.Count && progressFills[slotIndex] != null)
                progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
        }
    }
}
```

**Step 3: Verify compile**

Check Unity Console. Enter Play mode — circle button appears on right of Backyard page.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/BackyardViewUI.cs Assets/UI/Styles/Backyard.uss
git commit -m "feat: BackyardViewUI consumable picker — env-scoped applies immediately, slot-scoped enters apply mode"
```

---

### Task 8: BackyardIsometricView — consumable prefab spawning

**Files:**
- Modify: `Assets/Scripts/UI/BackyardIsometricView.cs`

**Step 1: Add consumable prefab array and spawn/clear methods**

Read `Assets/Scripts/UI/BackyardIsometricView.cs`, then:

After the `private readonly List<float> plantBaseScales = new();` line, add:

```csharp
        [SerializeField] private GameObject[] consumablePrefabs; // length 6, indexed by (int)ConsumableType

        // Slot-scoped consumable GOs: tile index → list of GOs (Fertilizer, QualityDirt)
        private readonly Dictionary<int, List<GameObject>> _slotConsumableGOs = new();

        // Env-scoped consumable GOs: type → single GO (Fan, Igloo, Heater, Cloud)
        private readonly Dictionary<ConsumableType, GameObject> _envConsumableGOs = new();
```

Then add these public methods after `SetPlantScale`:

```csharp
        /// <summary>Spawns a slot-scoped consumable visual as a child of the tile GO.</summary>
        public void SpawnSlotConsumableVisual(int slotIndex, ConsumableType type)
        {
            if (consumablePrefabs == null || (int)type >= consumablePrefabs.Length) return;
            var prefab = consumablePrefabs[(int)type];
            if (prefab == null || slotIndex >= tiles.Count) return;

            if (!_slotConsumableGOs.ContainsKey(slotIndex))
                _slotConsumableGOs[slotIndex] = new List<GameObject>();

            int existing = _slotConsumableGOs[slotIndex].Count;
            var go = Instantiate(prefab, tiles[slotIndex].transform);
            go.transform.localPosition = new Vector3(0.25f + existing * 0.18f, 0.15f, -0.55f);
            _slotConsumableGOs[slotIndex].Add(go);
        }

        /// <summary>Destroys all slot-scoped consumable GOs for a tile (called on harvest).</summary>
        public void ClearSlotConsumableVisuals(int slotIndex)
        {
            if (!_slotConsumableGOs.TryGetValue(slotIndex, out var gos)) return;
            foreach (var go in gos) if (go) Destroy(go);
            gos.Clear();
            _slotConsumableGOs.Remove(slotIndex);
        }

        /// <summary>
        /// Spawns an env-scoped consumable visual at a fixed position relative to the view root.
        /// Replaces any existing GO of the same type.
        /// </summary>
        public void SpawnEnvConsumableVisual(ConsumableType type)
        {
            if (consumablePrefabs == null || (int)type >= consumablePrefabs.Length) return;
            var prefab = consumablePrefabs[(int)type];
            if (prefab == null) return;

            // Remove existing of same type
            if (_envConsumableGOs.TryGetValue(type, out var existing))
            {
                if (existing) Destroy(existing);
                _envConsumableGOs.Remove(type);
            }

            var go = Instantiate(prefab, transform);
            // Place env consumables in a row to the left of the grid, offset per count
            int envCount = _envConsumableGOs.Count;
            go.transform.localPosition = new Vector3(-2.0f + envCount * 0.5f, 0.8f, -0.5f);
            _envConsumableGOs[type] = go;
        }

        /// <summary>Removes an env-scoped consumable GO.</summary>
        public void ClearEnvConsumableVisual(ConsumableType type)
        {
            if (_envConsumableGOs.TryGetValue(type, out var go))
            {
                if (go) Destroy(go);
                _envConsumableGOs.Remove(type);
            }
        }
```

**Step 2: Verify compile**

Check Unity Console — no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/BackyardIsometricView.cs
git commit -m "feat: BackyardIsometricView slot and env consumable prefab spawn/clear"
```

---

### Task 9: Wire up ConsumableManager in scene + assign prefab slots

**Files:**
- Modify: `Assets/Scenes/Garden.unity` (via Unity MCP)

> **Note on consumable prefabs:** The `consumablePrefabs` array on `BackyardIsometricView` needs 6 prefabs (enum order: Fertilizer=0, QualityDirt=1, Fan=2, Igloo=3, Heater=4, Cloud=5). If art isn't ready, create 6 minimal placeholder prefabs (colored `SpriteRenderer` GameObjects). The system works with null entries — spawn methods silently skip them.

**Step 1: Set active Unity instance**

Use `set_active_instance` with `Garden@<hash>` to pin routing.

**Step 2: Wait for compilation**

Read `mcpforunity://editor/state`. If `isCompiling` is true, wait and retry.

**Step 3: Find the manager GameObject**

Use `find_gameobjects` to find the GameObject holding other managers (e.g., `SeedShopManager`). ConsumableManager goes on the same object.

**Step 4: Add ConsumableManager component**

Use `manage_components action=add` with `component_type: "Garden.ConsumableManager"` on the manager GameObject.

**Step 5: Save scene**

Use `manage_scene action=save`.

**Step 6: Verify end-to-end**

1. Enter Play mode
2. Open Shop → "Consumables" section visible with 6 cards
3. Use debug "Max Currency" → buy 1× Fan, 1× Cloud, 1× Fertilizer
4. Go to Backyard → tap circle button → dropdown shows owned consumables
5. Tap Fan → it applies immediately to whole Backyard (no slot tap), env-level visual spawns
6. Tap circle → tap Fertilizer → slots highlight yellow → tap a growing slot → Fertilizer applied to that slot, count drops
7. Harvest the fertilized plant → slot consumable visual disappears; Fan visual remains
8. Open Shop → fan count shows 0 (spent), fertilizer count shows 0

**Step 7: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat: ConsumableManager wired in scene"
```
