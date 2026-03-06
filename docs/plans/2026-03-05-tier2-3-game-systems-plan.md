# Tiers 2 & 3: Server-Authoritative Game Systems — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move all game entity state, timers, weather, and quality scoring to the server. Server becomes full source of truth. Client becomes display layer that syncs on startup.

**Architecture:** Entity tables for plots/vases/gardens/mallums with lazy timer evaluation. Weather GenServer proactively polls OWM for locations with active growth. GrowthRecipe evaluation ported to Elixir. seed_configs table stores recipe params. JSONB catch-all for cosmetic state. Single `GET /game/state` endpoint for full sync.

**Tech Stack:** Elixir/Phoenix, Ecto, GenServer, OpenWeatherMap API, Unity C#

**Design Doc:** `docs/plans/2026-03-05-tier2-3-game-systems-design.md`

---

## Task 1: Database Migration — Entity Tables

**Files:**
- Create: `server/priv/repo/migrations/20260305150000_create_game_tables.exs`

**Step 1: Write the migration**

```elixir
defmodule CampFire.Repo.Migrations.CreateGameTables do
  use Ecto.Migration

  def change do
    # Modify player_economies to add location
    alter table(:player_economies) do
      add :lat, :float
      add :lon, :float
    end

    # Plot entities
    create table(:player_plots) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :seed_name, :text
      add :state, :text, null: false, default: "empty"
      add :plant_time_utc, :utc_datetime
      add :water_count, :integer, default: 0
      add :last_watered_utc, :utc_datetime
      add :snapshots, :map, default: %{}
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :skin_name, :text
      add :unlocked_skins, {:array, :text}, default: []
      timestamps(type: :utc_datetime)
    end
    create index(:player_plots, [:player_uid])

    # Vase entities
    create table(:player_vases) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :capacity, :integer, null: false, default: 5
      add :current_water, :integer, default: 0
      add :state, :text, null: false, default: "empty"
      add :fill_start_time_utc, :utc_datetime
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :skin_name, :text
      add :unlocked_skins, {:array, :text}, default: []
      timestamps(type: :utc_datetime)
    end
    create index(:player_vases, [:player_uid])

    # Garden entities (permanent plants)
    create table(:player_gardens) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :plant_name, :text, null: false
      add :plant_time_utc, :utc_datetime, null: false
      add :last_yield_time_utc, :utc_datetime
      add :mature, :boolean, default: false
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      timestamps(type: :utc_datetime)
    end
    create index(:player_gardens, [:player_uid])

    # Mallum entities
    create table(:player_mallums) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :state, :text, null: false, default: "idle"
      add :assigned_quest_name, :text
      add :start_time_utc, :utc_datetime
      add :assigned_vase_id, references(:player_vases)
      add :pending_rewards, :map, default: fragment("'[]'::jsonb")
      timestamps(type: :utc_datetime)
    end
    create index(:player_mallums, [:player_uid])

    # Seed config (server-side recipe data)
    create table(:seed_configs) do
      add :seed_name, :text, null: false
      add :growth_duration_hours, :float, null: false
      add :base_drops, :integer, null: false
      add :mana_cost, :float, null: false, default: 0
      add :recipe, :map, null: false, default: %{}
      timestamps(type: :utc_datetime)
    end
    create unique_index(:seed_configs, [:seed_name])

    # Weather cache
    create table(:weather_cache) do
      add :lat, :float, null: false
      add :lon, :float, null: false
      add :weather_data, :map, null: false, default: %{}
      add :rain_start_utc, :utc_datetime
      add :last_rain_effect_utc, :utc_datetime
      add :fetched_at, :utc_datetime, null: false
      timestamps(type: :utc_datetime)
    end
    create unique_index(:weather_cache, [:lat, :lon])

    # Catch-all JSONB state (cosmetics, birds, apotheke position, etc.)
    create table(:player_states, primary_key: false) do
      add :player_uid, references(:players, column: :uid, type: :text), primary_key: true
      add :data, :map, default: %{}
      timestamps(type: :utc_datetime)
    end
  end
end
```

**Step 2: Run the migration**

Run: `cd server && mix ecto.migrate`
Expected: All tables created successfully.

**Step 3: Commit**

```bash
git add server/priv/repo/migrations/20260305150000_create_game_tables.exs
git commit -m "feat(server): add game entity tables migration for tiers 2-3"
```

---

## Task 2: Ecto Schemas

**Files:**
- Create: `server/lib/camp_fire/game/player_plot.ex`
- Create: `server/lib/camp_fire/game/player_vase.ex`
- Create: `server/lib/camp_fire/game/player_garden.ex`
- Create: `server/lib/camp_fire/game/player_mallum.ex`
- Create: `server/lib/camp_fire/game/seed_config.ex`
- Create: `server/lib/camp_fire/game/weather_cache.ex`
- Create: `server/lib/camp_fire/game/player_state.ex`

**Step 1: Write all schemas**

`player_plot.ex`:
```elixir
defmodule CampFire.Game.PlayerPlot do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_plots" do
    field :player_uid, :string
    field :seed_name, :string
    field :state, :string, default: "empty"
    field :plant_time_utc, :utc_datetime
    field :water_count, :integer, default: 0
    field :last_watered_utc, :utc_datetime
    field :snapshots, :map, default: %{}
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(plot, attrs) do
    plot
    |> cast(attrs, [:player_uid, :seed_name, :state, :plant_time_utc, :water_count,
                     :last_watered_utc, :snapshots, :grid_x, :grid_y, :skin_name, :unlocked_skins])
    |> validate_required([:player_uid, :state, :grid_x, :grid_y])
    |> validate_inclusion(:state, ["empty", "growing", "mature"])
  end
end
```

`player_vase.ex`:
```elixir
defmodule CampFire.Game.PlayerVase do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_vases" do
    field :player_uid, :string
    field :capacity, :integer, default: 5
    field :current_water, :integer, default: 0
    field :state, :string, default: "empty"
    field :fill_start_time_utc, :utc_datetime
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(vase, attrs) do
    vase
    |> cast(attrs, [:player_uid, :capacity, :current_water, :state, :fill_start_time_utc,
                     :grid_x, :grid_y, :skin_name, :unlocked_skins])
    |> validate_required([:player_uid, :capacity, :state, :grid_x, :grid_y])
    |> validate_inclusion(:state, ["empty", "filling", "full"])
    |> validate_number(:current_water, greater_than_or_equal_to: 0)
  end
end
```

`player_garden.ex`:
```elixir
defmodule CampFire.Game.PlayerGarden do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_gardens" do
    field :player_uid, :string
    field :plant_name, :string
    field :plant_time_utc, :utc_datetime
    field :last_yield_time_utc, :utc_datetime
    field :mature, :boolean, default: false
    field :grid_x, :integer
    field :grid_y, :integer
    timestamps(type: :utc_datetime)
  end

  def changeset(garden, attrs) do
    garden
    |> cast(attrs, [:player_uid, :plant_name, :plant_time_utc, :last_yield_time_utc,
                     :mature, :grid_x, :grid_y])
    |> validate_required([:player_uid, :plant_name, :plant_time_utc, :grid_x, :grid_y])
  end
end
```

`player_mallum.ex`:
```elixir
defmodule CampFire.Game.PlayerMallum do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_mallums" do
    field :player_uid, :string
    field :state, :string, default: "idle"
    field :assigned_quest_name, :string
    field :start_time_utc, :utc_datetime
    field :assigned_vase_id, :integer
    field :pending_rewards, {:array, :map}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(mallum, attrs) do
    mallum
    |> cast(attrs, [:player_uid, :state, :assigned_quest_name, :start_time_utc,
                     :assigned_vase_id, :pending_rewards])
    |> validate_required([:player_uid, :state])
    |> validate_inclusion(:state, ["idle", "fetching_water", "on_quest", "quest_complete"])
  end
end
```

`seed_config.ex`:
```elixir
defmodule CampFire.Game.SeedConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "seed_configs" do
    field :seed_name, :string
    field :growth_duration_hours, :float
    field :base_drops, :integer
    field :mana_cost, :float, default: 0.0
    field :recipe, :map, default: %{}
    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:seed_name, :growth_duration_hours, :base_drops, :mana_cost, :recipe])
    |> validate_required([:seed_name, :growth_duration_hours, :base_drops])
    |> unique_constraint(:seed_name)
  end
end
```

`weather_cache.ex`:
```elixir
defmodule CampFire.Game.WeatherCache do
  use Ecto.Schema
  import Ecto.Changeset

  schema "weather_cache" do
    field :lat, :float
    field :lon, :float
    field :weather_data, :map, default: %{}
    field :rain_start_utc, :utc_datetime
    field :last_rain_effect_utc, :utc_datetime
    field :fetched_at, :utc_datetime
    timestamps(type: :utc_datetime)
  end

  def changeset(cache, attrs) do
    cache
    |> cast(attrs, [:lat, :lon, :weather_data, :rain_start_utc, :last_rain_effect_utc, :fetched_at])
    |> validate_required([:lat, :lon, :weather_data, :fetched_at])
    |> unique_constraint([:lat, :lon])
  end
end
```

`player_state.ex`:
```elixir
defmodule CampFire.Game.PlayerState do
  use Ecto.Schema
  import Ecto.Changeset

  @primary_key false
  schema "player_states" do
    field :player_uid, :string, primary_key: true
    field :data, :map, default: %{}
    timestamps(type: :utc_datetime)
  end

  def changeset(state, attrs) do
    state
    |> cast(attrs, [:player_uid, :data])
    |> validate_required([:player_uid])
  end
end
```

**Step 2: Verify compilation**

Run: `cd server && mix compile`
Expected: No errors.

**Step 3: Commit**

```bash
git add server/lib/camp_fire/game/
git commit -m "feat(server): add Ecto schemas for game entities"
```

---

## Task 3: Seed Configs Seeding

**Files:**
- Create: `server/priv/repo/seeds.exs`
- Modify: `server/mix.exs` (add seed alias if not present)

**Step 1: Write the seed file**

Seed data comes from the Unity `.asset` files. Each seed's recipe has per-axis config: `{enabled, ideal_min, ideal_max, tolerance, weight}`. The recipe map stores axes as keys.

```elixir
# server/priv/repo/seeds.exs
alias CampFire.Repo
alias CampFire.Game.SeedConfig

seeds = [
  %{seed_name: "Sprouts", growth_duration_hours: 0.0167, base_drops: 1, mana_cost: 0,
    recipe: %{}},
  %{seed_name: "Cress", growth_duration_hours: 0.5, base_drops: 1, mana_cost: 0,
    recipe: %{}},
  %{seed_name: "Basil", growth_duration_hours: 2.0, base_drops: 2, mana_cost: 5,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => 18, "ideal_max" => 30, "tolerance" => 10, "weight" => 1},
      "waterings" => %{"enabled" => true, "ideal_min" => 2, "ideal_max" => 5, "tolerance" => 2, "weight" => 1}
    }},
  %{seed_name: "Mint", growth_duration_hours: 3.0, base_drops: 2, mana_cost: 8,
    recipe: %{
      "humidity" => %{"enabled" => true, "ideal_min" => 50, "ideal_max" => 80, "tolerance" => 20, "weight" => 1},
      "waterings" => %{"enabled" => true, "ideal_min" => 3, "ideal_max" => 6, "tolerance" => 2, "weight" => 1}
    }},
  %{seed_name: "Chamomile", growth_duration_hours: 4.0, base_drops: 2, mana_cost: 10,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => 15, "ideal_max" => 25, "tolerance" => 8, "weight" => 1},
      "sunlight" => %{"enabled" => true, "ideal_min" => 30, "ideal_max" => 80, "tolerance" => 20, "weight" => 1}
    }},
  %{seed_name: "Lavender", growth_duration_hours: 6.0, base_drops: 3, mana_cost: 15,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 35, "tolerance" => 10, "weight" => 1},
      "wind" => %{"enabled" => true, "ideal_min" => 5, "ideal_max" => 20, "tolerance" => 10, "weight" => 0.5},
      "waterings" => %{"enabled" => true, "ideal_min" => 2, "ideal_max" => 4, "tolerance" => 2, "weight" => 0.5}
    }},
  %{seed_name: "Rosemary", growth_duration_hours: 8.0, base_drops: 3, mana_cost: 20,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => 15, "ideal_max" => 30, "tolerance" => 10, "weight" => 1},
      "humidity" => %{"enabled" => true, "ideal_min" => 30, "ideal_max" => 60, "tolerance" => 15, "weight" => 0.8}
    }},
  %{seed_name: "Marigold", growth_duration_hours: 5.0, base_drops: 2, mana_cost: 12,
    recipe: %{
      "sunlight" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 60, "tolerance" => 20, "weight" => 1},
      "heat" => %{"enabled" => true, "ideal_min" => 18, "ideal_max" => 32, "tolerance" => 10, "weight" => 0.8}
    }},
  %{seed_name: "Poppy", growth_duration_hours: 7.0, base_drops: 3, mana_cost: 18,
    recipe: %{
      "rain" => %{"enabled" => true, "ideal_min" => 0.2, "ideal_max" => 0.6, "tolerance" => 0.3, "weight" => 1},
      "wind" => %{"enabled" => true, "ideal_min" => 0, "ideal_max" => 15, "tolerance" => 10, "weight" => 0.5}
    }},
  %{seed_name: "Dahlia", growth_duration_hours: 10.0, base_drops: 4, mana_cost: 25,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 30, "tolerance" => 8, "weight" => 1},
      "humidity" => %{"enabled" => true, "ideal_min" => 40, "ideal_max" => 70, "tolerance" => 15, "weight" => 0.8},
      "waterings" => %{"enabled" => true, "ideal_min" => 4, "ideal_max" => 8, "tolerance" => 3, "weight" => 0.7}
    }},
  %{seed_name: "Jasmine", growth_duration_hours: 12.0, base_drops: 4, mana_cost: 30,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => 22, "ideal_max" => 35, "tolerance" => 8, "weight" => 1},
      "moon" => %{"enabled" => true, "ideal_min" => 4, "ideal_max" => 6, "tolerance" => 2, "weight" => 0.8},
      "humidity" => %{"enabled" => true, "ideal_min" => 50, "ideal_max" => 80, "tolerance" => 15, "weight" => 0.6}
    }},
  %{seed_name: "Moonflower", growth_duration_hours: 16.0, base_drops: 5, mana_cost: 40,
    recipe: %{
      "moon" => %{"enabled" => true, "ideal_min" => 6, "ideal_max" => 8, "tolerance" => 2, "weight" => 1.5},
      "humidity" => %{"enabled" => true, "ideal_min" => 60, "ideal_max" => 90, "tolerance" => 15, "weight" => 0.8},
      "heat" => %{"enabled" => true, "ideal_min" => 10, "ideal_max" => 22, "tolerance" => 8, "weight" => 0.5}
    }},
  %{seed_name: "Snowdrop", growth_duration_hours: 14.0, base_drops: 4, mana_cost: 35,
    recipe: %{
      "heat" => %{"enabled" => true, "ideal_min" => -5, "ideal_max" => 10, "tolerance" => 8, "weight" => 1.2},
      "rain" => %{"enabled" => true, "ideal_min" => 0.3, "ideal_max" => 0.8, "tolerance" => 0.2, "weight" => 0.8}
    }}
]

for seed <- seeds do
  Repo.insert!(
    %SeedConfig{}
    |> SeedConfig.changeset(seed),
    on_conflict: {:replace, [:growth_duration_hours, :base_drops, :mana_cost, :recipe, :updated_at]},
    conflict_target: :seed_name
  )
end

IO.puts("Seeded #{length(seeds)} seed configs.")
```

**Step 2: Run seeds**

Run: `cd server && mix run priv/repo/seeds.exs`
Expected: "Seeded 14 seed configs."

**Step 3: Commit**

```bash
git add server/priv/repo/seeds.exs
git commit -m "feat(server): add seed config data for growth recipe evaluation"
```

---

## Task 4: GrowthRecipe Evaluation Module

Port the C# `GrowthRecipe.Evaluate()` algorithm to Elixir. This is a pure function module — no database access.

**Files:**
- Create: `server/lib/camp_fire/game/growth_recipe.ex`
- Create: `server/test/camp_fire/game/growth_recipe_test.exs`

**Step 1: Write tests first**

```elixir
# server/test/camp_fire/game/growth_recipe_test.exs
defmodule CampFire.Game.GrowthRecipeTest do
  use ExUnit.Case, async: true
  alias CampFire.Game.GrowthRecipe

  describe "score_range/4" do
    test "returns 1.0 when value is within ideal range" do
      assert GrowthRecipe.score_range(25.0, 20.0, 30.0, 10.0) == 1.0
    end

    test "returns 1.0 at ideal boundary" do
      assert GrowthRecipe.score_range(20.0, 20.0, 30.0, 10.0) == 1.0
    end

    test "returns 0.0 beyond tolerance" do
      assert GrowthRecipe.score_range(5.0, 20.0, 30.0, 10.0) == 0.0
    end

    test "linear falloff within tolerance below ideal" do
      score = GrowthRecipe.score_range(15.0, 20.0, 30.0, 10.0)
      assert_in_delta score, 0.5, 0.01
    end

    test "linear falloff within tolerance above ideal" do
      score = GrowthRecipe.score_range(35.0, 20.0, 30.0, 10.0)
      assert_in_delta score, 0.5, 0.01
    end
  end

  describe "evaluate/3" do
    test "no axes enabled returns 1.0" do
      assert GrowthRecipe.evaluate(%{}, %{}, 0) == 1.0
    end

    test "single axis with perfect conditions" do
      recipe = %{
        "heat" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 30, "tolerance" => 10, "weight" => 1}
      }
      snapshots = %{"temperatures" => [25.0], "snapshot_count" => 1}
      assert GrowthRecipe.evaluate(recipe, snapshots, 0) == 1.0
    end

    test "waterings axis" do
      recipe = %{
        "waterings" => %{"enabled" => true, "ideal_min" => 2, "ideal_max" => 5, "tolerance" => 2, "weight" => 1}
      }
      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 1}, 3) == 1.0
    end

    test "multi-axis weighted average" do
      recipe = %{
        "heat" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 30, "tolerance" => 10, "weight" => 1},
        "waterings" => %{"enabled" => true, "ideal_min" => 2, "ideal_max" => 5, "tolerance" => 2, "weight" => 1}
      }
      # Perfect heat, zero waterings (beyond tolerance -> 0.0)
      snapshots = %{"temperatures" => [25.0], "snapshot_count" => 1}
      score = GrowthRecipe.evaluate(recipe, snapshots, 0)
      # weighted avg: (1.0*1 + 0.0*1) / (1+1) = 0.5
      assert_in_delta score, 0.5, 0.01
    end

    test "zero snapshots returns 0.0 for weather axes" do
      recipe = %{
        "heat" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 30, "tolerance" => 10, "weight" => 1}
      }
      snapshots = %{"snapshot_count" => 0}
      assert GrowthRecipe.evaluate(recipe, snapshots, 0) == 0.0
    end

    test "calculates drops from score" do
      assert GrowthRecipe.calculate_drops(1.0, 4) == 4
      assert GrowthRecipe.calculate_drops(0.5, 4) == 2
      assert GrowthRecipe.calculate_drops(0.0, 4) == 1
      assert GrowthRecipe.calculate_drops(0.1, 4) == 1
    end
  end
end
```

**Step 2: Run tests to verify they fail**

Run: `cd server && mix test test/camp_fire/game/growth_recipe_test.exs`
Expected: FAIL (module not defined)

**Step 3: Implement GrowthRecipe module**

```elixir
# server/lib/camp_fire/game/growth_recipe.ex
defmodule CampFire.Game.GrowthRecipe do
  @moduledoc """
  Server-side port of the C# GrowthRecipe evaluation.
  Computes a 0-1 quality score from weather snapshots and recipe axes.
  """

  @doc "Score a single value against an ideal range with tolerance. Returns 0.0-1.0."
  def score_range(actual, ideal_min, ideal_max, tolerance) do
    cond do
      actual >= ideal_min and actual <= ideal_max -> 1.0
      actual < ideal_min ->
        distance = ideal_min - actual
        if tolerance > 0, do: max(0.0, 1.0 - distance / tolerance), else: 0.0
      actual > ideal_max ->
        distance = actual - ideal_max
        if tolerance > 0, do: max(0.0, 1.0 - distance / tolerance), else: 0.0
    end
  end

  @doc """
  Evaluate a recipe against snapshots and water count.
  Returns a 0.0-1.0 quality score (weighted average of all enabled axes).
  """
  def evaluate(recipe, snapshots, water_count) do
    enabled_axes =
      recipe
      |> Enum.filter(fn {_key, config} -> config["enabled"] == true end)

    if enabled_axes == [] do
      1.0
    else
      snapshot_count = Map.get(snapshots, "snapshot_count", 0)

      {total_weighted_score, total_weight} =
        Enum.reduce(enabled_axes, {0.0, 0.0}, fn {axis, config}, {score_acc, weight_acc} ->
          weight = config["weight"] || 1.0
          axis_score = score_axis(axis, config, snapshots, snapshot_count, water_count)
          {score_acc + axis_score * weight, weight_acc + weight}
        end)

      if total_weight > 0, do: total_weighted_score / total_weight, else: 1.0
    end
  end

  @doc "Calculate item drops: max(1, round(base_drops * score))"
  def calculate_drops(score, base_drops) do
    max(1, round(base_drops * score))
  end

  # -- Private axis scorers --

  defp score_axis("heat", config, snapshots, count, _water) do
    avg_from_list(snapshots, "temperatures", count)
    |> score_config(config)
  end

  defp score_axis("wind", config, snapshots, count, _water) do
    avg_from_list(snapshots, "wind_speeds", count)
    |> score_config(config)
  end

  defp score_axis("humidity", config, snapshots, count, _water) do
    avg_from_list(snapshots, "humidities", count)
    |> score_config(config)
  end

  defp score_axis("sunlight", config, snapshots, count, _water) do
    # Sunlight = 100 - cloud_cover (inverted)
    avg_from_list(snapshots, "cloud_covers", count)
    |> case do
      nil -> nil
      avg -> 100.0 - avg
    end
    |> score_config(config)
  end

  defp score_axis("rain", config, snapshots, count, _water) do
    rain_count = length(Map.get(snapshots, "rain_snapshots", []))
    if count > 0 do
      score_range(rain_count / count, config["ideal_min"], config["ideal_max"], config["tolerance"])
    else
      0.0
    end
  end

  defp score_axis("moon", config, snapshots, _count, _water) do
    phases = Map.get(snapshots, "moon_phase_snapshots", [])
    if phases == [] do
      0.0
    else
      # Dominant phase (most frequent)
      dominant = phases |> Enum.frequencies() |> Enum.max_by(fn {_, c} -> c end) |> elem(0)
      score_range(dominant, config["ideal_min"], config["ideal_max"], config["tolerance"])
    end
  end

  defp score_axis("waterings", config, _snapshots, _count, water_count) do
    score_range(water_count, config["ideal_min"], config["ideal_max"], config["tolerance"])
  end

  defp score_axis(_unknown, _config, _snapshots, _count, _water), do: 1.0

  defp avg_from_list(snapshots, key, count) do
    values = Map.get(snapshots, key, [])
    if count > 0 and values != [] do
      Enum.sum(values) / count
    else
      nil
    end
  end

  defp score_config(nil, _config), do: 0.0
  defp score_config(value, config) do
    score_range(value, config["ideal_min"], config["ideal_max"], config["tolerance"])
  end
end
```

**Step 4: Run tests to verify they pass**

Run: `cd server && mix test test/camp_fire/game/growth_recipe_test.exs`
Expected: All pass.

**Step 5: Commit**

```bash
git add server/lib/camp_fire/game/growth_recipe.ex server/test/camp_fire/game/growth_recipe_test.exs
git commit -m "feat(server): port GrowthRecipe evaluation to Elixir with tests"
```

---

## Task 5: Game Context — Plots

**Files:**
- Create: `server/lib/camp_fire/game/plots.ex`
- Create: `server/test/camp_fire/game/plots_test.exs`

**Step 1: Write tests**

```elixir
# server/test/camp_fire/game/plots_test.exs
defmodule CampFire.Game.PlotsTest do
  use CampFire.DataCase, async: true
  alias CampFire.Game.Plots
  alias CampFire.{Economy, Repo}
  alias CampFire.Game.SeedConfig
  import CampFire.TestHelpers

  setup do
    player = register_player()
    Economy.init_economy(player.uid)
    # Ensure seed config exists for Basil
    Repo.insert!(%SeedConfig{seed_name: "Basil", growth_duration_hours: 2.0, base_drops: 2, mana_cost: 5,
      recipe: %{"heat" => %{"enabled" => true, "ideal_min" => 20, "ideal_max" => 30, "tolerance" => 10, "weight" => 1}}})
    %{uid: player.uid}
  end

  test "craft_plot creates an empty plot and deducts mana", %{uid: uid} do
    assert {:ok, plot} = Plots.craft_plot(uid, 1, 0)
    assert plot.state == "empty"
    assert plot.grid_x == 1
  end

  test "craft_plot fails with insufficient mana", %{uid: uid} do
    Economy.spend_mana(uid, 50)  # drain mana
    assert {:error, _} = Plots.craft_plot(uid, 1, 0)
  end

  test "plant sets plot to growing and deducts seed", %{uid: uid} do
    {:ok, plot} = Plots.craft_plot(uid, 1, 0)
    Economy.upsert_seed(uid, "Basil", 3)
    assert {:ok, planted} = Plots.plant(uid, plot.id, "Basil")
    assert planted.state == "growing"
    assert planted.seed_name == "Basil"
    assert planted.plant_time_utc != nil
  end

  test "plant fails when plot not empty", %{uid: uid} do
    {:ok, plot} = Plots.craft_plot(uid, 1, 0)
    Economy.upsert_seed(uid, "Basil", 3)
    {:ok, _} = Plots.plant(uid, plot.id, "Basil")
    assert {:error, :not_empty} = Plots.plant(uid, plot.id, "Basil")
  end

  test "water increments water_count and deducts from vase", %{uid: uid} do
    {:ok, plot} = Plots.craft_plot(uid, 1, 0)
    Economy.upsert_seed(uid, "Basil", 3)
    {:ok, plot} = Plots.plant(uid, plot.id, "Basil")
    # Create a vase with water
    {:ok, vase} = CampFire.Game.Vases.craft_vase(uid, 2, 0)
    CampFire.Game.Vases.set_water(vase.id, 5)
    assert {:ok, watered} = Plots.water(uid, plot.id, vase.id)
    assert watered.water_count == 1
  end

  test "harvest evaluates recipe and returns drops", %{uid: uid} do
    {:ok, plot} = Plots.craft_plot(uid, 1, 0)
    Economy.upsert_seed(uid, "Basil", 3)
    {:ok, plot} = Plots.plant(uid, plot.id, "Basil")
    # Force mature state for testing
    Plots.force_mature(plot.id)
    assert {:ok, result} = Plots.harvest(uid, plot.id)
    assert result.drops >= 1
    assert result.score >= 0.0 and result.score <= 1.0
  end
end
```

**Step 2: Write the Plots context**

```elixir
# server/lib/camp_fire/game/plots.ex
defmodule CampFire.Game.Plots do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerPlot, SeedConfig, GrowthRecipe}
  alias CampFire.Economy

  @plot_mana_cost 10
  @water_cooldown_seconds 7200  # 2 hours

  def list_plots(player_uid) do
    from(p in PlayerPlot, where: p.player_uid == ^player_uid)
    |> Repo.all()
  end

  def craft_plot(player_uid, grid_x, grid_y) do
    Repo.transaction(fn ->
      case Economy.spend_mana(player_uid, @plot_mana_cost) do
        {:ok, _} ->
          %PlayerPlot{}
          |> PlayerPlot.changeset(%{player_uid: player_uid, state: "empty", grid_x: grid_x, grid_y: grid_y})
          |> Repo.insert!()
        {:error, reason} ->
          Repo.rollback(reason)
      end
    end)
  end

  def plant(player_uid, plot_id, seed_name) do
    with %PlayerPlot{state: "empty"} = plot <- get_plot(player_uid, plot_id),
         {:ok, _} <- Economy.spend_seed(player_uid, seed_name, 1) do
      plot
      |> PlayerPlot.changeset(%{
        seed_name: seed_name,
        state: "growing",
        plant_time_utc: DateTime.utc_now() |> DateTime.truncate(:second),
        water_count: 0,
        snapshots: %{"temperatures" => [], "wind_speeds" => [], "humidities" => [],
                     "cloud_covers" => [], "rain_snapshots" => [], "moon_phase_snapshots" => [],
                     "snapshot_count" => 0}
      })
      |> Repo.update()
    else
      %PlayerPlot{} -> {:error, :not_empty}
      nil -> {:error, :not_found}
      error -> error
    end
  end

  def water(player_uid, plot_id, vase_id) do
    alias CampFire.Game.Vases

    with %PlayerPlot{state: "growing"} = plot <- get_plot(player_uid, plot_id),
         true <- water_cooldown_elapsed?(plot),
         {:ok, _vase} <- Vases.use_water(vase_id, 1) do
      plot
      |> PlayerPlot.changeset(%{
        water_count: plot.water_count + 1,
        last_watered_utc: DateTime.utc_now() |> DateTime.truncate(:second)
      })
      |> Repo.update()
    else
      %PlayerPlot{} -> {:error, :not_growing}
      nil -> {:error, :not_found}
      false -> {:error, :water_cooldown}
      error -> error
    end
  end

  def harvest(player_uid, plot_id) do
    with %PlayerPlot{state: "mature", seed_name: seed_name} = plot <- get_plot(player_uid, plot_id),
         %SeedConfig{} = config <- Repo.one(from s in SeedConfig, where: s.seed_name == ^seed_name) do
      score = GrowthRecipe.evaluate(config.recipe, plot.snapshots, plot.water_count)
      drops = GrowthRecipe.calculate_drops(score, config.base_drops)
      item_name = "#{seed_name}_harvest"

      Repo.transaction(fn ->
        Economy.upsert_item(player_uid, item_name, drops)
        plot
        |> PlayerPlot.changeset(%{state: "empty", seed_name: nil, plant_time_utc: nil,
                                   water_count: 0, snapshots: %{}, last_watered_utc: nil})
        |> Repo.update!()
      end)

      {:ok, %{score: score, drops: drops, item_name: item_name}}
    else
      %PlayerPlot{} -> {:error, :not_mature}
      nil -> {:error, :not_found}
    end
  end

  def check_maturity(plot_id) do
    with %PlayerPlot{state: "growing", seed_name: seed_name} = plot <- Repo.get(PlayerPlot, plot_id),
         %SeedConfig{} = config <- Repo.one(from s in SeedConfig, where: s.seed_name == ^seed_name) do
      elapsed_hours = DateTime.diff(DateTime.utc_now(), plot.plant_time_utc, :second) / 3600
      if elapsed_hours >= config.growth_duration_hours do
        plot |> PlayerPlot.changeset(%{state: "mature"}) |> Repo.update()
      else
        {:ok, plot}
      end
    end
  end

  def record_snapshot(plot_id, weather_data) do
    plot = Repo.get!(PlayerPlot, plot_id)
    if plot.state != "growing", do: {:ok, plot}, else: do_record_snapshot(plot, weather_data)
  end

  def set_skin(player_uid, plot_id, skin_name) do
    with %PlayerPlot{} = plot <- get_plot(player_uid, plot_id),
         true <- skin_name in plot.unlocked_skins do
      plot |> PlayerPlot.changeset(%{skin_name: skin_name}) |> Repo.update()
    else
      false -> {:error, :skin_locked}
      nil -> {:error, :not_found}
    end
  end

  # For testing
  def force_mature(plot_id) do
    Repo.get!(PlayerPlot, plot_id)
    |> PlayerPlot.changeset(%{state: "mature"})
    |> Repo.update!()
  end

  # -- Private --

  defp get_plot(player_uid, plot_id) do
    Repo.one(from p in PlayerPlot, where: p.id == ^plot_id and p.player_uid == ^player_uid)
  end

  defp water_cooldown_elapsed?(%{last_watered_utc: nil}), do: true
  defp water_cooldown_elapsed?(%{last_watered_utc: last}) do
    DateTime.diff(DateTime.utc_now(), last, :second) >= @water_cooldown_seconds
  end

  defp do_record_snapshot(plot, weather_data) do
    snapshots = plot.snapshots || %{}
    new_snapshots = %{
      "temperatures" => (snapshots["temperatures"] || []) ++ [weather_data["temperature"]],
      "wind_speeds" => (snapshots["wind_speeds"] || []) ++ [weather_data["wind_speed"]],
      "humidities" => (snapshots["humidities"] || []) ++ [weather_data["humidity"]],
      "cloud_covers" => (snapshots["cloud_covers"] || []) ++ [weather_data["cloud_cover"]],
      "rain_snapshots" => if(weather_data["is_raining"], do: (snapshots["rain_snapshots"] || []) ++ [true], else: snapshots["rain_snapshots"] || []),
      "moon_phase_snapshots" => (snapshots["moon_phase_snapshots"] || []) ++ [weather_data["moon_phase"]],
      "snapshot_count" => (snapshots["snapshot_count"] || 0) + 1
    }
    plot |> PlayerPlot.changeset(%{snapshots: new_snapshots}) |> Repo.update()
  end
end
```

**Step 3: Run tests**

Run: `cd server && mix test test/camp_fire/game/plots_test.exs`
Expected: All pass (some may need Vases context from Task 6 — see note below).

> **Note:** The water test depends on `Vases.craft_vase` and `Vases.set_water` from Task 6. The subagent should implement Tasks 5-8 together and run tests at the end.

**Step 4: Commit**

```bash
git add server/lib/camp_fire/game/plots.ex server/test/camp_fire/game/plots_test.exs
git commit -m "feat(server): add Plots game context with craft/plant/water/harvest"
```

---

## Task 6: Game Context — Vases

**Files:**
- Create: `server/lib/camp_fire/game/vases.ex`
- Create: `server/test/camp_fire/game/vases_test.exs`

**Step 1: Write the Vases context**

```elixir
# server/lib/camp_fire/game/vases.ex
defmodule CampFire.Game.Vases do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerVase, PlayerMallum}
  alias CampFire.Economy

  @vase_mana_cost 15
  @default_capacity 5
  # Fill duration = capacity * 60 seconds (1 min per unit)
  @fill_seconds_per_unit 60

  def list_vases(player_uid) do
    from(v in PlayerVase, where: v.player_uid == ^player_uid) |> Repo.all()
  end

  def craft_vase(player_uid, grid_x, grid_y) do
    Repo.transaction(fn ->
      case Economy.spend_mana(player_uid, @vase_mana_cost) do
        {:ok, _} ->
          %PlayerVase{}
          |> PlayerVase.changeset(%{player_uid: player_uid, capacity: @default_capacity,
                                     state: "empty", grid_x: grid_x, grid_y: grid_y})
          |> Repo.insert!()
        {:error, reason} -> Repo.rollback(reason)
      end
    end)
  end

  def start_fill(player_uid, vase_id) do
    with %PlayerVase{state: state} = vase when state in ["empty", "full"] <- get_vase(player_uid, vase_id),
         {:ok, mallum} <- claim_idle_mallum(player_uid, vase_id) do
      vase
      |> PlayerVase.changeset(%{state: "filling",
                                 fill_start_time_utc: DateTime.utc_now() |> DateTime.truncate(:second)})
      |> Repo.update()
    else
      %PlayerVase{} -> {:error, :already_filling}
      nil -> {:error, :not_found}
      {:error, reason} -> {:error, reason}
    end
  end

  def check_fill(player_uid, vase_id) do
    with %PlayerVase{state: "filling"} = vase <- get_vase(player_uid, vase_id) do
      fill_duration = vase.capacity * @fill_seconds_per_unit
      elapsed = DateTime.diff(DateTime.utc_now(), vase.fill_start_time_utc, :second)

      if elapsed >= fill_duration do
        Repo.transaction(fn ->
          updated = vase
          |> PlayerVase.changeset(%{state: "full", current_water: vase.capacity, fill_start_time_utc: nil})
          |> Repo.update!()

          free_mallum_from_vase(vase.id)
          updated
        end)
      else
        {:ok, vase}
      end
    else
      nil -> {:error, :not_found}
      %PlayerVase{} -> {:error, :not_filling}
    end
  end

  def use_water(vase_id, amount) do
    vase = Repo.get!(PlayerVase, vase_id)
    if vase.current_water >= amount do
      new_water = vase.current_water - amount
      new_state = if new_water == 0, do: "empty", else: vase.state
      vase
      |> PlayerVase.changeset(%{current_water: new_water, state: if(new_state == "empty" and vase.state == "full", do: "empty", else: vase.state)})
      |> Repo.update()
    else
      {:error, :insufficient_water}
    end
  end

  def set_water(vase_id, amount) do
    Repo.get!(PlayerVase, vase_id)
    |> PlayerVase.changeset(%{current_water: amount, state: if(amount > 0, do: "full", else: "empty")})
    |> Repo.update()
  end

  def rain_fill_all(player_uid) do
    from(v in PlayerVase, where: v.player_uid == ^player_uid)
    |> Repo.all()
    |> Enum.each(fn vase ->
      vase |> PlayerVase.changeset(%{current_water: vase.capacity, state: "full", fill_start_time_utc: nil}) |> Repo.update!()
    end)

    # Free all mallums fetching water
    from(m in PlayerMallum, where: m.player_uid == ^player_uid and m.state == "fetching_water")
    |> Repo.update_all(set: [state: "idle", assigned_vase_id: nil, start_time_utc: nil])

    :ok
  end

  def set_skin(player_uid, vase_id, skin_name) do
    with %PlayerVase{} = vase <- get_vase(player_uid, vase_id),
         true <- skin_name in vase.unlocked_skins do
      vase |> PlayerVase.changeset(%{skin_name: skin_name}) |> Repo.update()
    else
      false -> {:error, :skin_locked}
      nil -> {:error, :not_found}
    end
  end

  defp get_vase(player_uid, vase_id) do
    Repo.one(from v in PlayerVase, where: v.id == ^vase_id and v.player_uid == ^player_uid)
  end

  defp claim_idle_mallum(player_uid, vase_id) do
    mallum = Repo.one(
      from m in PlayerMallum,
      where: m.player_uid == ^player_uid and m.state == "idle",
      limit: 1
    )

    case mallum do
      nil -> {:error, :no_idle_mallum}
      m ->
        m |> Ecto.Changeset.change(%{state: "fetching_water", assigned_vase_id: vase_id,
                                      start_time_utc: DateTime.utc_now() |> DateTime.truncate(:second)})
        |> Repo.update()
    end
  end

  defp free_mallum_from_vase(vase_id) do
    from(m in PlayerMallum, where: m.assigned_vase_id == ^vase_id and m.state == "fetching_water")
    |> Repo.update_all(set: [state: "idle", assigned_vase_id: nil, start_time_utc: nil])
  end
end
```

Tests should cover: craft, start_fill (claims mallum), check_fill (timer completion), use_water, rain_fill_all, set_skin.

**Step 2: Commit**

```bash
git add server/lib/camp_fire/game/vases.ex server/test/camp_fire/game/vases_test.exs
git commit -m "feat(server): add Vases game context with fill timer and rain support"
```

---

## Task 7: Game Context — Gardens

**Files:**
- Create: `server/lib/camp_fire/game/gardens.ex`

Garden plants are permanent. They grow to maturity, then yield items on a recurring interval.

```elixir
defmodule CampFire.Game.Gardens do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.PlayerGarden
  alias CampFire.Economy

  # Garden plant configs (from GardenPlantData assets)
  @garden_configs %{
    "BerryBush" => %{growth_hours: 24, yield_item: "Berry", yield_amount: 2, yield_interval_hours: 12, mana_cost: 30},
    "Oak" => %{growth_hours: 48, yield_item: "Acorn", yield_amount: 1, yield_interval_hours: 24, mana_cost: 50}
  }

  def list_gardens(player_uid) do
    from(g in PlayerGarden, where: g.player_uid == ^player_uid) |> Repo.all()
  end

  def plant(player_uid, plant_name, grid_x, grid_y) do
    config = Map.get(@garden_configs, plant_name)
    unless config, do: throw({:error, :unknown_plant})

    Repo.transaction(fn ->
      case Economy.spend_mana(player_uid, config.mana_cost) do
        {:ok, _} ->
          %PlayerGarden{}
          |> PlayerGarden.changeset(%{
            player_uid: player_uid, plant_name: plant_name,
            plant_time_utc: DateTime.utc_now() |> DateTime.truncate(:second),
            grid_x: grid_x, grid_y: grid_y
          })
          |> Repo.insert!()
        {:error, reason} -> Repo.rollback(reason)
      end
    end)
  end

  def check_and_collect(player_uid, garden_id) do
    with %PlayerGarden{} = garden <- get_garden(player_uid, garden_id),
         config when config != nil <- Map.get(@garden_configs, garden.plant_name) do
      now = DateTime.utc_now()

      # Check maturity
      garden = if not garden.mature do
        elapsed_hours = DateTime.diff(now, garden.plant_time_utc, :second) / 3600
        if elapsed_hours >= config.growth_hours do
          garden |> PlayerGarden.changeset(%{mature: true}) |> Repo.update!()
        else
          garden
        end
      else
        garden
      end

      if not garden.mature, do: {:error, :not_mature}, else: try_collect(garden, config, now, player_uid)
    else
      nil -> {:error, :not_found}
    end
  end

  defp try_collect(garden, config, now, player_uid) do
    last_yield = garden.last_yield_time_utc || garden.plant_time_utc
    elapsed_hours = DateTime.diff(now, last_yield, :second) / 3600

    if elapsed_hours >= config.yield_interval_hours do
      Economy.upsert_item(player_uid, config.yield_item, config.yield_amount)
      {:ok, updated} = garden
        |> PlayerGarden.changeset(%{last_yield_time_utc: now |> DateTime.truncate(:second)})
        |> Repo.update()
      {:ok, %{garden: updated, yield_item: config.yield_item, yield_amount: config.yield_amount}}
    else
      {:error, :yield_not_ready}
    end
  end

  defp get_garden(player_uid, garden_id) do
    Repo.one(from g in PlayerGarden, where: g.id == ^garden_id and g.player_uid == ^player_uid)
  end
end
```

**Commit:**
```bash
git add server/lib/camp_fire/game/gardens.ex
git commit -m "feat(server): add Gardens game context with growth and yield timers"
```

---

## Task 8: Game Context — Mallums & Quests

**Files:**
- Create: `server/lib/camp_fire/game/mallums.ex`

Quest data from assets: SwampForage (30min, flame 1), MeadowExpedition (60min, flame 2), DeepWoodsTrek (120min, flame 3), MountainPass (180min, flame 4), CrystalCavern (240min, flame 5), StarlitMarsh (300min, flame 6), FrostpeakSummit (360min, flame 7), AncientGrove (480min, flame 8). Each has a reward pool of weighted seed drops.

```elixir
defmodule CampFire.Game.Mallums do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.PlayerMallum
  alias CampFire.Economy

  # Quest configs from QuestData assets
  @quest_configs %{
    "SwampForage" => %{duration_minutes: 30, flame_level: 1, reward_rolls: 1, rewards: [
      %{seed: "Sprouts", weight: 40, min: 1, max: 2}, %{seed: "Cress", weight: 30, min: 1, max: 1},
      %{seed: "Basil", weight: 20, min: 1, max: 1}, %{seed: "Mint", weight: 10, min: 1, max: 1}
    ]},
    "MeadowExpedition" => %{duration_minutes: 60, flame_level: 2, reward_rolls: 1, rewards: [
      %{seed: "Basil", weight: 30, min: 1, max: 2}, %{seed: "Chamomile", weight: 25, min: 1, max: 1},
      %{seed: "Mint", weight: 25, min: 1, max: 1}, %{seed: "Marigold", weight: 15, min: 1, max: 1},
      %{seed: "Sprouts", weight: 5, min: 1, max: 2}
    ]},
    "DeepWoodsTrek" => %{duration_minutes: 120, flame_level: 3, reward_rolls: 2, rewards: [
      %{seed: "Lavender", weight: 25, min: 1, max: 2}, %{seed: "Rosemary", weight: 25, min: 1, max: 1},
      %{seed: "Chamomile", weight: 20, min: 1, max: 2}, %{seed: "Poppy", weight: 20, min: 1, max: 1},
      %{seed: "Basil", weight: 10, min: 1, max: 2}
    ]},
    "MountainPass" => %{duration_minutes: 180, flame_level: 4, reward_rolls: 2, rewards: [
      %{seed: "Dahlia", weight: 25, min: 1, max: 1}, %{seed: "Poppy", weight: 25, min: 1, max: 2},
      %{seed: "Lavender", weight: 20, min: 1, max: 2}, %{seed: "Rosemary", weight: 15, min: 1, max: 1},
      %{seed: "Marigold", weight: 15, min: 1, max: 2}
    ]},
    "CrystalCavern" => %{duration_minutes: 240, flame_level: 5, reward_rolls: 2, rewards: [
      %{seed: "Jasmine", weight: 25, min: 1, max: 1}, %{seed: "Dahlia", weight: 25, min: 1, max: 2},
      %{seed: "Moonflower", weight: 15, min: 1, max: 1}, %{seed: "Lavender", weight: 20, min: 1, max: 2},
      %{seed: "Poppy", weight: 15, min: 1, max: 1}
    ]},
    "StarlitMarsh" => %{duration_minutes: 300, flame_level: 6, reward_rolls: 3, rewards: [
      %{seed: "Moonflower", weight: 25, min: 1, max: 1}, %{seed: "Jasmine", weight: 25, min: 1, max: 2},
      %{seed: "Snowdrop", weight: 15, min: 1, max: 1}, %{seed: "Dahlia", weight: 20, min: 1, max: 1},
      %{seed: "Rosemary", weight: 15, min: 1, max: 2}
    ]},
    "FrostpeakSummit" => %{duration_minutes: 360, flame_level: 7, reward_rolls: 3, rewards: [
      %{seed: "Snowdrop", weight: 30, min: 1, max: 2}, %{seed: "Moonflower", weight: 25, min: 1, max: 1},
      %{seed: "Jasmine", weight: 20, min: 1, max: 2}, %{seed: "Dahlia", weight: 15, min: 1, max: 1},
      %{seed: "Lavender", weight: 10, min: 1, max: 2}
    ]},
    "AncientGrove" => %{duration_minutes: 480, flame_level: 8, reward_rolls: 3, rewards: [
      %{seed: "Moonflower", weight: 25, min: 1, max: 2}, %{seed: "Snowdrop", weight: 25, min: 1, max: 2},
      %{seed: "Jasmine", weight: 20, min: 1, max: 2}, %{seed: "Dahlia", weight: 15, min: 1, max: 2},
      %{seed: "Rosemary", weight: 15, min: 1, max: 2}
    ]}
  }

  def list_mallums(player_uid) do
    from(m in PlayerMallum, where: m.player_uid == ^player_uid) |> Repo.all()
  end

  def get_quest_configs, do: @quest_configs

  def send_on_quest(player_uid, quest_name) do
    config = Map.get(@quest_configs, quest_name)
    unless config, do: {:error, :unknown_quest}

    # Check flame level
    economy = Economy.get_economy(player_uid)
    if economy.flame_level < config.flame_level do
      {:error, :flame_too_low}
    else
      with {:ok, mallum} <- claim_idle_mallum_for_quest(player_uid, quest_name) do
        {:ok, mallum}
      end
    end
  end

  def check_quest(player_uid, mallum_id) do
    with %PlayerMallum{state: "on_quest"} = mallum <- get_mallum(player_uid, mallum_id),
         config when config != nil <- Map.get(@quest_configs, mallum.assigned_quest_name) do
      elapsed_min = DateTime.diff(DateTime.utc_now(), mallum.start_time_utc, :second) / 60

      if elapsed_min >= config.duration_minutes do
        rewards = roll_rewards(config)
        mallum
        |> Ecto.Changeset.change(%{state: "quest_complete", pending_rewards: rewards})
        |> Repo.update()
      else
        {:ok, mallum}
      end
    else
      nil -> {:error, :not_found}
      %PlayerMallum{} -> {:error, :not_on_quest}
    end
  end

  def collect_rewards(player_uid, mallum_id) do
    with %PlayerMallum{state: "quest_complete", pending_rewards: rewards} = mallum
         when rewards != [] <- get_mallum(player_uid, mallum_id) do
      Repo.transaction(fn ->
        for reward <- rewards do
          Economy.upsert_seed(player_uid, reward["seed_name"], reward["count"])
        end

        mallum
        |> Ecto.Changeset.change(%{state: "idle", assigned_quest_name: nil,
                                    start_time_utc: nil, pending_rewards: []})
        |> Repo.update!()
      end)
    else
      nil -> {:error, :not_found}
      %PlayerMallum{} -> {:error, :not_quest_complete}
    end
  end

  def speed_up_quest(player_uid, mallum_id) do
    with %PlayerMallum{state: "on_quest"} = mallum <- get_mallum(player_uid, mallum_id),
         config when config != nil <- Map.get(@quest_configs, mallum.assigned_quest_name),
         {:ok, _} <- Economy.spend_item(player_uid, "Speed_Potion", 1) do
      rewards = roll_rewards(config)
      mallum
      |> Ecto.Changeset.change(%{state: "quest_complete", pending_rewards: rewards})
      |> Repo.update()
    else
      nil -> {:error, :not_found}
      %PlayerMallum{} -> {:error, :not_on_quest}
      {:error, reason} -> {:error, reason}
    end
  end

  def create_mallum(player_uid) do
    %PlayerMallum{}
    |> PlayerMallum.changeset(%{player_uid: player_uid, state: "idle"})
    |> Repo.insert()
  end

  # -- Private --

  defp get_mallum(player_uid, mallum_id) do
    Repo.one(from m in PlayerMallum, where: m.id == ^mallum_id and m.player_uid == ^player_uid)
  end

  defp claim_idle_mallum_for_quest(player_uid, quest_name) do
    mallum = Repo.one(
      from m in PlayerMallum,
      where: m.player_uid == ^player_uid and m.state == "idle",
      limit: 1
    )

    case mallum do
      nil -> {:error, :no_idle_mallum}
      m ->
        m |> Ecto.Changeset.change(%{
          state: "on_quest",
          assigned_quest_name: quest_name,
          start_time_utc: DateTime.utc_now() |> DateTime.truncate(:second)
        }) |> Repo.update()
    end
  end

  def roll_rewards(config) do
    total_weight = Enum.reduce(config.rewards, 0, fn r, acc -> acc + r.weight end)

    for _i <- 1..config.reward_rolls do
      roll = :rand.uniform() * total_weight
      reward = pick_reward(config.rewards, roll, 0)
      count = Enum.random(reward.min..reward.max)
      %{"seed_name" => reward.seed, "count" => count}
    end
  end

  defp pick_reward([reward | rest], roll, cumulative) do
    cumulative = cumulative + reward.weight
    if roll <= cumulative, do: reward, else: pick_reward(rest, roll, cumulative)
  end
  defp pick_reward([], _roll, _cumulative), do: raise("unreachable")
end
```

**Commit:**
```bash
git add server/lib/camp_fire/game/mallums.ex
git commit -m "feat(server): add Mallums game context with quest lifecycle and reward rolling"
```

---

## Task 9: Weather System

**Files:**
- Create: `server/lib/camp_fire/game/weather.ex` (OWM client + cache logic)
- Create: `server/lib/camp_fire/game/weather_poller.ex` (GenServer for proactive polling)
- Create: `server/lib/camp_fire/game/moon_phase.ex` (moon phase calculator)

**Step 1: Moon phase calculator** (ported from C# MoonPhaseCalculator)

```elixir
defmodule CampFire.Game.MoonPhase do
  @moduledoc "Server-side moon phase calculation. Returns 0-7 phase index."

  def calculate(datetime \\ DateTime.utc_now()) do
    jd = julian_date(datetime)
    days_since_new = jd - 2451549.5  # Known new moon: Jan 6 2000
    cycles = days_since_new / 29.53
    phase = cycles - Float.floor(cycles)
    round(phase * 8) |> rem(8)
  end

  defp julian_date(%DateTime{year: y, month: m, day: d, hour: h, minute: min, second: s}) do
    {y, m} = if m <= 2, do: {y - 1, m + 12}, else: {y, m}
    a = div(y, 100)
    b = 2 - a + div(a, 4)
    day_fraction = (h + min / 60.0 + s / 3600.0) / 24.0
    Float.floor(365.25 * (y + 4716)) + Float.floor(30.6001 * (m + 1)) + d + day_fraction + b - 1524.5
  end
end
```

**Step 2: Weather module** (OWM API + cache)

```elixir
defmodule CampFire.Game.Weather do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{WeatherCache, MoonPhase}

  @cache_ttl_seconds 900  # 15 minutes
  @owm_base_url "https://api.openweathermap.org/data/2.5/weather"

  def get_or_fetch(lat, lon) do
    rounded_lat = Float.round(lat * 1.0, 2)
    rounded_lon = Float.round(lon * 1.0, 2)

    case get_cached(rounded_lat, rounded_lon) do
      %WeatherCache{} = cache ->
        if stale?(cache), do: fetch_and_cache(rounded_lat, rounded_lon), else: {:ok, cache}
      nil ->
        fetch_and_cache(rounded_lat, rounded_lon)
    end
  end

  def update_player_location(player_uid, lat, lon) do
    import Ecto.Query
    alias CampFire.Economy.PlayerEconomy

    rounded_lat = Float.round(lat * 1.0, 2)
    rounded_lon = Float.round(lon * 1.0, 2)

    from(e in PlayerEconomy, where: e.player_uid == ^player_uid)
    |> Repo.update_all(set: [lat: rounded_lat, lon: rounded_lon])
  end

  def active_locations do
    alias CampFire.Game.PlayerPlot
    alias CampFire.Economy.PlayerEconomy

    from(p in PlayerPlot,
      join: e in PlayerEconomy, on: p.player_uid == e.player_uid,
      where: p.state == "growing" and not is_nil(e.lat) and not is_nil(e.lon),
      select: {e.lat, e.lon},
      distinct: true
    )
    |> Repo.all()
  end

  def growing_plots_at_location(lat, lon) do
    alias CampFire.Game.PlayerPlot
    alias CampFire.Economy.PlayerEconomy

    from(p in PlayerPlot,
      join: e in PlayerEconomy, on: p.player_uid == e.player_uid,
      where: p.state == "growing" and e.lat == ^lat and e.lon == ^lon
    )
    |> Repo.all()
  end

  def process_rain(lat, lon, weather_data, cache) do
    is_raining = weather_data["condition"] in ["Rain", "Thunderstorm", "Drizzle"]

    if is_raining do
      rain_start = cache.rain_start_utc || DateTime.utc_now() |> DateTime.truncate(:second)
      rain_duration_min = DateTime.diff(DateTime.utc_now(), rain_start, :second) / 60

      cache
      |> WeatherCache.changeset(%{rain_start_utc: rain_start})
      |> Repo.update!()

      if rain_duration_min >= 15 and should_apply_rain_effect?(cache) do
        apply_rain_effects(lat, lon, cache)
      end
    else
      if cache.rain_start_utc do
        cache |> WeatherCache.changeset(%{rain_start_utc: nil}) |> Repo.update!()
      end
    end
  end

  # -- Private --

  defp get_cached(lat, lon) do
    Repo.one(from c in WeatherCache, where: c.lat == ^lat and c.lon == ^lon)
  end

  defp stale?(%WeatherCache{fetched_at: fetched_at}) do
    DateTime.diff(DateTime.utc_now(), fetched_at, :second) > @cache_ttl_seconds
  end

  defp fetch_and_cache(lat, lon) do
    case fetch_owm(lat, lon) do
      {:ok, raw} ->
        weather_data = parse_owm(raw)
        upsert_cache(lat, lon, weather_data)
      {:error, reason} -> {:error, reason}
    end
  end

  defp fetch_owm(lat, lon) do
    api_key = Application.get_env(:camp_fire, :owm_api_key, "")
    url = "#{@owm_base_url}?lat=#{lat}&lon=#{lon}&appid=#{api_key}&units=metric"

    case Req.get(url) do
      {:ok, %{status: 200, body: body}} -> {:ok, body}
      {:ok, %{status: status}} -> {:error, "OWM returned #{status}"}
      {:error, reason} -> {:error, reason}
    end
  end

  defp parse_owm(body) do
    %{
      "temperature" => get_in(body, ["main", "temp"]) || 0,
      "humidity" => get_in(body, ["main", "humidity"]) || 0,
      "wind_speed" => get_in(body, ["wind", "speed"]) || 0,
      "cloud_cover" => get_in(body, ["clouds", "all"]) || 0,
      "condition" => get_in(body, ["weather", Access.at(0), "main"]) || "Clear",
      "is_raining" => get_in(body, ["weather", Access.at(0), "main"]) in ["Rain", "Thunderstorm", "Drizzle"],
      "moon_phase" => MoonPhase.calculate()
    }
  end

  defp upsert_cache(lat, lon, weather_data) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    case get_cached(lat, lon) do
      nil ->
        %WeatherCache{}
        |> WeatherCache.changeset(%{lat: lat, lon: lon, weather_data: weather_data, fetched_at: now})
        |> Repo.insert()
      cache ->
        cache
        |> WeatherCache.changeset(%{weather_data: weather_data, fetched_at: now})
        |> Repo.update()
    end
  end

  defp should_apply_rain_effect?(%{last_rain_effect_utc: nil}), do: true
  defp should_apply_rain_effect?(%{last_rain_effect_utc: last, rain_start_utc: start}) do
    DateTime.compare(last, start) == :lt
  end

  defp apply_rain_effects(lat, lon, cache) do
    alias CampFire.Economy.PlayerEconomy
    alias CampFire.Game.{Vases, Plots}

    # Find all players at this location
    player_uids = from(e in PlayerEconomy, where: e.lat == ^lat and e.lon == ^lon, select: e.player_uid) |> Repo.all()

    for uid <- player_uids do
      Vases.rain_fill_all(uid)
      # Auto-water growing plots (with 6h cooldown via last_watered_utc)
    end

    cache |> WeatherCache.changeset(%{last_rain_effect_utc: DateTime.utc_now() |> DateTime.truncate(:second)}) |> Repo.update!()
  end
end
```

**Step 3: Weather GenServer** (proactive polling)

```elixir
defmodule CampFire.Game.WeatherPoller do
  use GenServer
  alias CampFire.Game.{Weather, Plots}

  @poll_interval_ms 15 * 60 * 1000  # 15 minutes

  def start_link(_opts) do
    GenServer.start_link(__MODULE__, %{}, name: __MODULE__)
  end

  def init(state) do
    schedule_poll()
    {:ok, state}
  end

  def handle_info(:poll, state) do
    poll_all_locations()
    schedule_poll()
    {:noreply, state}
  end

  defp schedule_poll do
    Process.send_after(self(), :poll, @poll_interval_ms)
  end

  defp poll_all_locations do
    for {lat, lon} <- Weather.active_locations() do
      case Weather.get_or_fetch(lat, lon) do
        {:ok, cache} ->
          weather_data = cache.weather_data
          # Record snapshots on all growing plots at this location
          for plot <- Weather.growing_plots_at_location(lat, lon) do
            Plots.record_snapshot(plot.id, weather_data)
          end
          # Process rain effects
          Weather.process_rain(lat, lon, weather_data, cache)
        {:error, reason} ->
          require Logger
          Logger.warning("Weather poll failed for #{lat},#{lon}: #{inspect(reason)}")
      end
    end
  end
end
```

Add to supervision tree in `server/lib/camp_fire/application.ex`:
```elixir
children = [
  # ... existing children ...
  CampFire.Game.WeatherPoller
]
```

Add `{:req, "~> 0.5"}` to `mix.exs` deps for HTTP client.

**Commit:**
```bash
git add server/lib/camp_fire/game/weather.ex server/lib/camp_fire/game/weather_poller.ex server/lib/camp_fire/game/moon_phase.ex
git commit -m "feat(server): add weather system with OWM polling, cache, and rain effects"
```

---

## Task 10: Game Controller & Routes — Plots, Vases, Gardens

**Files:**
- Create: `server/lib/camp_fire_web/controllers/game_controller.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

**Step 1: Write the game controller**

```elixir
defmodule CampFireWeb.GameController do
  use CampFireWeb, :controller
  alias CampFire.Game.{Plots, Vases, Gardens, Mallums, Weather}
  alias CampFire.Game.PlayerState
  alias CampFire.{Economy, Repo}

  # -- Plots --
  def list_plots(conn, _params) do
    uid = conn.assigns.current_player.uid
    plots = Plots.list_plots(uid)
    json(conn, %{plots: Enum.map(plots, &serialize_plot/1)})
  end

  def craft_plot(conn, %{"gridX" => x, "gridY" => y}) do
    uid = conn.assigns.current_player.uid
    case Plots.craft_plot(uid, x, y) do
      {:ok, plot} -> conn |> put_status(201) |> json(%{plot: serialize_plot(plot)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def plant_seed(conn, %{"plotId" => plot_id, "seedName" => seed_name}) do
    uid = conn.assigns.current_player.uid
    case Plots.plant(uid, plot_id, seed_name) do
      {:ok, plot} -> json(conn, %{plot: serialize_plot(plot)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def water_plot(conn, %{"plotId" => plot_id, "vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid
    case Plots.water(uid, plot_id, vase_id) do
      {:ok, plot} -> json(conn, %{plot: serialize_plot(plot)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def harvest_plot(conn, %{"plotId" => plot_id}) do
    uid = conn.assigns.current_player.uid
    # Check maturity first (lazy evaluation)
    Plots.check_maturity(plot_id)
    case Plots.harvest(uid, plot_id) do
      {:ok, result} -> json(conn, %{score: result.score, drops: result.drops, itemName: result.item_name})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def set_plot_skin(conn, %{"plotId" => plot_id, "skinName" => skin}) do
    uid = conn.assigns.current_player.uid
    case Plots.set_skin(uid, plot_id, skin) do
      {:ok, plot} -> json(conn, %{plot: serialize_plot(plot)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  # -- Vases --
  def list_vases(conn, _params) do
    uid = conn.assigns.current_player.uid
    vases = Vases.list_vases(uid)
    json(conn, %{vases: Enum.map(vases, &serialize_vase/1)})
  end

  def craft_vase(conn, %{"gridX" => x, "gridY" => y}) do
    uid = conn.assigns.current_player.uid
    case Vases.craft_vase(uid, x, y) do
      {:ok, vase} -> conn |> put_status(201) |> json(%{vase: serialize_vase(vase)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def fill_vase(conn, %{"vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid
    case Vases.start_fill(uid, vase_id) do
      {:ok, vase} -> json(conn, %{vase: serialize_vase(vase)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def check_vase(conn, %{"vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid
    case Vases.check_fill(uid, vase_id) do
      {:ok, vase} -> json(conn, %{vase: serialize_vase(vase)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def set_vase_skin(conn, %{"vaseId" => vase_id, "skinName" => skin}) do
    uid = conn.assigns.current_player.uid
    case Vases.set_skin(uid, vase_id, skin) do
      {:ok, vase} -> json(conn, %{vase: serialize_vase(vase)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  # -- Gardens --
  def list_gardens(conn, _params) do
    uid = conn.assigns.current_player.uid
    gardens = Gardens.list_gardens(uid)
    json(conn, %{gardens: Enum.map(gardens, &serialize_garden/1)})
  end

  def plant_garden(conn, %{"plantName" => name, "gridX" => x, "gridY" => y}) do
    uid = conn.assigns.current_player.uid
    case Gardens.plant(uid, name, x, y) do
      {:ok, garden} -> conn |> put_status(201) |> json(%{garden: serialize_garden(garden)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def collect_garden(conn, %{"gardenId" => garden_id}) do
    uid = conn.assigns.current_player.uid
    case Gardens.check_and_collect(uid, garden_id) do
      {:ok, result} -> json(conn, %{garden: serialize_garden(result.garden), yieldItem: result.yield_item, yieldAmount: result.yield_amount})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  # -- Quests --
  def start_quest(conn, %{"questName" => quest_name}) do
    uid = conn.assigns.current_player.uid
    case Mallums.send_on_quest(uid, quest_name) do
      {:ok, mallum} -> json(conn, %{mallum: serialize_mallum(mallum)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def check_quest(conn, %{"mallumId" => mallum_id}) do
    uid = conn.assigns.current_player.uid
    case Mallums.check_quest(uid, mallum_id) do
      {:ok, mallum} -> json(conn, %{mallum: serialize_mallum(mallum)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def collect_quest(conn, %{"mallumId" => mallum_id}) do
    uid = conn.assigns.current_player.uid
    case Mallums.collect_rewards(uid, mallum_id) do
      {:ok, mallum} -> json(conn, %{mallum: serialize_mallum(mallum), rewards: mallum.pending_rewards})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  def speed_up_quest(conn, %{"mallumId" => mallum_id}) do
    uid = conn.assigns.current_player.uid
    case Mallums.speed_up_quest(uid, mallum_id) do
      {:ok, mallum} -> json(conn, %{mallum: serialize_mallum(mallum)})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: to_string(reason)})
    end
  end

  # -- Weather --
  def submit_location(conn, %{"lat" => lat, "lon" => lon}) do
    uid = conn.assigns.current_player.uid
    Weather.update_player_location(uid, lat, lon)
    json(conn, %{ok: true})
  end

  def current_weather(conn, _params) do
    uid = conn.assigns.current_player.uid
    economy = Economy.get_economy(uid)
    if economy && economy.lat && economy.lon do
      case Weather.get_or_fetch(economy.lat, economy.lon) do
        {:ok, cache} -> json(conn, %{weather: cache.weather_data})
        {:error, _} -> conn |> put_status(503) |> json(%{error: "weather_unavailable"})
      end
    else
      conn |> put_status(404) |> json(%{error: "no_location"})
    end
  end

  # -- State Sync --
  def get_state(conn, _params) do
    uid = conn.assigns.current_player.uid

    # Lazy-evaluate all timers
    for plot <- Plots.list_plots(uid), plot.state == "growing", do: Plots.check_maturity(plot.id)
    for vase <- Vases.list_vases(uid), vase.state == "filling", do: Vases.check_fill(uid, vase.id)

    # Fetch fresh data after timer evaluation
    {economy, seeds, items} = Economy.get_full_state(uid)
    plots = Plots.list_plots(uid)
    vases = Vases.list_vases(uid)
    gardens = Gardens.list_gardens(uid)
    mallums = Mallums.list_mallums(uid)
    player_state = Repo.get(PlayerState, uid)
    weather = get_player_weather(uid, economy)

    json(conn, %{
      economy: %{
        mana: economy.mana, gems: economy.gems, flameLevel: economy.flame_level,
        lastManaCollectUtc: economy.last_mana_collect_utc,
        seeds: Enum.map(seeds, fn s -> %{seedName: s.seed_name, count: s.count} end),
        items: Enum.map(items, fn i -> %{itemName: i.item_name, count: i.count} end)
      },
      plots: Enum.map(plots, &serialize_plot/1),
      vases: Enum.map(vases, &serialize_vase/1),
      gardens: Enum.map(gardens, &serialize_garden/1),
      mallums: Enum.map(mallums, &serialize_mallum/1),
      cosmeticState: if(player_state, do: player_state.data, else: %{}),
      weather: weather
    })
  end

  def save_state(conn, %{"data" => data}) do
    uid = conn.assigns.current_player.uid
    case Repo.get(PlayerState, uid) do
      nil ->
        %PlayerState{} |> PlayerState.changeset(%{player_uid: uid, data: data}) |> Repo.insert()
      state ->
        state |> PlayerState.changeset(%{data: data}) |> Repo.update()
    end
    json(conn, %{ok: true})
  end

  # -- Serializers --

  defp serialize_plot(p) do
    %{id: p.id, seedName: p.seed_name, state: p.state,
      plantTimeUtc: p.plant_time_utc, waterCount: p.water_count,
      lastWateredUtc: p.last_watered_utc, snapshots: p.snapshots,
      gridX: p.grid_x, gridY: p.grid_y, skinName: p.skin_name,
      unlockedSkins: p.unlocked_skins}
  end

  defp serialize_vase(v) do
    %{id: v.id, capacity: v.capacity, currentWater: v.current_water,
      state: v.state, fillStartTimeUtc: v.fill_start_time_utc,
      gridX: v.grid_x, gridY: v.grid_y, skinName: v.skin_name,
      unlockedSkins: v.unlocked_skins}
  end

  defp serialize_garden(g) do
    %{id: g.id, plantName: g.plant_name, plantTimeUtc: g.plant_time_utc,
      lastYieldTimeUtc: g.last_yield_time_utc, mature: g.mature,
      gridX: g.grid_x, gridY: g.grid_y}
  end

  defp serialize_mallum(m) do
    %{id: m.id, state: m.state, assignedQuestName: m.assigned_quest_name,
      startTimeUtc: m.start_time_utc, assignedVaseId: m.assigned_vase_id,
      pendingRewards: m.pending_rewards}
  end

  defp get_player_weather(uid, economy) do
    if economy && economy.lat && economy.lon do
      case Weather.get_or_fetch(economy.lat, economy.lon) do
        {:ok, cache} -> cache.weather_data
        _ -> nil
      end
    else
      nil
    end
  end
end
```

**Step 2: Add routes to router**

Add to `server/lib/camp_fire_web/router.ex` inside the authenticated scope:

```elixir
# Game endpoints
scope "/game" do
  get "/state", GameController, :get_state
  put "/state", GameController, :save_state

  # Plots
  get "/plots", GameController, :list_plots
  post "/plot/craft", GameController, :craft_plot
  post "/plot/plant", GameController, :plant_seed
  post "/plot/water", GameController, :water_plot
  post "/plot/harvest", GameController, :harvest_plot
  post "/plot/set-skin", GameController, :set_plot_skin

  # Vases
  get "/vases", GameController, :list_vases
  post "/vase/craft", GameController, :craft_vase
  post "/vase/fill", GameController, :fill_vase
  post "/vase/check", GameController, :check_vase
  post "/vase/set-skin", GameController, :set_vase_skin

  # Gardens
  get "/gardens", GameController, :list_gardens
  post "/garden/plant", GameController, :plant_garden
  post "/garden/collect", GameController, :collect_garden

  # Quests
  post "/quest/start", GameController, :start_quest
  post "/quest/check", GameController, :check_quest
  post "/quest/collect", GameController, :collect_quest
  post "/quest/speed-up", GameController, :speed_up_quest
end

# Weather endpoints
scope "/weather" do
  post "/location", GameController, :submit_location
  get "/current", GameController, :current_weather
end
```

**Step 3: Commit**

```bash
git add server/lib/camp_fire_web/controllers/game_controller.ex server/lib/camp_fire_web/router.ex
git commit -m "feat(server): add game controller with all entity and weather endpoints"
```

---

## Task 11: Gift Validation Update

**Files:**
- Modify: `server/lib/camp_fire/gifts.ex`

Update `send_gift/3` to verify item ownership before creating the gift:

```elixir
# In send_gift, add item ownership check before insert:
def send_gift(from_uid, to_uid, items) do
  cond do
    from_uid == to_uid -> {:error, :self_gift}
    not is_list(items) or items == [] -> {:error, :empty_items}
    length(items) > @max_items_per_gift -> {:error, :too_many_items}
    gifts_today(from_uid, to_uid) >= @max_gifts_per_day -> {:error, :daily_limit}
    true ->
      # NEW: Verify sender owns all items
      case verify_and_deduct_items(from_uid, items) do
        :ok ->
          %Gift{}
          |> Gift.changeset(%{from_uid: from_uid, to_uid: to_uid, items: items})
          |> Repo.insert()
        {:error, reason} -> {:error, reason}
      end
  end
end

defp verify_and_deduct_items(player_uid, items) do
  alias CampFire.Economy
  Repo.transaction(fn ->
    for %{"item_name" => name, "count" => count} <- items do
      case Economy.spend_item(player_uid, name, count) do
        {:ok, _} -> :ok
        {:error, reason} -> Repo.rollback(reason)
      end
    end
  end)
  |> case do
    {:ok, _} -> :ok
    {:error, reason} -> {:error, reason}
  end
end
```

Also update `claim_gift` to add items to receiver's inventory:

```elixir
def claim_gift(player_uid, gift_id) do
  # ... existing claim logic ...
  # After claiming, add items to receiver
  case result do
    {:ok, items} ->
      alias CampFire.Economy
      for item <- items do
        Economy.upsert_item(player_uid, item["item_name"], item["count"])
      end
      {:ok, items}
    error -> error
  end
end
```

**Commit:**
```bash
git add server/lib/camp_fire/gifts.ex
git commit -m "feat(server): add item ownership validation to gift sending"
```

---

## Task 12: Server Tests

**Files:**
- Create: `server/test/camp_fire/game/plots_test.exs`
- Create: `server/test/camp_fire/game/vases_test.exs`
- Create: `server/test/camp_fire/game/gardens_test.exs`
- Create: `server/test/camp_fire/game/mallums_test.exs`
- Create: `server/test/camp_fire/game/weather_test.exs`
- Create: `server/test/camp_fire_web/controllers/game_controller_test.exs`

Write tests covering:

**Context tests (~25):**
- Plots: craft, plant, water (with cooldown), harvest (with recipe scoring), check_maturity, record_snapshot, set_skin
- Vases: craft, start_fill (claims mallum), check_fill (timer), use_water, rain_fill_all, set_skin
- Gardens: plant, check_and_collect (maturity + yield interval)
- Mallums: send_on_quest (flame level check), check_quest (timer), collect_rewards, speed_up_quest, roll_rewards distribution
- Weather: get_or_fetch (cache hit/miss), update_player_location, active_locations, process_rain

**Controller tests (~15):**
- Full lifecycle: craft plot → plant → water → harvest
- Full lifecycle: craft vase → fill → check → use water
- Full lifecycle: garden plant → collect
- Full lifecycle: quest start → check → collect
- GET /game/state returns complete data
- PUT /game/state saves cosmetic data
- POST /weather/location
- Gift send with insufficient items fails

Run: `cd server && mix test`
Expected: All tests pass (~70+ total with existing tests).

**Commit:**
```bash
git add server/test/
git commit -m "test(server): add comprehensive game system tests for tiers 2-3"
```

---

## Task 13: Application Setup & Dependencies

**Files:**
- Modify: `server/mix.exs` (add `req` dependency)
- Modify: `server/lib/camp_fire/application.ex` (add WeatherPoller to supervision tree)
- Modify: `server/config/config.exs` (add OWM config)
- Modify: `server/config/dev.exs` (add OWM dev key)

Add to `mix.exs` deps:
```elixir
{:req, "~> 0.5"}
```

Add to `application.ex` children:
```elixir
CampFire.Game.WeatherPoller
```

Add to `config/config.exs`:
```elixir
config :camp_fire, :owm_api_key, System.get_env("OWM_API_KEY") || ""
```

Run: `cd server && mix deps.get && mix compile`

**Commit:**
```bash
git add server/mix.exs server/mix.lock server/lib/camp_fire/application.ex server/config/
git commit -m "feat(server): add req dependency, weather poller to supervision tree, OWM config"
```

---

## Task 14: Modify player_economies Schema for lat/lon

**Files:**
- Modify: `server/lib/camp_fire/economy/player_economy.ex`

Add `lat` and `lon` fields to the existing PlayerEconomy schema and changeset.

**Commit:**
```bash
git add server/lib/camp_fire/economy/player_economy.ex
git commit -m "feat(server): add lat/lon fields to PlayerEconomy schema"
```

---

## Task 15: Client — Game State DTOs & GameService

**Files:**
- Create: `Assets/Scripts/Services/GameService.cs`
- Create: `Assets/Scripts/Data/GameStateResponse.cs`

`GameStateResponse.cs` — DTOs for deserializing `GET /game/state`:
```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class GameStateResponse
    {
        public EconomyState economy;
        public List<ServerPlot> plots;
        public List<ServerVase> vases;
        public List<ServerGarden> gardens;
        public List<ServerMallum> mallums;
        public ServerWeather weather;
        // cosmeticState handled separately as raw JSON
    }

    [Serializable]
    public class ServerPlot
    {
        public int id;
        public string seedName;
        public string state;
        public string plantTimeUtc;
        public int waterCount;
        public string lastWateredUtc;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins;
    }

    [Serializable]
    public class ServerVase
    {
        public int id;
        public int capacity;
        public int currentWater;
        public string state;
        public string fillStartTimeUtc;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins;
    }

    [Serializable]
    public class ServerGarden
    {
        public int id;
        public string plantName;
        public string plantTimeUtc;
        public string lastYieldTimeUtc;
        public bool mature;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class ServerMallum
    {
        public int id;
        public string state;
        public string assignedQuestName;
        public string startTimeUtc;
        public int assignedVaseId;
        public List<ServerReward> pendingRewards;
    }

    [Serializable]
    public class ServerReward
    {
        public string seed_name;
        public int count;
    }

    [Serializable]
    public class ServerWeather
    {
        public float temperature;
        public float humidity;
        public float wind_speed;
        public float cloud_cover;
        public string condition;
        public bool is_raining;
        public int moon_phase;
    }

    // Request DTOs for game actions
    [Serializable] public class CraftRequest { public int gridX; public int gridY; }
    [Serializable] public class PlantRequest { public int plotId; public string seedName; }
    [Serializable] public class WaterRequest { public int plotId; public int vaseId; }
    [Serializable] public class HarvestRequest { public int plotId; }
    [Serializable] public class FillVaseRequest { public int vaseId; }
    [Serializable] public class CheckVaseRequest { public int vaseId; }
    [Serializable] public class PlantGardenRequest { public string plantName; public int gridX; public int gridY; }
    [Serializable] public class CollectGardenRequest { public int gardenId; }
    [Serializable] public class QuestRequest { public string questName; }
    [Serializable] public class MallumRequest { public int mallumId; }
    [Serializable] public class SetSkinRequest { public int plotId; public int vaseId; public string skinName; }
    [Serializable] public class LocationRequest { public float lat; public float lon; }
    [Serializable] public class HarvestResponse { public float score; public int drops; public string itemName; }
}
```

`GameService.cs` — Singleton service that calls game endpoints:
```csharp
namespace Garden
{
    public class GameService : MonoBehaviour
    {
        public static GameService Instance { get; private set; }
        public bool IsInitialized { get; private set; }
        public event Action OnStateSynced;

        // Stores server-side entity IDs mapped to local indices
        private Dictionary<int, int> _plotIdMap = new();  // serverID -> local index
        private Dictionary<int, int> _vaseIdMap = new();
        private Dictionary<int, int> _gardenIdMap = new();
        private Dictionary<int, int> _mallumIdMap = new();

        public async void Initialize() { /* GET /game/state, populate SaveData */ }
        public async Task<ServerPlot> CraftPlot(int gridX, int gridY) { /* POST /game/plot/craft */ }
        public async Task<ServerPlot> PlantSeed(int plotId, string seedName) { /* POST /game/plot/plant */ }
        public async Task<ServerPlot> WaterPlot(int plotId, int vaseId) { /* POST /game/plot/water */ }
        public async Task<HarvestResponse> Harvest(int plotId) { /* POST /game/plot/harvest */ }
        public async Task<ServerVase> CraftVase(int gridX, int gridY) { /* POST /game/vase/craft */ }
        public async Task<ServerVase> FillVase(int vaseId) { /* POST /game/vase/fill */ }
        public async Task<ServerVase> CheckVase(int vaseId) { /* POST /game/vase/check */ }
        public async Task<ServerGarden> PlantGarden(string plantName, int gridX, int gridY) { /* POST /game/garden/plant */ }
        public async Task CollectGarden(int gardenId) { /* POST /game/garden/collect */ }
        public async Task<ServerMallum> StartQuest(string questName) { /* POST /game/quest/start */ }
        public async Task<ServerMallum> CheckQuest(int mallumId) { /* POST /game/quest/check */ }
        public async Task CollectQuest(int mallumId) { /* POST /game/quest/collect */ }
        public async Task<ServerMallum> SpeedUpQuest(int mallumId) { /* POST /game/quest/speed-up */ }
        public async Task SubmitLocation(float lat, float lon) { /* POST /weather/location */ }
        public async Task SaveCosmeticState(string jsonData) { /* PUT /game/state */ }
        // HTTP helpers follow same pattern as EconomyService
    }
}
```

The subagent should implement the full method bodies following the EconomyService HTTP pattern.

**Commit:**
```bash
git add Assets/Scripts/Services/GameService.cs Assets/Scripts/Data/GameStateResponse.cs
git commit -m "feat(client): add GameService and game state DTOs for server sync"
```

---

## Task 16: Client — Manager Refactoring

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs` — Replace local timer logic with GameService calls
- Modify: `Assets/Scripts/Managers/VaseManager.cs` — Replace local fill timer with server check
- Modify: `Assets/Scripts/Managers/GardenManager.cs` — Replace local yield timer with server collect
- Modify: `Assets/Scripts/Managers/MallumManager.cs` — Replace client-side RNG and timers with server calls
- Modify: `Assets/Scripts/Services/WeatherService.cs` — On location resolved, also submit to server
- Modify: `Assets/Scripts/Managers/GameManager.cs` — Initialize GameService on sign-in

Key changes:
1. **PlotManager**: `CraftPlot()` calls `GameService.CraftPlot()`, gets back server ID. `Plant()` calls `GameService.PlantSeed()`. `Water()` calls `GameService.WaterPlot()`. `Harvest()` calls `GameService.Harvest()` and uses server-returned score/drops.
2. **VaseManager**: `CraftVase()` calls `GameService.CraftVase()`. `SendToCollect()` calls `GameService.FillVase()`. Fill completion check calls `GameService.CheckVase()`.
3. **GardenManager**: `Plant()` calls `GameService.PlantGarden()`. Yield collection calls `GameService.CollectGarden()`.
4. **MallumManager**: `SendOnQuest()` calls `GameService.StartQuest()`. Quest completion check calls `GameService.CheckQuest()`. `CollectQuestRewards()` calls `GameService.CollectQuest()`. `SpeedUpQuest()` calls `GameService.SpeedUpQuest()`. Remove client-side `RollRewards()`.
5. **WeatherService**: After GPS resolution, call `GameService.SubmitLocation(lat, lon)`.
6. **GameManager**: On sign-in, call `GameService.Initialize()` after `EconomyService.Initialize()`.

The subagent should keep local optimistic updates for immediate UI feedback, but ensure server is the authority. On server response, overwrite local state.

**Commit:**
```bash
git add Assets/Scripts/Managers/ Assets/Scripts/Services/
git commit -m "feat(client): refactor managers to use server-authoritative game service"
```

---

## Task 17: Client Tests

**Files:**
- Create: `Assets/Tests/EditMode/TestGameService.cs`

Tests for DTO serialization/deserialization:
```csharp
[TestFixture]
public class TestGameService
{
    [Test]
    public void GameStateResponse_DeserializesPlots()
    {
        string json = @"{""economy"":{""mana"":50,""gems"":5,""flameLevel"":1,""seeds"":[],""items"":[]},""plots"":[{""id"":1,""seedName"":""Basil"",""state"":""growing"",""gridX"":1,""gridY"":0,""waterCount"":2}],""vases"":[],""gardens"":[],""mallums"":[]}";
        var state = JsonUtility.FromJson<GameStateResponse>(json);
        Assert.AreEqual(1, state.plots.Count);
        Assert.AreEqual("Basil", state.plots[0].seedName);
        Assert.AreEqual("growing", state.plots[0].state);
    }

    [Test]
    public void HarvestResponse_Deserializes()
    {
        string json = @"{""score"":0.85,""drops"":3,""itemName"":""Basil_harvest""}";
        var resp = JsonUtility.FromJson<HarvestResponse>(json);
        Assert.AreEqual(0.85f, resp.score, 0.01f);
        Assert.AreEqual(3, resp.drops);
    }

    [Test]
    public void CraftRequest_Serializes()
    {
        var req = new CraftRequest { gridX = 2, gridY = -1 };
        string json = JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("2"));
    }

    [Test]
    public void QuestRequest_Serializes()
    {
        var req = new QuestRequest { questName = "SwampForage" };
        string json = JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("SwampForage"));
    }

    [Test]
    public void ServerMallum_DeserializesRewards()
    {
        string json = @"{""id"":1,""state"":""quest_complete"",""assignedQuestName"":""SwampForage"",""pendingRewards"":[{""seed_name"":""Basil"",""count"":2}]}";
        var mallum = JsonUtility.FromJson<ServerMallum>(json);
        Assert.AreEqual("quest_complete", mallum.state);
        Assert.AreEqual(1, mallum.pendingRewards.Count);
    }
}
```

Run Unity tests to verify.

**Commit:**
```bash
git add Assets/Tests/EditMode/TestGameService.cs
git commit -m "test(client): add game service DTO serialization tests"
```

---

## Task 18: Scene Wiring & Docs

**Files:**
- Modify: `Assets/Scenes/Garden.unity` — Add GameService component to "--- Social ---" GameObject
- Modify: `docs/plans/backend-migration-todo.md` — Mark Tiers 2 & 3 complete

**Step 1: Add GameService to scene** (via Unity MCP or manual YAML edit)

**Step 2: Update todo**

Mark items 5-13 as complete in `docs/plans/backend-migration-todo.md`.

**Step 3: Final verification**

Run: `cd server && mix test` — All server tests pass
Run Unity EditMode tests — All client tests pass

**Commit:**
```bash
git add Assets/Scenes/Garden.unity docs/plans/backend-migration-todo.md
git commit -m "feat: complete tiers 2-3 game systems migration"
```

---

## Execution Notes

**Task ordering:** Tasks 1-3 are foundational and must go first. Tasks 4-8 (game contexts) depend on Task 1-2 schemas but are mostly independent of each other (except Plots.water depends on Vases). Task 9 (weather) depends on Tasks 4-5. Task 10 (controller) depends on all contexts. Tasks 11-14 are independent fixes. Tasks 15-17 (client) depend on server being complete.

**Recommended batching for subagent execution:**
- Batch 1: Tasks 1-3 (migration, schemas, GrowthRecipe)
- Batch 2: Tasks 4-8 (all game contexts)
- Batch 3: Tasks 9-11 (controller, gift validation, app setup)
- Batch 4: Task 12 (server tests)
- Batch 5: Tasks 13-14 (schema update, deps)
- Batch 6: Tasks 15-17 (client)
- Batch 7: Task 18 (scene, docs, verification)
