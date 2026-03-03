# Night Merchant System Design

## Overview

A Night Merchant appears on the campsite grid every night from 10 PM to midnight (local time). It claims a free hex tile (same pattern as birds). The merchant offers 2-3 randomized trades gated by flame level — player gives harvest items/crafted items, receives rare seeds. The merchant disappears at midnight or when stale on load.

## Data Model

### MerchantData (ScriptableObject)

Single asset for now (`Resources/Merchants/NightMerchant.asset`), extensible to multiple later.

- `merchantName` (string)
- `flavorText` (string)
- `List<MerchantOffer> offerPool` — all possible trades this merchant can offer

Each `MerchantOffer`:
- `int requiredFlameLevel` — minimum flame level for this offer to appear in the pool
- `List<TradeCost> costs` — item name + count pairs the player must pay
- `SeedData rewardSeed` — the seed given in return
- `int rewardCount` — how many seeds
- `float weight` — for weighted random selection when rolling offers

`TradeCost`:
- `string itemName` — matches `InventoryItem.itemName` in SaveData
- `int count`

### MerchantSave (Serializable)

Stored in `SaveData.merchants`:
- `int gridX`, `int gridY` — hex position on campsite
- `string merchantName` — resolves to MerchantData asset
- `List<MerchantOfferSave> offers` — the 2-3 rolled offers for this visit (persisted so they survive app backgrounding)
- `string appearedAtUtc` — ISO timestamp of when merchant appeared

`MerchantOfferSave`:
- `List<TradeCost> costs`
- `string rewardSeedName`
- `int rewardCount`

### SaveData Additions

- `List<MerchantSave> merchants = new()`
- `string lastMerchantDateUtc` — once-per-day gate (UTC date string, same format as `lastVisitorDateUtc`)

## Arrival / Departure Logic (MerchantManager)

Singleton MonoBehaviour following the existing manager pattern.

### Arrival
- `Update()` checks `GameTime.Now.Hour >= 22` (10 PM local) AND `lastMerchantDateUtc != today's UTC date`
- On trigger: finds a free tile via the same pattern as `BirdManager.GetFreeTiles()`, picks a random free tile
- Rolls 2-3 offers from `MerchantData.offerPool` filtered by current flame level, using weighted random selection
- Creates `MerchantSave` with the tile position and rolled offers
- Sets `lastMerchantDateUtc` to today
- Fires `OnMerchantArrived` event

### Departure
- `Update()` checks if local hour is >= 0 and < 22 (i.e., past midnight or daytime) — removes any active merchants
- On load: removes any merchants whose `appearedAtUtc` date is not today (stale cleanup)
- Fires `OnMerchantDeparted` event

### Static Helpers
- Core logic (offer rolling, trade validation, trade execution) exposed as `public static` methods for testability, following `MallumManager` pattern.

## Trade Execution

1. Player taps merchant tile on campsite grid
2. Merchant overlay panel slides up (same pattern as Apotheke panel)
3. Panel shows 2-3 offers, each with cost items on left, arrow, reward seed on right, Accept button
4. Accept button disabled if player lacks required items
5. On accept: consume items from `SaveData.items` via existing inventory helpers, add seeds via `ApothekeManager.AddSeed()`
6. Offer remains visible for repeat trades — player can trade as many times as they have items
7. Merchant stays until midnight regardless of trades made

## Grid Integration

- Add `Merchant` to `CampBuildingType` enum in `GameEnums.cs`
- Extend occupied tile tracking to include merchant positions (so birds and other entities don't overlap)
- `CampsiteViewUI` handles merchant tile taps → opens merchant overlay

## UI Components

### Campsite Tile
- Merchant rendered on hex tile with a sprite (placeholder for now)
- Visual indicator that it's interactable (same pattern as bird tiles)

### MerchantUI (Overlay Panel)
- Slide-up panel (same pattern as ApothekeUI, BuildUI)
- Header with merchant name and flavor text
- List of 2-3 offer rows
- Each offer row: cost items (icon + count) → arrow → reward seed (icon + count) + Accept button
- Template: `Resources/UI/Templates/MerchantOfferRow.uxml`
- Styles in `Assets/UI/Styles/Merchant.uss`

## File Locations

- `Assets/Scripts/Data/MerchantData.cs` — ScriptableObject + MerchantOffer, TradeCost structs
- `Assets/Scripts/Data/MerchantSave.cs` — MerchantSave, MerchantOfferSave serializable structs
- `Assets/Scripts/Managers/MerchantManager.cs` — singleton manager
- `Assets/Scripts/UI/MerchantUI.cs` — overlay panel controller
- `Assets/Resources/Merchants/NightMerchant.asset` — merchant data asset
- `Assets/Resources/UI/Templates/MerchantOfferRow.uxml` — offer row template
- `Assets/UI/Styles/Merchant.uss` — merchant panel styles
- `Assets/Tests/EditMode/MerchantManagerTests.cs` — unit tests
