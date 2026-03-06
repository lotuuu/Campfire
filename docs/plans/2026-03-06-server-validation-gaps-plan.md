# Server Validation Gaps — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close all exploitable gaps between the Unity client and Elixir backend by adding server-side validation, moving client-only systems to the server, and wiring client managers to use server endpoints.

**Architecture:** Optimistic local + server validation. Existing craft/plant/water flows keep their fire-and-forget pattern with server rejection triggering full resync. New systems (birds, apotheke craft, skins) are server-authoritative (client waits for response). All entity placement validates grid bounds and collision server-side.

**Tech Stack:** Elixir/Phoenix (server contexts, Ecto migrations, controllers), Unity C# (managers, GameService, ConfigService), NUnit (Unity EditMode tests), ExUnit (server tests)

---

## Task 1: Shared Hex Grid Validation Module (Server)

Create a reusable module for entity cap + grid validation used by all craft/plant operations.

**Files:**
- Create: `server/lib/camp_fire/game/grid_validation.ex`
- Test: `server/test/camp_fire/game/grid_validation_test.exs`

**Step 1: Write failing tests**

```elixir
# server/test/camp_fire/game/grid_validation_test.exs
defmodule CampFire.Game.GridValidationTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.GridValidation
  alias CampFire.Economy

  defp setup_player do
    seed_building_costs()
    seed_flame_config()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    # Boost mana for crafting
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
    player
  end

  describe "hex_distance/2" do
    test "origin has distance 0" do
      assert GridValidation.hex_distance(0, 0) == 0
    end

    test "adjacent hex has distance 1" do
      assert GridValidation.hex_distance(1, 0) == 1
      assert GridValidation.hex_distance(0, 1) == 1
      assert GridValidation.hex_distance(-1, 1) == 1
    end

    test "distant hex uses axial distance formula" do
      # max(|q|, |r|, |q+r|)
      assert GridValidation.hex_distance(2, -1) == 2
      assert GridValidation.hex_distance(3, -3) == 3
    end
  end

  describe "validate_grid_placement/3" do
    test "rejects coordinates outside grid radius" do
      player = setup_player()
      # Flame level 1 has grid radius from config (typically 2)
      assert {:error, :out_of_bounds} = GridValidation.validate_grid_placement(player.uid, 99, 99)
    end

    test "rejects flame origin (0,0)" do
      player = setup_player()
      assert {:error, :hex_occupied} = GridValidation.validate_grid_placement(player.uid, 0, 0)
    end

    test "rejects hex occupied by existing plot" do
      player = setup_player()
      # init_economy creates a plot at (-1, 0)
      assert {:error, :hex_occupied} = GridValidation.validate_grid_placement(player.uid, -1, 0)
    end

    test "rejects hex occupied by existing vase" do
      player = setup_player()
      # init_economy creates a vase at (0, -1)
      assert {:error, :hex_occupied} = GridValidation.validate_grid_placement(player.uid, 0, -1)
    end

    test "accepts valid empty hex" do
      player = setup_player()
      assert :ok = GridValidation.validate_grid_placement(player.uid, 1, 0)
    end
  end

  describe "check_entity_cap/1" do
    test "allows placement when under cap" do
      player = setup_player()
      assert :ok = GridValidation.check_entity_cap(player.uid)
    end

    test "rejects when at cap" do
      player = setup_player()
      # Fill up to cap by crafting many plots
      # (cap depends on flame_config; test with known values)
      seed_flame_config_with_low_cap()
      assert {:error, :entity_cap_reached} = GridValidation.check_entity_cap(player.uid)
    end
  end
end
```

**Step 2: Run tests to verify they fail**

Run: `cd server && mix test test/camp_fire/game/grid_validation_test.exs`
Expected: Compilation error — `GridValidation` module doesn't exist.

**Step 3: Add `seed_flame_config` to test helpers**

```elixir
# Add to server/test/support/test_helpers.ex

def seed_flame_config do
  config = %{
    "base_mana_per_second" => 0.5,
    "mana_per_level" => 0.3,
    "max_flame_level" => 12,
    "entity_caps" => [5, 7, 10, 13, 16, 20, 24, 28, 32, 36, 40, 45],
    "grid_sizes" => [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7],
    "upgrade_recipes" => []
  }
  :ets.insert(:config_cache, {"flame_config", config})
end

def seed_flame_config_with_low_cap do
  config = %{
    "base_mana_per_second" => 0.5,
    "mana_per_level" => 0.3,
    "max_flame_level" => 12,
    "entity_caps" => [3, 5, 7, 10, 13, 16, 20, 24, 28, 32, 36, 40],
    "grid_sizes" => [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7],
    "upgrade_recipes" => []
  }
  :ets.insert(:config_cache, {"flame_config", config})
end
```

**Step 4: Implement the module**

```elixir
# server/lib/camp_fire/game/grid_validation.ex
defmodule CampFire.Game.GridValidation do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerGarden, PlayerState}
  alias CampFire.ConfigCache

  @doc "Axial hex distance from origin."
  def hex_distance(q, r) do
    max(abs(q), max(abs(r), abs(q + r)))
  end

  @doc "Check entity cap not exceeded for player."
  def check_entity_cap(player_uid) do
    economy = Economy.get_economy(player_uid)
    flame_config = ConfigCache.get("flame_config")

    cap = get_entity_cap(flame_config, economy.flame_level)
    count = count_all_entities(player_uid)

    if count < cap, do: :ok, else: {:error, :entity_cap_reached}
  end

  @doc "Validate grid placement: bounds check + collision check."
  def validate_grid_placement(player_uid, grid_x, grid_y) do
    economy = Economy.get_economy(player_uid)
    flame_config = ConfigCache.get("flame_config")
    radius = get_grid_radius(flame_config, economy.flame_level)

    cond do
      hex_distance(grid_x, grid_y) > radius ->
        {:error, :out_of_bounds}

      hex_occupied?(player_uid, grid_x, grid_y) ->
        {:error, :hex_occupied}

      true ->
        :ok
    end
  end

  defp get_entity_cap(flame_config, flame_level) do
    caps = flame_config["entity_caps"] || [5]
    Enum.at(caps, flame_level - 1, List.last(caps))
  end

  defp get_grid_radius(flame_config, flame_level) do
    sizes = flame_config["grid_sizes"] || [2]
    Enum.at(sizes, flame_level - 1, List.last(sizes))
  end

  defp count_all_entities(player_uid) do
    plots = Repo.one(from p in PlayerPlot, where: p.player_uid == ^player_uid, select: count(p.id)) || 0
    vases = Repo.one(from v in PlayerVase, where: v.player_uid == ^player_uid, select: count(v.id)) || 0
    gardens = Repo.one(from g in PlayerGarden, where: g.player_uid == ^player_uid, select: count(g.id)) || 0
    # mallum_houses counted once that table exists (Task 3)
    houses = count_mallum_houses(player_uid)
    apotheke = 1  # always 1
    plots + vases + gardens + houses + apotheke
  end

  # Placeholder until mallum_houses table exists
  def count_mallum_houses(_player_uid), do: 0

  defp hex_occupied?(player_uid, gx, gy) do
    # Flame is always at (0, 0)
    if gx == 0 and gy == 0, do: (return true)

    # Check apotheke position from player_states
    apotheke_occupied?(player_uid, gx, gy) or
      entity_at?(PlayerPlot, player_uid, gx, gy) or
      entity_at?(PlayerVase, player_uid, gx, gy) or
      entity_at?(PlayerGarden, player_uid, gx, gy)
    # birds + mallum_houses added in later tasks
  end

  defp entity_at?(schema, player_uid, gx, gy) do
    Repo.exists?(from e in schema, where: e.player_uid == ^player_uid and e.grid_x == ^gx and e.grid_y == ^gy)
  end

  defp apotheke_occupied?(player_uid, gx, gy) do
    case Repo.get(PlayerState, player_uid) do
      nil -> gx == 1 and gy == 0  # default apotheke position
      ps ->
        data = ps.data || %{}
        ax = Map.get(data, "apothekeGridX", 1)
        ay = Map.get(data, "apothekeGridY", 0)
        gx == ax and gy == ay
    end
  end
end
```

Note: The `if ... do: (return true)` is not valid Elixir — use a `cond` or early match instead. The implementing agent should use idiomatic Elixir (e.g., check `{gx, gy} == {0, 0}` as the first clause in a `cond`).

**Step 5: Run tests and verify they pass**

Run: `cd server && mix test test/camp_fire/game/grid_validation_test.exs`
Expected: All tests pass.

**Step 6: Commit**

```
feat(server): add GridValidation module for entity cap and hex grid checks
```

---

## Task 2: Wire Grid Validation into Existing Craft/Plant Endpoints (Server)

Add entity cap + grid validation to `craft_plot`, `craft_vase`, and `Gardens.plant`.

**Files:**
- Modify: `server/lib/camp_fire/game/plots.ex:39-67` (craft_plot)
- Modify: `server/lib/camp_fire/game/vases.ex:30-60` (craft_vase)
- Modify: `server/lib/camp_fire/game/gardens.ex:43-69` (plant)
- Modify: `server/lib/camp_fire/game/plots.ex:71-101` (plant — add seed config validation)
- Modify: `server/lib/camp_fire/game/plots.ex:105-136` (water — add vase ownership + transaction)
- Modify: `server/lib/camp_fire/game/plots.ex:140-173` (harvest — wrap in transaction)
- Test: `server/test/camp_fire/game/plots_test.exs` (add validation tests)
- Test: `server/test/camp_fire/game/vases_test.exs` (add validation tests)

**Step 1: Write failing tests for new validations**

Add to existing `plots_test.exs`:

```elixir
describe "craft_plot/3 validation" do
  test "rejects when entity cap reached" do
    player = setup_player()
    seed_flame_config_with_low_cap()  # cap = 3, init creates 2 entities + apotheke = 3
    assert {:error, :entity_cap_reached} = Plots.craft_plot(player.uid, 1, 1)
  end

  test "rejects out-of-bounds coordinates" do
    player = setup_player()
    assert {:error, :out_of_bounds} = Plots.craft_plot(player.uid, 99, 99)
  end

  test "rejects occupied hex" do
    player = setup_player()
    # (0, 0) is flame
    assert {:error, :hex_occupied} = Plots.craft_plot(player.uid, 0, 0)
  end
end

describe "plant/3 validation" do
  test "rejects unknown seed name" do
    player = setup_player()
    plots = Plots.list_plots(player.uid)
    plot = List.first(plots)
    # No seed config for "FakeSeed"
    assert {:error, :unknown_seed} = Plots.plant(player.uid, plot.id, "FakeSeed")
  end
end

describe "water/3 validation" do
  test "rejects vase not owned by player" do
    player1 = setup_player()
    player2 = setup_player()
    # Get player2's vase, try to use it for player1's plot
    vases2 = Vases.list_vases(player2.uid)
    plots1 = Plots.list_plots(player1.uid)
    plot = List.first(plots1)
    vase = List.first(vases2)
    # Plant something first so plot is "growing"
    seed_seed_configs()
    Economy.upsert_seed(player1.uid, "Basil", 1)
    {:ok, _} = Plots.plant(player1.uid, plot.id, "Basil")
    assert {:error, :not_owned} = Plots.water(player1.uid, plot.id, vase.id)
  end
end
```

**Step 2: Run tests to verify they fail**

Run: `cd server && mix test test/camp_fire/game/plots_test.exs`
Expected: New tests fail (no validation exists yet).

**Step 3: Implement validations**

In `plots.ex`, modify `craft_plot/3` (line 39):
```elixir
def craft_plot(player_uid, grid_x, grid_y) do
  alias CampFire.Game.GridValidation

  with :ok <- GridValidation.check_entity_cap(player_uid),
       :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
    plot_count = count_plots(player_uid)
    cost = get_plot_cost(plot_count)
    # ... existing transaction ...
  end
end
```

In `plots.ex`, modify `plant/3` (line 71) — add seed config check:
```elixir
def plant(player_uid, plot_id, seed_name) do
  seed_configs = ConfigCache.get("seed_configs") || %{}
  unless Map.has_key?(seed_configs, seed_name) do
    return {:error, :unknown_seed}
  end
  # ... existing logic ...
end
```

In `plots.ex`, modify `water/3` (line 105) — add vase ownership check + transaction:
```elixir
def water(player_uid, plot_id, vase_id) do
  alias CampFire.Game.Vases

  plot = Repo.get!(PlayerPlot, plot_id)
  vase = Repo.get!(PlayerVase, vase_id)
  now = DateTime.utc_now() |> DateTime.truncate(:second)

  cond do
    plot.player_uid != player_uid -> {:error, :not_owned}
    vase.player_uid != player_uid -> {:error, :not_owned}
    plot.state != "growing" -> {:error, :not_growing}
    # ... cooldown check ...
    true ->
      Repo.transaction(fn ->
        case Vases.use_water(vase_id, 1) do
          {:error, reason} -> Repo.rollback(reason)
          {:ok, _vase} ->
            plot
            |> PlayerPlot.changeset(%{water_count: plot.water_count + 1, last_watered_utc: now})
            |> Repo.update!()
        end
      end)
  end
end
```

In `plots.ex`, modify `harvest/2` (line 140) — wrap in transaction:
```elixir
def harvest(player_uid, plot_id) do
  plot = Repo.get!(PlayerPlot, plot_id)

  cond do
    plot.player_uid != player_uid -> {:error, :not_owned}
    plot.state != "mature" -> {:error, :not_mature}
    true ->
      Repo.transaction(fn ->
        seed_config = Repo.one!(from sc in SeedConfig, where: sc.seed_name == ^plot.seed_name)
        score = GrowthRecipe.evaluate(seed_config.recipe, plot.snapshots, plot.water_count)
        drops = GrowthRecipe.calculate_drops(score, seed_config.base_drops)
        item_name = "#{plot.seed_name}_harvest"

        Economy.upsert_item(player_uid, item_name, drops)

        plot
        |> PlayerPlot.changeset(%{state: "empty", seed_name: nil, plant_time_utc: nil, water_count: 0, last_watered_utc: nil, snapshots: %{}})
        |> Repo.update!()

        %{score: score, drops: drops, item_name: item_name}
      end)
  end
end
```

Apply same `GridValidation` checks to `vases.ex:craft_vase/3` and `gardens.ex:plant/4`.

**Step 4: Add `seed_seed_configs` test helper**

```elixir
# Add to test_helpers.ex
def seed_seed_configs do
  configs = %{
    "Basil" => %{"growth_duration_hours" => 2.0, "base_drops" => 3, "tier" => 1, "recipe" => %{}},
    "Sprouts" => %{"growth_duration_hours" => 0.5, "base_drops" => 2, "tier" => 0, "recipe" => %{}},
    "Cress" => %{"growth_duration_hours" => 1.0, "base_drops" => 2, "tier" => 0, "recipe" => %{}}
  }
  :ets.insert(:config_cache, {"seed_configs", configs})
end
```

**Step 5: Run all server tests**

Run: `cd server && mix test`
Expected: All pass.

**Step 6: Update controller error handling**

In `game_controller.ex`, handle new error atoms in the craft/plant/water actions:
```elixir
{:error, :entity_cap_reached} -> conn |> put_status(422) |> json(%{error: "entity_cap_reached"})
{:error, :out_of_bounds} -> conn |> put_status(422) |> json(%{error: "out_of_bounds"})
{:error, :hex_occupied} -> conn |> put_status(422) |> json(%{error: "hex_occupied"})
{:error, :unknown_seed} -> conn |> put_status(422) |> json(%{error: "unknown_seed"})
```

**Step 7: Commit**

```
feat(server): add entity cap, grid validation, ownership checks, and transactions
```

---

## Task 3: Mallum Houses — Server Table + Context

**Files:**
- Create: `server/priv/repo/migrations/TIMESTAMP_create_mallum_houses.exs`
- Create: `server/lib/camp_fire/game/player_mallum_house.ex`
- Create: `server/lib/camp_fire/game/mallum_houses.ex`
- Modify: `server/lib/camp_fire/game/grid_validation.ex` (add house counting + collision)
- Modify: `server/lib/camp_fire/economy.ex:199-228` (create starter house in init_economy)
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex` (add craft endpoint + include in get_state)
- Modify: `server/lib/camp_fire_web/router.ex:112-135` (add route)
- Test: `server/test/camp_fire/game/mallum_houses_test.exs`

**Step 1: Create migration**

```elixir
# server/priv/repo/migrations/YYYYMMDDHHMMSS_create_mallum_houses.exs
defmodule CampFire.Repo.Migrations.CreateMallumHouses do
  use Ecto.Migration

  def change do
    create table(:player_mallum_houses) do
      add :player_uid, references(:players, column: :uid, type: :string), null: false
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :skin_name, :string
      add :unlocked_skins, {:array, :string}, default: []
      timestamps()
    end

    create index(:player_mallum_houses, [:player_uid])
  end
end
```

**Step 2: Create schema**

```elixir
# server/lib/camp_fire/game/player_mallum_house.ex
defmodule CampFire.Game.PlayerMallumHouse do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_mallum_houses" do
    field :player_uid, :string
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    timestamps()
  end

  def changeset(house, attrs) do
    house
    |> cast(attrs, [:player_uid, :grid_x, :grid_y, :skin_name, :unlocked_skins])
    |> validate_required([:player_uid, :grid_x, :grid_y])
  end
end
```

**Step 3: Write failing tests**

```elixir
# server/test/camp_fire/game/mallum_houses_test.exs
defmodule CampFire.Game.MallumHousesTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.{MallumHouses, Mallums}
  alias CampFire.Economy

  defp setup_player do
    seed_building_costs()
    seed_flame_config()
    seed_mallum_house_config()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
    player
  end

  describe "craft_house/3" do
    test "creates house and spawns mallums" do
      player = setup_player()
      initial_mallums = length(Mallums.list_mallums(player.uid))
      {:ok, house} = MallumHouses.craft_house(player.uid, 1, 1)
      assert house.grid_x == 1
      assert house.grid_y == 1
      new_mallums = length(Mallums.list_mallums(player.uid))
      assert new_mallums > initial_mallums
    end

    test "rejects when entity cap reached" do
      player = setup_player()
      seed_flame_config_with_low_cap()
      assert {:error, :entity_cap_reached} = MallumHouses.craft_house(player.uid, 1, 1)
    end

    test "spends mana and items" do
      player = setup_player()
      Economy.upsert_item(player.uid, "Basil_harvest", 5)
      before_mana = Economy.get_economy(player.uid).mana
      {:ok, _} = MallumHouses.craft_house(player.uid, 1, 1)
      after_mana = Economy.get_economy(player.uid).mana
      assert after_mana < before_mana
    end
  end

  describe "init_economy creates starter house" do
    test "new player has one mallum house" do
      seed_building_costs()
      seed_flame_config()
      seed_mallum_house_config()
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      houses = MallumHouses.list_houses(player.uid)
      assert length(houses) == 1
      house = List.first(houses)
      assert house.grid_x == 1
      assert house.grid_y == -1
    end
  end
end
```

**Step 4: Implement context**

```elixir
# server/lib/camp_fire/game/mallum_houses.ex
defmodule CampFire.Game.MallumHouses do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Game.{PlayerMallumHouse, Mallums, GridValidation}
  alias CampFire.ConfigCache

  def list_houses(player_uid) do
    Repo.all(from h in PlayerMallumHouse, where: h.player_uid == ^player_uid)
  end

  def count_houses(player_uid) do
    Repo.one(from h in PlayerMallumHouse, where: h.player_uid == ^player_uid, select: count(h.id)) || 0
  end

  def craft_house(player_uid, grid_x, grid_y) do
    with :ok <- GridValidation.check_entity_cap(player_uid),
         :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
      house_count = count_houses(player_uid)
      config = ConfigCache.get("mallum_house_config") || %{}
      costs = config["house_costs"] || []
      cost = Enum.at(costs, min(house_count, length(costs) - 1), %{})

      Repo.transaction(fn ->
        mana_cost = cost["manaCost"] || 0
        case Economy.spend_mana(player_uid, mana_cost) do
          {:ok, _} -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end

        harvest_costs = cost["harvestCosts"] || []
        Enum.each(harvest_costs, fn %{"itemName" => name, "count" => count} ->
          case Economy.spend_item(player_uid, name, count) do
            {:ok, _} -> :ok
            {:error, reason} -> Repo.rollback(reason)
          end
        end)

        house = %PlayerMallumHouse{}
        |> PlayerMallumHouse.changeset(%{player_uid: player_uid, grid_x: grid_x, grid_y: grid_y})
        |> Repo.insert!()

        # Spawn mallums up to new cap
        mallums_per_house = config["mallums_per_house"] || 2
        new_house_count = house_count + 1
        target = mallums_per_house * new_house_count
        current = length(Mallums.list_mallums(player_uid))
        for _ <- 1..(target - current), target > current do
          Mallums.create_mallum(player_uid)
        end

        house
      end)
    end
  end
end
```

**Step 5: Add `seed_mallum_house_config` to test helpers**

```elixir
def seed_mallum_house_config do
  config = %{
    "mallums_per_house" => 2,
    "house_costs" => [
      %{"manaCost" => 200, "harvestCosts" => [%{"itemName" => "Basil_harvest", "count" => 1}]},
      %{"manaCost" => 350, "harvestCosts" => [%{"itemName" => "Chamomile_harvest", "count" => 2}]}
    ]
  }
  :ets.insert(:config_cache, {"mallum_house_config", config})
end
```

**Step 6: Update GridValidation to count houses + check house collisions**

Replace the placeholder `count_mallum_houses/1` with a real query, and add `PlayerMallumHouse` to collision checks in `hex_occupied?/3`.

**Step 7: Update init_economy to create starter house**

In `economy.ex:create_starter_buildings/1`, add after the mallum insert:

```elixir
alias CampFire.Game.PlayerMallumHouse

%PlayerMallumHouse{}
|> PlayerMallumHouse.changeset(%{player_uid: player_uid, grid_x: 1, grid_y: -1})
|> Repo.insert!()
```

And derive mallum count from config:
```elixir
config = ConfigCache.get("mallum_house_config") || %{}
mallums_per_house = config["mallums_per_house"] || 2
for _ <- 1..mallums_per_house do
  %PlayerMallum{} |> PlayerMallum.changeset(%{player_uid: player_uid, state: "idle"}) |> Repo.insert!()
end
```

**Step 8: Add route + controller action**

Router: `post "/mallum-house/craft", GameController, :craft_mallum_house`

Controller:
```elixir
def craft_mallum_house(conn, %{"gridX" => gx, "gridY" => gy}) do
  uid = conn.assigns.current_player.uid
  case MallumHouses.craft_house(uid, gx, gy) do
    {:ok, house} -> conn |> put_status(201) |> json(serialize_mallum_house(house))
    {:error, reason} -> conn |> put_status(422) |> json(%{error: format_error(reason)})
  end
end
```

**Step 9: Include mallum houses in `get_state` response**

Add to `get_state/2`: `mallumHouses: Enum.map(houses, &serialize_mallum_house/1)`

**Step 10: Run all tests**

Run: `cd server && mix test`

**Step 11: Commit**

```
feat(server): add mallum houses table, context, craft endpoint, and init_economy house
```

---

## Task 4: Birds — Server Table + Context

**Files:**
- Create: `server/priv/repo/migrations/TIMESTAMP_create_birds.exs`
- Create: `server/lib/camp_fire/game/player_bird.ex`
- Create: `server/lib/camp_fire/game/birds.ex`
- Modify: `server/lib/camp_fire/game/grid_validation.ex` (add bird collision)
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex` (add check/collect endpoints + include in get_state)
- Modify: `server/lib/camp_fire_web/router.ex` (add routes)
- Test: `server/test/camp_fire/game/birds_test.exs`

**Step 1: Create migration**

```elixir
defmodule CampFire.Repo.Migrations.CreateBirds do
  use Ecto.Migration

  def change do
    create table(:player_birds) do
      add :player_uid, references(:players, column: :uid, type: :string), null: false
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :seed_name, :string, null: false
      add :seed_count, :integer, null: false
      add :spawned_at_utc, :utc_datetime, null: false
      timestamps()
    end

    create index(:player_birds, [:player_uid])
  end
end
```

**Step 2: Create schema**

```elixir
# server/lib/camp_fire/game/player_bird.ex
defmodule CampFire.Game.PlayerBird do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_birds" do
    field :player_uid, :string
    field :grid_x, :integer
    field :grid_y, :integer
    field :seed_name, :string
    field :seed_count, :integer
    field :spawned_at_utc, :utc_datetime
    timestamps()
  end

  def changeset(bird, attrs) do
    bird
    |> cast(attrs, [:player_uid, :grid_x, :grid_y, :seed_name, :seed_count, :spawned_at_utc])
    |> validate_required([:player_uid, :grid_x, :grid_y, :seed_name, :seed_count, :spawned_at_utc])
  end
end
```

**Step 3: Write failing tests**

```elixir
# server/test/camp_fire/game/birds_test.exs
defmodule CampFire.Game.BirdsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.Birds
  alias CampFire.Economy

  @base_chance 0.33
  @halving_factor 0.5

  defp setup_player do
    seed_building_costs()
    seed_flame_config()
    seed_mallum_house_config()
    seed_seed_configs()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    player
  end

  describe "check_spawns/1" do
    test "spawns birds on free tiles" do
      player = setup_player()
      # Advance time by a few hours so there are hours to check
      set_last_bird_check(player.uid, hours_ago(3))
      {:ok, new_birds} = Birds.check_spawns(player.uid)
      # Probabilistic: may or may not have birds, but function should succeed
      assert is_list(new_birds)
    end

    test "does not spawn on occupied tiles" do
      player = setup_player()
      set_last_bird_check(player.uid, hours_ago(1))
      {:ok, birds} = Birds.check_spawns(player.uid)
      # All spawned birds should be on unique, unoccupied tiles
      coords = Enum.map(birds, fn b -> {b.grid_x, b.grid_y} end)
      assert coords == Enum.uniq(coords)
    end
  end

  describe "collect_bird/2" do
    test "removes bird and grants seeds" do
      player = setup_player()
      # Manually insert a bird
      {:ok, bird} = Birds.insert_bird(player.uid, 1, 1, "Basil", 2)
      {:ok, reward} = Birds.collect_bird(player.uid, bird.id)
      assert reward.seed_name == "Basil"
      assert reward.seed_count == 2
      # Verify seeds added to inventory
      seeds = Economy.list_seeds(player.uid)
      basil = Enum.find(seeds, fn s -> s.seed_name == "Basil" end)
      assert basil.count >= 2
    end

    test "rejects collecting another player's bird" do
      player1 = setup_player()
      player2 = setup_player()
      {:ok, bird} = Birds.insert_bird(player2.uid, 1, 1, "Basil", 2)
      assert {:error, :not_owned} = Birds.collect_bird(player1.uid, bird.id)
    end
  end
end
```

**Step 4: Implement context**

```elixir
# server/lib/camp_fire/game/birds.ex
defmodule CampFire.Game.Birds do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Game.{PlayerBird, PlayerState, GridValidation}
  alias CampFire.ConfigCache

  @base_chance 0.33
  @halving_factor 0.5

  def list_birds(player_uid) do
    Repo.all(from b in PlayerBird, where: b.player_uid == ^player_uid)
  end

  def insert_bird(player_uid, gx, gy, seed_name, seed_count) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    %PlayerBird{}
    |> PlayerBird.changeset(%{
      player_uid: player_uid, grid_x: gx, grid_y: gy,
      seed_name: seed_name, seed_count: seed_count, spawned_at_utc: now
    })
    |> Repo.insert()
  end

  def check_spawns(player_uid) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    last_check = get_last_bird_check(player_uid)
    last_hour = truncate_to_hour(last_check || DateTime.add(now, -3600, :second))
    current_hour = truncate_to_hour(now)

    economy = Economy.get_economy(player_uid)
    flame_level = if economy, do: economy.flame_level, else: 1
    seed_configs = ConfigCache.get("seed_configs") || %{}
    eligible_seeds = get_eligible_seeds(seed_configs, flame_level)

    new_birds = walk_hours(player_uid, last_hour, current_hour, eligible_seeds, flame_level)

    update_last_bird_check(player_uid, now)
    {:ok, new_birds}
  end

  def collect_bird(player_uid, bird_id) do
    bird = Repo.get!(PlayerBird, bird_id)

    cond do
      bird.player_uid != player_uid -> {:error, :not_owned}
      true ->
        Economy.upsert_seed(player_uid, bird.seed_name, bird.seed_count)
        Repo.delete!(bird)
        {:ok, %{seed_name: bird.seed_name, seed_count: bird.seed_count}}
    end
  end

  # --- Private ---

  defp walk_hours(player_uid, from_hour, to_hour, eligible_seeds, flame_level) do
    if DateTime.compare(from_hour, to_hour) != :lt or eligible_seeds == [] do
      []
    else
      next_hour = DateTime.add(from_hour, 3600, :second)
      current_birds = list_birds(player_uid)
      chance = @base_chance * :math.pow(@halving_factor, length(current_birds))

      new_bird =
        if :rand.uniform() < chance do
          free_tiles = GridValidation.get_free_tiles(player_uid)
          case free_tiles do
            [] -> nil
            tiles ->
              {gx, gy} = Enum.random(tiles)
              {seed_name, seed_count} = roll_seed_drop(eligible_seeds, flame_level)
              {:ok, bird} = insert_bird(player_uid, gx, gy, seed_name, seed_count)
              bird
          end
        end

      birds = if new_bird, do: [new_bird], else: []
      birds ++ walk_hours(player_uid, next_hour, to_hour, eligible_seeds, flame_level)
    end
  end

  defp get_eligible_seeds(seed_configs, flame_level) do
    seed_configs
    |> Enum.filter(fn {_name, config} -> (config["tier"] || 0) <= flame_level end)
    |> Enum.map(fn {name, _config} -> name end)
  end

  defp roll_seed_drop(eligible_seeds, flame_level) do
    seed_name = Enum.random(eligible_seeds)
    seed_configs = ConfigCache.get("seed_configs") || %{}
    tier = get_in(seed_configs, [seed_name, "tier"]) || 0
    base_count = max(1, flame_level - tier + 1)
    count = Enum.random(max(1, base_count - 1)..base_count + 1)
    {seed_name, count}
  end

  defp truncate_to_hour(dt) do
    %{dt | minute: 0, second: 0, microsecond: {0, 0}}
  end

  defp get_last_bird_check(player_uid) do
    case Repo.get(PlayerState, player_uid) do
      nil -> nil
      ps ->
        case get_in(ps.data || %{}, ["lastBirdCheckHourUtc"]) do
          nil -> nil
          str -> case DateTime.from_iso8601(str) do
            {:ok, dt, _} -> dt
            _ -> nil
          end
        end
    end
  end

  defp update_last_bird_check(player_uid, now) do
    data = case Repo.get(PlayerState, player_uid) do
      nil -> %{}
      ps -> ps.data || %{}
    end
    new_data = Map.put(data, "lastBirdCheckHourUtc", DateTime.to_iso8601(now))

    case Repo.get(PlayerState, player_uid) do
      nil ->
        %PlayerState{} |> PlayerState.changeset(%{player_uid: player_uid, data: new_data}) |> Repo.insert!()
      ps ->
        ps |> PlayerState.changeset(%{data: new_data}) |> Repo.update!()
    end
  end
end
```

**Step 5: Add `get_free_tiles/1` to GridValidation**

Query all entities (plots, vases, gardens, houses, birds) + reserved hexes (flame, apotheke), compute occupied set, return all hexes within grid radius that are not occupied.

**Step 6: Add routes + controller actions**

Router: `post "/bird/check", GameController, :check_birds` and `post "/bird/collect", GameController, :collect_bird`

Include birds in `get_state` response: `birds: Enum.map(birds, &serialize_bird/1)`

**Step 7: Run tests**

Run: `cd server && mix test`

**Step 8: Commit**

```
feat(server): add birds table, context, check/collect endpoints
```

---

## Task 5: Apotheke Craft — Server Endpoint

**Files:**
- Create: `server/lib/camp_fire/game/apotheke.ex`
- Modify: `server/lib/camp_fire/config_cache.ex` (load recipe_configs)
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex` (add craft endpoint + include recipes in configs)
- Modify: `server/lib/camp_fire_web/router.ex` (add route)
- Test: `server/test/camp_fire/game/apotheke_test.exs`

**Step 1: Add recipe_configs to ConfigCache**

In `config_cache.ex:load_all/0`, add loading `"recipe_configs"` from `game_configs` table (same pattern as `building_cost_config`).

**Step 2: Add `seed_recipe_configs` test helper**

```elixir
def seed_recipe_configs do
  recipes = %{
    "Fertilizer" => %{
      "ingredients" => [%{"item_name" => "Basil_harvest", "count" => 2}],
      "result_item" => "Fertilizer",
      "result_quantity" => 1,
      "category" => "consumable"
    },
    "Speed_Potion" => %{
      "ingredients" => [%{"item_name" => "Mint_harvest", "count" => 2}, %{"item_name" => "Chamomile_harvest", "count" => 1}],
      "result_item" => "Speed_Potion",
      "result_quantity" => 1,
      "category" => "consumable"
    }
  }
  :ets.insert(:config_cache, {"recipe_configs", recipes})
end
```

**Step 3: Write failing tests**

```elixir
# server/test/camp_fire/game/apotheke_test.exs
defmodule CampFire.Game.ApothekeTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.Apotheke
  alias CampFire.Economy

  defp setup_player do
    seed_recipe_configs()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    player
  end

  describe "craft/2" do
    test "consumes ingredients and produces result" do
      player = setup_player()
      Economy.upsert_item(player.uid, "Basil_harvest", 5)
      {:ok, result} = Apotheke.craft(player.uid, "Fertilizer")
      assert result.result_item == "Fertilizer"
      assert result.result_quantity == 1
      # Check Basil_harvest reduced by 2
      items = Economy.list_items(player.uid)
      basil = Enum.find(items, fn i -> i.item_name == "Basil_harvest" end)
      assert basil.count == 3
      # Check Fertilizer added
      fert = Enum.find(items, fn i -> i.item_name == "Fertilizer" end)
      assert fert.count == 1
    end

    test "rejects unknown recipe" do
      player = setup_player()
      assert {:error, :unknown_recipe} = Apotheke.craft(player.uid, "FakeRecipe")
    end

    test "rejects insufficient ingredients" do
      player = setup_player()
      # Don't give Basil_harvest
      assert {:error, _} = Apotheke.craft(player.uid, "Fertilizer")
    end
  end
end
```

**Step 4: Implement context**

```elixir
# server/lib/camp_fire/game/apotheke.ex
defmodule CampFire.Game.Apotheke do
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.ConfigCache

  def craft(player_uid, recipe_name) do
    recipes = ConfigCache.get("recipe_configs") || %{}

    case Map.get(recipes, recipe_name) do
      nil -> {:error, :unknown_recipe}
      recipe ->
        Repo.transaction(fn ->
          # Deduct ingredients
          Enum.each(recipe["ingredients"], fn %{"item_name" => name, "count" => count} ->
            case Economy.spend_item(player_uid, name, count) do
              {:ok, _} -> :ok
              {:error, reason} -> Repo.rollback(reason)
            end
          end)

          # Grant result
          Economy.upsert_item(player_uid, recipe["result_item"], recipe["result_quantity"])

          %{result_item: recipe["result_item"], result_quantity: recipe["result_quantity"]}
        end)
    end
  end
end
```

**Step 5: Add route + controller**

Router: `post "/apotheke/craft", GameController, :craft_apotheke`

Controller:
```elixir
def craft_apotheke(conn, %{"recipeName" => recipe_name}) do
  uid = conn.assigns.current_player.uid
  case Apotheke.craft(uid, recipe_name) do
    {:ok, result} -> conn |> put_status(200) |> json(%{resultItem: result.result_item, resultQuantity: result.result_quantity})
    {:error, reason} -> conn |> put_status(422) |> json(%{error: format_error(reason)})
  end
end
```

**Step 6: Include recipes in `get_configs` response**

Add `recipes: ConfigCache.get("recipe_configs") || %{}` to the configs response.

**Step 7: Run tests**

Run: `cd server && mix test`

**Step 8: Commit**

```
feat(server): add apotheke craft endpoint with recipe validation
```

---

## Task 6: Skins — Server Validation

**Files:**
- Create: `server/lib/camp_fire/game/skins.ex`
- Modify: `server/lib/camp_fire/game/plots.ex` (update set_skin)
- Modify: `server/lib/camp_fire/game/vases.ex` (update set_skin)
- Modify: `server/lib/camp_fire/game/mallum_houses.ex` (add set_skin)
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex` (update skin endpoints + add house skin)
- Modify: `server/lib/camp_fire_web/router.ex` (add mallum house skin route)
- Test: `server/test/camp_fire/game/skins_test.exs`

**Step 1: Add skin configs to ConfigCache**

Store skins as `game_configs` key `"skin_configs"`:
```json
{
  "GreenPlot": {"building_type": "plot", "cost_item_name": "Basil_harvest", "cost_quantity": 3},
  ...
}
```

**Step 2: Write failing tests**

```elixir
# server/test/camp_fire/game/skins_test.exs
defmodule CampFire.Game.SkinsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.{Skins, Plots}
  alias CampFire.Economy

  defp setup_player do
    seed_building_costs()
    seed_flame_config()
    seed_skin_configs()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
    player
  end

  describe "apply_skin/4 for plots" do
    test "unlocks and applies skin, deducting items" do
      player = setup_player()
      Economy.upsert_item(player.uid, "Basil_harvest", 10)
      plots = Plots.list_plots(player.uid)
      plot = List.first(plots)
      {:ok, updated} = Skins.apply_skin(player.uid, :plot, plot.id, "GreenPlot")
      assert updated.skin_name == "GreenPlot"
      assert "GreenPlot" in updated.unlocked_skins
    end

    test "re-applying already unlocked skin is free" do
      player = setup_player()
      Economy.upsert_item(player.uid, "Basil_harvest", 10)
      plots = Plots.list_plots(player.uid)
      plot = List.first(plots)
      {:ok, _} = Skins.apply_skin(player.uid, :plot, plot.id, "GreenPlot")
      items_before = Economy.list_items(player.uid)
      {:ok, _} = Skins.apply_skin(player.uid, :plot, plot.id, "GreenPlot")
      items_after = Economy.list_items(player.uid)
      assert items_before == items_after
    end

    test "rejects insufficient items" do
      player = setup_player()
      plots = Plots.list_plots(player.uid)
      plot = List.first(plots)
      assert {:error, _} = Skins.apply_skin(player.uid, :plot, plot.id, "GreenPlot")
    end
  end
end
```

**Step 3: Implement Skins module**

```elixir
# server/lib/camp_fire/game/skins.ex
defmodule CampFire.Game.Skins do
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerMallumHouse}
  alias CampFire.ConfigCache

  def apply_skin(player_uid, entity_type, entity_id, skin_name) do
    skin_configs = ConfigCache.get("skin_configs") || %{}
    skin = Map.get(skin_configs, skin_name)

    unless skin, do: (return {:error, :unknown_skin})

    schema = schema_for(entity_type)
    entity = Repo.get!(schema, entity_id)

    cond do
      entity.player_uid != player_uid -> {:error, :not_owned}
      true ->
        already_unlocked = skin_name in (entity.unlocked_skins || [])

        Repo.transaction(fn ->
          unless already_unlocked do
            cost_item = skin["cost_item_name"]
            cost_qty = skin["cost_quantity"] || 1
            case Economy.spend_item(player_uid, cost_item, cost_qty) do
              {:ok, _} -> :ok
              {:error, reason} -> Repo.rollback(reason)
            end
          end

          new_unlocked = if already_unlocked, do: entity.unlocked_skins, else: (entity.unlocked_skins || []) ++ [skin_name]

          entity
          |> Ecto.Changeset.change(%{skin_name: skin_name, unlocked_skins: new_unlocked})
          |> Repo.update!()
        end)
    end
  end

  defp schema_for(:plot), do: PlayerPlot
  defp schema_for(:vase), do: PlayerVase
  defp schema_for(:mallum_house), do: PlayerMallumHouse
end
```

Note: The `unless ... do: (return ...)` is not valid Elixir. The implementing agent should use a `with` or `cond` pattern instead.

**Step 4: Update existing set_skin controller actions**

Replace direct `Plots.set_skin`/`Vases.set_skin` calls with `Skins.apply_skin` calls. Add `mallum_house/set-skin` route.

**Step 5: Add `seed_skin_configs` test helper**

```elixir
def seed_skin_configs do
  configs = %{
    "GreenPlot" => %{"building_type" => "plot", "cost_item_name" => "Basil_harvest", "cost_quantity" => 3},
    "BluePlot" => %{"building_type" => "plot", "cost_item_name" => "Chamomile_harvest", "cost_quantity" => 2}
  }
  :ets.insert(:config_cache, {"skin_configs", configs})
end
```

**Step 6: Include skin configs in `get_configs` response**

**Step 7: Run tests**

Run: `cd server && mix test`

**Step 8: Commit**

```
feat(server): add skin unlock/apply with item cost validation
```

---

## Task 7: Client — Wire MallumManager to Craft House via Server

**Files:**
- Modify: `Assets/Scripts/Managers/MallumManager.cs:292-328` (CraftMallumHouse)
- Modify: `Assets/Scripts/Services/GameService.cs` (add CraftMallumHouse endpoint)
- Modify: `Assets/Scripts/Data/MallumHouseSave.cs` (add serverId)
- Modify: `Assets/Scripts/Services/GameService.cs:73-187` (include houses in ApplyGameState)

**Step 1: Add serverId to MallumHouseSave**

```csharp
// MallumHouseSave.cs
public int serverId;
```

**Step 2: Add CraftMallumHouse to GameService**

```csharp
public async Task<ServerMallumHouse> CraftMallumHouse(int gridX, int gridY)
{
    var body = $"{{\"gridX\":{gridX},\"gridY\":{gridY}}}";
    var response = await PostJson("/game/mallum-house/craft", body);
    if (response == null) return null;
    return ParseMallumHouse(response);
}
```

**Step 3: Wire MallumManager.CraftMallumHouse**

After local house creation, call server:
```csharp
if (GameService.Instance != null && GameService.Instance.IsOnline)
{
    var result = await GameService.Instance.CraftMallumHouse(gridX, gridY);
    if (result != null)
    {
        var house = data.mallumHouses[data.mallumHouses.Count - 1];
        house.serverId = result.id;
        SaveManager.Instance.Save();
    }
}
```

**Step 4: Include mallum houses in ApplyGameState**

Parse `mallumHouses` array from server response and merge with local state (same pattern as plots/vases).

**Step 5: Run Unity tests**

Run via Unity MCP: `run_tests` with mode "EditMode"

**Step 6: Commit**

```
feat(client): wire MallumManager.CraftMallumHouse to server endpoint
```

---

## Task 8: Client — Wire BirdManager to Server

**Files:**
- Modify: `Assets/Scripts/Managers/BirdManager.cs` (replace local logic with server calls)
- Modify: `Assets/Scripts/Services/GameService.cs` (add CheckBirds + CollectBird endpoints)
- Modify: `Assets/Scripts/Data/BirdSave.cs` (add serverId)
- Modify: `Assets/Scripts/Services/GameService.cs:73-187` (include birds in ApplyGameState)

**Step 1: Add serverId to BirdSave**

```csharp
public int serverId;
```

**Step 2: Add bird endpoints to GameService**

```csharp
public async Task<List<ServerBird>> CheckBirds()
{
    var response = await PostJson("/game/bird/check", "{}");
    if (response == null) return null;
    return ParseBirdList(response);
}

public async Task<BirdReward> CollectBird(int birdId)
{
    var body = $"{{\"birdId\":{birdId}}}";
    var response = await PostJson("/game/bird/collect", body);
    if (response == null) return null;
    return ParseBirdReward(response);
}
```

**Step 3: Rewrite BirdManager.Update to use server**

Replace `ProcessHourlyChecks` call with:
```csharp
private async void CheckBirdsFromServer()
{
    if (GameService.Instance == null || !GameService.Instance.IsOnline) return;
    if (_isChecking) return;
    _isChecking = true;

    var newBirds = await GameService.Instance.CheckBirds();
    if (newBirds != null)
    {
        var data = SaveManager.Instance.Data;
        foreach (var bird in newBirds)
        {
            data.birds.Add(new BirdSave { serverId = bird.id, gridX = bird.gridX, gridY = bird.gridY, seedName = bird.seedName, seedCount = bird.seedCount });
        }
        SaveManager.Instance.Save();
        if (newBirds.Count > 0) OnBirdPlaced?.Invoke();
    }
    _isChecking = false;
}
```

Call `CheckBirdsFromServer()` hourly in Update (same timing as before, but server call instead of local RNG).

**Step 4: Rewrite CollectBird to use server**

```csharp
public async Task<BirdSave> CollectBirdFromServer(int birdIndex)
{
    var data = SaveManager.Instance.Data;
    if (birdIndex < 0 || birdIndex >= data.birds.Count) return null;
    var bird = data.birds[birdIndex];
    if (bird.serverId <= 0) return null;

    var reward = await GameService.Instance.CollectBird(bird.serverId);
    if (reward == null) return null;

    data.birds.RemoveAt(birdIndex);
    ApothekeManager.Instance.AddSeed(reward.seedName, reward.seedCount);
    SaveManager.Instance.Save();
    OnBirdCollected?.Invoke(bird);
    return bird;
}
```

**Step 5: Keep static methods for offline fallback / test compatibility**

Keep `GetFreeTiles`, `GetEligibleSeeds`, `RollSeedDrop`, `ProcessHourlyChecks` as static methods but mark them as only used when offline (or for tests). The Update loop should prefer server calls when online.

**Step 6: Include birds in ApplyGameState**

Parse `birds` array from server get_state response.

**Step 7: Run Unity tests**

Existing `TestBirdManager` tests should still pass since static methods are preserved.

**Step 8: Commit**

```
feat(client): wire BirdManager to server check/collect endpoints
```

---

## Task 9: Client — Wire ApothekeManager to Server Craft

**Files:**
- Modify: `Assets/Scripts/Managers/ApothekeManager.cs:35-71` (Mix method → craft endpoint)
- Modify: `Assets/Scripts/Services/GameService.cs` (add CraftApotheke endpoint)
- Modify: `Assets/Scripts/Services/ConfigService.cs` (parse recipe configs)

**Step 1: Add CraftApotheke to GameService**

```csharp
public async Task<CraftResult> CraftApotheke(string recipeName)
{
    var body = $"{{\"recipeName\":\"{recipeName}\"}}";
    var response = await PostJson("/game/apotheke/craft", body);
    if (response == null) return null;
    return ParseCraftResult(response);
}
```

**Step 2: Rewrite ApothekeManager.Mix to call server**

```csharp
public async Task<bool> Mix(RecipeData recipe)
{
    if (!CanMix(recipe)) return false;

    if (GameService.Instance != null && GameService.Instance.IsOnline)
    {
        var result = await GameService.Instance.CraftApotheke(recipe.recipeName);
        if (result == null) return false;

        // Server succeeded — sync inventory
        await EconomyService.Instance.SyncFromServer();
        return true;
    }
    else
    {
        // Offline fallback: local mix with economy queue (existing logic)
        // ... keep existing code as fallback ...
    }
}
```

Note: Since `Mix` changes from `bool` to `async Task<bool>`, callers (UI code) will need to be updated to `await` it.

**Step 3: Parse recipe configs in ConfigService**

Add recipes to the `ParseResponse` method and expose them for the Apotheke UI.

**Step 4: Run Unity tests**

**Step 5: Commit**

```
feat(client): wire ApothekeManager.Mix to server craft endpoint
```

---

## Task 10: Client — Wire SkinManager to Server

**Files:**
- Modify: `Assets/Scripts/Managers/SkinManager.cs:65-118` (ApplySkin method → server call)
- Modify: `Assets/Scripts/Services/GameService.cs` (update SetPlotSkin/SetVaseSkin to handle unlock response, add SetMallumHouseSkin)

**Step 1: Add skin endpoints to GameService**

Existing `SetPlotSkin`/`SetVaseSkin` already call the server. Update them to parse the response which now includes updated `unlockedSkins`.

Add `SetMallumHouseSkin`:
```csharp
public async Task<ServerMallumHouse> SetMallumHouseSkin(int houseId, string skinName)
{
    var body = $"{{\"houseId\":{houseId},\"skinName\":\"{skinName}\"}}";
    return await PostJson<ServerMallumHouse>("/game/mallum-house/set-skin", body);
}
```

**Step 2: Rewrite SkinManager.ApplySkin to call server first**

```csharp
public async Task<bool> ApplySkin(CampBuildingType type, int index, SkinData skin)
{
    // ... existing validation ...

    if (GameService.Instance != null && GameService.Instance.IsOnline)
    {
        int serverId = GetServerId(type, index);
        if (serverId > 0)
        {
            var result = await CallSkinEndpoint(type, serverId, skin.skinName);
            if (result == null) return false;
            // Apply server response locally
            ApplyLocally(type, index, skin, result.unlockedSkins);
            return true;
        }
    }

    // Offline fallback: existing local logic
    // ...
}
```

Note: `ApplySkin` changes from `bool` to `async Task<bool>` — callers need updating.

**Step 3: Run Unity tests**

**Step 4: Commit**

```
feat(client): wire SkinManager.ApplySkin to server endpoints
```

---

## Task 11: Client — Garden Mana Cost

**Files:**
- Modify: `Assets/Scripts/Managers/GardenManager.cs:59-86` (Plant method — add mana spend)
- Test: `Assets/Tests/EditMode/TestGardenManager.cs` (add mana cost test)

**Step 1: Write failing test**

```csharp
[Test]
public void Plant_SpendsManaCost()
{
    // Setup with known garden config that has manaCost
    // Verify mana is deducted after Plant()
}
```

Since `GardenManager.Plant()` uses the `CurrencyManager` singleton, and tests avoid singletons, this test should verify the logic conceptually or test the static helper if one is extracted.

**Step 2: Add mana cost to GardenManager.Plant**

In `GardenManager.cs:Plant()`, after loading `plantData`, before spending water:
```csharp
// Spend mana if plant has a cost (from server config)
float manaCost = plantData.manaCost;
if (manaCost > 0 && !CurrencyManager.Instance.SpendMana(manaCost))
    return false;
```

The `manaCost` field on `GardenPlantData` is already populated by `ConfigService.ApplyServerGardenConfigs()` (it sets `waterRequired` but needs to also set `manaCost`).

**Step 3: Add manaCost field to GardenPlantData if missing**

Check if `GardenPlantData` has a `manaCost` field. If not, add `public float manaCost;`.

**Step 4: Wire ConfigService to populate manaCost**

In `GardenManager.ApplyServerGardenConfigs()`, add: `plant.manaCost = (float)config.manaCost;`

**Step 5: Run Unity tests**

**Step 6: Commit**

```
feat(client): charge mana cost when planting gardens
```

---

## Task 12: Integration Testing + Controller Tests

**Files:**
- Modify: `server/test/camp_fire_web/controllers/game_controller_test.exs` (add tests for new endpoints)

**Step 1: Write controller tests for new endpoints**

```elixir
describe "POST /game/mallum-house/craft" do
  test "creates house and returns serialized response", %{conn: conn} do
    player = setup_player()
    conn = authed_conn(conn, player)
    |> post("/game/mallum-house/craft", %{gridX: 1, gridY: 1})
    assert %{"id" => _, "gridX" => 1, "gridY" => 1} = json_response(conn, 201)
  end
end

describe "POST /game/bird/check" do
  test "returns bird list", %{conn: conn} do
    player = setup_player()
    conn = authed_conn(conn, player) |> post("/game/bird/check")
    assert %{"newBirds" => _} = json_response(conn, 200)
  end
end

describe "POST /game/bird/collect" do
  test "collects bird and returns reward", %{conn: conn} do
    player = setup_player()
    {:ok, bird} = Birds.insert_bird(player.uid, 1, 1, "Basil", 2)
    conn = authed_conn(conn, player) |> post("/game/bird/collect", %{birdId: bird.id})
    assert %{"seedName" => "Basil", "seedCount" => 2} = json_response(conn, 200)
  end
end

describe "POST /game/apotheke/craft" do
  test "crafts recipe and returns result", %{conn: conn} do
    player = setup_player()
    Economy.upsert_item(player.uid, "Basil_harvest", 5)
    conn = authed_conn(conn, player) |> post("/game/apotheke/craft", %{recipeName: "Fertilizer"})
    assert %{"resultItem" => "Fertilizer"} = json_response(conn, 200)
  end
end

describe "entity cap rejection" do
  test "craft_plot returns 422 when at cap", %{conn: conn} do
    player = setup_player()
    seed_flame_config_with_low_cap()
    conn = authed_conn(conn, player) |> post("/game/plot/craft", %{gridX: 1, gridY: 1})
    assert json_response(conn, 422)["error"] == "entity_cap_reached"
  end
end
```

**Step 2: Run all server tests**

Run: `cd server && mix test`

**Step 3: Run all Unity tests**

Run via Unity MCP: `run_tests` mode "EditMode"

**Step 4: Commit**

```
test: add controller tests for mallum houses, birds, apotheke craft, and validation errors
```

---

## Summary of All Tasks

| # | What | Where | Depends On |
|---|------|-------|------------|
| 1 | GridValidation module | Server | — |
| 2 | Wire validation into existing endpoints | Server | 1 |
| 3 | Mallum houses table + context | Server | 1, 2 |
| 4 | Birds table + context | Server | 1, 3 |
| 5 | Apotheke craft endpoint | Server | — |
| 6 | Skins validation | Server | 3 |
| 7 | Client: wire mallum house crafting | Client | 3 |
| 8 | Client: wire birds | Client | 4 |
| 9 | Client: wire apotheke craft | Client | 5 |
| 10 | Client: wire skins | Client | 6 |
| 11 | Client: garden mana cost | Client | — |
| 12 | Integration + controller tests | Both | All |
