# Weather Potions Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow players to apply weather-modifying potions to growing plots, altering the weather data recorded in snapshots for the rest of the growth cycle.

**Architecture:** Potions are stored as a JSONB array on `player_plots`. When the WeatherPoller records a snapshot, it applies the plot's potions to the raw weather data before recording. GrowthRecipe evaluation is untouched — it sees already-modified snapshots.

**Tech Stack:** Elixir/Phoenix (server), Unity C# (client), Postgres JSONB, NUnit (client tests), ExUnit (server tests)

**Spec:** `docs/superpowers/specs/2026-03-16-weather-potions-design.md`

---

## Chunk 1: Prerequisite Rain Scoring Fix

### Task 1: Fix rain scoring bug in server GrowthRecipe

**Files:**
- Modify: `server/lib/camp_fire/game/growth_recipe.ex`

- [ ] **Step 1: Fix `build_axes` rain calculation**

In `server/lib/camp_fire/game/growth_recipe.ex`, find the `build_axes` rain section (~line 166-173):

```elixir
    axes = maybe_add_axis(axes, recipe, "rain", fn ->
      if count == 0 do
        0.0
      else
        rain_count = length(Map.get(snapshots, "rain_snapshots", []))
        rain_count / count
      end
    end)
```

Replace `length(...)` with `Enum.sum(...)`:

```elixir
    axes = maybe_add_axis(axes, recipe, "rain", fn ->
      if count == 0 do
        0.0
      else
        rain_sum = Enum.sum(Map.get(snapshots, "rain_snapshots", []))
        rain_sum / count
      end
    end)
```

- [ ] **Step 2: Fix `evaluate_per_axis` rain calculation**

In the same file, find the `evaluate_per_axis` rain section (~line 83-86):

```elixir
      {"rain", fn ->
        if count == 0, do: 0.0,
        else: length(Map.get(snapshots, "rain_snapshots", [])) / count
      end},
```

Replace with:

```elixir
      {"rain", fn ->
        if count == 0, do: 0.0,
        else: Enum.sum(Map.get(snapshots, "rain_snapshots", [])) / count
      end},
```

- [ ] **Step 3: Run server tests**

Run: `cd /Users/lotu/game/Garden/server && mix test`
Expected: All existing tests pass (rain axis was always 1.0 before, so no test was relying on correct values).

- [ ] **Step 4: Commit**

```bash
git add server/lib/camp_fire/game/growth_recipe.ex
git commit -m "fix(recipe): use Enum.sum instead of length for rain scoring

length(rain_snapshots) always equaled snapshot_count since every
snapshot appends a value. Enum.sum correctly counts only rainy snapshots."
```

---

## Chunk 2: Data Model — Migration, Schema, Items

### Task 2: Add potions column to player_plots

**Files:**
- Create: `server/priv/repo/migrations/TIMESTAMP_add_potions_to_player_plots.exs`
- Modify: `server/lib/camp_fire/game/player_plot.ex`

- [ ] **Step 1: Create migration**

Create `server/priv/repo/migrations/20260316130000_add_potions_to_player_plots.exs`:

```elixir
defmodule CampFire.Repo.Migrations.AddPotionsToPlayerPlots do
  use Ecto.Migration

  def change do
    alter table(:player_plots) do
      add :potions, :map, default: fragment("'[]'::jsonb"), null: false
    end
  end
end
```

- [ ] **Step 2: Update PlayerPlot schema**

In `server/lib/camp_fire/game/player_plot.ex`, add to the schema block (after line 21):

```elixir
    field :potions, {:array, :map}, default: []
```

Add `:potions` to the changeset cast list (line 28). The updated cast list:

```elixir
    |> cast(attrs, [
      :player_uid, :seed_item_id, :state, :plant_time_utc, :water_count,
      :last_watered_utc, :snapshots, :grid_x, :grid_y, :skin_name, :unlocked_skins,
      :fertilized, :potions
    ])
```

- [ ] **Step 3: Add potions reset in plant and harvest**

In `server/lib/camp_fire/game/plots.ex`, in the `plant` function (~line 104-112), add `potions: []` to the planting changeset to ensure no stale potions from a previous cycle:

```elixir
            planted =
              plot
              |> PlayerPlot.changeset(%{
                seed_item_id: seed_config.item_id,
                state: "growing",
                plant_time_utc: now,
                water_count: 0,
                last_watered_utc: nil,
                snapshots: @empty_snapshots,
                potions: []
              })
              |> Repo.update!()
```

Also in the `harvest` function (~line 220-228), add `potions: []` to the reset changeset:

```elixir
        plot
        |> PlayerPlot.changeset(%{
          state: "empty",
          seed_item_id: nil,
          plant_time_utc: nil,
          water_count: 0,
          last_watered_utc: nil,
          snapshots: %{},
          fertilized: false,
          potions: []
        })
        |> Repo.update!()
```

- [ ] **Step 4: Run migration**

Run: `cd /Users/lotu/game/Garden/server && mix ecto.migrate`
Expected: Migration succeeds.

- [ ] **Step 5: Run tests**

Run: `cd /Users/lotu/game/Garden/server && mix test`
Expected: All existing tests pass.

- [ ] **Step 6: Commit**

```bash
git add server/priv/repo/migrations/20260316130000_add_potions_to_player_plots.exs \
       server/lib/camp_fire/game/player_plot.ex \
       server/lib/camp_fire/game/plots.ex
git commit -m "feat(potions): add potions JSONB column to player_plots

Stores applied weather potions as a JSONB array. Reset to [] on harvest."
```

### Task 3: Add potion items to seeds and test helpers

**Files:**
- Modify: `server/priv/repo/seeds.exs`
- Modify: `server/test/support/test_helpers.ex`

- [ ] **Step 1: Add potion items to seeds.exs**

In `server/priv/repo/seeds.exs`, in the `items` list (~line 36-40), add the weather potion items alongside the existing potions/consumables:

```elixir
    %{item_key: "hot_potion", display_name: "Hot Potion", category: "potion"},
    %{item_key: "cool_potion", display_name: "Cool Potion", category: "potion"},
    %{item_key: "wind_potion", display_name: "Wind Potion", category: "potion"},
    %{item_key: "calm_potion", display_name: "Calm Potion", category: "potion"},
    %{item_key: "humid_potion", display_name: "Humid Potion", category: "potion"},
    %{item_key: "dry_potion", display_name: "Dry Potion", category: "potion"},
    %{item_key: "sun_potion", display_name: "Sun Potion", category: "potion"},
    %{item_key: "shadow_potion", display_name: "Shadow Potion", category: "potion"},
    %{item_key: "rain_potion", display_name: "Rain Potion", category: "potion"},
    %{item_key: "impermeable_potion", display_name: "Impermeable Potion", category: "potion"},
    %{item_key: "moon_potion", display_name: "Moon Potion", category: "potion"},
```

- [ ] **Step 2: Add potion items to test helpers**

In `server/test/support/test_helpers.ex`, in the `seed_items` function, add the same 11 items to the items list (~line 33-35, alongside speed_potion/fertilizer/energy_drink):

```elixir
        %{item_key: "hot_potion", display_name: "Hot Potion", category: "potion"},
        %{item_key: "cool_potion", display_name: "Cool Potion", category: "potion"},
        %{item_key: "wind_potion", display_name: "Wind Potion", category: "potion"},
        %{item_key: "calm_potion", display_name: "Calm Potion", category: "potion"},
        %{item_key: "humid_potion", display_name: "Humid Potion", category: "potion"},
        %{item_key: "dry_potion", display_name: "Dry Potion", category: "potion"},
        %{item_key: "sun_potion", display_name: "Sun Potion", category: "potion"},
        %{item_key: "shadow_potion", display_name: "Shadow Potion", category: "potion"},
        %{item_key: "rain_potion", display_name: "Rain Potion", category: "potion"},
        %{item_key: "impermeable_potion", display_name: "Impermeable Potion", category: "potion"},
        %{item_key: "moon_potion", display_name: "Moon Potion", category: "potion"},
```

- [ ] **Step 3: Add plot_config to test helpers**

Add a `seed_plot_config` helper to `test_helpers.ex` (needed for snapshot recording tests, since `record_snapshot` is called during potion tests and `speed_item` reads from `plot_config`):

```elixir
  def seed_plot_config do
    config = %{
      "water_cooldown_seconds" => 7200,
      "rain_water_cooldown_seconds" => 21600,
      "rain_trigger_minutes" => 15,
      "drop_spread_factor" => 0.3,
      "speed_item" => "speed_potion"
    }

    :ets.insert(:config_cache, {"plot_config", config})
  end
```

- [ ] **Step 4: Run tests**

Run: `cd /Users/lotu/game/Garden/server && mix test`
Expected: All existing tests pass.

- [ ] **Step 5: Commit**

```bash
git add server/priv/repo/seeds.exs server/test/support/test_helpers.ex
git commit -m "feat(potions): add 11 weather potion items to seeds and test helpers"
```

---

## Chunk 3: Server Logic — apply_potion and snapshot modifier

### Task 4: Implement apply_potion and apply_potions in Plots

**Files:**
- Modify: `server/lib/camp_fire/game/plots.ex`

- [ ] **Step 1: Add potion type mapping**

At the top of `server/lib/camp_fire/game/plots.ex` (after the `@empty_snapshots` module attribute, ~line 21):

```elixir
  @potion_types %{
    "hot_potion" => %{"type" => "hot", "value" => 15},
    "cool_potion" => %{"type" => "cool", "value" => -15},
    "wind_potion" => %{"type" => "wind", "value" => 10},
    "calm_potion" => %{"type" => "calm", "value" => -10},
    "humid_potion" => %{"type" => "humid", "value" => 30},
    "dry_potion" => %{"type" => "dry", "value" => -30},
    "sun_potion" => %{"type" => "sun"},
    "shadow_potion" => %{"type" => "shadow"},
    "rain_potion" => %{"type" => "rain"},
    "impermeable_potion" => %{"type" => "impermeable"},
    "moon_potion" => %{"type" => "moon"}
  }
```

- [ ] **Step 2: Implement apply_potion function**

Add after the `fertilize` function (~line 191), before the harvest section:

```elixir
  # --- Apply Potion ---

  def apply_potion(player_uid, plot_id, potion_item_key) do
    case Map.get(@potion_types, potion_item_key) do
      nil ->
        {:error, :unknown_potion}

      potion_entry ->
        with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
             true <- plot.player_uid == player_uid || {:error, :not_owned},
             true <- plot.state == "growing" || {:error, :not_growing},
             {:ok, _} <- Economy.spend_item(player_uid, potion_item_key, 1) do
          updated_potions = (plot.potions || []) ++ [potion_entry]

          plot
          |> PlayerPlot.changeset(%{potions: updated_potions})
          |> Repo.update()
        else
          nil -> {:error, :not_found}
          {:error, _} = err -> err
        end
    end
  end
```

- [ ] **Step 3: Implement apply_potions helper for snapshot modification**

Add as a private function before the snapshot recording section (~line 296):

```elixir
  # --- Potion Weather Modifier ---

  defp apply_potions(weather_data, [], _plot), do: weather_data

  defp apply_potions(weather_data, potions, plot) do
    Enum.reduce(potions, weather_data, fn potion, wd ->
      case potion["type"] do
        "hot" ->
          Map.update(wd, "temperature", 0.0, &(&1 + potion["value"]))

        "cool" ->
          Map.update(wd, "temperature", 0.0, &(&1 + potion["value"]))

        "wind" ->
          Map.update(wd, "wind_speed", 0.0, &max(&1 + potion["value"], 0.0))

        "calm" ->
          Map.update(wd, "wind_speed", 0.0, &max(&1 + potion["value"], 0.0))

        "humid" ->
          Map.update(wd, "humidity", 0.0, &min(max(&1 + potion["value"], 0.0), 100.0))

        "dry" ->
          Map.update(wd, "humidity", 0.0, &min(max(&1 + potion["value"], 0.0), 100.0))

        "sun" ->
          Map.put(wd, "cloud_cover", 0.0)

        "shadow" ->
          Map.put(wd, "cloud_cover", 100.0)

        "rain" ->
          Map.put(wd, "is_raining", true)

        "impermeable" ->
          Map.put(wd, "is_raining", false)

        "moon" ->
          seed_config = CampFire.Game.get_seed_config_by_item_id!(plot.seed_item_id)
          moon_axis = get_in(seed_config.recipe, ["moon"])

          if is_map(moon_axis) and moon_axis["enabled"] do
            Map.put(wd, "moon_phase", moon_axis["ideal_min"])
          else
            wd
          end

        _ ->
          wd
      end
    end)
  end
```

- [ ] **Step 4: Wire apply_potions into record_snapshot**

Modify `record_snapshot/2` (~line 309-335) to apply potions before recording. Replace the function body:

```elixir
  def record_snapshot(plot_id, weather_data) do
    case Repo.get(PlayerPlot, plot_id) do
      nil ->
        {:error, :not_found}

      plot ->
        if plot.state == "growing" do
          effective_weather = apply_potions(weather_data, plot.potions || [], plot)
          snapshots = plot.snapshots || @empty_snapshots

          updated_snapshots = %{
            "temperatures" => (snapshots["temperatures"] || []) ++ [effective_weather["temperature"] || 0.0],
            "wind_speeds" => (snapshots["wind_speeds"] || []) ++ [effective_weather["wind_speed"] || 0.0],
            "humidities" => (snapshots["humidities"] || []) ++ [effective_weather["humidity"] || 0.0],
            "cloud_covers" => (snapshots["cloud_covers"] || []) ++ [effective_weather["cloud_cover"] || 0.0],
            "rain_snapshots" => (snapshots["rain_snapshots"] || []) ++ [if(effective_weather["is_raining"], do: 1.0, else: 0.0)],
            "moon_phase_snapshots" => (snapshots["moon_phase_snapshots"] || []) ++ [effective_weather["moon_phase"] || 0.0],
            "snapshot_count" => (snapshots["snapshot_count"] || 0) + 1
          }

          plot
          |> PlayerPlot.changeset(%{snapshots: updated_snapshots})
          |> Repo.update()
        else
          {:ok, plot}
        end
    end
  end
```

- [ ] **Step 5: Commit**

```bash
git add server/lib/camp_fire/game/plots.ex
git commit -m "feat(potions): implement apply_potion and snapshot weather modifier

apply_potion spends the potion item and appends to the plot's potions array.
record_snapshot applies potions to weather data before recording into snapshots."
```

### Task 5: Add route and controller action

**Files:**
- Modify: `server/lib/camp_fire_web/router.ex`
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex`

- [ ] **Step 1: Add route**

In `server/lib/camp_fire_web/router.ex`, add after the `post "/plot/fertilize"` line (~line 138):

```elixir
    post "/plot/apply-potion", GameController, :apply_potion
```

- [ ] **Step 2: Add controller action**

In `server/lib/camp_fire_web/controllers/game_controller.ex`, add after the `fertilize_plot` functions (~line 433):

```elixir
  def apply_potion(conn, %{"plotId" => plot_id, "potionItemKey" => potion_item_key}) do
    uid = conn.assigns.current_player.uid

    case Plots.apply_potion(uid, plot_id, potion_item_key) do
      {:ok, plot} ->
        conn |> put_status(200) |> json(serialize_plot(plot))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def apply_potion(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'plotId' and 'potionItemKey'"})
  end
```

- [ ] **Step 3: Add potions to serialize_plot**

In `serialize_plot` (~line 807-828), add `potionItemKeys` to the response map. Add after `fertilized: plot.fertilized`:

```elixir
      potionItemKeys: Enum.map(plot.potions || [], fn p ->
        Enum.find_value(@potion_types_reverse, fn {key, type} ->
          if type == p["type"], do: key
        end) || p["type"]
      end)
```

Wait — `serialize_plot` is in the controller but `@potion_types` is in `Plots.ex`. Simpler approach: add a reverse lookup function to `Plots.ex` and call it from the serializer.

Actually, even simpler — add `@potion_types_reverse` as a module attribute in `GameController.ex`:

```elixir
  @potion_type_to_item_key %{
    "hot" => "hot_potion",
    "cool" => "cool_potion",
    "wind" => "wind_potion",
    "calm" => "calm_potion",
    "humid" => "humid_potion",
    "dry" => "dry_potion",
    "sun" => "sun_potion",
    "shadow" => "shadow_potion",
    "rain" => "rain_potion",
    "impermeable" => "impermeable_potion",
    "moon" => "moon_potion"
  }
```

Add this at the top of `GameController` (module level). Then in `serialize_plot`, add:

```elixir
      potionItemKeys: Enum.map(plot.potions || [], fn p ->
        Map.get(@potion_type_to_item_key, p["type"], p["type"])
      end)
```

- [ ] **Step 4: Run tests**

Run: `cd /Users/lotu/game/Garden/server && mix test`
Expected: All existing tests pass.

- [ ] **Step 5: Commit**

```bash
git add server/lib/camp_fire_web/router.ex \
       server/lib/camp_fire_web/controllers/game_controller.ex
git commit -m "feat(potions): add POST /game/plot/apply-potion endpoint

Returns serialized plot with potionItemKeys for client display."
```

### Task 6: Write server tests for potions

**Files:**
- Create: `server/test/camp_fire/game/potions_test.exs`

- [ ] **Step 1: Write test file**

Create `server/test/camp_fire/game/potions_test.exs`:

```elixir
defmodule CampFire.Game.PotionsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Plots, PlayerPlot}
  alias CampFire.Economy

  defp setup_growing_plot do
    seed_items()
    seed_flame_config()
    seed_building_costs()
    seed_seed_configs()
    seed_mallum_house_config()
    seed_new_player_config()
    seed_plot_config()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)

    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()

    Economy.upsert_item(player.uid, "sprouts", 10)
    Economy.upsert_item(player.uid, "basil", 10)
    Economy.upsert_item(player.uid, "basil_seed", 5)
    Economy.upsert_item(player.uid, "cress", 10)

    [pos | _] = free_positions(player.uid)
    {:ok, plot} = Plots.craft_plot(player.uid, elem(pos, 0), elem(pos, 1))
    {:ok, plot} = Plots.plant(player.uid, plot.id, "basil")

    {player, plot}
  end

  describe "apply_potion/3" do
    test "applies a hot potion to a growing plot" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 1)

      {:ok, updated} = Plots.apply_potion(player.uid, plot.id, "hot_potion")

      assert length(updated.potions) == 1
      assert hd(updated.potions)["type"] == "hot"
      assert hd(updated.potions)["value"] == 15

      # Item was consumed
      inv = Economy.list_inventory(player.uid)
      hot = Enum.find(inv, &(&1.item_key == "hot_potion"))
      assert hot == nil or hot.count == 0
    end

    test "rejects unknown potion" do
      {player, plot} = setup_growing_plot()

      assert {:error, :unknown_potion} = Plots.apply_potion(player.uid, plot.id, "fake_potion")
    end

    test "rejects when plot is not growing" do
      {player, _plot} = setup_growing_plot()
      # Get the starter plot (which is empty)
      starter = Plots.list_plots(player.uid) |> Enum.find(&(&1.state == "empty"))
      Economy.upsert_item(player.uid, "hot_potion", 1)

      assert {:error, :not_growing} = Plots.apply_potion(player.uid, starter.id, "hot_potion")
    end

    test "rejects when player lacks the potion item" do
      {player, plot} = setup_growing_plot()

      assert {:error, _} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
    end

    test "stacks multiple potions" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 2)
      Economy.upsert_item(player.uid, "rain_potion", 1)

      {:ok, p1} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
      {:ok, p2} = Plots.apply_potion(player.uid, p1.id, "hot_potion")
      {:ok, p3} = Plots.apply_potion(player.uid, p2.id, "rain_potion")

      assert length(p3.potions) == 3
    end
  end

  describe "apply_potions in record_snapshot" do
    test "hot potion adds to temperature" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "hot_potion")

      weather = %{"temperature" => 20.0, "wind_speed" => 5.0, "humidity" => 50.0,
                  "cloud_cover" => 30.0, "is_raining" => false, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      # Temperature should be 20 + 15 = 35
      temps = updated.snapshots["temperatures"]
      # Last entry is the one we just recorded (first was the initial snapshot at plant time)
      assert List.last(temps) == 35.0
    end

    test "wind potion clamps at zero" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "calm_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "calm_potion")

      weather = %{"temperature" => 20.0, "wind_speed" => 3.0, "humidity" => 50.0,
                  "cloud_cover" => 30.0, "is_raining" => false, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      # 3 - 10 = -7, clamped to 0
      assert List.last(updated.snapshots["wind_speeds"]) == 0.0
    end

    test "sun potion sets cloud cover to 0" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "sun_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "sun_potion")

      weather = %{"temperature" => 20.0, "wind_speed" => 5.0, "humidity" => 50.0,
                  "cloud_cover" => 80.0, "is_raining" => false, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      assert List.last(updated.snapshots["cloud_covers"]) == 0.0
    end

    test "rain potion forces rain snapshot" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "rain_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "rain_potion")

      weather = %{"temperature" => 20.0, "wind_speed" => 5.0, "humidity" => 50.0,
                  "cloud_cover" => 30.0, "is_raining" => false, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      assert List.last(updated.snapshots["rain_snapshots"]) == 1.0
    end

    test "impermeable potion blocks rain" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "impermeable_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "impermeable_potion")

      weather = %{"temperature" => 20.0, "wind_speed" => 5.0, "humidity" => 50.0,
                  "cloud_cover" => 30.0, "is_raining" => true, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      assert List.last(updated.snapshots["rain_snapshots"]) == 0.0
    end

    test "two hot potions stack additively" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 2)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "hot_potion")

      weather = %{"temperature" => 10.0, "wind_speed" => 5.0, "humidity" => 50.0,
                  "cloud_cover" => 30.0, "is_raining" => false, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      # 10 + 15 + 15 = 40
      assert List.last(updated.snapshots["temperatures"]) == 40.0
    end
  end

  describe "moon potion resolves required phase" do
    test "moon potion sets moon_phase from seed config recipe" do
      # Use a seed config with a moon recipe axis
      seed_items()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      seed_plot_config()

      # Seed a custom seed config with moon axis enabled
      resolve_id = fn key ->
        case Repo.get_by(CampFire.Game.Item, item_key: key) do
          nil -> raise "Item #{key} not found"
          item -> item.id
        end
      end

      moon_config = %{
        "harvesttest" => %{
          item_id: resolve_id.("harvesttest_seed"),
          item_key: "harvesttest_seed",
          harvest_item_id: resolve_id.("harvesttest"),
          harvest_item_key: "harvesttest",
          growth_duration_hours: 0.001,
          min_drops: 2,
          max_drops: 8,
          tier: 0,
          recipe: %{
            "moon" => %{"enabled" => true, "ideal_min" => 4.0, "ideal_max" => 4.0, "tolerance" => 0, "weight" => 3.0}
          }
        }
      }

      :ets.insert(:config_cache, {"seed_configs", moon_config})
      by_item_id = Map.new(moon_config, fn {_k, v} -> {v.item_id, v} end)
      :ets.insert(:config_cache, {"seed_configs_by_item_id", by_item_id})
      items = Repo.all(CampFire.Game.Item)
      :ets.insert(:config_cache, {"item_key_to_id", Map.new(items, fn i -> {i.item_key, i.id} end)})
      :ets.insert(:config_cache, {"item_id_to_key", Map.new(items, fn i -> {i.id, i.item_key} end)})

      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "sprouts", 10)
      Economy.upsert_item(player.uid, "harvesttest_seed", 5)
      Economy.upsert_item(player.uid, "cress", 10)

      [pos | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos, 0), elem(pos, 1))
      {:ok, plot} = Plots.plant(player.uid, plot.id, "harvesttest")

      Economy.upsert_item(player.uid, "moon_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "moon_potion")

      weather = %{"temperature" => 20.0, "wind_speed" => 5.0, "humidity" => 50.0,
                  "cloud_cover" => 30.0, "is_raining" => false, "moon_phase" => 0.0}

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      # Moon phase should be 4.0 (from recipe ideal_min), not 0.0
      assert List.last(updated.snapshots["moon_phase_snapshots"]) == 4.0
    end
  end

  describe "rain scoring fix regression" do
    test "rain fraction correctly reflects rainy vs non-rainy snapshots" do
      alias CampFire.Game.GrowthRecipe

      recipe = %{
        "rain" => %{"enabled" => true, "ideal_min" => 0.4, "ideal_max" => 0.6, "tolerance" => 0, "weight" => 1.0}
      }

      # 2 out of 4 snapshots are rainy = 0.5 fraction
      snapshots = %{
        "snapshot_count" => 4,
        "temperatures" => [20.0, 20.0, 20.0, 20.0],
        "wind_speeds" => [5.0, 5.0, 5.0, 5.0],
        "humidities" => [50.0, 50.0, 50.0, 50.0],
        "cloud_covers" => [30.0, 30.0, 30.0, 30.0],
        "rain_snapshots" => [1.0, 0.0, 1.0, 0.0],
        "moon_phase_snapshots" => [0.0, 0.0, 0.0, 0.0]
      }

      score = GrowthRecipe.evaluate(recipe, snapshots, 0)
      # 0.5 is within [0.4, 0.6] ideal range, so score should be 1.0
      assert score == 1.0

      # All rainy = 1.0 fraction, outside [0.4, 0.6], tolerance 0 => score 0.0
      all_rain = put_in(snapshots, ["rain_snapshots"], [1.0, 1.0, 1.0, 1.0])
      score2 = GrowthRecipe.evaluate(recipe, all_rain, 0)
      assert score2 == 0.0
    end
  end

  describe "harvest resets potions" do
    test "potions cleared after harvest" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 1)
      {:ok, plot} = Plots.apply_potion(player.uid, plot.id, "hot_potion")

      # Force mature so we can harvest
      Plots.force_mature(plot.id)

      {:ok, _result} = Plots.harvest(player.uid, plot.id)

      harvested_plot = Repo.get(PlayerPlot, plot.id)
      assert harvested_plot.potions == []
    end
  end
end
```

- [ ] **Step 2: Run the new tests**

Run: `cd /Users/lotu/game/Garden/server && mix test test/camp_fire/game/potions_test.exs --trace`
Expected: All tests pass.

- [ ] **Step 3: Run full test suite**

Run: `cd /Users/lotu/game/Garden/server && mix test`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add server/test/camp_fire/game/potions_test.exs
git commit -m "test(potions): add tests for apply_potion and snapshot modification"
```

---

## Chunk 4: Client Integration

### Task 7: Update client data models

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Scripts/Data/GameStateResponse.cs`

- [ ] **Step 1: Add potions to PlotSave**

In `Assets/Scripts/Data/SaveData.cs`, add to `PlotSave` class (after `fertilized` field, ~line 62):

```csharp
        public List<string> potions = new();
```

- [ ] **Step 2: Add potionItemKeys to ServerPlot**

In `Assets/Scripts/Data/GameStateResponse.cs`, add to `ServerPlot` class (after `fertilized` field, ~line 33):

```csharp
        public List<string> potionItemKeys = new();
```

- [ ] **Step 3: Add ApplyPotionRequest DTO**

In `Assets/Scripts/Data/GameStateResponse.cs`, add alongside the other request DTOs (~line 170):

```csharp
    [Serializable] public class ApplyPotionRequest { public int plotId; public string potionItemKey; }
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/GameStateResponse.cs
git commit -m "feat(potions): add potion fields to PlotSave and ServerPlot DTOs"
```

### Task 8: Add GameService endpoint and PlotManager method

**Files:**
- Modify: `Assets/Scripts/Services/GameService.cs`
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

- [ ] **Step 1: Add ApplyPotion to GameService**

In `Assets/Scripts/Services/GameService.cs`, add after the `FertilizePlot` method (~line 561):

```csharp
        public async Task<ServerPlot> ApplyPotion(int plotId, string potionItemKey)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new ApplyPotionRequest { plotId = plotId, potionItemKey = potionItemKey });
                using var req = PostJson("/game/plot/apply-potion", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: ApplyPotion failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: ApplyPotion failed: {e.Message}"); }
            return null;
        }
```

- [ ] **Step 2: Wire potionItemKeys into ApplyGameState**

In `GameService.cs`, in the `ApplyGameState` method where plots are deserialized (~line 240-249), add `potions` to the PlotSave initialization:

```csharp
                    potions = sp.potionItemKeys ?? new List<string>(),
```

Add this line after `fertilized = sp.fertilized`.

- [ ] **Step 3: Add ApplyPotion to PlotManager**

In `Assets/Scripts/Managers/PlotManager.cs`, add a public method (after the Fertilize method or similar consumable methods):

```csharp
        public async void ApplyPotion(int plotIndex, string potionItemKey)
        {
            if (plotIndex < 0 || plotIndex >= SaveManager.Instance.Data.plots.Count) return;
            var plot = SaveManager.Instance.Data.plots[plotIndex];
            if (plot.state != PlotState.Growing) return;
            if (plot.serverId <= 0) return;

            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                var result = await GameService.Instance.ApplyPotion(plot.serverId, potionItemKey);
                if (result == null)
                {
                    await GameService.Instance.ResyncFullState();
                    return;
                }

                plot.potions = result.potionItemKeys ?? new List<string>();
                SaveManager.Instance.Save();
                OnPlotChanged?.Invoke(plotIndex);
            }
        }
```

Note: `OnPlotChanged` must already exist as an event. Check that it does — it's used by the existing plot lifecycle. If it's a different name, match the existing pattern.

- [ ] **Step 4: Clear potions in PlotManager.Harvest local reset**

In `PlotManager.cs`, find the `Harvest` method where it resets the local plot state after a successful harvest. Add `plot.potions = new List<string>();` alongside the existing reset fields (state, seedItemKey, waterCount, etc.).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Services/GameService.cs Assets/Scripts/Managers/PlotManager.cs
git commit -m "feat(potions): add ApplyPotion endpoint and PlotManager method

Includes clearing potions list on local harvest reset."
```

### Task 9: Update interaction panel UI

**Files:**
- Modify: The UI controller that handles plot interaction (likely `Assets/Scripts/UI/CampsiteViewUI.cs` or a plot interaction panel)

- [ ] **Step 1: Identify the interaction panel**

Find where fertilize/speed buttons are shown for growing plots. This is the same panel where potion buttons should appear. Look for where `FertilizePlot` or `InstantFinishPlot` is called from in the UI code.

- [ ] **Step 2: Add potion buttons to the interaction panel**

For each weather potion the player has in inventory (category "potion", excluding speed_potion and energy_drink which are handled separately), show a button. When tapped, call `PlotManager.Instance.ApplyPotion(plotIndex, potionItemKey)`.

Show applied potions on the plot detail view as a list of potion names/icons.

Implementation details depend on the exact UI structure found in step 1. Follow the existing pattern for fertilize/speed buttons.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/...
git commit -m "feat(potions): add weather potion buttons to plot interaction panel"
```

---

## Chunk 5: Visitor Pool Updates

### Task 10: Add weather potions to visitor pools

**Files:**
- Modify: `server/priv/repo/seeds.exs`

- [ ] **Step 1: Add potions to Willow gifter's gift pool**

In `seeds.exs`, update the `willow_gifter` template's `gift_pool` (~line 139-144) to include some weather potions:

```elixir
    gift_pool: [
      %{"itemKey" => "chamomile_seed", "count" => 2},
      %{"type" => "water", "count" => 3},
      %{"itemKey" => "basil_seed", "count" => 3},
      %{"itemKey" => "basil", "count" => 2},
      %{"itemKey" => "hot_potion", "count" => 1},
      %{"itemKey" => "rain_potion", "count" => 1},
      %{"itemKey" => "sun_potion", "count" => 1}
    ],
```

- [ ] **Step 2: Add potions to Thorn merchant's offer pool**

In `seeds.exs`, update the `thorn_merchant` template's `offer_pool` (~line 95-114) to include potion trades:

```elixir
      %{
        "costs" => [%{"itemKey" => "marigold", "count" => 3}],
        "rewardItemKey" => "hot_potion",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "snowdrop", "count" => 3}],
        "rewardItemKey" => "cool_potion",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "mint", "count" => 3}],
        "rewardItemKey" => "humid_potion",
        "rewardCount" => 1
      },
```

- [ ] **Step 3: Add potions to Ember quester's quest reward pools**

In `seeds.exs`, update the `ember_quester` template's `quest_pool` rewards to include weather potions as possible rewards:

```elixir
      %{
        "request_item_key" => "lavender",
        "request_count" => 3,
        "return_days" => 7,
        "reward" => %{"itemKey" => "moon_potion", "count" => 1},
        "return_dialogue" => ["Incredible! The moonlight guided me back.", "Here, this is special."]
      },
```

- [ ] **Step 4: Commit**

```bash
git add server/priv/repo/seeds.exs
git commit -m "content(potions): add weather potions to visitor gift/trade/quest pools"
```

---

## Summary

| Task | Description | Files |
|------|-------------|-------|
| 1 | Fix rain scoring bug | `growth_recipe.ex` |
| 2 | Migration + schema + harvest reset | `migration`, `player_plot.ex`, `plots.ex` |
| 3 | Add potion items to seeds + test helpers | `seeds.exs`, `test_helpers.ex` |
| 4 | Implement apply_potion + snapshot modifier | `plots.ex` |
| 5 | Route + controller + serializer | `router.ex`, `game_controller.ex` |
| 6 | Server tests | `potions_test.exs` |
| 7 | Client data models | `SaveData.cs`, `GameStateResponse.cs` |
| 8 | GameService + PlotManager | `GameService.cs`, `PlotManager.cs` |
| 9 | UI interaction panel | UI controller files |
| 10 | Visitor pool content | `seeds.exs` |
