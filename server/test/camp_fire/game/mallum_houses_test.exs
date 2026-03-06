defmodule CampFire.Game.MallumHousesTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{MallumHouses, Mallums}
  alias CampFire.Economy

  defp setup_player(_context \\ %{}) do
    seed_building_costs()
    seed_flame_config()
    seed_mallum_house_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    # Boost mana for crafting tests
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 5000.0) |> Repo.update!()

    # Give harvest items needed for house crafting
    Economy.upsert_item(player.uid, "Basil_harvest", 20)
    Economy.upsert_item(player.uid, "Chamomile_harvest", 20)

    player
  end

  describe "craft_house/3" do
    test "creates house and spawns mallums" do
      player = setup_player()

      # init_economy creates 1 house with 2 mallums
      initial_mallums = length(Mallums.list_mallums(player.uid))
      assert initial_mallums == 2

      {:ok, house} = MallumHouses.craft_house(player.uid, 2, 0)

      assert house.grid_x == 2
      assert house.grid_y == 0
      assert house.player_uid == player.uid

      # After crafting 2nd house: target = 2 * 2 = 4 mallums, had 2, so 2 more spawned
      updated_mallums = length(Mallums.list_mallums(player.uid))
      assert updated_mallums == 4
    end

    test "spends mana and items" do
      player = setup_player()

      {:ok, _house} = MallumHouses.craft_house(player.uid, 2, 0)

      economy = Economy.get_economy(player.uid)
      # Started with 5000, 2nd house (index 1) costs 350 mana
      assert economy.mana == 4650.0

      # 2nd house costs 2 Chamomile_harvest
      items = Economy.list_items(player.uid)
      chamomile_h = Enum.find(items, &(&1.item_name == "Chamomile_harvest"))
      assert chamomile_h.count == 18
    end

    test "rejects when entity cap reached" do
      seed_flame_config_with_low_cap()
      seed_building_costs()
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      # Boost mana and give items
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "Basil_harvest", 50)
      Economy.upsert_item(player.uid, "Chamomile_harvest", 50)

      # low_cap at level 1 = 4, init creates 4 entities (plot + vase + house + apotheke)
      {:error, :entity_cap_reached} = MallumHouses.craft_house(player.uid, 2, 0)
    end

    test "rejects occupied hex" do
      player = setup_player()

      {:ok, _house} = MallumHouses.craft_house(player.uid, 2, 0)

      # Try to place another house at the same hex
      {:error, :hex_occupied} = MallumHouses.craft_house(player.uid, 2, 0)
    end
  end

  describe "init_economy creates starter house" do
    test "creates 1 house at (1, -1)" do
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      houses = MallumHouses.list_houses(player.uid)
      assert length(houses) == 1

      [house] = houses
      assert house.grid_x == 1
      assert house.grid_y == -1
    end

    test "creates mallums_per_house mallums" do
      seed_mallum_house_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      mallums = Mallums.list_mallums(player.uid)
      assert length(mallums) == 2
    end
  end
end
