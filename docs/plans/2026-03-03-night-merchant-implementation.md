# Night Merchant Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a Night Merchant that appears on the campsite grid every night 10 PM–midnight, offers randomized item-for-seed trades gated by flame level, and disappears at midnight.

**Architecture:** ScriptableObject-driven data (`MerchantData`) with serializable save structs (`MerchantSave`). Singleton `MerchantManager` handles arrival/departure/trade logic with static helpers for testability. UI uses the existing overlay panel pattern (like ApothekeUI). Grid integration extends `CampsiteViewUI`'s occupied tile map.

**Tech Stack:** Unity 6 UI Toolkit, C# NUnit tests, ScriptableObject assets.

**Design doc:** `docs/plans/2026-03-03-night-merchant-design.md`

---

### Task 1: Data Model — MerchantData ScriptableObject and Save Structs

**Files:**
- Create: `Assets/Scripts/Data/MerchantData.cs`
- Create: `Assets/Scripts/Data/MerchantSave.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs` (add merchant fields)
- Modify: `Assets/Scripts/Data/GameEnums.cs` (add enum value)

**Step 1: Create `MerchantData.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewMerchant", menuName = "CampFire/Merchant Data")]
    public class MerchantData : ScriptableObject
    {
        public string merchantName;
        [TextArea] public string flavorText;
        public int offerCount = 3;
        public List<MerchantOffer> offerPool = new();
    }

    [Serializable]
    public class MerchantOffer
    {
        public int requiredFlameLevel = 1;
        public List<TradeCost> costs = new();
        public SeedData rewardSeed;
        public int rewardCount = 1;
        public float weight = 1f;
    }

    [Serializable]
    public class TradeCost
    {
        public string itemName;
        public int count;
    }
}
```

**Step 2: Create `MerchantSave.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class MerchantSave
    {
        public int gridX;
        public int gridY;
        public string merchantName;
        public List<MerchantOfferSave> offers = new();
        public string appearedAtUtc;
    }

    [Serializable]
    public class MerchantOfferSave
    {
        public List<TradeCost> costs = new();
        public string rewardSeedName;
        public int rewardCount;
    }
}
```

**Step 3: Add merchant fields to SaveData**

In `Assets/Scripts/Data/SaveData.cs`, after `public string lastBirdCheckHourUtc;` (line 27), add:

```csharp
        public List<MerchantSave> merchants = new();
        public string lastMerchantDateUtc;
```

**Step 4: Add `NightMerchant` to CampBuildingType enum**

In `Assets/Scripts/Data/GameEnums.cs`, change:

```csharp
    public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke, MallumHouse, Bird }
```

to:

```csharp
    public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke, MallumHouse, Bird, NightMerchant }
```

**Step 5: Compile and check console**

Run `read_console` to verify no compilation errors.

**Step 6: Commit**

```
git add Assets/Scripts/Data/MerchantData.cs Assets/Scripts/Data/MerchantSave.cs Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/GameEnums.cs
git commit -m "feat: add merchant data model and save structs"
```

---

### Task 2: MerchantManager — Core Logic with Static Helpers

**Files:**
- Create: `Assets/Scripts/Managers/MerchantManager.cs`
- Modify: `Assets/Scripts/Managers/BirdManager.cs` (add merchants to GetFreeTiles)

**Step 1: Create `MerchantManager.cs`**

The manager follows BirdManager's singleton pattern. Core logic is in static methods for testability.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class MerchantManager : MonoBehaviour
    {
        public static MerchantManager Instance { get; private set; }

        public event Action OnMerchantArrived;
        public event Action OnMerchantDeparted;

        private List<MerchantData> allMerchants;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allMerchants = new List<MerchantData>(Resources.LoadAll<MerchantData>("Merchants"));
        }

        private void Update()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null || allMerchants.Count == 0) return;

            int gridRadius = FlameManager.Instance != null
                ? FlameManager.Instance.Config.GetGridSize(data.flameLevel)
                : 2;

            var now = GameTime.Now;
            var utcNow = GameTime.UtcNow;

            // Departure: remove merchants if outside 10 PM–midnight window
            if (data.merchants.Count > 0 && !IsNightMerchantHour(now))
            {
                DismissAllMerchants(data);
                SaveManager.Instance.Save();
                OnMerchantDeparted?.Invoke();
                return;
            }

            // Arrival: spawn if in window, no active merchants, and not already spawned today
            if (IsNightMerchantHour(now) && data.merchants.Count == 0)
            {
                string todayUtc = utcNow.Date.ToString("o");
                if (data.lastMerchantDateUtc == todayUtc) return;

                var merchant = allMerchants[UnityEngine.Random.Range(0, allMerchants.Count)];
                bool spawned = TrySpawnMerchant(data, merchant, gridRadius, data.flameLevel, utcNow);
                if (spawned)
                {
                    data.lastMerchantDateUtc = todayUtc;
                    SaveManager.Instance.Save();
                    OnMerchantArrived?.Invoke();
                }
            }
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static bool IsNightMerchantHour(DateTime localTime)
        {
            return localTime.Hour >= 22;
        }

        public static void DismissAllMerchants(SaveData data)
        {
            data.merchants.Clear();
        }

        public static void CleanStaleMerchants(SaveData data, DateTime utcNow)
        {
            string todayUtc = utcNow.Date.ToString("o");
            data.merchants.RemoveAll(m =>
            {
                if (string.IsNullOrEmpty(m.appearedAtUtc)) return true;
                var appeared = DateTime.Parse(m.appearedAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                return appeared.Date.ToString("o") != todayUtc;
            });
        }

        public static bool TrySpawnMerchant(SaveData data, MerchantData merchantData,
            int gridRadius, int flameLevel, DateTime utcNow)
        {
            var freeTiles = BirdManager.GetFreeTiles(data, gridRadius);
            if (freeTiles.Count == 0) return false;

            var offers = RollOffers(merchantData, flameLevel);
            if (offers.Count == 0) return false;

            var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];
            var save = new MerchantSave
            {
                gridX = tile.q,
                gridY = tile.r,
                merchantName = merchantData.merchantName,
                offers = offers,
                appearedAtUtc = utcNow.ToString("o")
            };
            data.merchants.Add(save);
            return true;
        }

        public static List<MerchantOfferSave> RollOffers(MerchantData merchantData, int flameLevel)
        {
            var eligible = new List<MerchantOffer>();
            foreach (var offer in merchantData.offerPool)
            {
                if (offer.requiredFlameLevel <= flameLevel)
                    eligible.Add(offer);
            }

            if (eligible.Count == 0) return new List<MerchantOfferSave>();

            float totalWeight = 0f;
            foreach (var o in eligible) totalWeight += o.weight;

            int count = Mathf.Min(merchantData.offerCount, eligible.Count);
            var picked = new List<MerchantOfferSave>();
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cumulative = 0f;
                for (int j = 0; j < eligible.Count; j++)
                {
                    if (usedIndices.Contains(j)) continue;
                    cumulative += eligible[j].weight;
                    if (roll < cumulative)
                    {
                        var offer = eligible[j];
                        var save = new MerchantOfferSave
                        {
                            rewardSeedName = offer.rewardSeed.seedName,
                            rewardCount = offer.rewardCount,
                            costs = new List<TradeCost>(offer.costs)
                        };
                        picked.Add(save);
                        usedIndices.Add(j);
                        totalWeight -= eligible[j].weight;
                        break;
                    }
                }
            }

            return picked;
        }

        public static bool CanAffordOffer(MerchantOfferSave offer, List<InventoryItem> items)
        {
            foreach (var cost in offer.costs)
            {
                var item = items.Find(i => i.itemName == cost.itemName);
                if (item == null || item.count < cost.count) return false;
            }
            return true;
        }

        public static void ExecuteTrade(MerchantOfferSave offer, List<InventoryItem> items,
            List<SeedInventoryEntry> seedInventory)
        {
            // Consume items
            foreach (var cost in offer.costs)
            {
                var item = items.Find(i => i.itemName == cost.itemName);
                item.count -= cost.count;
                if (item.count <= 0) items.Remove(item);
            }

            // Add seeds
            var entry = seedInventory.Find(s => s.seedName == offer.rewardSeedName);
            if (entry != null)
                entry.count += offer.rewardCount;
            else
                seedInventory.Add(new SeedInventoryEntry
                    { seedName = offer.rewardSeedName, count = offer.rewardCount });
        }
    }
}
```

**Step 2: Add merchants to BirdManager.GetFreeTiles**

In `Assets/Scripts/Managers/BirdManager.cs`, in the `GetFreeTiles` method, after the `// Birds` block (after `foreach (var bird in data.birds) occupied.Add(...);`), add:

```csharp
            // Merchants
            foreach (var merchant in data.merchants)
                occupied.Add((merchant.gridX, merchant.gridY));
```

**Step 3: Compile and check console**

Run `read_console` to verify no compilation errors.

**Step 4: Commit**

```
git add Assets/Scripts/Managers/MerchantManager.cs Assets/Scripts/Managers/BirdManager.cs
git commit -m "feat: add MerchantManager with arrival/departure and trade logic"
```

---

### Task 3: Unit Tests for MerchantManager

**Files:**
- Create: `Assets/Tests/EditMode/TestMerchantManager.cs`

**Step 1: Write tests**

Follow `TestBirdManager.cs` pattern — all tests operate on `new SaveData()` + `ScriptableObject.CreateInstance<MerchantData>()`, calling only static methods.

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMerchantManager
    {
        private MerchantData CreateTestMerchant()
        {
            var seed1 = ScriptableObject.CreateInstance<SeedData>();
            seed1.seedName = "Moonflower";
            seed1.tier = 2;

            var seed2 = ScriptableObject.CreateInstance<SeedData>();
            seed2.seedName = "Dahlia";
            seed2.tier = 3;

            var merchant = ScriptableObject.CreateInstance<MerchantData>();
            merchant.merchantName = "Night Merchant";
            merchant.flavorText = "Rare seeds for trade...";
            merchant.offerCount = 3;
            merchant.offerPool = new List<MerchantOffer>
            {
                new MerchantOffer
                {
                    requiredFlameLevel = 1,
                    costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                    rewardSeed = seed1,
                    rewardCount = 1,
                    weight = 1f
                },
                new MerchantOffer
                {
                    requiredFlameLevel = 2,
                    costs = new List<TradeCost> { new TradeCost { itemName = "Chamomile_harvest", count = 5 } },
                    rewardSeed = seed2,
                    rewardCount = 1,
                    weight = 1f
                }
            };
            return merchant;
        }

        [Test]
        public void IsNightMerchantHour_At22_ReturnsTrue()
        {
            var time = new DateTime(2026, 3, 3, 22, 0, 0);
            Assert.IsTrue(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void IsNightMerchantHour_At23_ReturnsTrue()
        {
            var time = new DateTime(2026, 3, 3, 23, 30, 0);
            Assert.IsTrue(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void IsNightMerchantHour_At21_ReturnsFalse()
        {
            var time = new DateTime(2026, 3, 3, 21, 59, 0);
            Assert.IsFalse(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void IsNightMerchantHour_At0_ReturnsFalse()
        {
            var time = new DateTime(2026, 3, 4, 0, 0, 0);
            Assert.IsFalse(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void RollOffers_FiltersbyFlameLevel()
        {
            var merchant = CreateTestMerchant();

            // Flame level 1: only first offer eligible
            var offers = MerchantManager.RollOffers(merchant, 1);
            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual("Moonflower", offers[0].rewardSeedName);
        }

        [Test]
        public void RollOffers_HigherFlameLevelUnlocksMore()
        {
            var merchant = CreateTestMerchant();

            var offers = MerchantManager.RollOffers(merchant, 3);
            Assert.AreEqual(2, offers.Count);
        }

        [Test]
        public void RollOffers_RespectsOfferCount()
        {
            var merchant = CreateTestMerchant();
            merchant.offerCount = 1;

            var offers = MerchantManager.RollOffers(merchant, 3);
            Assert.AreEqual(1, offers.Count);
        }

        [Test]
        public void RollOffers_EmptyPoolReturnsEmpty()
        {
            var merchant = ScriptableObject.CreateInstance<MerchantData>();
            merchant.offerPool = new List<MerchantOffer>();

            var offers = MerchantManager.RollOffers(merchant, 5);
            Assert.AreEqual(0, offers.Count);
        }

        [Test]
        public void CanAffordOffer_WithSufficientItems_ReturnsTrue()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 }
            };

            Assert.IsTrue(MerchantManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void CanAffordOffer_WithInsufficientItems_ReturnsFalse()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 2 }
            };

            Assert.IsFalse(MerchantManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void CanAffordOffer_MissingItem_ReturnsFalse()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>();

            Assert.IsFalse(MerchantManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void ExecuteTrade_ConsumesItemsAndAddsSeed()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 2
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 }
            };
            var seeds = new List<SeedInventoryEntry>();

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(2, items[0].count);
            Assert.AreEqual(1, seeds.Count);
            Assert.AreEqual("Moonflower", seeds[0].seedName);
            Assert.AreEqual(2, seeds[0].count);
        }

        [Test]
        public void ExecuteTrade_RemovesItemWhenCountReachesZero()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 3 }
            };
            var seeds = new List<SeedInventoryEntry>();

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void ExecuteTrade_AddsToExistingSeedEntry()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 1 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 }
            };
            var seeds = new List<SeedInventoryEntry>
            {
                new SeedInventoryEntry { seedName = "Moonflower", count = 3 }
            };

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(1, seeds.Count);
            Assert.AreEqual(4, seeds[0].count);
        }

        [Test]
        public void TrySpawnMerchant_PlacesMerchantOnFreeTile()
        {
            var data = new SaveData();
            var merchant = CreateTestMerchant();
            var utcNow = new DateTime(2026, 3, 3, 22, 0, 0, DateTimeKind.Utc);

            bool result = MerchantManager.TrySpawnMerchant(data, merchant, 2, 1, utcNow);

            Assert.IsTrue(result);
            Assert.AreEqual(1, data.merchants.Count);
            Assert.AreEqual("Night Merchant", data.merchants[0].merchantName);
            Assert.IsTrue(data.merchants[0].offers.Count > 0);
        }

        [Test]
        public void DismissAllMerchants_ClearsList()
        {
            var data = new SaveData();
            data.merchants.Add(new MerchantSave { merchantName = "Test" });

            MerchantManager.DismissAllMerchants(data);

            Assert.AreEqual(0, data.merchants.Count);
        }

        [Test]
        public void CleanStaleMerchants_RemovesOldMerchants()
        {
            var data = new SaveData();
            var yesterday = new DateTime(2026, 3, 2, 22, 0, 0, DateTimeKind.Utc);
            data.merchants.Add(new MerchantSave
            {
                merchantName = "Stale",
                appearedAtUtc = yesterday.ToString("o")
            });

            var today = new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc);
            MerchantManager.CleanStaleMerchants(data, today);

            Assert.AreEqual(0, data.merchants.Count);
        }

        [Test]
        public void CleanStaleMerchants_KeepsTodayMerchants()
        {
            var data = new SaveData();
            var todayEvening = new DateTime(2026, 3, 3, 22, 0, 0, DateTimeKind.Utc);
            data.merchants.Add(new MerchantSave
            {
                merchantName = "Fresh",
                appearedAtUtc = todayEvening.ToString("o")
            });

            var todayLater = new DateTime(2026, 3, 3, 23, 0, 0, DateTimeKind.Utc);
            MerchantManager.CleanStaleMerchants(data, todayLater);

            Assert.AreEqual(1, data.merchants.Count);
        }

        [Test]
        public void ExecuteTrade_MultipleCosts_ConsumesAll()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Basil_harvest", count = 2 },
                    new TradeCost { itemName = "Mint_harvest", count = 1 }
                },
                rewardSeedName = "Dahlia",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 },
                new InventoryItem { itemName = "Mint_harvest", count = 3 }
            };
            var seeds = new List<SeedInventoryEntry>();

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(3, items[0].count);
            Assert.AreEqual(2, items[1].count);
            Assert.AreEqual("Dahlia", seeds[0].seedName);
        }
    }
}
```

**Step 2: Run tests**

Run via Unity MCP: `run_tests` with `mode: "EditMode"`, `test_names: ["Garden.Tests.TestMerchantManager"]`.

Expected: All tests pass.

**Step 3: Commit**

```
git add Assets/Tests/EditMode/TestMerchantManager.cs
git commit -m "test: add MerchantManager unit tests"
```

---

### Task 4: Grid Integration — CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

This task adds merchant tiles to the campsite grid and handles taps.

**Step 1: Add OnMerchantTapped event**

Near the existing `public event Action OnApothekeTapped;` (line 70), add:

```csharp
        public event Action<int> OnMerchantTapped;
```

**Step 2: Add merchants to occupied map in RebuildGrid**

After the birds block (line 224: `occupied[(data.birds[i].gridX, data.birds[i].gridY)] = (CampBuildingType.Bird, i);`), add:

```csharp
            for (int i = 0; i < data.merchants.Count; i++)
                occupied[(data.merchants[i].gridX, data.merchants[i].gridY)] = (CampBuildingType.NightMerchant, i);
```

**Step 3: Mark NightMerchant as non-movable**

On line 272, change:

```csharp
                        bool isMovable = cellType != CampBuildingType.Flame && cellType != CampBuildingType.Bird;
```

to:

```csharp
                        bool isMovable = cellType != CampBuildingType.Flame && cellType != CampBuildingType.Bird && cellType != CampBuildingType.NightMerchant;
```

**Step 4: Add NightMerchant case to PopulateOccupiedCell**

After the `case CampBuildingType.Bird:` block (after line 402), add:

```csharp
                case CampBuildingType.NightMerchant:
                    cell.AddToClassList("grid-cell--merchant");
                    var merchant = SaveManager.Instance.Data.merchants[index];
                    if (label != null) label.text = "Merchant";
                    if (status != null) status.text = $"{merchant.offers.Count} trades";
                    break;
```

**Step 5: Handle merchant tap in OnCellTapped**

In the `OnCellTapped` method (around line 427), after the Apotheke tap handler and before `ShowInteraction`, add:

```csharp
            if (type == CampBuildingType.NightMerchant)
            {
                OnMerchantTapped?.Invoke(index);
                return;
            }
```

**Step 6: Compile and check console**

Run `read_console` to verify no compilation errors.

**Step 7: Commit**

```
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add merchant tile to campsite grid"
```

---

### Task 5: MerchantUI Overlay Panel

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (add merchant panel)
- Create: `Assets/UI/Styles/Merchant.uss` (merchant styles)
- Create: `Assets/Resources/UI/Templates/MerchantOfferRow.uxml` (offer row template)
- Create: `Assets/Scripts/UI/MerchantUI.cs` (overlay controller)
- Modify: `Assets/Scripts/UI/CampFireUI.cs` (wire merchant panel)
- Modify: `Assets/UI/Styles/CampsiteGrid.uss` (add merchant tile style)

**Step 1: Add merchant tile style to CampsiteGrid.uss**

After the `.grid-cell--bird` block (line 177), add:

```css
/* ── Merchant cell ── */
.grid-cell--merchant {
    --hex-fill: rgba(180, 140, 60, 0.45);
    --hex-border: rgba(220, 180, 80, 0.5);
}

.grid-cell--merchant:hover {
    --hex-fill: rgba(200, 160, 70, 0.6);
    --hex-border: rgba(240, 200, 100, 0.7);
}

.grid-cell--merchant .cell-icon {
    width: 56px;
    height: 56px;
    border-radius: 12px;
    background-color: rgba(180, 140, 60, 0.4);
}
```

**Step 2: Create `Merchant.uss`**

```css
/* Merchant.uss — Night Merchant overlay panel styles */

.merchant-flavor {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    -unity-font-style: italic;
    margin-bottom: var(--spacing-md);
    padding: 0 var(--spacing-sm);
}

.merchant-offer-row {
    flex-direction: row;
    align-items: center;
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    background-color: var(--color-bg-slot);
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: var(--color-border);
}

.merchant-offer-costs {
    flex: 1;
    flex-direction: column;
}

.merchant-cost-item {
    font-size: var(--font-sm);
    color: var(--color-text);
    margin-bottom: var(--spacing-xxs);
}

.merchant-cost-item--insufficient {
    color: rgb(200, 80, 60);
}

.merchant-offer-arrow {
    font-size: var(--font-lg);
    color: var(--color-text-accent);
    margin: 0 var(--spacing-sm);
}

.merchant-offer-reward {
    flex-direction: column;
    align-items: center;
    margin-right: var(--spacing-sm);
}

.merchant-reward-text {
    font-size: var(--font-sm);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

.merchant-trade-btn {
    padding: var(--spacing-xs) var(--spacing-sm);
    background-color: var(--color-button-bg);
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: var(--color-border-accent);
    color: var(--color-text-bright);
    font-size: var(--font-sm);
}

.merchant-trade-btn:hover {
    background-color: var(--color-button-bg-hover);
}

.merchant-trade-btn:disabled {
    background-color: var(--color-button-bg-disabled);
    color: var(--color-text-dim);
    border-color: var(--color-border);
}
```

**Step 3: Create `MerchantOfferRow.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="merchant-offer-row">
        <ui:VisualElement class="merchant-offer-costs" name="offer-costs" />
        <ui:Label class="merchant-offer-arrow" text="&#x2192;" />
        <ui:VisualElement class="merchant-offer-reward">
            <ui:Label class="merchant-reward-text" name="reward-text" />
        </ui:VisualElement>
        <ui:Button class="merchant-trade-btn" name="trade-btn" text="Trade" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 4: Create `MerchantUI.cs`**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class MerchantUI : MonoBehaviour
    {
        private VisualElement merchantList;
        private Label merchantFlavor;
        private VisualTreeAsset offerTemplate;

        private int activeMerchantIndex = -1;

        public void Initialize(VisualElement root)
        {
            merchantFlavor = root.Q<Label>("merchant-flavor");
            merchantList = root.Q("merchant-list");
            offerTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/MerchantOfferRow");
        }

        public void ShowMerchant(int index)
        {
            activeMerchantIndex = index;
            Refresh();
        }

        public void Refresh()
        {
            if (merchantList == null) return;
            merchantList.Clear();

            var data = SaveManager.Instance?.Data;
            if (data == null || activeMerchantIndex < 0 || activeMerchantIndex >= data.merchants.Count)
                return;

            var merchant = data.merchants[activeMerchantIndex];

            // Load MerchantData for flavor text
            var allMerchants = Resources.LoadAll<MerchantData>("Merchants");
            MerchantData merchantData = null;
            foreach (var md in allMerchants)
            {
                if (md.merchantName == merchant.merchantName) { merchantData = md; break; }
            }

            if (merchantFlavor != null)
                merchantFlavor.text = merchantData != null ? merchantData.flavorText : "";

            foreach (var offer in merchant.offers)
            {
                var el = offerTemplate.CloneTree();
                var costsContainer = el.Q("offer-costs");
                var rewardText = el.Q<Label>("reward-text");
                var tradeBtn = el.Q<Button>("trade-btn");

                bool canAfford = MerchantManager.CanAffordOffer(offer, data.items);

                // Populate costs
                foreach (var cost in offer.costs)
                {
                    string displayName = cost.itemName.Replace("_harvest", "");
                    var item = data.items.Find(i => i.itemName == cost.itemName);
                    int have = item != null ? item.count : 0;
                    bool enough = have >= cost.count;

                    var costLabel = new Label($"{cost.count}x {displayName} ({have})");
                    costLabel.AddToClassList("merchant-cost-item");
                    if (!enough) costLabel.AddToClassList("merchant-cost-item--insufficient");
                    costsContainer.Add(costLabel);
                }

                // Reward
                if (rewardText != null)
                    rewardText.text = $"{offer.rewardCount}x {offer.rewardSeedName}";

                // Trade button
                if (tradeBtn != null)
                {
                    tradeBtn.SetEnabled(canAfford);
                    var capturedOffer = offer;
                    tradeBtn.clicked += () =>
                    {
                        if (!MerchantManager.CanAffordOffer(capturedOffer, data.items)) return;
                        MerchantManager.ExecuteTrade(capturedOffer, data.items, data.seedInventory);
                        SaveManager.Instance.Save();
                        Refresh();
                    };
                }

                merchantList.Add(el);
            }
        }
    }
}
```

**Step 5: Add merchant panel to CampFireRoot.uxml**

Inside the `<ui:ScrollView name="overlay-body">`, after the quests-panel and before the debug-panel, add:

```xml
                    <!-- merchant-panel -->
                    <ui:VisualElement name="merchant-panel">
                        <ui:Label name="merchant-flavor" class="merchant-flavor" />
                        <ui:VisualElement name="merchant-list" />
                    </ui:VisualElement>
```

**Step 6: Add Merchant.uss stylesheet reference to CampFireRoot.uxml**

Add at the top with the other Style imports:

```xml
    <Style src="project://database/Assets/UI/Styles/Merchant.uss" />
```

**Step 7: Wire MerchantUI in CampFireUI.cs**

In `Assets/Scripts/UI/CampFireUI.cs`:

a) Add fields (near the other sub-controller fields):
```csharp
        private MerchantUI merchantUI;
```

And near overlay panel fields:
```csharp
        private VisualElement merchantPanel;
```

b) In `Start()`, after `questButton?.Initialize(root);`, add:
```csharp
            merchantUI = GetComponent<MerchantUI>();
            merchantUI?.Initialize(root);
```

c) In the overlay setup section, after `questsPanel = root.Q("quests-panel");`, add:
```csharp
            merchantPanel = root.Q("merchant-panel");
```

d) In `HideAllPanels()`, add:
```csharp
            if (merchantPanel != null) merchantPanel.style.display = DisplayStyle.None;
```

e) After the `campsiteView.OnApothekeTapped` wiring, add:
```csharp
            if (campsiteView != null)
                campsiteView.OnMerchantTapped += index =>
                {
                    merchantUI?.ShowMerchant(index);
                    OpenOverlay("Night Merchant", merchantPanel);
                };
```

**Step 8: Compile and check console**

Run `read_console` to verify no compilation errors.

**Step 9: Commit**

```
git add Assets/UI/Styles/CampsiteGrid.uss Assets/UI/Styles/Merchant.uss Assets/Resources/UI/Templates/MerchantOfferRow.uxml Assets/Scripts/UI/MerchantUI.cs Assets/Scripts/UI/CampFireUI.cs Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat: add merchant UI overlay panel and grid styling"
```

---

### Task 6: Create NightMerchant ScriptableObject Asset and Wire to Scene

**Files:**
- Create: `Assets/Resources/Merchants/NightMerchant.asset` (via Unity MCP)
- Modify: Scene — add MerchantManager + MerchantUI components

**Step 1: Create the Merchants folder**

```bash
mkdir -p Assets/Resources/Merchants
```

**Step 2: Create NightMerchant.asset via MerchantData ScriptableObject**

Use Unity MCP `manage_asset` to create a ScriptableObject asset of type `Garden.MerchantData` at `Assets/Resources/Merchants/NightMerchant.asset`. Set fields:
- `merchantName`: "Night Merchant"
- `flavorText`: "Rare seeds, if you have the harvest to trade..."
- `offerCount`: 3
- `offerPool`: Populate with 4-6 offers at various flame levels using existing seeds. Example offers:
  - Flame 1: 5x Basil_harvest → 1x Chamomile seed
  - Flame 1: 3x Mint_harvest → 1x Lavender seed
  - Flame 2: 5x Chamomile_harvest → 1x Dahlia seed
  - Flame 2: 3x Lavender_harvest + 2x Rosemary_harvest → 1x Moonflower seed
  - Flame 3: 8x Dahlia_harvest → 2x Jasmine seed
  - Flame 3: 5x Moonflower_harvest → 1x Poppy seed

If MCP asset creation doesn't support ScriptableObject creation cleanly, create the `.asset` file by hand-writing the YAML (following the pattern of existing `.asset` files in `Resources/Quests/`).

**Step 3: Add MerchantManager to the scene**

The managers live on dedicated GameObjects in the scene. Either:
- Use Unity MCP to add a `MerchantManager` component to an existing managers GameObject, OR
- Add a new GameObject named "MerchantManager" with the `MerchantManager` component

**Step 4: Add MerchantUI component to UI GameObject**

Add a `MerchantUI` MonoBehaviour component to the `"--- UI ---"` GameObject (same object that has `CampFireUI`, `ApothekeUI`, etc.).

**Step 5: Wire MerchantManager events to CampsiteViewUI refresh**

In `CampFireUI.Start()`, after the existing event wiring, add:

```csharp
            if (MerchantManager.Instance != null)
            {
                MerchantManager.Instance.OnMerchantArrived += () => campsiteView?.RebuildGrid();
                MerchantManager.Instance.OnMerchantDeparted += () => campsiteView?.RebuildGrid();
            }
```

Note: Check if `RebuildGrid` is public. If not, check what the existing pattern is for refreshing the grid (BirdManager uses `OnBirdPlaced` which triggers a grid rebuild — find and follow that pattern).

**Step 6: Compile, check console, run tests**

Run all EditMode tests to make sure nothing is broken.

**Step 7: Commit**

```
git add Assets/Resources/Merchants/ Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat: add NightMerchant asset and wire to scene"
```

---

### Task 7: Clean Stale Merchants on Load

**Files:**
- Modify: `Assets/Scripts/Managers/MerchantManager.cs` (add Start cleanup)

**Step 1: Add stale cleanup in Start**

In `MerchantManager`, add a `Start()` method after `Awake()`:

```csharp
        private void Start()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            int before = data.merchants.Count;
            CleanStaleMerchants(data, GameTime.UtcNow);
            if (data.merchants.Count < before)
            {
                SaveManager.Instance.Save();
                OnMerchantDeparted?.Invoke();
            }
        }
```

**Step 2: Compile and verify**

**Step 3: Commit**

```
git add Assets/Scripts/Managers/MerchantManager.cs
git commit -m "feat: clean stale merchants on game load"
```

---

### Task 8: Final Integration Test

**Step 1: Run all EditMode tests**

Run via Unity MCP: `run_tests` with `mode: "EditMode"`.
Expected: All tests pass including new TestMerchantManager tests.

**Step 2: Check console for any warnings/errors**

Run `read_console` and check for compilation errors or runtime warnings.

**Step 3: Manual verification checklist**

- [ ] MerchantManager component exists in scene
- [ ] MerchantUI component exists on UI GameObject
- [ ] NightMerchant.asset exists with populated offer pool
- [ ] CampFireRoot.uxml has merchant-panel
- [ ] CampsiteGrid.uss has .grid-cell--merchant styles
- [ ] Merchant.uss stylesheet is referenced in root UXML
