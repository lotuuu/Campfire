defmodule CampFire.Game.ApothekeTest do
  use CampFire.DataCase
  import CampFire.TestHelpers

  alias CampFire.Game.Apotheke
  alias CampFire.Economy

  defp setup_player do
    seed_building_costs()
    seed_flame_config()
    seed_mallum_house_config()
    seed_recipe_configs()
    player = register_player()
    {:ok, _} = Economy.init_economy(player.uid)
    player
  end

  describe "craft/2" do
    test "consumes ingredients and produces result" do
      player = setup_player()
      Economy.upsert_item(player.uid, "Basil_harvest", 5)

      {:ok, result} = Apotheke.craft(player.uid, "Fertilizer")

      assert result.result_item == "Fertilizer"
      assert result.result_quantity == 1

      items = Economy.list_items(player.uid)
      basil = Enum.find(items, fn i -> i.item_name == "Basil_harvest" end)
      assert basil.count == 3

      fert = Enum.find(items, fn i -> i.item_name == "Fertilizer" end)
      assert fert.count == 1
    end

    test "rejects unknown recipe" do
      player = setup_player()
      assert {:error, :unknown_recipe} = Apotheke.craft(player.uid, "FakeRecipe")
    end

    test "rejects insufficient ingredients" do
      player = setup_player()
      assert {:error, _} = Apotheke.craft(player.uid, "Fertilizer")
    end

    test "multi-ingredient recipe works" do
      player = setup_player()
      Economy.upsert_item(player.uid, "Mint_harvest", 3)
      Economy.upsert_item(player.uid, "Chamomile_harvest", 2)

      {:ok, result} = Apotheke.craft(player.uid, "Speed_Potion")

      assert result.result_item == "Speed_Potion"
      assert result.result_quantity == 1

      items = Economy.list_items(player.uid)
      mint = Enum.find(items, fn i -> i.item_name == "Mint_harvest" end)
      assert mint.count == 1

      cham = Enum.find(items, fn i -> i.item_name == "Chamomile_harvest" end)
      assert cham.count == 1
    end

    test "rolls back on partial ingredient failure" do
      player = setup_player()
      # Give Mint but not Chamomile for Speed_Potion
      Economy.upsert_item(player.uid, "Mint_harvest", 5)

      assert {:error, _} = Apotheke.craft(player.uid, "Speed_Potion")

      # Mint should not have been spent (transaction rolled back)
      items = Economy.list_items(player.uid)
      mint = Enum.find(items, fn i -> i.item_name == "Mint_harvest" end)
      assert mint.count == 5
    end
  end
end
