defmodule CampFire.Game.SkinsTest do
  use CampFire.DataCase, async: true

  alias CampFire.Game.{Skins, MallumHouses}
  alias CampFire.Economy
  import CampFire.TestHelpers

  setup do
    seed_items()
    player = register_player()
    seed_skin_configs()
    seed_flame_config()
    seed_building_costs()
    seed_mallum_house_config()
    seed_new_player_config()
    {:ok, _eco} = Economy.init_economy(player.uid)
    %{player: player, uid: player.uid}
  end

  describe "apply_skin/4 on plots" do
    test "unlocks and applies skin, deducting items", %{uid: uid} do
      [plot | _] = CampFire.Game.Plots.list_plots(uid)

      # Give the player enough items to unlock
      Economy.upsert_item(uid, "basil", 5)

      assert {:ok, updated} = Skins.apply_skin(uid, :plot, plot.id, "GreenPlot")
      assert updated.skin_name == "GreenPlot"
      assert "GreenPlot" in updated.unlocked_skins

      # Items were deducted (cost = 3)
      {_economy, items} = Economy.get_full_state(uid)
      basil = Enum.find(items, &(&1.item_key == "basil"))
      assert basil.count == 2
    end

    test "re-applying already unlocked skin is free", %{uid: uid} do
      [plot | _] = CampFire.Game.Plots.list_plots(uid)
      Economy.upsert_item(uid, "basil", 5)

      # First apply unlocks
      {:ok, _} = Skins.apply_skin(uid, :plot, plot.id, "GreenPlot")

      # Apply a different skin to change away
      Economy.upsert_item(uid, "chamomile", 5)
      {:ok, _} = Skins.apply_skin(uid, :plot, plot.id, "BluePlot")

      # Re-apply GreenPlot — should be free (no item deduction)
      {_economy, items_before} = Economy.get_full_state(uid)
      basil_before = Enum.find(items_before, &(&1.item_key == "basil"))

      {:ok, updated} = Skins.apply_skin(uid, :plot, plot.id, "GreenPlot")
      assert updated.skin_name == "GreenPlot"

      {_economy, items_after} = Economy.get_full_state(uid)
      basil_after = Enum.find(items_after, &(&1.item_key == "basil"))
      assert basil_after.count == basil_before.count
    end

    test "rejects unknown skin name", %{uid: uid} do
      [plot | _] = CampFire.Game.Plots.list_plots(uid)
      assert {:error, :unknown_skin} = Skins.apply_skin(uid, :plot, plot.id, "NonExistent")
    end

    test "rejects insufficient items for unlock", %{uid: uid} do
      [plot | _] = CampFire.Game.Plots.list_plots(uid)
      # No items given — should fail
      assert {:error, :insufficient_items} = Skins.apply_skin(uid, :plot, plot.id, "GreenPlot")
    end

    test "rejects not-owned entity", %{uid: uid} do
      other = register_player()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      {:ok, _} = Economy.init_economy(other.uid)
      [other_plot | _] = CampFire.Game.Plots.list_plots(other.uid)

      Economy.upsert_item(uid, "basil", 5)
      assert {:error, :not_owned} = Skins.apply_skin(uid, :plot, other_plot.id, "GreenPlot")
    end
  end

  describe "apply_skin/4 on vases" do
    test "unlocks and applies skin on vase", %{uid: uid} do
      [vase | _] = CampFire.Game.Vases.list_vases(uid)
      Economy.upsert_item(uid, "basil", 5)

      assert {:ok, updated} = Skins.apply_skin(uid, :vase, vase.id, "FancyVase")
      assert updated.skin_name == "FancyVase"
      assert "FancyVase" in updated.unlocked_skins

      # Cost was 2
      {_economy, items} = Economy.get_full_state(uid)
      basil = Enum.find(items, &(&1.item_key == "basil"))
      assert basil.count == 3
    end
  end

  describe "apply_skin/4 on mallum houses" do
    test "unlocks and applies skin on mallum house", %{uid: uid} do
      # init_economy no longer creates a house, so create one explicitly
      [house_pos | _] = free_positions(uid)
      {:ok, _house} = MallumHouses.craft_house(uid, elem(house_pos, 0), elem(house_pos, 1), [free_mode: true])

      [house | _] = MallumHouses.list_houses(uid)
      Economy.upsert_item(uid, "basil", 10)

      assert {:ok, updated} = Skins.apply_skin(uid, :mallum_house, house.id, "CozyHouse")
      assert updated.skin_name == "CozyHouse"
      assert "CozyHouse" in updated.unlocked_skins

      # Cost was 5
      {_economy, items} = Economy.get_full_state(uid)
      basil = Enum.find(items, &(&1.item_key == "basil"))
      assert basil.count == 5
    end
  end
end
