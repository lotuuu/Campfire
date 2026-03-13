defmodule CampFire.Game.MallumHousesTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{MallumHouses, Mallums}
  alias CampFire.Economy

  defp setup_player(_context \\ %{}) do
    seed_items()
    seed_flame_config()
    seed_building_costs()
    seed_mallum_house_config()
    seed_new_player_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    # Boost mana for crafting tests
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 5000.0) |> Repo.update!()

    # Give harvest items needed for house crafting
    Economy.upsert_item(player.uid, "basil", 20)
    Economy.upsert_item(player.uid, "chamomile", 20)

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

      # 2nd house costs 2 chamomile
      inventory = Economy.list_inventory(player.uid)
      chamomile_h = Enum.find(inventory, &(&1.item_key == "chamomile"))
      assert chamomile_h.count == 18
    end

    test "rejects when entity cap reached" do
      seed_items()
      seed_flame_config_with_low_cap()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      # Boost mana and give items
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "basil", 50)
      Economy.upsert_item(player.uid, "chamomile", 50)

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
    test "creates 1 house" do
      seed_items()
      seed_mallum_house_config()
      seed_new_player_config()
      seed_flame_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      houses = MallumHouses.list_houses(player.uid)
      assert length(houses) == 1
    end

    test "creates mallums_per_house mallums" do
      seed_items()
      seed_mallum_house_config()
      seed_new_player_config()
      seed_flame_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      mallums = Mallums.list_mallums(player.uid)
      assert length(mallums) == 2
    end
  end
end
