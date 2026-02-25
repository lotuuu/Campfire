# Consumables Design

## Goal

Six single-use consumable items purchasable in the Shop with Gold, applicable to the Backyard. Each consumable has a persistent world-space visual and an effect that stays active after application.

---

## Consumable Scopes

**Slot-scoped** (applied to a specific plant slot, cleared on harvest):
- **Fertilizer** — +1.0 additive growth multiplier on that slot
- **Quality Dirt** — forces Sync Shield quality probability table at harvest

**Environment-scoped** (applied to the whole Backyard, persist indefinitely):
- **Fan** — adds `magnitude` m/s to `windSpeed` in effective weather for all slots
- **Igloo** — subtracts `magnitude` °C from `temperature` for all slots
- **Heater** — adds `magnitude` °C to `temperature` for all slots
- **Cloud** — forces `WeatherCondition.Rain` for all slots

Default magnitudes: Fan = 5 m/s, Igloo = 10°C, Heater = 10°C.

No same-type stacking: at most one of each ConsumableType per slot/env.

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
public int buyPrice;           // Gold
public float magnitude;        // Fan: m/s; Igloo/Heater: °C delta; unused for others
public bool isEnvironmentScoped; // true for Fan, Igloo, Heater, Cloud
[TextArea] public string description;
```

### SaveData additions
- `List<ConsumableInventoryEntry> consumableInventory` — same shape as `seedInventory`
- `List<EnvironmentConsumableSave> environmentConsumables` — env-level applied consumables
- `PlantSlotSave` gains `List<string> appliedConsumables` — slot-scoped only (Fertilizer, QualityDirt)

### PlantSlot (runtime)
- Gains `List<ConsumableData> appliedConsumables` — slot-scoped only

---

## Effects

Effective weather is computed per-slot at two moments:
1. `PlantManager.RefreshMultipliers()` — re-evaluates variant trigger match → growth speed
2. `HarvestEngine.Roll()` — quality probability roll

For each slot, effective weather = global weather + env-scoped consumable overrides (Fan/Igloo/Heater/Cloud). The global `WeatherService.CurrentWeather` is never modified.

Fertilizer and Quality Dirt affect growth and quality independently, not via effective weather.

---

## Architecture

### New files
- `Assets/Scripts/Data/ConsumableType.cs` — enum
- `Assets/Scripts/Data/ConsumableData.cs` — ScriptableObject
- `Assets/Scripts/Managers/ConsumableManager.cs` — singleton; owns purchase, apply, inventory/env queries
- `Assets/Resources/Consumables/*.asset` — 6 ConsumableData assets

### Modified files
- `SaveData.cs` — add `consumableInventory`, `environmentConsumables`; add `appliedConsumables` to `PlantSlotSave`
- `PlantSlot.cs` — add `List<ConsumableData> appliedConsumables`
- `PlantManager.cs` — compute effective weather per slot from env consumables; fold Fertilizer into growth; `ApplyConsumable` (slot-scoped); `ForceRefreshMultipliers()` public
- `HarvestEngine.cs` — accept `bool qualityBoosted` parameter
- `SeedShopUI.cs` — add "Consumables" section after seeds
- `BackyardViewUI.cs` — circle button + dropdown + split apply flow (env-scoped: immediate; slot-scoped: tap-a-slot)
- `BackyardIsometricView.cs` — `consumablePrefabs[]`; slot-tile children for Fertilizer/QualityDirt; env-level GOs at fixed position for Fan/Igloo/Heater/Cloud

---

## UI

### Shop
- Section header "Consumables" in the shop scroll, below all seeds
- Same card template as seeds — icon, name, price in Gold, owned count
- Sorted by buyPrice

### Backyard consumable picker
- Small circle `Button` anchored to the right edge of the terrarium page, vertically centered
- Tapping toggles a vertical dropdown panel to the left of the button
- Each row: displayName + `x{count}` (only shows owned consumables)
- Tapping a row:
  - **Environment-scoped** (Fan/Igloo/Heater/Cloud): applies immediately to the whole Backyard, closes dropdown
  - **Slot-scoped** (Fertilizer/Quality Dirt): closes dropdown, enters apply mode

### Apply mode (slot-scoped only)
- Slot overlay buttons highlight with yellow border
- Tapping a slot: removes from inventory, adds to slot's appliedConsumables, spawns tile visual, exits apply mode
- Tapping the circle button again cancels apply mode

### World-space visuals
- `BackyardIsometricView` gains `GameObject[] consumablePrefabs` (length 6, indexed by `(int)ConsumableType`)
- **Slot-scoped**: instantiate as child of that tile's GameObject, offset beside the plant
- **Env-scoped**: instantiate as child of `BackyardIsometricView` root at fixed position; tracked by `Dictionary<ConsumableType, GameObject>`, replaced on re-apply
- On slot harvest: destroy slot-scoped consumable GOs for that tile
- On scene restore: re-instantiate from save data
