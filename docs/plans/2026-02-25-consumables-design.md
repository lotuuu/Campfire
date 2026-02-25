# Consumables Design

## Goal

Six single-use consumable items purchasable in the Shop with Gold, applicable to Backyard plant slots. Each consumable has a persistent world-space visual on the tile and an effect active until the plant is harvested.

---

## Data Model

### ConsumableType enum
```csharp
public enum ConsumableType { Fertilizer, QualityDirt, Fan, Igloo, Heater, Cloud }
```

### ConsumableData : ScriptableObject
```csharp
public ConsumableType type;
public string displayName;
public Sprite icon;
public int buyPrice;      // Gold
public float magnitude;   // Fan: m/s added; Igloo/Heater: °C delta; unused for others
```

Default magnitudes: Fan = 5 m/s, Igloo = 10°C, Heater = 10°C.

### SaveData additions
- `List<ConsumableInventoryEntry> consumableInventory` — same shape as `seedInventory`
- `PlantSlotSave` gains `List<string> appliedConsumables` — names of applied ConsumableData assets

### PlantSlot (runtime)
- Gains `List<ConsumableData> appliedConsumables`

**No same-type stacking**: at most one of each ConsumableType per slot.

---

## Effects

Effects are applied by computing an **effective WeatherData** per slot, and by per-slot flags/bonuses. The global `WeatherService.CurrentWeather` is never modified.

| Consumable   | Effect |
|---|---|
| Fertilizer   | +1.0 additive to the slot's growth multiplier (same mechanism as env bonus) |
| Quality Dirt | Sets `qualityBoosted = true` on slot; HarvestEngine uses Sync Shield probability table regardless of weather |
| Fan          | Adds `magnitude` m/s to `windSpeed` in effective weather |
| Igloo        | Subtracts `magnitude` °C from `temperature` in effective weather |
| Heater       | Adds `magnitude` °C to `temperature` in effective weather |
| Cloud        | Forces `WeatherCondition.Rain` in effective weather |

Effective weather is computed per-slot at two moments:
1. `PlantManager.RefreshMultipliers()` — re-evaluates variant trigger match → growth speed
2. `HarvestEngine.Roll()` — quality probability roll

---

## Architecture

### New files
- `Assets/Scripts/Data/ConsumableType.cs` — enum
- `Assets/Scripts/Data/ConsumableData.cs` — ScriptableObject
- `Assets/Scripts/Managers/ConsumableManager.cs` — singleton; owns purchase, apply, inventory queries
- `Assets/Resources/Consumables/*.asset` — 6 ConsumableData assets
- Consumable prefabs (world-space sprites) referenced by `BackyardIsometricView`

### Modified files
- `SaveData.cs` — add `consumableInventory`; add `appliedConsumables` to `PlantSlotSave`
- `PlantSlot.cs` — add `List<ConsumableData> appliedConsumables`, `bool qualityBoosted`
- `PlantManager.cs` — compute effective weather per slot; fold Fertilizer bonus into growth calc
- `HarvestEngine.cs` — accept `bool qualityBoosted` parameter; use Sync Shield table if true
- `SeedShopUI.cs` — add "Consumables" section after seeds
- `BackyardViewUI.cs` — add circle button + dropdown panel + apply-mode flow
- `BackyardIsometricView.cs` — add `consumablePrefabs` array (indexed by ConsumableType); spawn/destroy on apply/harvest

---

## UI

### Shop
- Section header "Consumables" rendered as a label in the shop scroll, below all seeds
- Consumable cards: same template as seed cards — icon, name, price in Gold, owned count
- Sorted by buyPrice

### Backyard consumable picker
- Small circle `Button` anchored to the right edge of the terrarium page, vertically centered; added by `BackyardViewUI.Initialize()`
- Tapping toggles a vertical `ScrollView` panel to the left of the button
- Each row: `ConsumableData.icon` + displayName + `x{count}`
- Tapping a row selects that consumable type and closes the dropdown → enters apply mode

### Apply mode
- Slot overlay buttons pulse (CSS class toggle)
- Tapping a slot: removes consumable from inventory, adds to slot's appliedConsumables list, triggers `BackyardIsometricView` to spawn the world prefab, exits apply mode
- Tapping anywhere outside a slot cancels apply mode

### World-space slot visuals
- `BackyardIsometricView` gains `[SerializeField] private GameObject[] consumablePrefabs` (length 6, indexed by `(int)ConsumableType`)
- On apply: instantiate the prefab as child of that tile's GameObject, offset to the side of the plant GO
- On harvest: destroy all consumable GOs on that tile
- On scene restore (load): re-instantiate from `PlantSlotSave.appliedConsumables`
