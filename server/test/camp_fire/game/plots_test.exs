defmodule CampFire.Game.PlotsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Plots, Vases, Mallums, MallumHouses, PlayerPlot}
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

  # init_economy creates 1 starter plot + 1 starter vase + 1 apotheke (3 entities, no house)
  # craft_plot subtracts 1 for the free starter, so first crafted = cost index 0:
  #   plot_costs[0] = 150 mana + 1 sprouts
  # Second crafted = cost index 1 = 200 mana + 1 basil

  defp setup_player(_context \\ %{}) do
    seed_items()
    seed_flame_config()
    seed_building_costs()
    seed_seed_configs()
    seed_mallum_house_config()
    seed_new_player_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    # Boost mana for crafting tests
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()

    # Give player some Basil seeds
    {:ok, _} = Economy.upsert_item(player.uid, "basil_seed", 5)

    # Give harvest items needed for plot/vase crafting
    Economy.upsert_item(player.uid, "sprouts", 10)
    Economy.upsert_item(player.uid, "basil", 10)
    Economy.upsert_item(player.uid, "cress", 10)

    player
  end

  describe "craft_plot/3" do
    test "creates empty plot and deducts mana + harvest items" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      assert plot.state == "empty"
      assert plot.grid_x == elem(pos1, 0)
      assert plot.grid_y == elem(pos1, 1)
      assert plot.player_uid == player.uid

      economy = Economy.get_economy(player.uid)
      # Started with 1000, cost index 0 (after starter) = 150 mana + 1 sprouts
      assert economy.mana == 850.0

      inventory = Economy.list_inventory(player.uid)
      sprouts_h = Enum.find(inventory, &(&1.item_key == "sprouts"))
      assert sprouts_h.count == 9
    end

    test "escalates cost for subsequent plots" do
      player = setup_player()
      [pos1, pos2 | _] = free_positions(player.uid)

      {:ok, _} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.craft_plot(player.uid, elem(pos2, 0), elem(pos2, 1))

      economy = Economy.get_economy(player.uid)
      # 1000 - 150 (index 0) - 200 (index 1) = 650
      assert economy.mana == 650.0

      # index 0 costs 1 sprouts, index 1 costs 1 basil = total 1 basil spent
      inventory = Economy.list_inventory(player.uid)
      basil_h = Enum.find(inventory, &(&1.item_key == "basil"))
      assert basil_h.count == 9
    end

    test "fails with insufficient mana" do
      seed_items()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      # Default 50 mana, 2nd plot (index 1) costs 200
      Economy.upsert_item(player.uid, "basil", 10)

      [pos1 | _] = free_positions(player.uid)
      {:error, :insufficient_mana} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
    end

    test "fails with insufficient harvest items" do
      seed_items()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      # No basil given — 2nd plot (index 1) needs 1 basil

      [pos1 | _] = free_positions(player.uid)
      {:error, :insufficient_items} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
    end
  end

  describe "plant/3" do
    test "sets plot to growing with seed and initializes snapshots" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      {:ok, planted} = Plots.plant(player.uid, plot.id, "basil")

      assert planted.state == "growing"
      assert planted.seed_item_id != nil
      assert planted.water_count == 0
      assert planted.plant_time_utc != nil
      assert planted.snapshots["snapshot_count"] == 0
    end

    test "fails on non-empty plot" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "basil")

      {:error, :plot_not_empty} = Plots.plant(player.uid, plot.id, "basil")
    end

    test "fails with no seeds" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      # Sprouts is in seed_configs but the player has none left after init
      {:ok, _} = Economy.spend_item(player.uid, "sprouts_seed", 5)
      {:error, :insufficient_items} = Plots.plant(player.uid, plot.id, "sprouts")
    end
  end

  describe "water/3" do
    test "increments water_count" do
      player = setup_player()
      [pos1, pos2, pos3 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "basil")

      # Create a mallum house so we have mallums
      {:ok, _house} = MallumHouses.craft_house(player.uid, elem(pos2, 0), elem(pos2, 1), [free_mode: true])
      {:ok, vase} = Vases.craft_vase(player.uid, elem(pos3, 0), elem(pos3, 1))
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, watered} = Plots.water(player.uid, plot.id, vase.id)

      assert watered.water_count == 1
      assert watered.last_watered_utc != nil
    end

    test "fails during cooldown" do
      player = setup_player()
      [pos1, pos2, pos3 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "basil")

      {:ok, _house} = MallumHouses.craft_house(player.uid, elem(pos2, 0), elem(pos2, 1), [free_mode: true])
      {:ok, vase} = Vases.craft_vase(player.uid, elem(pos3, 0), elem(pos3, 1))
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, _} = Plots.water(player.uid, plot.id, vase.id)

      # Second water immediately should fail
      {:error, :water_cooldown} = Plots.water(player.uid, plot.id, vase.id)
    end
  end

  describe "harvest/2" do
    test "on mature plot returns drops" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _} = Economy.upsert_item(player.uid, "harvesttest_seed", 1)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "harvesttest")

      # Force the plot to mature
      {:ok, _} = Plots.force_mature(plot.id)

      {:ok, result} = Plots.harvest(player.uid, plot.id)

      assert result.harvest_item_key == "harvesttest"
      assert result.drops >= 2 and result.drops <= 8
      assert result.score == 1.0
    end

    test "resets plot to empty after harvest" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _} = Economy.upsert_item(player.uid, "simpleseed_seed", 1)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "simpleseed")
      {:ok, _} = Plots.force_mature(plot.id)

      {:ok, _} = Plots.harvest(player.uid, plot.id)

      updated = Repo.get!(PlayerPlot, plot.id)
      assert updated.state == "empty"
      assert updated.seed_item_id == nil
      assert updated.water_count == 0
    end

    test "fails when not mature" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "basil")

      {:error, :not_mature} = Plots.harvest(player.uid, plot.id)
    end
  end

  describe "check_maturity/1" do
    test "transitions growing to mature when time elapsed" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, planted} = Plots.plant(player.uid, plot.id, "basil")

      # Set plant_time_utc far in the past (basil growth_duration_hours is 2.0)
      past = DateTime.add(DateTime.utc_now(), -7200, :second) |> DateTime.truncate(:second)

      planted
      |> Ecto.Changeset.change(plant_time_utc: past)
      |> Repo.update!()

      {:ok, matured} = Plots.check_maturity(plot.id)
      assert matured.state == "mature"
    end

    test "does not transition if time has not elapsed" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _} = Economy.upsert_item(player.uid, "slowplant_seed", 1)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "slowplant")

      {:ok, still_growing} = Plots.check_maturity(plot.id)
      assert still_growing.state == "growing"
    end
  end

  describe "record_snapshot/2" do
    test "appends to snapshot arrays" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Plots.plant(player.uid, plot.id, "basil")

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
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      {:error, :skin_not_unlocked} = Plots.set_skin(player.uid, plot.id, "fancy_skin")
    end
  end

  describe "craft_plot/3 grid validation" do
    test "rejects when entity cap reached" do
      seed_items()
      seed_flame_config_with_low_cap()
      seed_building_costs()
      seed_seed_configs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      # Boost mana and give harvest items
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "sprouts", 50)
      Economy.upsert_item(player.uid, "basil", 50)
      Economy.upsert_item(player.uid, "chamomile", 50)
      Economy.upsert_item(player.uid, "cress", 50)

      # low_cap at level 1 = 4, init_economy creates 1 plot + 1 vase + 1 apotheke = 3 entities
      # So we can craft 1 more, then the next should fail
      [pos1, pos2 | _] = free_positions(player.uid)
      {:ok, _} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:error, :entity_cap_reached} = Plots.craft_plot(player.uid, elem(pos2, 0), elem(pos2, 1))
    end

    test "rejects out-of-bounds coordinates" do
      player = setup_player()

      # Grid radius at level 1 = 2, hex_distance(10, 0) = 10 > 2
      {:error, :out_of_bounds} = Plots.craft_plot(player.uid, 10, 0)
    end

    test "rejects occupied hex" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      # Try to place another entity at the same hex
      {:error, :hex_occupied} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))
    end
  end

  describe "plant/3 seed validation" do
    test "rejects unknown seed name" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, plot} = Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      {:error, :unknown_seed} = Plots.plant(player.uid, plot.id, "nonexistentseed")
    end
  end

  describe "water/3 ownership validation" do
    test "rejects vase not owned by player" do
      player1 = setup_player()
      player2 = setup_player()

      # Give player2 resources
      economy2 = Economy.get_economy(player2.uid)
      economy2 |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      Economy.upsert_item(player2.uid, "basil", 10)
      Economy.upsert_item(player2.uid, "cress", 10)

      # Create and plant on player1's plot
      [p1_pos1 | _] = free_positions(player1.uid)
      {:ok, plot} = Plots.craft_plot(player1.uid, elem(p1_pos1, 0), elem(p1_pos1, 1))
      {:ok, _} = Plots.plant(player1.uid, plot.id, "basil")

      # Create player2's mallum house and vase, fill it
      [p2_pos1, p2_pos2 | _] = free_positions(player2.uid)
      {:ok, _house} = MallumHouses.craft_house(player2.uid, elem(p2_pos1, 0), elem(p2_pos1, 1), [free_mode: true])
      {:ok, vase2} = Vases.craft_vase(player2.uid, elem(p2_pos2, 0), elem(p2_pos2, 1))
      {:ok, _} = Vases.set_water(vase2.id, 5)

      # Player1 tries to use player2's vase
      {:error, :not_owned} = Plots.water(player1.uid, plot.id, vase2.id)
    end
  end
end
