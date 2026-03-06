defmodule CampFire.Game.VasesTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Vases, Mallums, PlayerVase, PlayerMallum}
  alias CampFire.Economy

  # init_economy creates 1 starter vase + 1 starter plot + 1 mallum
  # So the first craft_vase in tests is the 2nd vase (index 1):
  #   vase_costs[1] = 120 mana + 2 Basil_harvest
  # The 3rd vase (index 2) = 150 mana + 1 Chamomile_harvest

  defp setup_player(_context \\ %{}) do
    seed_building_costs()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)
    # Boost mana for crafting tests
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
    # Give harvest items for vase crafting
    Economy.upsert_item(player.uid, "Cress_harvest", 10)
    Economy.upsert_item(player.uid, "Basil_harvest", 10)
    Economy.upsert_item(player.uid, "Chamomile_harvest", 10)
    # init_economy creates a starter mallum
    [mallum] = Mallums.list_mallums(player.uid)
    {player, mallum}
  end

  describe "craft_vase/3" do
    test "creates empty vase and deducts mana + harvest items" do
      {player, _mallum} = setup_player()

      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)

      assert vase.state == "empty"
      assert vase.capacity == 5
      assert vase.current_water == 0
      assert vase.grid_x == 0
      assert vase.grid_y == 0

      economy = Economy.get_economy(player.uid)
      # Started with 1000, 2nd vase (index 1) costs 120
      assert economy.mana == 880.0

      # 2nd vase costs 2 Basil_harvest
      items = Economy.list_items(player.uid)
      basil_h = Enum.find(items, &(&1.item_name == "Basil_harvest"))
      assert basil_h.count == 8
    end

    test "escalates cost for subsequent vases" do
      {player, _mallum} = setup_player()

      {:ok, _} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, _} = Vases.craft_vase(player.uid, 1, 0)

      economy = Economy.get_economy(player.uid)
      # 1000 - 120 (index 1) - 150 (index 2) = 730
      assert economy.mana == 730.0

      # 3rd vase (index 2) costs 1 Chamomile_harvest
      items = Economy.list_items(player.uid)
      chamomile_h = Enum.find(items, &(&1.item_name == "Chamomile_harvest"))
      assert chamomile_h.count == 9
    end

    test "fails with insufficient mana" do
      seed_building_costs()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      # Default 50 mana, 2nd vase (index 1) costs 120
      Economy.upsert_item(player.uid, "Basil_harvest", 10)

      {:error, :insufficient_mana} = Vases.craft_vase(player.uid, 0, 0)
    end

    test "fails with insufficient harvest items" do
      seed_building_costs()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      # No Basil_harvest given — 2nd vase (index 1) needs 2 Basil_harvest

      {:error, :insufficient_items} = Vases.craft_vase(player.uid, 0, 0)
    end
  end

  describe "start_fill/2" do
    test "claims mallum and sets filling state" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)

      {:ok, filling_vase} = Vases.start_fill(player.uid, vase.id)

      assert filling_vase.state == "filling"
      assert filling_vase.fill_start_time_utc != nil

      # Mallum should be fetching water
      mallums = Mallums.list_mallums(player.uid)
      fetching = Enum.find(mallums, &(&1.state == "fetching_water"))
      assert fetching != nil
      assert fetching.assigned_vase_id == vase.id
    end

    test "fails with no idle mallum" do
      {player, mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)

      # Put the mallum on a quest so none are idle
      mallum
      |> PlayerMallum.changeset(%{state: "on_quest", assigned_quest_name: "SwampForage"})
      |> Repo.update!()

      {:error, :no_idle_mallum} = Vases.start_fill(player.uid, vase.id)
    end

    test "fails when already filling" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, _} = Vases.start_fill(player.uid, vase.id)

      {:error, :already_filling} = Vases.start_fill(player.uid, vase.id)
    end
  end

  describe "check_fill/2" do
    test "transitions to full when time elapsed" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, filling} = Vases.start_fill(player.uid, vase.id)

      # Set fill_start_time_utc far in the past
      past = DateTime.add(DateTime.utc_now(), -3600, :second) |> DateTime.truncate(:second)

      filling
      |> Ecto.Changeset.change(fill_start_time_utc: past)
      |> Repo.update!()

      {:ok, full_vase} = Vases.check_fill(player.uid, vase.id)

      assert full_vase.state == "full"
      assert full_vase.current_water == full_vase.capacity
      assert full_vase.fill_start_time_utc == nil
    end

    test "frees mallum on completion" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, filling} = Vases.start_fill(player.uid, vase.id)

      past = DateTime.add(DateTime.utc_now(), -3600, :second) |> DateTime.truncate(:second)

      filling
      |> Ecto.Changeset.change(fill_start_time_utc: past)
      |> Repo.update!()

      {:ok, _} = Vases.check_fill(player.uid, vase.id)

      mallums = Mallums.list_mallums(player.uid)
      assert Enum.all?(mallums, &(&1.state == "idle"))
    end

    test "returns unchanged vase when fill not complete" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, _} = Vases.start_fill(player.uid, vase.id)

      # Don't manipulate time — fill just started
      {:ok, still_filling} = Vases.check_fill(player.uid, vase.id)
      assert still_filling.state == "filling"
    end
  end

  describe "use_water/2" do
    test "deducts water" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, updated} = Vases.use_water(vase.id, 2)

      assert updated.current_water == 3
    end

    test "fails with insufficient water" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)

      {:error, :insufficient_water} = Vases.use_water(vase.id, 1)
    end

    test "sets state to empty when water reaches zero" do
      {player, _mallum} = setup_player()
      {:ok, vase} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, _} = Vases.set_water(vase.id, 1)

      {:ok, empty} = Vases.use_water(vase.id, 1)
      assert empty.current_water == 0
      assert empty.state == "empty"
    end
  end

  describe "rain_fill_all/1" do
    test "fills all vases and frees mallums" do
      {player, _mallum} = setup_player()
      {:ok, vase1} = Vases.craft_vase(player.uid, 0, 0)
      {:ok, vase2} = Vases.craft_vase(player.uid, 1, 0)
      {:ok, _} = Vases.start_fill(player.uid, vase1.id)

      :ok = Vases.rain_fill_all(player.uid)

      updated1 = Repo.get!(PlayerVase, vase1.id)
      updated2 = Repo.get!(PlayerVase, vase2.id)

      assert updated1.state == "full"
      assert updated1.current_water == updated1.capacity
      assert updated2.state == "full"
      assert updated2.current_water == updated2.capacity

      # Mallums should all be idle
      mallums = Mallums.list_mallums(player.uid)
      assert Enum.all?(mallums, &(&1.state == "idle"))
    end
  end
end
