defmodule CampFire.Game.ApothekeTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.Apotheke
  alias CampFire.Economy

  defp setup_player do
    seed_items()
    seed_flame_config()
    seed_building_costs()
    seed_mallum_house_config()
    seed_recipe_configs()
    seed_new_player_config()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    player
  end

  describe "craft/2" do
    test "consumes ingredients and produces result" do
      player = setup_player()
      Economy.upsert_item(player.uid, "basil", 5)

      {:ok, result} = Apotheke.craft(player.uid, "fertilizer")

      assert result.result_item == "fertilizer"
      assert result.result_quantity == 1

      inventory = Economy.list_inventory(player.uid)
      basil = Enum.find(inventory, fn i -> i.item_key == "basil" end)
      assert basil.count == 3

      fert = Enum.find(inventory, fn i -> i.item_key == "fertilizer" end)
      assert fert.count == 1
    end

    test "rejects unknown recipe" do
      player = setup_player()
      assert {:error, :unknown_recipe} = Apotheke.craft(player.uid, "FakeRecipe")
    end

    test "rejects insufficient ingredients" do
      player = setup_player()
      assert {:error, _} = Apotheke.craft(player.uid, "fertilizer")
    end

    test "multi-ingredient recipe works" do
      player = setup_player()
      Economy.upsert_item(player.uid, "mint", 3)
      Economy.upsert_item(player.uid, "chamomile", 2)

      {:ok, result} = Apotheke.craft(player.uid, "speed_potion")

      assert result.result_item == "speed_potion"
      assert result.result_quantity == 1

      inventory = Economy.list_inventory(player.uid)
      mint = Enum.find(inventory, fn i -> i.item_key == "mint" end)
      assert mint.count == 1

      cham = Enum.find(inventory, fn i -> i.item_key == "chamomile" end)
      assert cham.count == 1
    end

    test "rolls back on partial ingredient failure" do
      player = setup_player()
      # Give Mint but not Chamomile for speed_potion
      Economy.upsert_item(player.uid, "mint", 5)

      assert {:error, _} = Apotheke.craft(player.uid, "speed_potion")

      # Mint should not have been spent (transaction rolled back)
      inventory = Economy.list_inventory(player.uid)
      mint = Enum.find(inventory, fn i -> i.item_key == "mint" end)
      assert mint.count == 5
    end
  end
end
