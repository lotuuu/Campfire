defmodule CampFire.Game.PotionsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Plots, GrowthRecipe, PlayerPlot}
  alias CampFire.Economy

  @standard_weather %{
    "temperature" => 20.0,
    "wind_speed" => 5.0,
    "humidity" => 50.0,
    "cloud_cover" => 30.0,
    "is_raining" => false,
    "moon_phase" => 0.0
  }

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
    {:ok, plot} = Plots.build_plot(player.uid, elem(pos, 0), elem(pos, 1))
    {:ok, plot} = Plots.plant(player.uid, plot.id, "basil")
    {player, plot}
  end

  describe "apply_potion/3" do
    test "applies a hot potion to a growing plot" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 1)

      {:ok, updated} = Plots.apply_potion(player.uid, plot.id, "hot_potion")

      assert length(updated.potions) == 1
      [potion] = updated.potions
      assert potion["type"] == "hot"
      assert potion["value"] == 15

      # Item was consumed
      inventory = Economy.list_inventory(player.uid)
      hot = Enum.find(inventory, &(&1.item_key == "hot_potion"))
      assert hot == nil or hot.count == 0
    end

    test "rejects unknown potion" do
      {player, plot} = setup_growing_plot()

      assert {:error, :unknown_potion} = Plots.apply_potion(player.uid, plot.id, "fake_potion")
    end

    test "rejects when plot is not growing" do
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
      Economy.upsert_item(player.uid, "cress", 10)
      Economy.upsert_item(player.uid, "hot_potion", 1)

      [pos | _] = free_positions(player.uid)
      {:ok, plot} = Plots.build_plot(player.uid, elem(pos, 0), elem(pos, 1))
      # Plot is "empty", not "growing"

      assert {:error, :not_growing} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
    end

    test "rejects when player lacks the potion item" do
      {player, plot} = setup_growing_plot()
      # Don't give the player any hot_potion

      assert {:error, :insufficient_items} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
    end

    test "stacks multiple potions" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 2)
      Economy.upsert_item(player.uid, "rain_potion", 1)

      {:ok, _} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
      {:ok, _} = Plots.apply_potion(player.uid, plot.id, "hot_potion")
      {:ok, updated} = Plots.apply_potion(player.uid, plot.id, "rain_potion")

      assert length(updated.potions) == 3
      types = Enum.map(updated.potions, & &1["type"])
      assert types == ["hot", "hot", "rain"]
    end
  end

  describe "snapshot modification" do
    test "hot potion adds 15 to temperature" do
      {_player, plot} = setup_growing_plot()
      # Plot already has potions=[] from plant. We need to add a hot potion directly.
      plot
      |> PlayerPlot.changeset(%{potions: [%{"type" => "hot", "value" => 15}]})
      |> Repo.update!()

      weather = %{@standard_weather | "temperature" => 20.0}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      temps = updated.snapshots["temperatures"]
      assert List.last(temps) == 35.0
    end

    test "calm potion clamps wind at zero" do
      {_player, plot} = setup_growing_plot()
      plot
      |> PlayerPlot.changeset(%{potions: [%{"type" => "calm", "value" => -10}]})
      |> Repo.update!()

      weather = %{@standard_weather | "wind_speed" => 3.0}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      winds = updated.snapshots["wind_speeds"]
      assert List.last(winds) == 0.0
    end

    test "sun potion sets cloud_cover to 0" do
      {_player, plot} = setup_growing_plot()
      plot
      |> PlayerPlot.changeset(%{potions: [%{"type" => "sun"}]})
      |> Repo.update!()

      weather = %{@standard_weather | "cloud_cover" => 80.0}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      clouds = updated.snapshots["cloud_covers"]
      assert List.last(clouds) == 0.0
    end

    test "rain potion forces rain_snapshots last entry to 1.0" do
      {_player, plot} = setup_growing_plot()
      plot
      |> PlayerPlot.changeset(%{potions: [%{"type" => "rain"}]})
      |> Repo.update!()

      weather = %{@standard_weather | "is_raining" => false}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      rains = updated.snapshots["rain_snapshots"]
      assert List.last(rains) == 1.0
    end

    test "impermeable potion blocks rain" do
      {_player, plot} = setup_growing_plot()
      plot
      |> PlayerPlot.changeset(%{potions: [%{"type" => "impermeable"}]})
      |> Repo.update!()

      weather = %{@standard_weather | "is_raining" => true}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      rains = updated.snapshots["rain_snapshots"]
      assert List.last(rains) == 0.0
    end

    test "two hot potions stack: temp 10 + 15 + 15 = 40" do
      {_player, plot} = setup_growing_plot()
      plot
      |> PlayerPlot.changeset(%{potions: [
        %{"type" => "hot", "value" => 15},
        %{"type" => "hot", "value" => 15}
      ]})
      |> Repo.update!()

      weather = %{@standard_weather | "temperature" => 10.0}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      temps = updated.snapshots["temperatures"]
      assert List.last(temps) == 40.0
    end
  end

  describe "moon potion" do
    test "sets moon_phase from seed config recipe's ideal_min" do
      seed_items()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      seed_plot_config()

      # Seed configs with a custom moon recipe for basil
      resolve_id = fn key ->
        Repo.get_by!(CampFire.Game.Item, item_key: key).id
      end

      configs = %{
        "basil" => %{
          item_id: resolve_id.("basil_seed"),
          item_key: "basil_seed",
          harvest_item_id: resolve_id.("basil"),
          harvest_item_key: "basil",
          growth_duration_hours: 2.0,
          min_drops: 1,
          max_drops: 4,
          tier: 1,
          recipe: %{
            "moon" => %{
              "enabled" => true,
              "ideal_min" => 4.0,
              "ideal_max" => 4.0,
              "tolerance" => 0,
              "weight" => 3.0
            }
          }
        },
        "sprouts" => %{
          item_id: resolve_id.("sprouts_seed"),
          item_key: "sprouts_seed",
          harvest_item_id: resolve_id.("sprouts"),
          harvest_item_key: "sprouts",
          growth_duration_hours: 0.5,
          min_drops: 1,
          max_drops: 3,
          tier: 0,
          recipe: %{}
        }
      }

      :ets.insert(:config_cache, {"seed_configs", configs})
      by_item_id = Map.new(configs, fn {_k, v} -> {v.item_id, v} end)
      :ets.insert(:config_cache, {"seed_configs_by_item_id", by_item_id})

      items = Repo.all(CampFire.Game.Item)
      item_key_to_id = Map.new(items, fn i -> {i.item_key, i.id} end)
      item_id_to_key = Map.new(items, fn i -> {i.id, i.item_key} end)
      :ets.insert(:config_cache, {"item_key_to_id", item_key_to_id})
      :ets.insert(:config_cache, {"item_id_to_key", item_id_to_key})

      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "sprouts", 10)
      Economy.upsert_item(player.uid, "basil", 10)
      Economy.upsert_item(player.uid, "basil_seed", 5)
      Economy.upsert_item(player.uid, "cress", 10)

      [pos | _] = free_positions(player.uid)
      {:ok, plot} = Plots.build_plot(player.uid, elem(pos, 0), elem(pos, 1))
      {:ok, plot} = Plots.plant(player.uid, plot.id, "basil")

      # Add moon potion directly
      plot
      |> PlayerPlot.changeset(%{potions: [%{"type" => "moon"}]})
      |> Repo.update!()

      weather = %{@standard_weather | "moon_phase" => 1.0}
      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      moon_phases = updated.snapshots["moon_phase_snapshots"]
      assert List.last(moon_phases) == 4.0
    end
  end

  describe "rain scoring regression" do
    test "rain fraction scoring with GrowthRecipe.evaluate" do
      rain_recipe = %{
        "rain" => %{
          "enabled" => true,
          "ideal_min" => 0.4,
          "ideal_max" => 0.6,
          "tolerance" => 0.0,
          "weight" => 1.0
        }
      }

      # [1.0, 0.0, 1.0, 0.0] → fraction = 0.5 → within [0.4, 0.6] → score 1.0
      snapshots_half = %{
        "rain_snapshots" => [1.0, 0.0, 1.0, 0.0],
        "snapshot_count" => 4,
        "temperatures" => [],
        "wind_speeds" => [],
        "humidities" => [],
        "cloud_covers" => [],
        "moon_phase_snapshots" => []
      }

      score_half = GrowthRecipe.evaluate(rain_recipe, snapshots_half, 0)
      assert score_half == 1.0

      # All rain [1.0, 1.0, 1.0, 1.0] → fraction = 1.0 → outside [0.4, 0.6] with tolerance 0 → score 0.0
      snapshots_all = %{
        "rain_snapshots" => [1.0, 1.0, 1.0, 1.0],
        "snapshot_count" => 4,
        "temperatures" => [],
        "wind_speeds" => [],
        "humidities" => [],
        "cloud_covers" => [],
        "moon_phase_snapshots" => []
      }

      score_all = GrowthRecipe.evaluate(rain_recipe, snapshots_all, 0)
      assert score_all == 0.0
    end
  end

  describe "harvest reset" do
    test "potions are cleared after harvest" do
      {player, plot} = setup_growing_plot()
      Economy.upsert_item(player.uid, "hot_potion", 1)

      {:ok, _} = Plots.apply_potion(player.uid, plot.id, "hot_potion")

      # Force mature so we can harvest
      {:ok, _} = Plots.force_mature(plot.id)
      {:ok, _result} = Plots.harvest(player.uid, plot.id)

      updated = Repo.get!(PlayerPlot, plot.id)
      assert updated.potions == []
      assert updated.state == "empty"
    end
  end
end
