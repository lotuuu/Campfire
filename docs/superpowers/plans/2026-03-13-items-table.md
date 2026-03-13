# Items Table Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace implicit string-based item references with a formal `items` database table, unifying seeds and items under a single registry with snake_case keys.

**Architecture:** New `items` table in Game context with `item_key` (string PK). All configs, economy, and inventory switch from display-name strings to item keys. API responses use camelCase (`itemKey`) matching existing convention. Client merges seed/item inventory and gets item metadata (display names, categories) from server config.

**Tech Stack:** Elixir/Phoenix (server), Unity C# (client), Ecto migrations, PostgreSQL

**Spec:** `docs/superpowers/specs/2026-03-13-items-table-design.md`

---

## Chunk 1: Server Schema & Migration

### Task 1: Create Item schema and migration

**Files:**
- Create: `server/lib/camp_fire/game/item.ex`
- Create: `server/lib/camp_fire/game.ex` (new Game context facade module)
- Create: `server/priv/repo/migrations/*_create_items_table.exs`

- [ ] **Step 1: Create the Item Ecto schema**

```elixir
# server/lib/camp_fire/game/item.ex
defmodule CampFire.Game.Item do
  use Ecto.Schema
  import Ecto.Changeset

  @primary_key {:item_key, :string, autogenerate: false}

  schema "items" do
    field :display_name, :string
    field :category, :string
    field :sprite_key, :string

    timestamps()
  end

  @valid_categories ~w(seed harvest pigment potion material consumable)

  def changeset(item, attrs) do
    item
    |> cast(attrs, [:item_key, :display_name, :category, :sprite_key])
    |> validate_required([:item_key, :display_name, :category])
    |> validate_inclusion(:category, @valid_categories)
    |> unique_constraint(:item_key, name: :items_pkey)
  end
end
```

- [ ] **Step 2: Create the Ecto migration**

Run: `cd server && mix ecto.gen.migration create_items_table`

Then edit the generated migration:

```elixir
defmodule CampFire.Repo.Migrations.CreateItemsTable do
  use Ecto.Migration

  def change do
    # 1. Create items table
    create table(:items, primary_key: false) do
      add :item_key, :string, primary_key: true
      add :display_name, :string, null: false
      add :category, :string, null: false
      add :sprite_key, :string

      timestamps()
    end

    create index(:items, [:category])

    # 2. Add item_key and harvest_item_key to seed_configs
    alter table(:seed_configs) do
      add :item_key, references(:items, column: :item_key, type: :string, on_update: :update_all)
      add :harvest_item_key, references(:items, column: :item_key, type: :string, on_update: :update_all)
    end

    create unique_index(:seed_configs, [:item_key])
    create unique_index(:seed_configs, [:harvest_item_key])

    # 3. Rename item_name → item_key in player_inventory
    #    Must drop and recreate unique constraint since column name changes
    drop unique_index(:player_inventory, [:player_uid, :item_name])
    rename table(:player_inventory), :item_name, to: :item_key
    create unique_index(:player_inventory, [:player_uid, :item_key])

    # 4. Add FK from player_inventory.item_key → items.item_key
    #    Done as a separate flush/step to avoid issues with rename + modify in same migration
    flush()

    alter table(:player_inventory) do
      modify :item_key, references(:items, column: :item_key, type: :string, on_update: :update_all),
        from: :string
    end
  end
end
```

- [ ] **Step 3: Create Game context facade module and add item query functions**

Create new file `server/lib/camp_fire/game.ex` (this module does not exist yet — the Game "context" is currently just a namespace with submodules):

```elixir
defmodule CampFire.Game do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{Item, SeedConfig}

  def list_items do
    Repo.all(Item)
  end

  def list_items_by_category(category) do
    from(i in Item, where: i.category == ^category) |> Repo.all()
  end

  def get_item(item_key) do
    Repo.get(Item, item_key)
  end

  def get_item!(item_key) do
    Repo.get!(Item, item_key)
  end

  def get_seed_config!(seed_name) do
    Repo.get_by!(SeedConfig, seed_name: seed_name)
  end
end
```

- [ ] **Step 4: Run migration**

Run: `cd server && mix ecto.migrate`
Expected: Migration succeeds, creates items table, adds columns to seed_configs, renames player_inventory column.

- [ ] **Step 5: Commit**

```bash
git add server/lib/camp_fire/game/item.ex server/lib/camp_fire/game.ex server/priv/repo/migrations/*_create_items_table.exs
git commit -m "feat(server): add items table schema and migration"
```

---

### Task 2: Update PlayerInventory schema

**Files:**
- Modify: `server/lib/camp_fire/economy/player_inventory.ex`

- [ ] **Step 1: Rename item_name to item_key in schema**

Change `field :item_name, :string` to `field :item_key, :string`. Update the unique constraint.

Current (line 7): `field :item_name, :string`
New: `field :item_key, :string`

Also update the changeset/validation to use `:item_key`.

- [ ] **Step 2: Commit**

```bash
git add server/lib/camp_fire/economy/player_inventory.ex
git commit -m "refactor(server): rename player_inventory.item_name to item_key"
```

---

### Task 3: Update SeedConfig schema

**Files:**
- Modify: `server/lib/camp_fire/game/seed_config.ex`

- [ ] **Step 1: Add item_key and harvest_item_key fields**

Add to the schema:
```elixir
field :item_key, :string
field :harvest_item_key, :string
```

Add both to the changeset cast list.

- [ ] **Step 2: Commit**

```bash
git add server/lib/camp_fire/game/seed_config.ex
git commit -m "feat(server): add item_key and harvest_item_key to seed_configs"
```

---

### Task 4: Update Economy context functions

**Files:**
- Modify: `server/lib/camp_fire/economy.ex`

- [ ] **Step 1: Rename all item_name references to item_key**

In `economy.ex`, find-and-replace across these functions:
- `upsert_item/3` (line ~180): parameter and query field `item_name` → `item_key`
- `spend_item/3,4` (line ~189): parameter and query field `item_name` → `item_key`
- `spend_items/2,3` (line ~213): parameter mapping `item_name` → `item_key`
- `spend_items_in_tx/3` (line ~285): parameter and query field
- `list_inventory/1`: already returns structs, field name changes via schema
- `init_economy/1` (line ~31): item grant loop — change `%{"name" => name}` handling to use `item_key` instead of constructing `seed_name <> "_Seed"`
- `upgrade_flame/2` (line ~145): ingredient spending — `%{"item_name" => name}` pattern match becomes `%{"itemKey" => key}`

For `init_economy`, the new_player_config changes from separate `seeds` and `items` lists to a single `items` list with item keys:
```elixir
# Before: separate seeds and items
seeds = config["seeds"] || []
Enum.each(seeds, fn %{"name" => name, "count" => count} ->
  upsert_item(player_uid, name <> "_Seed", count)
end)
items = config["items"] || []
Enum.each(items, fn %{"name" => name, "count" => count} ->
  upsert_item(player_uid, name, count)
end)

# After: single items list with item_key
items = config["items"] || []
Enum.each(items, fn %{"itemKey" => key, "count" => count} ->
  upsert_item(player_uid, key, count)
end)
```

- [ ] **Step 2: Verify compilation**

Run: `cd server && mix compile --warnings-as-errors`
Expected: Compiles with no errors (warnings about unused may be OK for now since controllers aren't updated yet).

- [ ] **Step 3: Commit**

```bash
git add server/lib/camp_fire/economy.ex
git commit -m "refactor(server): rename item_name to item_key in economy functions"
```

---

## Chunk 2: Server Seeds & Config

### Task 5: Rewrite seeds.exs with items-first seeding

**Files:**
- Modify: `server/priv/repo/seeds.exs`

This is the largest single change. The file must be restructured so items are seeded first, then everything else references item keys.

- [ ] **Step 1: Add item definitions at the top of seeds.exs (after imports)**

Add after the `import Ecto.Query` / `alias` block, before visitor templates:

```elixir
# --- Item Definitions ---
alias CampFire.Game.Item

# Helper: assert item exists in DB, return key (for use in configs below)
defmodule ItemHelper do
  def item!(key) do
    case CampFire.Repo.get(CampFire.Game.Item, key) do
      nil -> raise "Item '#{key}' not found — seed items before configs"
      item -> item.item_key
    end
  end
end

plants = ~w(sprouts cress basil chamomile marigold snowdrop mint lavender pansy poppy jasmine rosemary dahlia moonflower)

items =
  # Seeds
  Enum.map(plants, fn p ->
    %{item_key: "#{p}_seed", display_name: "#{String.capitalize(p)} Seed", category: "seed"}
  end) ++
  # Harvests
  Enum.map(plants, fn p ->
    %{item_key: p, display_name: String.capitalize(p), category: "harvest"}
  end) ++
  # Garden yields (not from plants list)
  [
    %{item_key: "berry", display_name: "Berry", category: "harvest"},
    %{item_key: "acorn", display_name: "Acorn", category: "harvest"}
  ] ++
  # Pigments (not all plants have pigment recipes — only tier 1+)
  (plants -- ~w(sprouts cress))
  |> Enum.map(fn p ->
    %{item_key: "#{p}_pigment", display_name: "#{String.capitalize(p)} Pigment", category: "pigment"}
  end) ++
  # Potions, materials, consumables
  [
    %{item_key: "speed_potion", display_name: "Speed Potion", category: "potion"},
    %{item_key: "fertilizer", display_name: "Fertilizer", category: "material"},
    %{item_key: "energy_drink", display_name: "Energy Drink", category: "consumable"}
  ]

for item <- items do
  %Item{}
  |> Item.changeset(item)
  |> Repo.insert!(
    on_conflict: {:replace, [:display_name, :category, :sprite_key, :updated_at]},
    conflict_target: :item_key
  )
end

IO.puts("Items seeded: #{length(items)}")
```

- [ ] **Step 2: Update seed_configs to include item_key and harvest_item_key**

Change the seed config insertion to include the new FK fields. Each seed config's `seed_name` (e.g., `"Sprouts"`) maps to:
- `item_key`: `"sprouts_seed"`
- `harvest_item_key`: `"sprouts"`

```elixir
# Add item_key and harvest_item_key to each seed config
seed_configs = [
  %{
    seed_name: "Sprouts",
    item_key: ItemHelper.item!("sprouts_seed"),
    harvest_item_key: ItemHelper.item!("sprouts"),
    growth_duration_hours: 0.00278,
    # ... rest unchanged
  },
  # ... repeat for all seeds
]
```

Update `replace_fields` to include `:item_key` and `:harvest_item_key`.

- [ ] **Step 3: Update game_configs to use item keys**

Replace all display-name item references with item keys throughout game_configs:

**flame_config upgrade_recipes**: `"itemName" => "Sprouts"` → `"itemKey" => ItemHelper.item!("sprouts")`

**flame_config plot_costs/vase_costs/garden_costs**: `"itemName" => "Cress"` → `"itemKey" => ItemHelper.item!("cress")`

**vase_config**: `"speed_item" => "Energy_Drink"` → `"speed_item" => ItemHelper.item!("energy_drink")`

**mallum_house_config**: `"quest_speed_item" => "Energy_Drink"` → `"quest_speed_item" => ItemHelper.item!("energy_drink")`

**mallum_house_config house_costs**: `"itemName" =>` → `"itemKey" =>`

**plot_config**: `"speed_item" => "Speed_Potion"` → `"speed_item" => ItemHelper.item!("speed_potion")`

**new_player_config**: Merge seeds and items into single list:
```elixir
%{key: "new_player_config", value: %{
  "mana" => 40,
  "gems" => 5,
  "starting_water" => 1,
  "items" => [
    %{"itemKey" => ItemHelper.item!("sprouts_seed"), "count" => 2},
    %{"itemKey" => ItemHelper.item!("speed_potion"), "count" => 3},
    %{"itemKey" => ItemHelper.item!("energy_drink"), "count" => 2}
  ]
}}
```

**recipe_configs**: Keys become item keys, all ingredient/result references use item keys:
```elixir
"basil_pigment" => %{
  "ingredients" => [%{"itemKey" => ItemHelper.item!("basil"), "count" => 3}],
  "result_item" => ItemHelper.item!("basil_pigment"),
  "result_quantity" => 1,
  "category" => "Pigment"
}
```

**skin_configs**: `"cost_item_name"` → `"cost_item_key"`, values become item keys.

- [ ] **Step 4: Update visitor templates to use item keys**

```elixir
# offer_pool: "Basil Leaf" → "basil" (these are harvest items)
offer_pool: [
  %{
    "costs" => [%{"itemKey" => "basil", "count" => 2}],
    "rewardItemKey" => "lavender_seed",
    "rewardCount" => 1
  },
  # ...
]

# gift_pool: unified item references
gift_pool: [
  %{"itemKey" => "chamomile_seed", "count" => 2},
  %{"type" => "water", "count" => 3},  # water is a resource, not an inventory item — keep type-based
  %{"itemKey" => "basil_seed", "count" => 3},
  %{"itemKey" => "basil", "count" => 2}
]

# quest_pool: request_item → request_item_key
quest_pool: [
  %{
    "request_item_key" => "lavender",
    "request_count" => 3,
    "return_days" => 7,
    "reward" => %{"itemKey" => "moonflower_seed", "count" => 2},
    "return_dialogue" => [...]
  }
]
```

- [ ] **Step 5: Update quest_configs reward pools**

```elixir
# Before
reward_pool: [%{"seed" => "Sprouts", "weight" => 3, "minCount" => 1, "maxCount" => 2}]
# After
reward_pool: [%{"itemKey" => "sprouts_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2}]
```

- [ ] **Step 6: Update garden_configs yield items**

```elixir
# Before
yield_item: "Berry"
# After
yield_item: "berry"
```

- [ ] **Step 7: Reset and re-seed database**

Run: `cd server && mix ecto.reset`
Expected: Database recreated, all seeds run successfully with item validation.

- [ ] **Step 8: Commit**

```bash
git add server/priv/repo/seeds.exs
git commit -m "feat(server): rewrite seeds with items-first seeding and item keys"
```

---

## Chunk 3: Server Game Modules & Controllers

### Task 6: Update game modules to use item keys

**Files:**
- Modify: `server/lib/camp_fire/game/plots.ex`
- Modify: `server/lib/camp_fire/game/vases.ex`
- Modify: `server/lib/camp_fire/game/mallums.ex`
- Modify: `server/lib/camp_fire/game/mallum_houses.ex`
- Modify: `server/lib/camp_fire/game/apotheke.ex`
- Modify: `server/lib/camp_fire/game/birds.ex`
- Modify: `server/lib/camp_fire/game/skins.ex`

- [ ] **Step 1: Update plots.ex**

Key changes:
1. Harvest function: Instead of `item_name = plot.seed_name`, look up the seed_config's `harvest_item_key`:
```elixir
# Before
item_name = plot.seed_name
Economy.upsert_item(player_uid, item_name, drops)

# After
seed_config = CampFire.Game.get_seed_config!(plot.seed_name)
Economy.upsert_item(player_uid, seed_config.harvest_item_key, drops)
```

2. Plant function: Consume seed from inventory using seed_config's `item_key`:
```elixir
# Before
Economy.spend_item(player_uid, plot.seed_name <> "_Seed", 1, opts)

# After
seed_config = CampFire.Game.get_seed_config!(seed_name)
Economy.spend_item(player_uid, seed_config.item_key, 1, opts)
```

3. Craft cost harvest items: Pattern match changes from `"itemName"` to `"itemKey"`:
```elixir
# Before
Enum.each(harvest_costs, fn %{"itemName" => name, "count" => count} ->
# After
Enum.each(harvest_costs, fn %{"itemKey" => key, "count" => count} ->
```

4. Speed item: Already reads from config, just the config value is now an item key — no code change needed (value just changes in DB).

- [ ] **Step 2: Update vases.ex**

Same pattern as plots for craft costs:
```elixir
# Before
fn %{"itemName" => name, "count" => count} -> Economy.spend_item(player_uid, name, count, opts)
# After
fn %{"itemKey" => key, "count" => count} -> Economy.spend_item(player_uid, key, count, opts)
```

Speed item: Already reads from vase_config, no code change needed.

- [ ] **Step 3: Update mallums.ex**

1. Quest rewards: Change reward pool pattern match:
```elixir
# Before
seed_name = pick["seed"]
Economy.upsert_item(player_uid, seed_name <> "_Seed", count)

# After
item_key = pick["itemKey"]
Economy.upsert_item(player_uid, item_key, count)
```

2. Collect rewards: Same pattern — no more `<> "_Seed"` append.

3. House costs: `"itemName"` → `"itemKey"` in pattern match.

4. Quest speed item: Already reads from config, no code change.

- [ ] **Step 4: Update apotheke.ex**

```elixir
# Before
Enum.each(recipe["ingredients"], fn %{"item_name" => name, "count" => count} ->
  Economy.spend_item(player_uid, name, count, opts)
end)
Economy.upsert_item(player_uid, recipe["result_item"], recipe["result_quantity"])

# After
Enum.each(recipe["ingredients"], fn %{"itemKey" => key, "count" => count} ->
  Economy.spend_item(player_uid, key, count, opts)
end)
Economy.upsert_item(player_uid, recipe["result_item"], recipe["result_quantity"])
```

- [ ] **Step 5: Update mallum_houses.ex**

Craft house cost pattern match: `"itemName"` → `"itemKey"` (same pattern as plots/vases).

- [ ] **Step 6: Update birds.ex**

Bird collection grants seeds — remove `<> "_Seed"` suffix:
```elixir
# Before
Economy.upsert_item(player_uid, bird.seed_name <> "_Seed", bird.seed_count)
# After — bird config should now contain item_key directly
Economy.upsert_item(player_uid, bird.item_key, bird.seed_count)
```

Note: The bird config/seed data that provides the seed name needs to use item keys too. Check how bird rewards are configured and update accordingly.

- [ ] **Step 7: Update skins.ex**

```elixir
# Before
skin["cost_item_name"]
# After
skin["cost_item_key"]
```

- [ ] **Step 8: `get_seed_config!` is already in Game context (created in Task 1)**

- [ ] **Step 9: Verify compilation**

Run: `cd server && mix compile --warnings-as-errors`

- [ ] **Step 10: Commit**

```bash
git add server/lib/camp_fire/game/plots.ex server/lib/camp_fire/game/vases.ex server/lib/camp_fire/game/mallums.ex server/lib/camp_fire/game/mallum_houses.ex server/lib/camp_fire/game/apotheke.ex server/lib/camp_fire/game/birds.ex server/lib/camp_fire/game/skins.ex
git commit -m "refactor(server): update game modules to use item keys"
```

---

### Task 7: Update controllers and config_cache to serve items

**Files:**
- Modify: `server/lib/camp_fire/config_cache.ex`
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex`
- Modify: `server/lib/camp_fire_web/controllers/economy_controller.ex`

- [ ] **Step 1: Add items to ConfigCache**

In `config_cache.ex`, add items loading in `load_all!/0`:
```elixir
# Load all items into ETS
items = CampFire.Game.list_items()
items_map = Map.new(items, fn i ->
  {i.item_key, %{
    "displayName" => i.display_name,
    "category" => i.category,
    "spriteKey" => i.sprite_key
  }}
end)
put("items", items_map)
```

- [ ] **Step 2: Update ConfigCache seed_map to include item_key and harvest_item_key**

The existing `load_all!/0` builds a seed_map from `SeedConfig` records. Add the new fields:
```elixir
# In the seed_map construction, include:
"item_key" => sc.item_key,
"harvest_item_key" => sc.harvest_item_key,
```

- [ ] **Step 3: Update game_controller configs endpoint to include items**

In the configs response (the function that handles `GET /api/game/configs`), add items:
```elixir
items = CampFire.ConfigCache.get("items") || %{}
# Add to response map:
%{
  # ... existing configs ...
  items: items
}
```

- [ ] **Step 3: Update game_controller inventory serialization**

In `game_controller.ex`, change inventory formatting:
```elixir
# Before (line ~156)
inventory: Enum.map(inventory, fn i -> %{itemName: i.item_name, count: i.count} end)

# After
inventory: Enum.map(inventory, fn i -> %{itemKey: i.item_key, count: i.count} end)
```

Apply this change everywhere inventory is serialized in game_controller (state endpoint, other responses).

- [ ] **Step 4: Update economy_controller**

In `economy_controller.ex`:

`format_inventory/1`:
```elixir
# Before
defp format_inventory(inventory) do
  Enum.map(inventory, fn i -> %{itemName: i.item_name, count: i.count} end)
end

# After
defp format_inventory(inventory) do
  Enum.map(inventory, fn i -> %{itemKey: i.item_key, count: i.count} end)
end
```

`add_items` endpoint: Change `"item_name"` param to `"item_key"`:
```elixir
# Before
%{"item_name" => item_name, "count" => count} = params
# After
%{"item_key" => item_key, "count" => count} = params
```

`spend_items` endpoint: Same rename in params pattern match.

`upgrade_flame` endpoint: Ingredient pattern match `"item_name"` → `"itemKey"`.

- [ ] **Step 6: Update quest reward serialization in game_controller**

Where quest rewards/pending rewards are serialized:
```elixir
# Before
%{seed_name: r["seed_name"], count: r["count"]}

# After
%{itemKey: r["itemKey"], count: r["count"]}
```

- [ ] **Step 7: Update harvest response in game_controller**

The harvest endpoint returns the item name:
```elixir
# Before
json(%{score: score, drops: drops, itemName: item_name})
# After
json(%{score: score, drops: drops, itemKey: harvest_item_key})
```

- [ ] **Step 8: Update bird collection response in game_controller**

The `collect_bird` endpoint returns `seedName` — change to `itemKey`.

- [ ] **Step 9: Update visitor-related server modules**

Visitor processing code that reads `"itemName"`, `"rewardSeedName"`, `"request_item"` from templates needs updating to `"itemKey"`, `"rewardItemKey"`, `"request_item_key"`. Check:
- `server/lib/camp_fire/visitors/` — visitor template processing
- `server/lib/camp_fire_web/controllers/game_controller.ex` — visitor endpoints

- [ ] **Step 10: Verify compilation and run tests**

Run: `cd server && mix compile --warnings-as-errors && mix test`

- [ ] **Step 11: Commit**

```bash
git add server/lib/camp_fire/config_cache.ex server/lib/camp_fire_web/controllers/game_controller.ex server/lib/camp_fire_web/controllers/economy_controller.ex server/lib/camp_fire/visitors/
git commit -m "feat(server): serve items in config endpoint, use itemKey in API responses"
```

---

### Task 8: Update admin panel

**Files:**
- Modify: `server/lib/camp_fire_web/live/items_live.ex`

- [ ] **Step 1: Update admin items LiveView**

The admin panel manages recipes and skins. Update:
1. Recipe editor: ingredient field names from `item_name` → `itemKey`, result from `result_item` (display name) to `result_item` (item key)
2. Skin editor: `cost_item_name` → `cost_item_key`
3. Add an "Items" tab that lists all items from the `items` table (read-only or editable)

- [ ] **Step 2: Verify admin panel loads**

Run: `cd server && mix phx.server`
Navigate to `http://localhost:4000/admin/items` and verify it loads.

- [ ] **Step 3: Commit**

```bash
git add server/lib/camp_fire_web/live/items_live.ex
git commit -m "refactor(server): update admin panel for item keys"
```

---

## Chunk 4: Client Data Layer

### Task 9: Update ConfigService with ItemConfig

**Files:**
- Modify: `Assets/Scripts/Services/ConfigService.cs`

- [ ] **Step 1: Add ItemConfig DTO**

Add near the top with other DTOs:
```csharp
[Serializable]
public class ItemConfig
{
    public string displayName;
    public string category;
    public string spriteKey;
}
```

- [ ] **Step 2: Add items dictionary and helper methods**

Add to ConfigService class:
```csharp
public Dictionary<string, ItemConfig> Items { get; private set; } = new();

public ItemConfig GetItem(string itemKey)
{
    return Items.TryGetValue(itemKey, out var config) ? config : null;
}

public string GetItemDisplayName(string itemKey)
{
    if (Items.TryGetValue(itemKey, out var config))
        return config.displayName;
    return itemKey; // fallback to raw key
}

public List<KeyValuePair<string, ItemConfig>> GetItemsByCategory(string category)
{
    return Items.Where(kv => kv.Value.category == category).ToList();
}
```

- [ ] **Step 3: Parse items from config response**

In the config response parsing method, add items parsing. The response contains `"items"` as a dictionary. Parse using MiniJson:
```csharp
// Parse items map
if (configDict.TryGetValue("items", out var itemsObj) && itemsObj is Dictionary<string, object> itemsDict)
{
    Items = new Dictionary<string, ItemConfig>();
    foreach (var kv in itemsDict)
    {
        if (kv.Value is Dictionary<string, object> itemData)
        {
            Items[kv.Key] = new ItemConfig
            {
                displayName = itemData.TryGetValue("displayName", out var dn) ? dn as string : kv.Key,
                category = itemData.TryGetValue("category", out var cat) ? cat as string : "harvest",
                spriteKey = itemData.TryGetValue("spriteKey", out var sk) ? sk as string : null
            };
        }
    }
}
```

- [ ] **Step 4: Update config JSON field parsing throughout ConfigService**

Replace `"itemName"` with `"itemKey"` in all MiniJson parsing:

1. **Building cost parsing** (~line 682): `"itemName"` → `"itemKey"`
2. **Flame ingredient parsing** (~line 519): `GetString(d, "itemName")` → `GetString(d, "itemKey")`
3. **Seed config parsing**: Add `item_key` and `harvest_item_key` fields to `ServerSeedConfig`
4. **Quest reward parsing**: `seedName` → `itemKey` in `ServerQuestReward`
5. **Garden config**: `yieldItem` stays as field name but values are now item keys
6. **New player config parsing**: Merge seeds+items into single items list with `itemKey`

- [ ] **Step 5: Update ServerSeedConfig DTO**

Add fields:
```csharp
public string item_key;
public string harvest_item_key;
```

- [ ] **Step 6: Update ServerQuestReward DTO**

```csharp
// Before
public string seedName;
// After
public string itemKey;
```

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Services/ConfigService.cs
git commit -m "feat(client): add ItemConfig DTO and items parsing to ConfigService"
```

---

### Task 10: Update SaveData, ConfigTypes, and RecipeData

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Scripts/Data/ConfigTypes.cs`
- Modify: `Assets/Scripts/Data/RecipeData.cs`

- [ ] **Step 1: Update InventoryItem in SaveData.cs**

```csharp
// Before
public class InventoryItem
{
    public string itemName;
    public int count;
}

// After
public class InventoryItem
{
    public string itemKey;
    public int count;
}
```

Also update `discoveredSeeds` comment to note it now stores item keys.

- [ ] **Step 2: Update ConfigTypes.cs**

Rename `itemName` to `itemKey` on all config types:

```csharp
// FlameIngredient
public string itemKey; // was: itemName

// HarvestCost
public string itemKey; // was: itemName

// IngredientEntry
public string itemKey; // was: itemName
```

Also update `NewPlayerItemGrant` if it exists:
```csharp
public string itemKey; // was: name
```

- [ ] **Step 3: Update RecipeData.cs**

Since recipes are moving to server-driven via `recipe_configs`, but the SO is still used locally, update the field name:
```csharp
// IngredientEntry
public string itemKey; // was: itemName
```

Update `FormatItemName` → `FormatItemKey` (or remove if display names come from ConfigService now). Actually, the simpler path: keep `FormatItemName` but have it call `ConfigService.Instance.GetItemDisplayName(key)` with a fallback to the old formatting logic for when ConfigService isn't loaded.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/ConfigTypes.cs Assets/Scripts/Data/RecipeData.cs
git commit -m "refactor(client): rename itemName to itemKey in data types"
```

---

### Task 11: Update EconomyService

**Files:**
- Modify: `Assets/Scripts/Services/EconomyService.cs`

- [ ] **Step 1: Update and consolidate request DTOs**

Rename fields in `AddItemRequest` and `SpendItemEntry`:
```csharp
// AddItemRequest
public string item_key; // was: item_name

// SpendItemEntry
public string item_key; // was: item_name
```

**Remove** `AddSeedRequest` and `SpendSeedRequest` DTOs (they have `seed_name` fields and are now redundant — seeds use the same `AddItemRequest`/`SpendItemEntry` with item keys). Update all callers to use the unified DTOs.

- [ ] **Step 2: Update ApplyServerState inventory parsing**

Where server inventory is applied to local SaveData:
```csharp
// Before
data.inventory.Add(new InventoryItem { itemName = i.itemName, count = i.count });
// After
data.inventory.Add(new InventoryItem { itemKey = i.itemKey, count = i.count });
```

Update the `EconomyInventoryItem` response DTO (or whatever class parses the server response):
```csharp
// Before
public string itemName;
// After
public string itemKey;
```

- [ ] **Step 3: Update all Enqueue calls that construct request JSON**

Search for all `new AddItemRequest` and `new SpendItemEntry` usages — update `item_name =` to `item_key =`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Services/EconomyService.cs
git commit -m "refactor(client): use item_key in EconomyService request DTOs"
```

---

### Task 12: Update GameService

**Files:**
- Modify: `Assets/Scripts/Services/GameService.cs`

- [ ] **Step 1: Update ApplyGameState inventory handling**

```csharp
// Before
data.inventory.Add(new InventoryItem { itemName = i.itemName, count = i.count });
// After
data.inventory.Add(new InventoryItem { itemKey = i.itemKey, count = i.count });
```

- [ ] **Step 2: Update quest reward handling**

Where pending rewards are parsed from server state:
```csharp
// Before
mallum.pendingRewards.Add(new RewardEntry { seedName = r.seed_name, count = r.count });
// After
mallum.pendingRewards.Add(new RewardEntry { itemKey = r.itemKey, count = r.count });
```

Update `RewardEntry` class — rename `seedName` to `itemKey`.

- [ ] **Step 3: Update response DTOs**

Any `GameStateResponse` inner classes that have `itemName` or `seed_name` fields need updating to `itemKey`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Services/GameService.cs
git commit -m "refactor(client): use itemKey in GameService state parsing"
```

---

## Chunk 5: Client Managers

### Task 13: Update ApothekeManager

**Files:**
- Modify: `Assets/Scripts/Managers/ApothekeManager.cs`

- [ ] **Step 1: Update AddSeed method**

```csharp
// Before
public void AddSeed(string plantName, int count = 1)
{
    var itemName = plantName + "_Seed";
    var entry = data.inventory.Find(i => i.itemName == itemName);
    // ...
    EconomyService.Instance?.Enqueue("add-items",
        JsonUtility.ToJson(new AddItemRequest { item_name = itemName, count = count }));
}

// After — takes item_key directly
public void AddItem(string itemKey, int count = 1)
{
    var data = SaveManager.Instance.Data;
    var entry = data.inventory.Find(i => i.itemKey == itemKey);
    if (entry != null)
        entry.count += count;
    else
        data.inventory.Add(new InventoryItem { itemKey = itemKey, count = count });

    if (!data.discoveredSeeds.Contains(itemKey))
        data.discoveredSeeds.Add(itemKey);

    SaveManager.Instance.Save();
    EconomyService.Instance?.Enqueue("add-items",
        JsonUtility.ToJson(new AddItemRequest { item_key = itemKey, count = count }));
}
```

The old `AddSeed(plantName)` callers need updating — they should pass the seed item key (e.g., `"sprouts_seed"`) directly. Remove the separate `AddSeed` method; consolidate into a single `AddItem(itemKey, count)`.

- [ ] **Step 2: Update Mix method**

Replace `i.itemName` lookups with `i.itemKey`:
```csharp
var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
```

And for economy requests:
```csharp
new SpendItemEntry { item_key = ing.itemKey, count = ing.quantity }
```

- [ ] **Step 3: Update Seeds property**

```csharp
// Before
public List<InventoryItem> Seeds => data.inventory.FindAll(i => i.itemName.EndsWith("_Seed"));
// After — use category from config
public List<InventoryItem> Seeds =>
    SaveManager.Instance.Data.inventory.FindAll(i =>
        ConfigService.Instance?.GetItem(i.itemKey)?.category == "seed");
```

- [ ] **Step 4: Update discoveredSeeds checks**

`IsDiscovered(seedName)` → `IsDiscovered(itemKey)` — already stores strings, just the values change.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Managers/ApothekeManager.cs
git commit -m "refactor(client): use item keys in ApothekeManager"
```

---

### Task 14: Update PlotManager

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

- [ ] **Step 1: Update item references**

Key changes:
1. Seed consumption at plant time: `itemName` → `itemKey` in inventory lookups
2. Harvest item addition: Use config's harvest_item_key instead of seed name
3. Speed item: Already reads from `ConfigService.Instance.PlotConfig.speed_item` — just the value changes
4. All `i.itemName` inventory lookups → `i.itemKey`
5. `GetSpeedItemCount()` inventory lookup: `i.itemName == speedItem` → `i.itemKey == speedItem`
6. Cost checking: `hc.itemName` → `hc.itemKey` in harvest cost validation
7. `GetSeedDisplayName(seedName)` — update to use `ConfigService.Instance.GetItemDisplayName(itemKey)`

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs
git commit -m "refactor(client): use item keys in PlotManager"
```

---

### Task 15: Update remaining managers

**Files:**
- Modify: `Assets/Scripts/Managers/VaseManager.cs`
- Modify: `Assets/Scripts/Managers/MallumManager.cs`
- Modify: `Assets/Scripts/Managers/FlameManager.cs`
- Modify: `Assets/Scripts/Managers/GardenManager.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Modify: `Assets/Scripts/Managers/BirdManager.cs`

- [ ] **Step 1: Update VaseManager**

Speed item and harvest cost references: `itemName` → `itemKey`.

- [ ] **Step 2: Update MallumManager**

1. Quest speed item: `QuestSpeedItem` property already reads from config — no change needed
2. Water fetch speed item: Should read from `ConfigService.Instance.VaseConfig.speed_item` (this was the bug from the previous session — fix it now)
3. Quest reward collection: `reward.seedName` → `reward.itemKey`, remove `+ "_Seed"` suffix logic
4. House cost checking: `hc.itemName` → `hc.itemKey`
5. Inventory lookups: `i.itemName` → `i.itemKey`
6. `ConsumeQuestSpeedItem()` and `ConsumeVaseSpeedItem()` — separate methods for quest vs vase speed items

- [ ] **Step 3: Update FlameManager**

Upgrade ingredient cost checking: `ing.itemName` → `ing.itemKey` in inventory lookups.

- [ ] **Step 4: Update GardenManager**

Yield item references, garden cost checking: `itemName` → `itemKey`.

- [ ] **Step 5: Update GameManager**

New player setup: If it manually adds starter items, switch to item keys. (May already go through server init_economy.)

- [ ] **Step 6: Update BirdManager**

Bird seed drops: `reward.seedName` → `reward.itemKey`. Update calls from `ApothekeManager.AddSeed(plantName)` to `ApothekeManager.AddItem(itemKey)`.

Also update `BirdSave.seedName` → `BirdSave.itemKey` (or similar).

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Managers/VaseManager.cs Assets/Scripts/Managers/MallumManager.cs Assets/Scripts/Managers/FlameManager.cs Assets/Scripts/Managers/GardenManager.cs Assets/Scripts/Managers/GameManager.cs Assets/Scripts/Managers/BirdManager.cs
git commit -m "refactor(client): use item keys in all managers"
```

---

## Chunk 6: Client UI & Sprites

### Task 16: Update SpriteService

**Files:**
- Modify: `Assets/Scripts/Services/SpriteService.cs`

- [ ] **Step 1: Update ItemToSpriteKey**

```csharp
public static string ItemToSpriteKey(string itemKey)
{
    if (string.IsNullOrEmpty(itemKey)) return null;

    // Check for explicit sprite_key override from config
    var config = ConfigService.Instance?.GetItem(itemKey);
    if (config?.spriteKey != null) return config.spriteKey;

    // Derive from item_key and category
    if (config != null)
    {
        return config.category switch
        {
            "seed" => $"items/{itemKey.Replace("_seed", "")}/seed",
            "harvest" => $"items/{itemKey}/harvest",
            "pigment" => $"items/{itemKey.Replace("_pigment", "")}/pigment",
            _ => $"items/{itemKey}"
        };
    }

    // Fallback: direct mapping
    return $"items/{itemKey}";
}
```

- [ ] **Step 2: Simplify SeedToSpriteKey**

This can now just extract the plant name from a seed item key:
```csharp
public static string SeedToSpriteKey(string seedItemKey)
{
    if (string.IsNullOrEmpty(seedItemKey)) return seedItemKey;
    // "basil_seed" → "basil"
    return seedItemKey.EndsWith("_seed") ? seedItemKey[..^5] : seedItemKey;
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Services/SpriteService.cs
git commit -m "refactor(client): simplify sprite resolution with item config"
```

---

### Task 17: Update UI files

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`
- Modify: `Assets/Scripts/UI/QuestUI.cs`
- Modify: `Assets/Scripts/UI/BuildUI.cs`
- Modify: `Assets/Scripts/UI/BuildCardHelper.cs`
- Modify: `Assets/Scripts/UI/RewardRevealUI.cs`

- [ ] **Step 1: Update ApothekeUI**

1. Replace `CategorizeItem()` suffix-based logic:
```csharp
private static InventoryCategory CategorizeItem(string itemKey)
{
    var config = ConfigService.Instance?.GetItem(itemKey);
    if (config == null) return InventoryCategory.Yields;
    return config.category switch
    {
        "seed" => InventoryCategory.Seeds,
        "pigment" => InventoryCategory.Pigments,
        "potion" or "consumable" or "material" => InventoryCategory.Consumables,
        _ => InventoryCategory.Yields
    };
}
```

2. Replace `RecipeData.FormatItemName(entry.itemName)` with `ConfigService.Instance.GetItemDisplayName(entry.itemKey)`.
3. Update all `entry.itemName` → `entry.itemKey`.
4. Update sprite lookups to use new `ItemToSpriteKey`.

- [ ] **Step 2: Update CampsiteViewUI**

1. All `itemName` references → `itemKey`
2. Display names: use `ConfigService.Instance.GetItemDisplayName()`
3. `GetSpeedItemCount()` / `GetQuestSpeedItemCount()` calls — update to match renamed methods
4. Seed-to-sprite resolution: use `SpriteService.ItemToSpriteKey()`

- [ ] **Step 3: Update QuestUI**

1. Speed item count/display: already uses MallumManager methods
2. Reward display: `reward.seedName` → `reward.itemKey`
3. Display names: use ConfigService lookup

- [ ] **Step 4: Update BuildUI**

1. Cost display: `hc.itemName` → `hc.itemKey` for harvest costs
2. Display names: ConfigService lookup

- [ ] **Step 5: Update BuildCardHelper**

1. `LoadHarvestIcon(hc.itemName)` → `LoadHarvestIcon(hc.itemKey)` — the function internally calls `SpriteService.ItemToSpriteKey()` which is already updated
2. `ing.itemName` → `ing.itemKey`

- [ ] **Step 6: Update RewardRevealUI**

1. `reward.seedName` → `reward.itemKey`
2. Display name: `ConfigService.Instance.GetItemDisplayName(reward.itemKey)`
3. Sprite key: `SpriteService.ItemToSpriteKey(reward.itemKey)`
4. Tier lookup: `ConfigService.Instance.GetSeed(reward.seedName)?.tier` → need to look up by item key. Add a helper or use seed_config's item_key field.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/UI/ApothekeUI.cs Assets/Scripts/UI/CampsiteViewUI.cs Assets/Scripts/UI/QuestUI.cs Assets/Scripts/UI/BuildUI.cs Assets/Scripts/UI/BuildCardHelper.cs Assets/Scripts/UI/RewardRevealUI.cs
git commit -m "refactor(client): use item keys and display names in all UI"
```

---

### Task 18: Update SaveManager version bump

**Files:**
- Modify: `Assets/Scripts/Services/SaveManager.cs`

- [ ] **Step 1: Bump save version**

In SaveData:
```csharp
public int version = 2; // was 1
```

In SaveManager's load logic, detect old version and reset:
```csharp
if (data.version < 2)
{
    Debug.Log("[SaveManager] Save version too old, resetting to fresh save");
    data = new SaveData();
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Services/SaveManager.cs Assets/Scripts/Data/SaveData.cs
git commit -m "chore(client): bump save version for item key migration"
```

---

## Chunk 7: Tests & Verification

### Task 19: Update server tests

**Files:**
- Modify: `server/test/camp_fire/economy_test.exs`
- Modify: `server/test/camp_fire_web/controllers/economy_controller_test.exs`
- Modify: `server/test/camp_fire_web/controllers/game_controller_test.exs`
- Modify: `server/test/camp_fire/game/plots_test.exs`
- Modify: `server/test/camp_fire/game/vases_test.exs`
- Modify: `server/test/camp_fire/game/mallum_houses_test.exs`
- Modify: `server/test/camp_fire/game/mallums_test.exs`
- Modify: `server/test/camp_fire/game/skins_test.exs`
- Modify: `server/test/camp_fire/game/birds_test.exs`
- Modify: `server/test/camp_fire/game/apotheke_test.exs`

This is substantial — every test that creates inventory items, checks config patterns, or sends API params with `item_name`/`itemName` needs updating.

- [ ] **Step 1: Update test helpers and fixtures**

Any test helper that creates inventory items or seeds needs `item_name` → `item_key`. Test setup that seeds configs needs `"itemName"` → `"itemKey"` in config JSON.

- [ ] **Step 2: Update economy tests**

All `item_name` params in `upsert_item`/`spend_item` calls → `item_key`.

- [ ] **Step 3: Update controller tests**

API param keys: `"item_name"` → `"item_key"`. Response assertion keys: `"itemName"` → `"itemKey"`.

- [ ] **Step 4: Update game module tests**

Config pattern matches in test data: `"itemName"` → `"itemKey"`. Seed name patterns: remove `<> "_Seed"` logic.

- [ ] **Step 5: Run all tests**

Run: `cd server && mix test`
Fix remaining failures iteratively.

- [ ] **Step 6: Commit**

```bash
git add server/test/
git commit -m "test(server): update all tests for item key migration"
```

---

### Task 20: Update client EditMode tests

**Files:**
- Modify: `Assets/Tests/EditMode/*.cs`

- [ ] **Step 1: Update test fixtures**

All `new InventoryItem { itemName = ... }` → `new InventoryItem { itemKey = ... }`. All config test data with `itemName` → `itemKey`.

- [ ] **Step 2: Verify tests pass via Unity Test Runner or MCP**

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/EditMode/
git commit -m "test(client): update EditMode tests for item key migration"
```

---

### Task 21: Server verification

- [ ] **Step 1: Reset and re-seed database**

Run: `cd server && mix ecto.reset`
Expected: All seeds pass with item validation.

- [ ] **Step 2: Run server tests**

Run: `cd server && mix test`
Fix any failures caused by the rename.

- [ ] **Step 3: Start server and test manually**

Run: `cd server && mix phx.server`

Verify:
- `GET /api/game/configs` returns `items` map with all items
- `POST /auth/register` succeeds (creates player)
- `POST /game/init` succeeds (grants new player items with item keys)
- `GET /game/state` returns inventory with `itemKey` field
- Admin panel loads at `/admin/items`

- [ ] **Step 4: Commit any test fixes**

```bash
git add -A
git commit -m "fix(server): update tests for item key migration"
```

---

### Task 22: Client verification

- [ ] **Step 1: Check for remaining itemName references**

Search the codebase for any remaining `itemName` or `item_name` references that should have been renamed:
- `grep -r "itemName" Assets/Scripts/` — should only appear in comments or unrelated contexts
- `grep -r "item_name" Assets/Scripts/` — same

- [ ] **Step 2: Check Unity compilation**

Open Unity or use MCP `read_console` to verify no compilation errors.

- [ ] **Step 3: Test full flow**

1. Delete local save file
2. Launch game — should get fresh save with version 2
3. Verify new player items appear (sprouts seeds, speed potion, energy drink)
4. Plant a seed, water it, speed up, harvest — verify item names display correctly
5. Open Apotheke — verify categories work (seeds, yields, pigments, consumables)
6. Check quest UI — verify rewards show with correct names

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "fix(client): resolve remaining item key migration issues"
```
