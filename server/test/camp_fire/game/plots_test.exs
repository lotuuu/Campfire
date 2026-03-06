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

  # init_economy creates 1 starter plot + 1 starter vase + 1 mallum house + 2 mallums
  # So the first craft_plot in tests is actually the 2nd plot (index 1):
  #   plot_costs[1] = 200 mana + 1 Basil_harvest
  # The 3rd plot (index 2) = 260 mana + 2 Basil_harvest

  defp setup_player(_context \\ %{}) do
    seed_building_costs()
    seed_flame_config()
    seed_seed_configs()
    seed_mallum_house_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    # Boost mana for crafting tests
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()

    # Insert a fast-growing seed config for testing (idempotent)
    import Ecto.Query

    unless Repo.one(from sc in SeedConfig, where: sc.seed_name == "Basil") do
      %SeedConfig{}
      |> SeedConfig.changeset(%{
        seed_name: "Basil",
        growth_duration_hours: 0.001,
        base_drops: 2,
        recipe: @basil_recipe
      })
      |> Repo.insert!()
    end

    # Give player some Basil seeds
    {:ok, _} = Economy.upsert_seed(player.uid, "Basil", 5)

    # Give harvest items needed for plot/vase crafting
    Economy.upsert_item(player.uid, "Sprouts_harvest", 10)
    Economy.upsert_item(player.uid, "Basil_harvest", 10)
    Economy.upsert_item(player.uid, "Cress_harvest", 10)

    player
  end

  describe "craft_plot/3" do
    test "creates empty plot and deducts mana + harvest items" do
      player = setup_player()

      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)

      assert plot.state == "empty"
      assert plot.grid_x == 2
      assert plot.grid_y == 0
      assert plot.player_uid == player.uid

      economy = Economy.get_economy(player.uid)
      # Started with 1000, 2nd plot (index 1) costs 200 mana
      assert economy.mana == 800.0

      # 2nd plot costs 1 Basil_harvest
      items = Economy.list_items(player.uid)
      basil_h = Enum.find(items, &(&1.item_name == "Basil_harvest"))
      assert basil_h.count == 9
    end

    test "escalates cost for subsequent plots" do
      player = setup_player()

      {:ok, _} = Plots.craft_plot(player.uid, 2, 0)
      {:ok, _} = Plots.craft_plot(player.uid, 0, 1)

      economy = Economy.get_economy(player.uid)
      # 1000 - 200 (index 1) - 260 (index 2) = 540
      assert economy.mana == 540.0

      # 3rd plot (index 2) costs 2 Basil_harvest, plus the 1 from 2nd plot = 3 total
      items = Economy.list_items(player.uid)
      basil_h = Enum.find(items, &(&1.item_name == "Basil_harvest"))
      assert basil_h.count == 7
    end

    test "fails with insufficient mana" do
      seed_building_costs()
      seed_flame_config()
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      # Default 50 mana, 2nd plot (index 1) costs 200
      Economy.upsert_item(player.uid, "Basil_harvest", 10)

      {:error, :insufficient_mana} = Plots.craft_plot(player.uid, 2, 0)
    end

    test "fails with insufficient harvest items" do
      seed_building_costs()
      seed_flame_config()
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      # No Basil_harvest given — 2nd plot (index 1) needs 1 Basil_harvest

      {:error, :insufficient_items} = Plots.craft_plot(player.uid, 2, 0)
    end
  end

  describe "plant/3" do
    test "sets plot to growing with seed and initializes snapshots" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)

      {:ok, planted} = Plots.plant(player.uid, plot.id, "Basil")

      assert planted.state == "growing"
      assert planted.seed_name == "Basil"
      assert planted.water_count == 0
      assert planted.plant_time_utc != nil
      assert planted.snapshots["snapshot_count"] == 0
    end

    test "fails on non-empty plot" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      {:error, :plot_not_empty} = Plots.plant(player.uid, plot.id, "Basil")
    end

    test "fails with no seeds" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)

      # Sprouts is in seed_configs but the player has none left after init
      {:ok, _} = Economy.spend_seed(player.uid, "Sprouts", 5)
      {:error, :insufficient_seeds} = Plots.plant(player.uid, plot.id, "Sprouts")
    end
  end

  describe "water/3" do
    test "increments water_count" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      # Create a mallum so we can create a vase and fill it
      {:ok, _mallum} = Mallums.create_mallum(player.uid)
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 2)
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, watered} = Plots.water(player.uid, plot.id, vase.id)

      assert watered.water_count == 1
      assert watered.last_watered_utc != nil
    end

    test "fails during cooldown" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      {:ok, _mallum} = Mallums.create_mallum(player.uid)
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 2)
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
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
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
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
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
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "Basil")

      {:error, :not_mature} = Plots.harvest(player.uid, plot.id)
    end
  end

  describe "check_maturity/1" do
    test "transitions growing to mature when time elapsed" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
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
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
      {:ok, _} = Plots.plant(player.uid, plot.id, "SlowPlant")

      {:ok, still_growing} = Plots.check_maturity(plot.id)
      assert still_growing.state == "growing"
    end
  end

  describe "record_snapshot/2" do
    test "appends to snapshot arrays" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)
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
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)

      {:error, :skin_not_unlocked} = Plots.set_skin(player.uid, plot.id, "fancy_skin")
    end
  end

  describe "craft_plot/3 grid validation" do
    test "rejects when entity cap reached" do
      seed_building_costs()
      seed_flame_config_with_low_cap()
      seed_seed_configs()
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      # Boost mana and give harvest items
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "Sprouts_harvest", 50)
      Economy.upsert_item(player.uid, "Basil_harvest", 50)
      Economy.upsert_item(player.uid, "Chamomile_harvest", 50)
      Economy.upsert_item(player.uid, "Cress_harvest", 50)

      # low_cap at level 1 = 4, init_economy creates 1 plot + 1 vase + 1 house = 3 entities + 1 apotheke = 4
      # So the next craft should fail
      {:error, :entity_cap_reached} = Plots.craft_plot(player.uid, -1, -1)
    end

    test "rejects out-of-bounds coordinates" do
      player = setup_player()

      # Grid radius at level 1 = 2, hex_distance(10, 0) = 10 > 2
      {:error, :out_of_bounds} = Plots.craft_plot(player.uid, 10, 0)
    end

    test "rejects occupied hex" do
      player = setup_player()

      {:ok, _plot} = Plots.craft_plot(player.uid, 2, 0)

      # Try to place another entity at the same hex
      {:error, :hex_occupied} = Plots.craft_plot(player.uid, 2, 0)
    end
  end

  describe "plant/3 seed validation" do
    test "rejects unknown seed name" do
      player = setup_player()
      {:ok, plot} = Plots.craft_plot(player.uid, 2, 0)

      {:error, :unknown_seed} = Plots.plant(player.uid, plot.id, "NonExistentSeed")
    end
  end

  describe "water/3 ownership validation" do
    test "rejects vase not owned by player" do
      player1 = setup_player()
      player2 = setup_player()

      # Give player2 resources
      economy2 = Economy.get_economy(player2.uid)
      economy2 |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      Economy.upsert_item(player2.uid, "Basil_harvest", 10)
      Economy.upsert_item(player2.uid, "Cress_harvest", 10)

      # Create and plant on player1's plot
      {:ok, plot} = Plots.craft_plot(player1.uid, 2, 0)
      {:ok, _} = Plots.plant(player1.uid, plot.id, "Basil")

      # Create player2's vase and fill it
      {:ok, _mallum} = Mallums.create_mallum(player2.uid)
      {:ok, vase2} = Vases.craft_vase(player2.uid, 2, 0)
      {:ok, _} = Vases.set_water(vase2.id, 5)

      # Player1 tries to use player2's vase
      {:error, :not_owned} = Plots.water(player1.uid, plot.id, vase2.id)
    end
  end
end
