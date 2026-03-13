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
      [pos1 | _] = free_positions(player.uid)

      # init_economy creates no house/mallums
      initial_mallums = length(Mallums.list_mallums(player.uid))
      assert initial_mallums == 0

      {:ok, house} = MallumHouses.craft_house(player.uid, elem(pos1, 0), elem(pos1, 1))

      assert house.grid_x == elem(pos1, 0)
      assert house.grid_y == elem(pos1, 1)
      assert house.player_uid == player.uid

      # After crafting 1st house: target = 1 * 2 = 2 mallums, had 0, so 2 spawned
      updated_mallums = length(Mallums.list_mallums(player.uid))
      assert updated_mallums == 2
    end

    test "spends mana and items" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _house} = MallumHouses.craft_house(player.uid, elem(pos1, 0), elem(pos1, 1))

      economy = Economy.get_economy(player.uid)
      # Started with 5000, 1st house (index 0) costs 200 mana
      assert economy.mana == 4800.0

      # 1st house costs 1 basil
      inventory = Economy.list_inventory(player.uid)
      basil_h = Enum.find(inventory, &(&1.item_key == "basil"))
      assert basil_h.count == 19
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

      # low_cap at level 1 = 4, init creates 3 entities (plot + vase + apotheke)
      # So we can craft 1 more, then the next should fail
      [pos1, pos2 | _] = free_positions(player.uid)
      {:ok, _} = MallumHouses.craft_house(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:error, :entity_cap_reached} = MallumHouses.craft_house(player.uid, elem(pos2, 0), elem(pos2, 1))
    end

    test "rejects occupied hex" do
      player = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _house} = MallumHouses.craft_house(player.uid, elem(pos1, 0), elem(pos1, 1))

      # Try to place another house at the same hex
      {:error, :hex_occupied} = MallumHouses.craft_house(player.uid, elem(pos1, 0), elem(pos1, 1))
    end
  end
end
