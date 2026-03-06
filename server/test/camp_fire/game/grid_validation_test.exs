defmodule CampFire.Game.GridValidationTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.GridValidation
  alias CampFire.Economy

  defp setup_player(_context \\ %{}) do
    seed_flame_config()
    seed_building_costs()
    seed_mallum_house_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)
    player
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

    test "rejects hex with existing plot" do
      # init_economy creates a plot at (-1, 0)
      player = setup_player()

      assert {:error, :hex_occupied} =
               GridValidation.validate_grid_placement(player.uid, -1, 0)
    end

    test "rejects hex with existing vase" do
      # init_economy creates a vase at (0, -1)
      player = setup_player()

      assert {:error, :hex_occupied} =
               GridValidation.validate_grid_placement(player.uid, 0, -1)
    end

    test "rejects apotheke default position (1, 0)" do
      player = setup_player()

      assert {:error, :hex_occupied} =
               GridValidation.validate_grid_placement(player.uid, 1, 0)
    end

    test "rejects hex with existing mallum house" do
      # init_economy creates a mallum house at (1, -1)
      player = setup_player()

      assert {:error, :hex_occupied} =
               GridValidation.validate_grid_placement(player.uid, 1, -1)
    end

    test "accepts valid empty hex within radius" do
      player = setup_player()

      # Grid radius at level 1 is 2, so (1, 1) has distance max(1, 1, 2) = 2 — within bounds
      assert :ok = GridValidation.validate_grid_placement(player.uid, 1, 1)
    end
  end

  describe "check_entity_cap/1" do
    test "allows when under cap" do
      # Default flame_config has cap=5 at level 1
      # init_economy creates: 1 plot + 1 vase + 1 house + 1 apotheke = 4 entities
      player = setup_player()

      assert :ok = GridValidation.check_entity_cap(player.uid)
    end

    test "rejects when at cap" do
      # Low cap config has cap=4 at level 1
      # init_economy creates: 1 plot + 1 vase + 1 house + 1 apotheke = 4 entities (exactly at cap)
      seed_flame_config_with_low_cap()
      seed_building_costs()
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

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

      # Flame at (0,0), plot at (-1,0), vase at (0,-1), apotheke at (1,0), house at (1,-1)
      refute {0, 0} in free
      refute {-1, 0} in free
      refute {0, -1} in free
      refute {1, 0} in free
      refute {1, -1} in free
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
