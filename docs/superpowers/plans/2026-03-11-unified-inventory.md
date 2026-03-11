# Unified Inventory Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge `SeedInventoryEntry` and `InventoryItem` into a single type with one inventory list, renaming seeds to `X_Seed` and harvests to bare names.

**Architecture:** Single `InventoryItem { itemName, count }` type, single `SaveData.inventory` list. Server merges `player_seeds` + `player_items` tables into `player_inventory`. All `_harvest` suffixes removed from item names, `_Seed` suffix added to seed inventory keys.

**Tech Stack:** Unity C# (client), Elixir/Phoenix + Postgres (server)

---

## Chunk 1: Server Database Migration & Schema

### Task 1: Create migration to merge player_seeds and player_items

**Files:**
- Create: `server/priv/repo/migrations/TIMESTAMP_merge_inventory.exs`
- Create: `server/lib/camp_fire/economy/player_inventory.ex`
- Delete: `server/lib/camp_fire/economy/player_seed.ex`
- Delete: `server/lib/camp_fire/economy/player_item.ex`

- [ ] **Step 1: Create PlayerInventory schema**

```elixir
# server/lib/camp_fire/economy/player_inventory.ex
defmodule CampFire.Economy.PlayerInventory do
  use Ecto.Schema

  schema "player_inventory" do
    field :player_uid, :string
    field :item_name, :string
    field :count, :integer, default: 0
  end
end
```

- [ ] **Step 2: Create migration**

```elixir
defmodule CampFire.Repo.Migrations.MergeInventory do
  use Ecto.Migration

  def up do
    create table(:player_inventory) do
      add :player_uid, :string, null: false
      add :item_name, :string, null: false
      add :count, :integer, default: 0, null: false
    end

    create unique_index(:player_inventory, [:player_uid, :item_name])
    create index(:player_inventory, [:player_uid])

    # Migrate existing data: seeds get _Seed suffix, items get _harvest stripped
    execute """
    INSERT INTO player_inventory (player_uid, item_name, count)
    SELECT player_uid, seed_name || '_Seed', count FROM player_seeds
    """

    # For items, rename _harvest to bare name, _pigment to _Pigment
    execute """
    INSERT INTO player_inventory (player_uid, item_name, count)
    SELECT player_uid,
      CASE
        WHEN item_name LIKE '%_harvest' THEN REPLACE(item_name, '_harvest', '')
        WHEN item_name LIKE '%_pigment' THEN
          REPLACE(
            REPLACE(item_name, '_pigment', '_Pigment'),
            '_pigment', '_Pigment'
          )
        ELSE item_name
      END,
      count
    FROM player_items
    """

    drop table(:player_seeds)
    drop table(:player_items)
  end

  def down do
    # Not needed — early development, no backwards compat
    raise "irreversible migration"
  end
end
```

- [ ] **Step 3: Delete old schema files**

Delete `server/lib/camp_fire/economy/player_seed.ex` and `server/lib/camp_fire/economy/player_item.ex`.

- [ ] **Step 4: Run migration**

```bash
cd server && mix ecto.migrate
```

- [ ] **Step 5: Commit**

```bash
git add -A server/lib/camp_fire/economy/ server/priv/repo/migrations/
git commit -m "feat(server): merge player_seeds and player_items into player_inventory"
```

---

### Task 2: Update Economy context to use PlayerInventory

**Files:**
- Modify: `server/lib/camp_fire/economy.ex`

Replace all `PlayerSeed`/`PlayerItem` references with `PlayerInventory`. Merge `upsert_seed`/`upsert_item` into single `upsert_item`. Merge `spend_seed`/`spend_item` into single `spend_item`. Remove `list_seeds`/`list_items` in favor of `list_inventory`.

- [ ] **Step 1: Update alias**

Change `alias CampFire.Economy.{PlayerEconomy, PlayerSeed, PlayerItem}` to `alias CampFire.Economy.{PlayerEconomy, PlayerInventory}`.

- [ ] **Step 2: Replace list_seeds and list_items**

Replace both with:
```elixir
def list_inventory(player_uid) do
  from(i in PlayerInventory, where: i.player_uid == ^player_uid) |> Repo.all()
end
```

- [ ] **Step 3: Merge upsert_seed into upsert_item**

Delete `upsert_seed/3`. Update `upsert_item/3` to use `PlayerInventory`:
```elixir
def upsert_item(player_uid, item_name, count) when is_integer(count) and count > 0 do
  %PlayerInventory{player_uid: player_uid, item_name: item_name, count: count}
  |> Repo.insert(
    on_conflict: [inc: [count: count]],
    conflict_target: [:player_uid, :item_name]
  )
end
```

- [ ] **Step 4: Merge spend_seed into spend_item**

Delete `spend_seed/4`. Update `spend_item/4` to use `PlayerInventory`:
```elixir
def spend_item(player_uid, item_name, count, opts \\ []) when is_integer(count) and count > 0 do
  free_mode = Keyword.get(opts, :free_mode, false)
  if free_mode do
    :ok
  else
    Repo.transaction(fn ->
      {updated, _} = from(i in PlayerInventory,
        where: i.player_uid == ^player_uid and i.item_name == ^item_name and i.count >= ^count
      ) |> Repo.update_all(inc: [count: -count])

      if updated == 0, do: Repo.rollback({:insufficient_items, item_name})

      from(i in PlayerInventory,
        where: i.player_uid == ^player_uid and i.item_name == ^item_name and i.count == 0
      ) |> Repo.delete_all()
    end)
  end
end
```

- [ ] **Step 5: Update spend_items and spend_items_in_tx**

Update to use `PlayerInventory` instead of `PlayerItem`.

- [ ] **Step 6: Update get_economy_state**

The function that builds the economy state for the client. Replace separate seed/item lists with single inventory list:
```elixir
inventory: list_inventory(player_uid)
```

- [ ] **Step 7: Compile and verify**

```bash
cd server && mix compile
```

- [ ] **Step 8: Commit**

```bash
git add server/lib/camp_fire/economy.ex
git commit -m "refactor(server): merge seed/item economy functions into unified inventory"
```

---

### Task 3: Update Economy Controller

**Files:**
- Modify: `server/lib/camp_fire_web/controllers/economy_controller.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

- [ ] **Step 1: Remove add_seeds and spend_seeds actions**

Delete the `add_seeds/2` and `spend_seeds/2` functions. Update `add_items/2` and `spend_items/2` to call the unified `Economy.upsert_item/3` and `Economy.spend_item/4`.

- [ ] **Step 2: Remove seed routes from router**

Remove these lines from `router.ex`:
```elixir
post "/add-seeds", EconomyController, :add_seeds
post "/spend-seeds", EconomyController, :spend_seeds
```

- [ ] **Step 3: Compile and verify**

```bash
cd server && mix compile
```

- [ ] **Step 4: Commit**

```bash
git add server/lib/camp_fire_web/controllers/economy_controller.ex server/lib/camp_fire_web/router.ex
git commit -m "refactor(server): remove separate seed endpoints, use unified add-items/spend-items"
```

---

### Task 4: Update server seeds.exs naming

**Files:**
- Modify: `server/priv/repo/seeds.exs`

- [ ] **Step 1: Rename all `_harvest` to bare names in config values**

All `itemName` / `item` / `item_name` fields that end in `_harvest` should have the suffix removed. For example:
- `"Sprouts_harvest"` → `"Sprouts"`
- `"Basil_harvest"` → `"Basil"`

This affects: `upgrade_recipes`, `house_costs`, `plot_costs`, `vase_costs`, `garden_costs`, and `recipe_configs`.

- [ ] **Step 2: Rename pigment results to use _Pigment casing**

In `recipe_configs`: change `result_item` values from `Basil_pigment` to `Basil_Pigment`, etc.

- [ ] **Step 3: Rename recipe ingredients from `_harvest` to bare names**

In `recipe_configs`: change `item_name` values like `"Basil_harvest"` to `"Basil"`.

- [ ] **Step 4: Commit**

```bash
git add server/priv/repo/seeds.exs
git commit -m "refactor(server): rename harvest/pigment item keys in seed configs"
```

---

### Task 5: Update Admin and Debug modules

**Files:**
- Modify: `server/lib/camp_fire/admin.ex`
- Modify: `server/lib/camp_fire/game/debug.ex`

- [ ] **Step 1: Update admin.ex**

Replace `PlayerSeed`/`PlayerItem` references with `PlayerInventory`. Update the player economy view query to use single `player_inventory` table.

- [ ] **Step 2: Update debug.ex**

Replace `PlayerSeed`/`PlayerItem` delete queries with single `PlayerInventory` query.

- [ ] **Step 3: Compile and verify**

```bash
cd server && mix compile
```

- [ ] **Step 4: Commit**

```bash
git add server/lib/camp_fire/admin.ex server/lib/camp_fire/game/debug.ex
git commit -m "refactor(server): update admin and debug to use PlayerInventory"
```

---

### Task 6: Update server economy state endpoint

**Files:**
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex` (or wherever economy state is serialized)

- [ ] **Step 1: Return single inventory list**

Instead of separate `seeds` and `items` fields, return a single `inventory` list with `item_name` and `count` fields.

- [ ] **Step 2: Compile and test**

```bash
cd server && mix compile && mix test
```

- [ ] **Step 3: Commit**

```bash
git add server/lib/
git commit -m "refactor(server): return unified inventory list in economy state"
```

---

## Chunk 2: Client Data Model & Core Managers

### Task 7: Update SaveData and delete SeedInventoryEntry

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`

- [ ] **Step 1: Delete SeedInventoryEntry class**

Remove the entire `SeedInventoryEntry` class (lines 78-83).

- [ ] **Step 2: Rename InventoryItem.itemName to name**

Change field from `itemName` to `name` for cleaner API. (Actually — keep as `itemName` to minimize churn; the field name is fine.)

- [ ] **Step 3: Replace seedInventory + items with single inventory list**

```csharp
// Delete these two lines:
public List<SeedInventoryEntry> seedInventory = new();
public List<InventoryItem> items = new();

// Replace with:
public List<InventoryItem> inventory = new();
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs
git commit -m "refactor: merge seedInventory and items into unified inventory list"
```

---

### Task 8: Update ApothekeManager

**Files:**
- Modify: `Assets/Scripts/Managers/ApothekeManager.cs`

- [ ] **Step 1: Update Seeds property**

```csharp
// Filter seeds from unified inventory
public List<InventoryItem> Seeds =>
    SaveManager.Instance.Data.inventory.FindAll(i => i.itemName.EndsWith("_Seed"));
```

- [ ] **Step 2: Update Items property**

```csharp
public List<InventoryItem> Items => SaveManager.Instance.Data.inventory;
```

- [ ] **Step 3: Update CanMix and Mix methods**

Replace `data.items` with `data.inventory` throughout.

- [ ] **Step 4: Update AddSeed method**

```csharp
public void AddSeed(string plantName, int count = 1)
{
    string itemName = plantName + "_Seed";
    var data = SaveManager.Instance.Data;
    var entry = data.inventory.Find(i => i.itemName == itemName);
    if (entry != null)
        entry.count += count;
    else
        data.inventory.Add(new InventoryItem { itemName = itemName, count = count });
    SaveManager.Instance.Save();
    EconomyService.Instance?.Enqueue("add-items",
        JsonUtility.ToJson(new AddItemRequest { item_name = itemName, count = count }));
}
```

Note: `AddSeed` still takes the plant name (e.g. `"Basil"`) for caller convenience — it appends `_Seed` internally.

- [ ] **Step 5: Add general AddItem helper**

```csharp
public void AddItem(string itemName, int count = 1)
{
    var data = SaveManager.Instance.Data;
    var entry = data.inventory.Find(i => i.itemName == itemName);
    if (entry != null)
        entry.count += count;
    else
        data.inventory.Add(new InventoryItem { itemName = itemName, count = count });
    SaveManager.Instance.Save();
}
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Managers/ApothekeManager.cs
git commit -m "refactor: update ApothekeManager for unified inventory"
```

---

### Task 9: Update PlotManager

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

- [ ] **Step 1: Update CraftPlot — harvest cost lookups**

Replace `data.items.Find(...)` with `data.inventory.Find(...)` for harvest cost checks. The `itemName` field in `HarvestCost` will now reference bare names (e.g. `"Basil"` instead of `"Basil_harvest"`), matching the server config change.

- [ ] **Step 2: Update Plant — seed consumption**

Change seed lookup from `data.seedInventory.Find(s => s.seedName == seedName)` to `data.inventory.Find(i => i.itemName == seedName + "_Seed")`. Update the count decrement and removal to use `data.inventory`.

- [ ] **Step 3: Update Harvest — drop naming**

Change `seed.seedName + "_harvest"` to just `seed.seedName` (bare plant name). Update the `AddItem` call and the `EconomyService.Enqueue` call.

- [ ] **Step 4: Update AddItem helper**

Change `data.items.Find` / `data.items.Add` to `data.inventory.Find` / `data.inventory.Add`.

- [ ] **Step 5: Update any Speed_Potion references**

Replace `data.items.Find(i => i.itemName == "Speed_Potion")` with `data.inventory.Find(...)`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs
git commit -m "refactor: update PlotManager for unified inventory and new naming"
```

---

### Task 10: Update VisitorManager

**Files:**
- Modify: `Assets/Scripts/Managers/VisitorManager.cs`

- [ ] **Step 1: Update ApplyGift**

Replace `data.items` with `data.inventory` for item gift handling. For seed gifts, add `_Seed` suffix.

- [ ] **Step 2: Update CanAffordOffer and ExecuteTrade**

Change `List<InventoryItem> items` parameter to use `data.inventory`. `ExecuteTrade` currently takes separate `items` and `seedInventory` lists — change to single `inventory` list. Seed rewards get `_Seed` suffix added.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Managers/VisitorManager.cs
git commit -m "refactor: update VisitorManager for unified inventory"
```

---

### Task 11: Update remaining managers

**Files:**
- Modify: `Assets/Scripts/Managers/MallumManager.cs`
- Modify: `Assets/Scripts/Managers/GardenManager.cs`
- Modify: `Assets/Scripts/Managers/FlameManager.cs`
- Modify: `Assets/Scripts/Managers/VaseManager.cs`
- Modify: `Assets/Scripts/Managers/SkinManager.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`
- Modify: `Assets/Scripts/Managers/BirdManager.cs`

- [ ] **Step 1: MallumManager**

- Replace `data.items` with `data.inventory` everywhere
- `CanAffordHarvests` static method: parameter stays `List<InventoryItem>` but callers pass `data.inventory`
- Quest reward collection: `AddSeed(r.seedName, r.count)` stays (AddSeed handles suffix)
- `EnergyDrinkItem` references: `data.items.Find` → `data.inventory.Find`

- [ ] **Step 2: GardenManager**

Replace `data.items` with `data.inventory` in `YieldItem` and harvest cost checks.

- [ ] **Step 3: FlameManager**

Replace `items` parameter in `CanAffordUpgrade`/`ConsumeIngredients` — callers pass `data.inventory`.

- [ ] **Step 4: VaseManager**

Replace `data.items` with `data.inventory` in craft cost checks.

- [ ] **Step 5: SkinManager**

Replace `data.items`/`items` with `data.inventory` in `CanAffordSkin`/`UnlockSkin`.

- [ ] **Step 6: GameManager**

Update initial setup:
```csharp
ApothekeManager.Instance.AddSeed("Sprouts", 5);  // Still works — AddSeed adds _Seed
ApothekeManager.Instance.AddSeed("Cress", 3);
data.inventory.Add(new InventoryItem { itemName = "Speed_Potion", count = 3 });
```

- [ ] **Step 7: BirdManager**

`AddSeed` calls stay the same (AddSeed handles suffix internally).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Managers/
git commit -m "refactor: update all managers for unified inventory"
```

---

## Chunk 3: Services & UI

### Task 12: Update EconomyService DTOs

**Files:**
- Modify: `Assets/Scripts/Services/EconomyService.cs`

- [ ] **Step 1: Delete seed-specific DTOs**

Delete `AddSeedRequest` and `SpendSeedRequest` classes.

- [ ] **Step 2: Update EconomyState**

Replace:
```csharp
public List<SeedInventoryEntry> seeds;
public List<InventoryItem> items;
```
With:
```csharp
public List<InventoryItem> inventory;
```

- [ ] **Step 3: Update Initialize/sync method**

Where it copies server economy state to SaveData, use single `inventory` list.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Services/EconomyService.cs
git commit -m "refactor: merge seed/item DTOs in EconomyService"
```

---

### Task 13: Update GameService

**Files:**
- Modify: `Assets/Scripts/Services/GameService.cs`

- [ ] **Step 1: Update economy state deserialization**

Replace the separate seed/item loops:
```csharp
data.inventory.Clear();
if (state.economy.inventory != null)
    foreach (var i in state.economy.inventory)
        data.inventory.Add(new InventoryItem { itemName = i.itemName, count = i.count });
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Services/GameService.cs
git commit -m "refactor: update GameService for unified inventory"
```

---

### Task 14: Update SocialService

**Files:**
- Modify: `Assets/Scripts/Services/SocialService.cs`

- [ ] **Step 1: Update DeductItemsLocally**

Remove the seed/item branching. All items use `data.inventory`:
```csharp
private void DeductItemsLocally(List<GiftItem> items)
{
    var data = SaveManager.Instance.Data;
    foreach (var item in items)
    {
        string name = item.type == "seed" ? item.name + "_Seed" : item.name;
        var entry = data.inventory.Find(i => i.itemName == name);
        if (entry != null)
        {
            entry.count -= item.count;
            if (entry.count <= 0) data.inventory.Remove(entry);
        }
    }
    SaveManager.Instance.Save();
}
```

- [ ] **Step 2: Update AddItemsLocally**

```csharp
private void AddItemsLocally(List<GiftItem> items)
{
    foreach (var item in items)
    {
        if (item.type == "seed")
            ApothekeManager.Instance.AddSeed(item.name, item.count);
        else
        {
            var data = SaveManager.Instance.Data;
            var entry = data.inventory.Find(i => i.itemName == item.name);
            if (entry != null)
                entry.count += item.count;
            else
                data.inventory.Add(new InventoryItem { itemName = item.name, count = item.count });
            SaveManager.Instance.Save();
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Services/SocialService.cs
git commit -m "refactor: update SocialService for unified inventory"
```

---

### Task 15: Update UI controllers

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`
- Modify: `Assets/Scripts/UI/VisitorUI.cs`
- Modify: `Assets/Scripts/UI/BuildCardHelper.cs`

- [ ] **Step 1: ApothekeUI**

`BuildSeedCard` parameter: change `SeedInventoryEntry` to `InventoryItem`. Update `entry.seedName` references to `entry.itemName` (and strip `_Seed` suffix for display).
`BuildRecipeCard`: change `data.items` references to `data.inventory`.

- [ ] **Step 2: CampsiteViewUI**

Replace `data.items` with `data.inventory`. Update any `_harvest` display name stripping.

- [ ] **Step 3: VisitorUI**

Replace `data.items` with `data.inventory`.

- [ ] **Step 4: BuildCardHelper**

Replace `data.items` with `data.inventory`. Update seed icon lookup if it parses seed names.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/
git commit -m "refactor: update UI controllers for unified inventory"
```

---

## Chunk 4: Tests

### Task 16: Update all tests

**Files:**
- Modify: `Assets/Tests/EditMode/TestSaveData.cs`
- Modify: `Assets/Tests/EditMode/TestApothekeManager.cs`
- Modify: `Assets/Tests/EditMode/TestVisitorManager.cs`
- Modify: `Assets/Tests/EditMode/TestMallumHouse.cs`
- Modify: `Assets/Tests/EditMode/TestFlameConfig.cs`

- [ ] **Step 1: TestSaveData**

Replace `data.seedInventory` with `data.inventory`. Replace `SeedInventoryEntry { seedName = "Basil" }` with `InventoryItem { itemName = "Basil_Seed" }`. Replace `InventoryItem { itemName = "Acorn" }` with `InventoryItem { itemName = "Acorn" }` (unchanged). Update assertions.

- [ ] **Step 2: TestApothekeManager**

Replace `data.items` with `data.inventory`. Item names stay the same (these are non-harvest items used in recipe tests).

- [ ] **Step 3: TestVisitorManager**

Replace `data.items` with `data.inventory`. `ExecuteTrade` tests: change from two list parameters to single `data.inventory`. Seed rewards: expect `_Seed` suffix. Replace `SeedInventoryEntry` with `InventoryItem`.

- [ ] **Step 4: TestMallumHouse**

Replace `InventoryItem { itemName = "Basil_harvest" }` with `InventoryItem { itemName = "Basil" }`. The `CanAffordHarvests` test items match the new naming.

- [ ] **Step 5: TestFlameConfig**

Same as TestMallumHouse — rename `_harvest` items to bare names.

- [ ] **Step 6: Run all tests**

```
Unity Test Runner > EditMode > Run All
```
Or via MCP: `run_tests` with `mode: "EditMode"`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Tests/
git commit -m "refactor: update all tests for unified inventory"
```

---

## Chunk 5: Server-Side Item Sprite Keys

### Task 17: Rename harvest sprite files to match new naming

**Files:**
- Rename files in: `server/priv/static/assets/sprites/items/harvests/`

- [ ] **Step 1: Rename harvest sprite files**

The harvest sprite filenames currently use `_harvest` suffix. Since inventory keys no longer have the suffix, the sprite keys should match. However, sprite keys are a display concern and don't need to match inventory keys exactly — they're looked up by the UI code. Check if any client code maps inventory item names to sprite keys and update accordingly.

For now, leave sprite file paths as-is since they're under `items/harvests/` which provides the category context. The client UI will need a mapping function from inventory name to sprite key.

- [ ] **Step 2: Commit if any changes**

---

## Chunk 6: Final Verification

### Task 18: Full integration check

- [ ] **Step 1: Server compile and test**

```bash
cd server && mix compile && mix test
```

- [ ] **Step 2: Run Unity EditMode tests**

Via Unity Test Runner or MCP `run_tests`.

- [ ] **Step 3: Delete old save file**

Since we're in early development with no backwards compat, delete any local `save.json` to start fresh.

- [ ] **Step 4: Verify in editor**

Enter play mode, check that initial seeds appear in inventory, plant a seed, harvest, verify harvest item appears with correct name.
