defmodule CampFire.Game.GridValidationTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.GridValidation
  alias CampFire.Economy

  defp setup_player(_context \\ %{}) do
    seed_items()
    seed_flame_config()
    seed_building_costs()
    seed_mallum_house_config()
    seed_new_player_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)
    player
  end

  defp occupied_positions(player_uid) do
    plots = CampFire.Game.Plots.list_plots(player_uid)
    vases = CampFire.Game.Vases.list_vases(player_uid)
    houses = CampFire.Game.MallumHouses.list_houses(player_uid)

    apotheke =
      case CampFire.Repo.get_by(CampFire.Game.PlayerApotheke, player_uid: player_uid) do
        nil -> []
        a -> [{a.grid_x, a.grid_y}]
      end

    Enum.map(plots, &{&1.grid_x, &1.grid_y}) ++
      Enum.map(vases, &{&1.grid_x, &1.grid_y}) ++
      Enum.map(houses, &{&1.grid_x, &1.grid_y}) ++
      apotheke ++
      [{0, 0}]
  end

  describe "hex_distance/2" do
    test "origin is distance 0" do
      assert GridValidation.hex_distance(0, 0) == 0
    end

    test "adjacent hexes are distance 1" do
      assert GridValidation.hex_distance(1, 0) == 1
      assert GridValidation.hex_distance(0, 1) == 1
      assert GridValidation.hex_distance(-1, 1) == 1
    end

    test "distant hexes" do
      assert GridValidation.hex_distance(2, -1) == 2
      assert GridValidation.hex_distance(3, -3) == 3
    end
  end

  describe "validate_grid_placement/3" do
    test "rejects coordinates outside grid radius" do
      player = setup_player()

      assert {:error, :out_of_bounds} =
               GridValidation.validate_grid_placement(player.uid, 99, 99)
    end

    test "rejects flame origin (0,0) as occupied" do
      player = setup_player()

      assert {:error, :hex_occupied} =
               GridValidation.validate_grid_placement(player.uid, 0, 0)
    end

    test "rejects hex with existing entity" do
      player = setup_player()

      # All occupied positions should be rejected
      occupied = occupied_positions(player.uid)
      # init_economy creates plot, vase, apotheke + flame = at least 4 occupied
      assert length(occupied) >= 4

      Enum.each(occupied, fn {q, r} ->
        assert {:error, :hex_occupied} =
                 GridValidation.validate_grid_placement(player.uid, q, r),
               "Expected hex (#{q}, #{r}) to be occupied"
      end)
    end

    test "accepts valid empty hex within radius" do
      player = setup_player()

      occupied = occupied_positions(player.uid)

      # Find a free hex within radius
      free =
        for q <- -2..2,
            r <- -2..2,
            max(abs(q), max(abs(r), abs(q + r))) <= 2,
            {q, r} not in occupied,
            do: {q, r}

      assert length(free) > 0

      {q, r} = List.first(free)
      assert :ok = GridValidation.validate_grid_placement(player.uid, q, r)
    end
  end

  describe "check_entity_cap/1" do
    test "allows when under cap" do
      # Default flame_config has cap=8 at level 1
      # init_economy creates: 1 plot + 1 vase + 1 apotheke = 3 entities
      player = setup_player()

      assert :ok = GridValidation.check_entity_cap(player.uid)
    end

    test "rejects when at cap" do
      # Low cap config has cap=4 at level 1
      # init_economy creates: 1 plot + 1 vase + 1 apotheke = 3 entities
      # Need to craft 1 more to reach cap, then check should fail
      seed_items()
      seed_flame_config_with_low_cap()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      # Boost mana and give items to craft one more entity
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "basil", 50)
      Economy.upsert_item(player.uid, "sprouts", 50)

      [pos1 | _] = free_positions(player.uid)
      {:ok, _} = CampFire.Game.Plots.craft_plot(player.uid, elem(pos1, 0), elem(pos1, 1))

      assert {:error, :entity_cap_reached} = GridValidation.check_entity_cap(player.uid)
    end
  end

  describe "get_free_tiles/1" do
    test "returns non-empty list" do
      player = setup_player()

      free = GridValidation.get_free_tiles(player.uid)
      assert length(free) > 0
    end

    test "does not include occupied hexes" do
      player = setup_player()

      free = GridValidation.get_free_tiles(player.uid)
      occupied = occupied_positions(player.uid)

      Enum.each(occupied, fn pos ->
        refute pos in free, "Occupied hex #{inspect(pos)} should not be in free tiles"
      end)
    end

    test "all returned tiles are within grid radius" do
      player = setup_player()

      # Grid radius at level 1 is 2
      free = GridValidation.get_free_tiles(player.uid)

      Enum.each(free, fn {q, r} ->
        assert GridValidation.hex_distance(q, r) <= 2,
               "Tile {#{q}, #{r}} has distance #{GridValidation.hex_distance(q, r)} but grid radius is 2"
      end)
    end
  end
end
