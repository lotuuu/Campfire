# Items Table Design

**Date**: 2026-03-13
**Status**: Draft
**Scope**: Server items table, config migration to item keys, client inventory unification

## Problem

Items (seeds, harvest drops, pigments, potions, materials, consumables) have no formal definition. They exist only as implicit string names scattered across configs, recipes, building costs, visitor trades, and economy endpoints. This causes:

- Fragile string references prone to typos and casing mismatches (e.g., `"Speed_Potion"` vs `"speed_potion"`)
- Categorization inferred from naming conventions (`_Seed`, `_Pigment` suffixes) rather than explicit metadata
- No validation that a referenced item actually exists
- Renaming an item requires finding and updating every string occurrence across server configs, client code, and admin UI
- Speed item mismatches between client and server (the bug that prompted this redesign)

## Design

### Items Table

New Ecto schema `CampFire.Game.Item` in the `Game` context:

| Column | Type | Notes |
|--------|------|-------|
| `item_key` | `string` | **Primary key**. Snake_case identifier, e.g. `"speed_potion"`, `"basil"`, `"basil_seed"` |
| `display_name` | `string` | Human-readable name, e.g. `"Speed Potion"`, `"Basil"`, `"Basil Seed"` |
| `category` | `string` | One of: `"seed"`, `"harvest"`, `"pigment"`, `"potion"`, `"material"`, `"consumable"` |
| `sprite_key` | `string` | Optional override for sprite resolution. If null, client derives from `item_key` |
| `inserted_at` | `utc_datetime` | |
| `updated_at` | `utc_datetime` | |

### Categories

| Category | Examples | Description |
|----------|----------|-------------|
| `seed` | `sprouts_seed`, `basil_seed` | Plantable seeds |
| `harvest` | `sprouts`, `basil`, `chamomile` | Drops from harvesting plots |
| `pigment` | `basil_pigment`, `chamomile_pigment` | Crafted dye items |
| `potion` | `speed_potion` | Crafted potions |
| `material` | `fertilizer` | Crafted materials |
| `consumable` | `energy_drink` | Granted/purchased utility items |

### Seed Unification

Seeds become items with `category: "seed"`. The existing `seed_configs` table gets a new `item_key` column (FK to `items`) linking the seed config to its seed item. `seed_configs` also gets a `harvest_item_key` column (FK to `items`) linking to the corresponding harvest drop. `seed_configs` retains all its existing fields (`growth_duration_hours`, `min_drops`, `max_drops`, `tier`, `recipe`).

Naming convention:
- `basil` = harvest drop item (category: `"harvest"`)
- `basil_seed` = plantable seed item (category: `"seed"`)

The `harvest_item_key` field makes the seed-to-harvest mapping explicit. `plots.ex` uses it at harvest time to know which harvest item to grant, and at plant time uses `item_key` to know which seed item to consume.

### Config References Migration

All config JSON values switch from display-name strings to item keys. The JSON field name changes from `itemName`/`item_name` to `itemKey` (camelCase in API responses, matching the existing convention).

**Building costs** (`plot_costs`, `vase_costs`, `garden_costs`):
```json
// Before
{"itemName": "Cress", "count": 1}
// After
{"itemKey": "cress", "count": 1}
```

**Upgrade recipes**:
```json
// Before
{"ingredients": [{"itemName": "Sprouts", "count": 10}]}
// After
{"ingredients": [{"itemKey": "sprouts", "count": 10}]}
```

**Recipe configs**:
```json
// Before
"Speed_Potion": {"ingredients": [{"item_name": "Mint", "count": 4}], "result_item": "Speed_Potion"}
// After
"speed_potion": {"ingredients": [{"itemKey": "mint", "count": 4}], "result_item": "speed_potion"}
```

**Speed items**:
```json
// Before
"plot_config": {"speed_item": "Speed_Potion"}
"vase_config": {"speed_item": "Energy_Drink"}
"mallum_house_config": {"quest_speed_item": "Energy_Drink"}
// After
"plot_config": {"speed_item": "speed_potion"}
"vase_config": {"speed_item": "energy_drink"}
"mallum_house_config": {"quest_speed_item": "energy_drink"}
```

**New player config**:
```json
// Before
{"seeds": [{"name": "Sprouts", "count": 2}], "items": [{"name": "Speed_Potion", "count": 3}]}
// After
{"items": [{"itemKey": "sprouts_seed", "count": 2}, {"itemKey": "speed_potion", "count": 3}, {"itemKey": "energy_drink", "count": 2}]}
```

**Garden yields**: `yield_item` changes from `"Berry"` to `"berry"`.

**Quest reward pools**: Change from `"seed" => "Sprouts"` to `"itemKey" => "sprouts_seed"`.

**Visitor templates**:
- `offer_pool` costs: `"itemName" => "Basil Leaf"` becomes `"itemKey" => "basil"` (harvest drops, not "leaves" — there are no separate leaf items)
- `gift_pool`: `{"type" => "seed", "name" => "Chamomile"}` becomes `{"itemKey" => "chamomile_seed"}`; `{"type" => "item", "name" => "Basil Leaf"}` becomes `{"itemKey" => "basil"}`
- `quest_pool`: `"request_item" => "Lavender Petal"` becomes `"request_item_key" => "lavender"`

**Skin costs**: `"cost_item_name" => "Basil_Pigment"` becomes `"cost_item_key" => "basil_pigment"`.

**Seed-time validation**: A helper `item!(key)` in seeds.exs asserts the item exists in the DB and returns the key. Typos in configs fail at seed time, not runtime.

### Server Endpoints & Economy

**`player_inventory` table**: Rename `item_name` to `item_key`. Add FK constraint to `items` table with `ON UPDATE CASCADE` for safe item key renames.

**Economy functions** — same interface, column rename:
- `upsert_item(player_uid, item_key, count)`
- `spend_item(player_uid, item_key, count)`
- `list_inventory(player_uid)` — returns `item_key` instead of `item_name`

**API JSON convention**: Server stores `item_key` (snake_case) internally. API responses use `itemKey` (camelCase) to match the existing convention used throughout the API (`itemName`, `seedName`, `questName`, `flameLevel`, etc.).

**API inventory payloads**:
```json
// Before
{"itemName": "Speed_Potion", "count": 3}
// After
{"itemKey": "speed_potion", "count": 3}
```

**`format_inventory` in economy_controller.ex**:
```elixir
# Before
%{itemName: i.item_name, count: i.count}
# After
%{itemKey: i.item_key, count: i.count}
```

**`/api/game/configs` response** adds an `items` map:
```json
{
  "items": {
    "speed_potion": {"displayName": "Speed Potion", "category": "potion", "spriteKey": null},
    "basil": {"displayName": "Basil", "category": "harvest", "spriteKey": null},
    "basil_seed": {"displayName": "Basil Seed", "category": "seed", "spriteKey": null}
  },
  "flame_config": {},
  "seed_configs": []
}
```

**`init_economy`**: New player grants reference item keys from `new_player_config`. Seeds and items are now a single list — no separate handling needed.

**Game endpoints** (plots, vases, mallums): Read speed items from config — configs now contain item keys. No endpoint logic changes beyond the column rename.

**`apotheke.ex`**: Recipe ingredient matching changes from `"item_name"` to `"itemKey"` in the pattern match.

### Client-Side Changes

**`ConfigService`**: New `ItemConfig` DTO:
```csharp
[Serializable]
public class ItemConfig
{
    public string displayName;
    public string category;
    public string spriteKey;
}
```

New fields: `Dictionary<string, ItemConfig> Items` parsed from config response. Helper methods: `GetItem(key)`, `GetItemDisplayName(key)`, `GetItemsByCategory(category)`.

**`SaveData`**: `InventoryItem` already holds all items in a single `inventory` list (seeds are already stored there as `"{Name}_Seed"` items). The only change is renaming the field:
```csharp
[Serializable]
public class InventoryItem
{
    public string itemKey;
    public int count;
}
```

`discoveredSeeds` list switches from plant names to item keys (e.g., `"basil"` instead of `"Basil"`).

**`RecipeData` ScriptableObjects**: These local `.asset` files in `Resources/Recipes/` store `IngredientEntry.itemName`. Two options exist:
1. Eliminate `RecipeData` SOs entirely — recipes are already server-driven via `recipe_configs`. `ApothekeManager` would load recipes from `ConfigService` instead of `Resources.LoadAll<RecipeData>()`.
2. Keep SOs but update the field name.

Option 1 is preferred — it aligns with the server-authoritative architecture and removes the dual-path (local SO + server config). `ApothekeManager.CraftOnServer()` already sends the recipe name to the server, which validates against `recipe_configs`. The local SOs are redundant.

**`ApothekeUI` categorization**: Replace suffix-based convention parsing with `ConfigService.Instance.GetItem(key)?.category`.

**`SpriteService`**: `ItemToSpriteKey()` and `SeedToSpriteKey()` simplified:
1. Check `ItemConfig.spriteKey` from config — if non-null, use it directly
2. If null, derive from item_key: `items/{item_key}` for most items, with category-based subpath (e.g., `items/basil/seed`, `items/basil/harvest`, `items/basil/pigment`)

**`EconomyService`**: Request payloads use `itemKey` instead of `item_name`. `ApplyServerState` parsing updates to match.

**Config DTOs** (`HarvestCost`, `FlameIngredient`, `BuildingCost` in `ConfigTypes.cs`): Rename `itemName` field to `itemKey`.

**All managers** (PlotManager, VaseManager, MallumManager, FlameManager, GardenManager, ApothekeManager, GameManager, BirdManager): Replace `itemName`/`seedName` string references with `itemKey`.

**UI display names**: Anywhere an item name is shown to the player, look up `ConfigService.Instance.GetItemDisplayName(key)` instead of using the raw key. Affects ApothekeUI, CampsiteViewUI, QuestUI, BuildUI, BuildCardHelper, RewardRevealUI.

### Migration & Data Reset

**New Ecto migration**:
1. Create `items` table
2. Add `item_key` and `harvest_item_key` columns to `seed_configs` (FK to items, with ON UPDATE CASCADE)
3. Rename `item_name` → `item_key` in `player_inventory`, add FK constraint with ON UPDATE CASCADE

**Wipe player data**: No save migration. Early development, no live users.

**Client save**: `SaveManager` detects old format via `SaveData.version` bump and resets to fresh save.

**Seeding order in seeds.exs**:
1. Items (all definitions — seeds, harvests, pigments, potions, materials, consumables)
2. Seed configs (with `item_key` and `harvest_item_key` FKs)
3. Quest configs (reward pools reference item keys)
4. Garden configs (yield items reference item keys)
5. Game configs (flame, vase, plot, mallum house, recipes, skins, new player)
6. Visitor templates (offer/gift pools reference item keys)

## Complete Item Registry

All items to be seeded:

**Seeds** (category: `"seed"`):
`sprouts_seed`, `cress_seed`, `basil_seed`, `chamomile_seed`, `marigold_seed`, `snowdrop_seed`, `mint_seed`, `lavender_seed`, `pansy_seed`, `poppy_seed`, `jasmine_seed`, `rosemary_seed`, `dahlia_seed`, `moonflower_seed`

**Harvests** (category: `"harvest"`):
`sprouts`, `cress`, `basil`, `chamomile`, `marigold`, `snowdrop`, `mint`, `lavender`, `pansy`, `poppy`, `jasmine`, `rosemary`, `dahlia`, `moonflower`, `berry`, `acorn`

**Pigments** (category: `"pigment"`):
`basil_pigment`, `chamomile_pigment`, `dahlia_pigment`, `jasmine_pigment`, `lavender_pigment`, `marigold_pigment`, `mint_pigment`, `moonflower_pigment`, `pansy_pigment`, `poppy_pigment`, `rosemary_pigment`, `snowdrop_pigment`

**Potions** (category: `"potion"`):
`speed_potion`

**Materials** (category: `"material"`):
`fertilizer`

**Consumables** (category: `"consumable"`):
`energy_drink`

## Files Affected

### Server
- `server/lib/camp_fire/game/item.ex` — new schema
- `server/lib/camp_fire/game.ex` — item query functions
- `server/lib/camp_fire/economy.ex` — `item_name` → `item_key`
- `server/lib/camp_fire/economy/player_inventory.ex` — column rename + FK
- `server/lib/camp_fire/game/plots.ex` — item key references, use `harvest_item_key` from seed_config
- `server/lib/camp_fire/game/vases.ex` — item key references
- `server/lib/camp_fire/game/mallums.ex` — item key references
- `server/lib/camp_fire/game/apotheke.ex` — recipe ingredient pattern match update
- `server/lib/camp_fire/config_cache.ex` — serve items in config response
- `server/lib/camp_fire_web/controllers/game_controller.ex` — include items in configs
- `server/lib/camp_fire_web/controllers/economy_controller.ex` — `itemKey` in payloads
- `server/lib/camp_fire_web/live/items_live.ex` — admin panel updates
- `server/priv/repo/migrations/*_create_items.exs` — new migration
- `server/priv/repo/seeds.exs` — restructured with items-first seeding

### Client
- `Assets/Scripts/Services/ConfigService.cs` — ItemConfig DTO, items dictionary
- `Assets/Scripts/Data/SaveData.cs` — `itemName` → `itemKey` on InventoryItem, `discoveredSeeds` uses item keys
- `Assets/Scripts/Data/ConfigTypes.cs` — `itemName` → `itemKey` on HarvestCost, BuildingCost, FlameIngredient
- `Assets/Scripts/Data/RecipeData.cs` — eliminate SO in favor of server-driven recipes, or rename field
- `Assets/Scripts/Services/EconomyService.cs` — `itemKey` in requests and `ApplyServerState` parsing
- `Assets/Scripts/Services/SpriteService.cs` — sprite_key config lookup, simplify `ItemToSpriteKey`/`SeedToSpriteKey`
- `Assets/Scripts/Services/SaveManager.cs` — version bump, old save reset
- `Assets/Scripts/Managers/PlotManager.cs` — item key references
- `Assets/Scripts/Managers/VaseManager.cs` — item key references
- `Assets/Scripts/Managers/MallumManager.cs` — item key references
- `Assets/Scripts/Managers/FlameManager.cs` — item key references
- `Assets/Scripts/Managers/GardenManager.cs` — item key references
- `Assets/Scripts/Managers/ApothekeManager.cs` — item key references, switch to server-driven recipes
- `Assets/Scripts/Managers/GameManager.cs` — new player setup uses item keys
- `Assets/Scripts/Managers/BirdManager.cs` — seed drop references
- `Assets/Scripts/UI/ApothekeUI.cs` — category from config, display names
- `Assets/Scripts/UI/CampsiteViewUI.cs` — display names, item keys
- `Assets/Scripts/UI/QuestUI.cs` — item key references
- `Assets/Scripts/UI/BuildUI.cs` — item key in cost display
- `Assets/Scripts/UI/BuildCardHelper.cs` — `LoadHarvestIcon` uses item key
- `Assets/Scripts/UI/RewardRevealUI.cs` — reward display uses item keys
- `Assets/Tests/EditMode/` — any tests referencing item names
