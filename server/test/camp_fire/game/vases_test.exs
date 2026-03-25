defmodule CampFire.Game.VasesTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Vases, Mallums, MallumHouses, PlayerVase, PlayerMallum}
  alias CampFire.Economy

  # init_economy creates 1 starter vase + 1 starter plot + 1 apotheke (3 entities, no house)
  # build_vase subtracts 1 for the free starter, so first crafted = cost index 0:
  #   vase_costs[0] = 100 mana + 1 cress
  # Second crafted = cost index 1 = 120 mana + 2 basil

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
    economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
    # Give harvest items for vase crafting
    Economy.upsert_item(player.uid, "cress", 10)
    Economy.upsert_item(player.uid, "basil", 10)
    Economy.upsert_item(player.uid, "chamomile", 10)

    # Create a mallum house so we have mallums for fill tests
    [house_pos | _] = free_positions(player.uid)
    {:ok, _house} = MallumHouses.craft_house(player.uid, elem(house_pos, 0), elem(house_pos, 1), [free_mode: true])
    [mallum | _] = Mallums.list_mallums(player.uid)
    {player, mallum}
  end

  describe "build_vase/3" do
    test "creates empty vase and deducts mana + harvest items" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))

      assert vase.state == "empty"
      assert vase.capacity == 5
      assert vase.current_water == 0
      assert vase.grid_x == elem(pos1, 0)
      assert vase.grid_y == elem(pos1, 1)

      economy = Economy.get_economy(player.uid)
      # Started with 1000, cost index 0 (after starter) = 100 mana + 1 cress
      assert economy.mana == 900.0

      inventory = Economy.list_inventory(player.uid)
      cress_h = Enum.find(inventory, &(&1.item_key == "cress"))
      assert cress_h.count == 9
    end

    test "escalates cost for subsequent vases" do
      {player, _mallum} = setup_player()
      [pos1, pos2 | _] = free_positions(player.uid)

      {:ok, _} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Vases.build_vase(player.uid, elem(pos2, 0), elem(pos2, 1))

      economy = Economy.get_economy(player.uid)
      # 1000 - 100 (index 0) - 120 (index 1) = 780
      assert economy.mana == 780.0

      # index 1 costs 2 basil
      inventory = Economy.list_inventory(player.uid)
      basil_h = Enum.find(inventory, &(&1.item_key == "basil"))
      assert basil_h.count == 8
    end

    test "fails with insufficient mana" do
      seed_items()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      # Default 50 mana, 2nd vase (index 1) costs 120
      Economy.upsert_item(player.uid, "basil", 10)

      [pos1 | _] = free_positions(player.uid)
      {:error, :insufficient_mana} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
    end

    test "fails with insufficient harvest items" do
      seed_items()
      seed_flame_config()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 1000.0) |> Repo.update!()
      # No basil given — 2nd vase (index 1) needs 2 basil

      [pos1 | _] = free_positions(player.uid)
      {:error, :insufficient_items} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
    end
  end

  describe "start_fill/2" do
    test "claims mallum and sets filling state" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))

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
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))

      # Put all mallums on quest so none are idle
      mallums = Mallums.list_mallums(player.uid)

      Enum.each(mallums, fn m ->
        m
        |> PlayerMallum.changeset(%{state: "on_quest", assigned_quest_name: "SwampForage"})
        |> Repo.update!()
      end)

      {:error, :no_idle_mallum} = Vases.start_fill(player.uid, vase.id)
    end

    test "fails when already filling" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Vases.start_fill(player.uid, vase.id)

      {:error, :already_filling} = Vases.start_fill(player.uid, vase.id)
    end
  end

  describe "check_fill/2" do
    test "transitions to full when time elapsed" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
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
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
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
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Vases.start_fill(player.uid, vase.id)

      # Don't manipulate time — fill just started
      {:ok, still_filling} = Vases.check_fill(player.uid, vase.id)
      assert still_filling.state == "filling"
    end
  end

  describe "use_water/2" do
    test "deducts water" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Vases.set_water(vase.id, 5)

      {:ok, updated} = Vases.use_water(vase.id, 2)

      assert updated.current_water == 3
    end

    test "fails with insufficient water" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))

      {:error, :insufficient_water} = Vases.use_water(vase.id, 1)
    end

    test "sets state to empty when water reaches zero" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)
      {:ok, vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, _} = Vases.set_water(vase.id, 1)

      {:ok, empty} = Vases.use_water(vase.id, 1)
      assert empty.current_water == 0
      assert empty.state == "empty"
    end
  end

  describe "rain_fill_all/1" do
    test "fills all vases and frees mallums" do
      {player, _mallum} = setup_player()
      [pos1, pos2 | _] = free_positions(player.uid)
      {:ok, vase1} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:ok, vase2} = Vases.build_vase(player.uid, elem(pos2, 0), elem(pos2, 1))
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

  describe "build_vase/3 grid validation" do
    test "rejects when entity cap reached" do
      seed_items()
      seed_flame_config_with_low_cap()
      seed_building_costs()
      seed_mallum_house_config()
      seed_new_player_config()
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      # Boost mana and give harvest items
      economy = Economy.get_economy(player.uid)
      economy |> Ecto.Changeset.change(mana: 10000.0) |> Repo.update!()
      Economy.upsert_item(player.uid, "cress", 50)
      Economy.upsert_item(player.uid, "basil", 50)
      Economy.upsert_item(player.uid, "chamomile", 50)

      # low_cap at level 1 = 4, init_economy creates 1 plot + 1 vase + 1 apotheke = 3 entities
      # So we can craft 1 more, then the next should fail
      [pos1, pos2 | _] = free_positions(player.uid)
      {:ok, _} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
      {:error, :entity_cap_reached} = Vases.build_vase(player.uid, elem(pos2, 0), elem(pos2, 1))
    end

    test "rejects occupied hex" do
      {player, _mallum} = setup_player()
      [pos1 | _] = free_positions(player.uid)

      {:ok, _vase} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))

      # Try to place another vase at the same hex
      {:error, :hex_occupied} = Vases.build_vase(player.uid, elem(pos1, 0), elem(pos1, 1))
    end
  end
end
