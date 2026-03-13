defmodule CampFire.Game.BirdsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Birds, PlayerState, GridValidation}
  alias CampFire.Economy

  defp setup_player(_context \\ %{}) do
    seed_items()
    seed_flame_config()
    seed_building_costs()
    seed_seed_configs()
    seed_mallum_house_config()
    seed_new_player_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)
    player
  end

  defp set_last_check_hour(player_uid, %DateTime{} = hour) do
    iso = DateTime.to_iso8601(hour)

    case Repo.get(PlayerState, player_uid) do
      nil ->
        %PlayerState{}
        |> PlayerState.changeset(%{
          player_uid: player_uid,
          data: %{"last_bird_check_hour_utc" => iso}
        })
        |> Repo.insert!()

      state ->
        new_data = Map.put(state.data || %{}, "last_bird_check_hour_utc", iso)

        state
        |> PlayerState.changeset(%{data: new_data})
        |> Repo.update!()
    end
  end

  describe "check_spawns/1" do
    test "succeeds and returns list" do
      player = setup_player()

      # Set last check to 2 hours ago so there are 2 spawn windows
      two_hours_ago =
        DateTime.utc_now()
        |> DateTime.add(-7200, :second)
        |> DateTime.truncate(:second)
        |> then(fn dt -> %{dt | minute: 0, second: 0, microsecond: {0, 0}} end)

      set_last_check_hour(player.uid, two_hours_ago)

      {:ok, new_birds} = Birds.check_spawns(player.uid)

      # Result is a list (may be empty due to randomness)
      assert is_list(new_birds)

      # All birds should belong to this player and have valid coords
      Enum.each(new_birds, fn bird ->
        assert bird.player_uid == player.uid
        assert is_integer(bird.grid_x)
        assert is_integer(bird.grid_y)
        assert is_binary(bird.seed_name)
        assert bird.seed_count >= 1
      end)

      # Coords should be unique across all birds
      coords = Enum.map(new_birds, fn b -> {b.grid_x, b.grid_y} end)
      assert length(coords) == length(Enum.uniq(coords))
    end

    test "does not spawn on occupied tiles" do
      player = setup_player()

      # Set last check to 3 hours ago for more spawn attempts
      three_hours_ago =
        DateTime.utc_now()
        |> DateTime.add(-10800, :second)
        |> DateTime.truncate(:second)
        |> then(fn dt -> %{dt | minute: 0, second: 0, microsecond: {0, 0}} end)

      set_last_check_hour(player.uid, three_hours_ago)

      {:ok, new_birds} = Birds.check_spawns(player.uid)

      # All bird coords should be on previously free tiles (not on flame, apotheke, etc.)
      Enum.each(new_birds, fn bird ->
        # Bird should not be at flame origin
        refute {bird.grid_x, bird.grid_y} == {0, 0}

        # Bird coords should be within grid radius
        distance = GridValidation.hex_distance(bird.grid_x, bird.grid_y)
        # Grid radius for flame level 1 is 2
        assert distance <= 2
      end)
    end

    test "respects seed tier eligibility" do
      player = setup_player()

      # Override seed configs: tier 0 seeds + one tier 5 seed that should be excluded at level 1
      configs = %{
        "Sprouts" => %{"growth_duration_hours" => 0.5, "min_drops" => 1, "max_drops" => 3, "tier" => 0, "recipe" => %{}},
        "Cress" => %{"growth_duration_hours" => 1.0, "min_drops" => 1, "max_drops" => 3, "tier" => 0, "recipe" => %{}},
        "RareSeed" => %{"growth_duration_hours" => 10.0, "min_drops" => 3, "max_drops" => 10, "tier" => 5, "recipe" => %{}}
      }

      :ets.insert(:config_cache, {"seed_configs", configs})

      # Set last check 2 hours ago
      two_hours_ago =
        DateTime.utc_now()
        |> DateTime.add(-7200, :second)
        |> DateTime.truncate(:second)
        |> then(fn dt -> %{dt | minute: 0, second: 0, microsecond: {0, 0}} end)

      set_last_check_hour(player.uid, two_hours_ago)

      {:ok, new_birds} = Birds.check_spawns(player.uid)

      # At flame_level=1, only tier 0 and tier 1 seeds should appear
      # RareSeed (tier 5) should never appear
      Enum.each(new_birds, fn bird ->
        refute bird.seed_name == "RareSeed",
               "Tier 5 seed should not spawn at flame level 1"

        assert bird.seed_name in ["Sprouts", "Cress"]
      end)
    end
  end

  describe "collect_bird/2" do
    test "removes bird and grants seeds" do
      player = setup_player()

      # Insert a seed config for Basil with item_key/harvest_item_key
      alias CampFire.Game.SeedConfig
      import Ecto.Query

      unless Repo.one(from sc in SeedConfig, where: sc.seed_name == "Basil") do
        %SeedConfig{}
        |> SeedConfig.changeset(%{
          seed_name: "Basil",
          growth_duration_hours: 2.0,
          min_drops: 1,
          max_drops: 4,
          recipe: %{},
          item_key: "basil_seed",
          harvest_item_key: "basil"
        })
        |> Repo.insert!()
      end

      {:ok, bird} = Birds.insert_bird(player.uid, 2, 0, "Basil", 3)

      {:ok, reward} = Birds.collect_bird(player.uid, bird.id)

      assert reward.item_key == "basil_seed"
      assert reward.seed_count == 3

      # Bird should be deleted
      assert Birds.list_birds(player.uid) == []

      # Seeds should be in inventory (uses seed_config.item_key = "basil_seed")
      inventory = Economy.list_inventory(player.uid)
      basil = Enum.find(inventory, &(&1.item_key == "basil_seed"))
      assert basil != nil
      assert basil.count >= 3
    end

    test "rejects collecting another player's bird" do
      player1 = setup_player()
      player2 = setup_player()

      {:ok, bird} = Birds.insert_bird(player1.uid, 2, 0, "Basil", 2)

      {:error, :not_owner} = Birds.collect_bird(player2.uid, bird.id)

      # Bird should still exist
      assert length(Birds.list_birds(player1.uid)) == 1
    end

    test "rejects collecting non-existent bird" do
      player = setup_player()

      {:error, :bird_not_found} = Birds.collect_bird(player.uid, 999_999)
    end
  end
end
