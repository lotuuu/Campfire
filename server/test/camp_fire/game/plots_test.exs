defmodule CampFire.Game.PlotsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Plots, Vases, Mallums, SeedConfig, PlayerPlot}
  alias CampFire.Economy

  @basil_recipe %{
    "heat" => %{
      "enabled" => true,
      "ideal_min" => 20.0,
      "ideal_max" => 30.0,
      "tolerance" => 10.0,
      "weight" => 1.0
    }
  }

  defp setup_player(_context \\ %{}) do
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    # Insert a fast-growing seed config for testing
    %SeedConfig{}
    |> SeedConfig.changeset(%{
      seed_name: "Basil",
      growth_duration_hours: 0.001,
      base_drops: 2,
      recipe: @basil_recipe
    })
    |> Repo.insert!()

    # Give player some Basil seeds
    {:ok, _} = Economy.upsert_seed(player.uid, "Basil", 5)

    player
  end

  describe "craft_plot/3" do
    test "creates empty plot and deducts mana" do
      player = setup_player()

      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)

      assert plot.state == "empty"
      assert plot.grid_x == 0
      assert plot.grid_y == 0
      assert plot.player_uid == player.uid

      economy = Economy.get_economy(player.uid)
      # Started with 50, plot costs 10
      assert economy.mana == 40.0
    end

    test "fails with insufficient mana" do
      player = setup_player()
      # Drain mana first
      {:ok, _} = Economy.spend_mana(player.uid, 50.0)

      {:error, :insufficient_mana} = Plots.craft_plot(player.uid, 0, 0)
    end
  end

  describe "plant/3" do
    test "sets plot to growing with seed and initializes snapshots" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)

      {:ok, planted} = Plots.plant(player.uid, plot.id, "Basil")

      assert planted.state == "growing"
      assert planted.seed_name == "Basil"
      assert planted.water_count == 0
      assert planted.plant_time_utc != nil
      assert planted.snapshots["snapshot_count"] == 0
    end

    test "fails on non-empty plot" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      {:error, :plot_not_empty} = Plots.plant(player.uid, plot.id, "Basil")
    end

    test "fails with no seeds" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)

      {:error, :insufficient_seeds} = Plots.plant(player.uid, plot.id, "Rosemary")
    end
  end

  describe "water/3" do
    test "increments water_count" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      # Create a mallum so we can create a vase and fill it
      {:ok, _mallum} = Mallums.create_mallum(player.uid)
      {:ok, vase} = Vases.craft_vase(player.uid, 1, 0)
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, watered} = Plots.water(player.uid, plot.id, vase.id)

      assert watered.water_count == 1
      assert watered.last_watered_utc != nil
    end

    test "fails during cooldown" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      {:ok, _mallum} = Mallums.create_mallum(player.uid)
      {:ok, vase} = Vases.craft_vase(player.uid, 1, 0)
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, _} = Plots.water(player.uid, plot.id, vase.id)

      # Second water immediately should fail
      {:error, :water_cooldown} = Plots.water(player.uid, plot.id, vase.id)
    end
  end

  describe "harvest/2" do
    test "on mature plot returns drops" do
      player = setup_player()

      # Use a seed with no recipe axes — evaluates to score 1.0 (vacuous truth)
      %SeedConfig{}
      |> SeedConfig.changeset(%{
        seed_name: "HarvestTest",
        growth_duration_hours: 0.001,
        base_drops: 4,
        recipe: %{}
      })
      |> Repo.insert!()

      {:ok, _} = Economy.upsert_seed(player.uid, "HarvestTest", 1)
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "HarvestTest")

      # Force the plot to mature
      {:ok, _} = Plots.force_mature(plot.id)

      {:ok, result} = Plots.harvest(player.uid, plot.id)

      assert result.item_name == "HarvestTest_harvest"
      assert result.drops == 4
      assert result.score == 1.0
    end

    test "resets plot to empty after harvest" do
      player = setup_player()

      # Use a seed with no recipe axes so harvest works without snapshots
      %SeedConfig{}
      |> SeedConfig.changeset(%{
        seed_name: "SimpleSeed",
        growth_duration_hours: 0.001,
        base_drops: 3,
        recipe: %{}
      })
      |> Repo.insert!()

      {:ok, _} = Economy.upsert_seed(player.uid, "SimpleSeed", 1)
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "SimpleSeed")
      {:ok, _} = Plots.force_mature(plot.id)

      {:ok, _} = Plots.harvest(player.uid, plot.id)

      updated = Repo.get!(PlayerPlot, plot.id)
      assert updated.state == "empty"
      assert updated.seed_name == nil
      assert updated.water_count == 0
    end

    test "fails when not mature" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      {:error, :not_mature} = Plots.harvest(player.uid, plot.id)
    end
  end

  describe "check_maturity/1" do
    test "transitions growing to mature when time elapsed" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, planted} = Plots.plant(player.uid, plot.id, "Basil")

      # Set plant_time_utc far in the past (growth_duration_hours is 0.001 = 3.6 seconds)
      past = DateTime.add(DateTime.utc_now(), -3600, :second) |> DateTime.truncate(:second)

      planted
      |> Ecto.Changeset.change(plant_time_utc: past)
      |> Repo.update!()

      {:ok, matured} = Plots.check_maturity(plot.id)
      assert matured.state == "mature"
    end

    test "does not transition if time has not elapsed" do
      player = setup_player()

      # Insert a slow seed config
      %SeedConfig{}
      |> SeedConfig.changeset(%{
        seed_name: "SlowPlant",
        growth_duration_hours: 9999.0,
        base_drops: 1,
        recipe: %{}
      })
      |> Repo.insert!()

      {:ok, _} = Economy.upsert_seed(player.uid, "SlowPlant", 1)
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "SlowPlant")

      {:ok, still_growing} = Plots.check_maturity(plot.id)
      assert still_growing.state == "growing"
    end
  end

  describe "record_snapshot/2" do
    test "appends to snapshot arrays" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      weather = %{
        "temperature" => 25.0,
        "wind_speed" => 5.0,
        "humidity" => 60.0,
        "cloud_cover" => 40.0,
        "is_raining" => false,
        "moon_phase" => 0.5
      }

      {:ok, updated} = Plots.record_snapshot(plot.id, weather)

      assert updated.snapshots["snapshot_count"] == 1
      assert updated.snapshots["temperatures"] == [25.0]
      assert updated.snapshots["wind_speeds"] == [5.0]
      assert updated.snapshots["humidities"] == [60.0]
      assert updated.snapshots["rain_snapshots"] == [0.0]
    end
  end

  describe "set_skin/3" do
    test "fails for locked skin" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 0, 0)

      {:error, :skin_not_unlocked} = Plots.set_skin(player.uid, plot.id, "fancy_skin")
    end
  end
end
