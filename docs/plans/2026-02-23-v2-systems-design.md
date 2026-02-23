# V2.0 Systems Design: Bloom Roll, Terrarium, Seed Shop

**Date:** 2026-02-23
**Status:** Approved
**Scope:** All new gameplay systems from v2.0 master spec

---

## Design Decisions

- **Multiple simultaneous growth**: Each terrarium slot grows its own plant independently (up to 12 concurrent plants)
- **Full variant catalog**: All 5 seeds get 12 variants each (60 total VariantData assets)
- **Sell-or-Keep harvest choice**: After Bloom Roll, player chooses to sell for Dewdrops or keep in Greenhouse for passive dust

---

## 1. Data Layer

### New Enum: QualityTier

```
D (Faded)   → 0.8x value, 15% base probability
C (Stable)  → 1.0x value, 55% base probability
B (Vibrant) → 1.5x value, 20% base probability
A (Radiant) → 2.2x value, 8% base probability
S (Eternal) → 3.5x value, 2% base probability
```

Added to `GameEnums.cs`.

### SeedData Extensions

New fields on existing `SeedData` ScriptableObject:
- `int buyPrice` — Dewdrop cost in shop (0 = starter/not purchasable)
- `int baseSellPrice` — C-tier Dewdrop payout on harvest
- `WeatherCondition preferredWeather` — triggers Sync Shield when matched at harvest
- `List<SeedSpecialCondition> specialConditions` — conditional tier bonuses

### SeedSpecialCondition (Serializable Class)

Embedded in SeedData, not a separate asset:
- `QualityTier targetTier` — which tier receives the bonus
- `float bonusPercent` — e.g., 0.10 for +10%
- `TriggerCondition condition` — when the bonus applies

### New ScriptableObject: EnvironmentData

Stored in `Resources/Config/Environments/`:
- `string environmentName` — Hearth, Balcony, Wild Patch, Deep Conservatory
- `int slotCount` — 2, 2, 4, 4
- `int unlockCostDewdrops` — 0, 5000, 15000, 25000
- `float growthSpeedBonus` — e.g., 0.10 for +10%
- `TriggerCondition bonusCondition` — when the growth bonus applies
- `bool allowsCrossPollination`

### New Class: PlantSlot

Plain C# class (not MonoBehaviour):
- `int environmentIndex`
- `int slotIndex`
- `PlantState state` (Empty, Growing, Mature)
- `SeedData seed`
- `VariantData variant`
- `DateTime plantTime`
- `float growthSpeedMultiplier`

### New Class: HarvestResult

- `QualityTier tier`
- `float valueMultiplier`
- `bool syncShieldActive`
- `int dewdropValue`
- `VariantData variant`
- `SeedData seed`

### SaveData Extensions

- `List<PlantSlotSave> activeSlots` — replaces single `ActivePlantSave`
  - Each: `seedName, variantName, qualityTier, plantTime, environmentIndex, slotIndex`
- `List<string> unlockedEnvironments`
- `GreenhousePlantSave` gains `QualityTier qualityTier` field

---

## 2. Services & Managers

### HarvestEngine (New Static Service)

File: `Assets/Scripts/Services/HarvestEngine.cs`

Core method:
```
static HarvestResult Roll(SeedData seed, VariantData variant, WeatherData weatherAtHarvest)
```

Logic:
1. Check Sync Shield: does weatherAtHarvest match seed.preferredWeather?
2. Build probability table (base rates or redistributed if shield active)
   - Shield active: D=0%, C=50%, B=30%, A=15%, S=5%
   - Shield inactive: D=15%, C=55%, B=20%, A=8%, S=2%
3. Apply seed special conditions (e.g., if condition met, add bonusPercent to targetTier, reduce proportionally from others)
4. Roll random against cumulative distribution
5. Calculate dewdropValue = seed.baseSellPrice * tier.valueMultiplier
6. Return HarvestResult

### PlantManager Refactor (Multi-Slot)

File: `Assets/Scripts/Managers/PlantManager.cs`

Changes:
- `List<PlantSlot> slots` replaces single plant fields
- Update loop iterates all slots, calculates progress per slot
- Events: `OnSlotStateChanged(int slotIndex, PlantState)`, `OnSlotGrowthUpdated(int slotIndex, float)`

New methods:
- `Plant(SeedData seed, int environmentIndex, int slotIndex)`
- `HarvestResult Harvest(int slotIndex)` — calls HarvestEngine.Roll(), returns result (UI decides sell vs keep)
- `GetSlot(int envIndex, int slotIndex)` → PlantSlot
- `GetSlotsForEnvironment(int envIndex)` → List<PlantSlot>
- `GetAllSlots()` → List<PlantSlot>

### EnvironmentManager (New Singleton)

File: `Assets/Scripts/Managers/EnvironmentManager.cs`

- Loads `EnvironmentData` assets from Resources
- `IsUnlocked(int envIndex)` → bool
- `Unlock(int envIndex)` → bool (spends Dewdrops)
- `GetGrowthBonus(int envIndex, WeatherData weather)` → float
- `GetTotalUnlockedSlots()` → int
- Event: `OnEnvironmentUnlocked(int envIndex)`

### SeedShopManager (New Singleton)

File: `Assets/Scripts/Managers/SeedShopManager.cs`

- `GetShopSeeds()` → List<SeedData> (all seeds with buyPrice > 0, plus starter)
- `BuySeed(string seedName)` → bool
- `CanBuy(string seedName)` → bool
- Event: `OnSeedPurchased(string seedName)`

### GreenhouseManager Extensions

- `AddPlant(SeedData, VariantData, QualityTier)` — stores quality tier
- `SellPlant(int index)` → int dewdrops (removes plant, returns sell value)
- Dust per hour factors in quality tier multiplier

### CurrencyConfig Extensions

- Add `GetSellValue(int baseSellPrice, QualityTier tier)` → int
- Quality tier multiplier constants

---

## 3. UI Architecture

### New Panels

**Seed Shop Panel (`SeedShopUI`)**
- Nav bar button: "Shop"
- Grid of 5 seed cards (SeedShopCard.uxml template)
- Each card: icon, name, price, special condition text, Buy button
- Disabled state when can't afford

**Harvest Result Popup (`HarvestResultUI`)**
- Modal overlay on harvest
- Plant visual, variant name, quality tier letter (animated, color-coded)
- Tier colors: D=gray, C=green, B=blue, A=gold, S=purple
- Sync Shield indicator ("Weather Sync!" badge)
- Dewdrop payout display
- Two buttons: "Sell" and "Keep" (Keep disabled if greenhouse full)

**Terrarium View (replaces Greenhouse panel)**
- 4 environment sections in vertical scroll
- Each section: name, slot grid, lock/unlock state
- Locked environments: cost + Unlock button
- Slot states: growing (progress), mature (harvest!), greenhouse plant (dust), empty (plant)
- Tap interactions per slot state

### Modified Panels

**SatchelUI**: Tracks target slot, shows seed special conditions in probability preview

**PulseButton**: Becomes multi-slot status. Shows "X plants ready!" summary, tapping opens terrarium to first mature plant.

**HortusUI Main Screen**: Centerpiece shows "featured" plant (first growing or first mature). Mini slot indicator row below.

### New Templates
- `SeedShopCard.uxml`
- `HarvestResultPopup.uxml`
- `EnvironmentSection.uxml`
- `TerrariumSlot.uxml`

### New Styles
- `SeedShop.uss`
- `HarvestResult.uss`
- `Terrarium.uss`
- Quality tier utility classes (`.tier-d` through `.tier-s`)

---

## 4. Seed Catalog

### Shop Inventory
| Seed | Buy Price | Base Sell (C-Tier) | Special Condition |
|------|-----------|-------------------|-------------------|
| Astra | 100 | 120 | None (Starter) |
| Cinder-Fern | 450 | 550 | +10% S-Tier if Temp > 25°C |
| Mist-Vine | 800 | 1,000 | +10% S-Tier if Humidity > 70% |
| Luna-Petal | 1,500 | 1,900 | Only grows during Nighttime |
| Storm-Root | 3,000 | 4,000 | +20% S-Tier during Wind/Rain |

### Variant Plan (12 per seed, 60 total)
Each seed follows the same 12-trigger structure as Astra:
1. Base (default)
2. Frost (Temp < 5°C)
3. Desert (Temp > 38°C)
4. Dew (Humidity > 80%)
5. Tempest (High Wind)
6. Lunar (Night + Clear)
7. Solar (Bright Daylight)
8. Celestial (Equinox/Eclipse)
9. Biolume (New Moon)
10. Nebula (Golden Hour)
11. Static (Thunderstorm)
12. Void (New Moon + Midnight)

Visuals and names themed per seed (e.g., Cinder-Fern's frost variant = "Ember-Glass Fern").

---

## 5. Testing Strategy

- Unit tests for HarvestEngine probability distribution (verify base rates, shield rates, special conditions)
- Unit tests for PlantSlot multi-slot growth calculations
- Unit tests for EnvironmentManager unlock/bonus logic
- Integration test for full harvest flow (plant → grow → harvest → sell/keep)
- Existing 7 tests updated for PlantManager API changes
