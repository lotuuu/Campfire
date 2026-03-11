# Unified Inventory Design Spec

**Goal:** Merge `SeedInventoryEntry` and `InventoryItem` into a single `InventoryItem` type with one `inventory` list, and rename items so harvests get clean names (e.g. `Basil`) and seeds get a suffix (e.g. `Basil_Seed`).

## Naming Convention

| Category | Old key | New key |
|----------|---------|---------|
| Seeds | `Basil` | `Basil_Seed` |
| Harvests | `Basil_harvest` | `Basil` |
| Pigments | `Basil_pigment` | `Basil_Pigment` |
| Potions | `Speed_Potion` | `Speed_Potion` |
| Other | `Energy_Drink` | `Energy_Drink` |

## Client Changes

- Delete `SeedInventoryEntry`. Keep `InventoryItem { itemName, count }` as the single type.
- `SaveData`: replace `seedInventory` + `items` with single `inventory` list.
- `ApothekeManager.AddSeed(name, count)` becomes `AddItem(name + "_Seed", count)` (or keep `AddSeed` as convenience).
- `PlotManager.Plant()`: consumes `seedName + "_Seed"` from inventory. `Harvest()`: adds `seedName` (no suffix) to inventory.
- `VisitorManager.ExecuteTrade()`: single list parameter instead of items + seeds.
- `EconomyService` DTOs: merge `AddSeedRequest`/`SpendSeedRequest` into `AddItemRequest`/`SpendItemsRequest`. Merge `add-seeds`/`spend-seeds` into `add-items`/`spend-items`.
- `GameService`: single inventory list from server response.
- `SocialService`: no seed/item type branching, just inventory operations.

## Server Changes

- Merge `player_seeds` and `player_items` DB tables into `player_inventory`.
- Merge `PlayerSeed` and `PlayerItem` schemas into `PlayerInventory`.
- Remove `add-seeds`/`spend-seeds` endpoints; `add-items`/`spend-items` handle everything.
- Update `seeds.exs`: rename all `_harvest` references to bare names, seed references to `_Seed` suffix.
- Update economy controller, economy context, admin, debug modules.

## What Doesn't Change

- `PlotSave.seedName` stays as plant name (e.g. `Basil`) — it's what's planted, not an inventory key.
- Hex sprite keys (`hex/plot/basil/0`) stay as-is.
- Server seed configs use plant names — the `_Seed` suffix is only for inventory.
- Sprite keys under `items/` stay as-is (display concern).
