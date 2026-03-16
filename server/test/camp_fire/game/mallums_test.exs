defmodule CampFire.Game.MallumsTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.{Mallums, MallumHouses, PlayerMallum}
  alias CampFire.Economy

  setup do
    seed_items()
    seed_quest_configs()
    seed_mallum_house_config()
    seed_new_player_config()
    seed_flame_config()
    :ok
  end

  defp setup_player(_context \\ %{}) do
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    # init_economy no longer creates a house/mallums, so create one explicitly
    [house_pos | _] = free_positions(player.uid)
    {:ok, _house} = MallumHouses.craft_house(player.uid, elem(house_pos, 0), elem(house_pos, 1), [free_mode: true])
    # craft_house spawns mallums_per_house (2) mallums
    [mallum | _] = Mallums.list_mallums(player.uid)
    {player, mallum}
  end

  describe "send_on_quest/2" do
    test "claims mallum and sets on_quest" do
      {player, _mallum} = setup_player()

      {:ok, quest_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")

      assert quest_mallum.state == "on_quest"
      assert quest_mallum.assigned_quest_name == "SwampForage"
      assert quest_mallum.start_time_utc != nil
    end

    test "fails with insufficient flame level" do
      {player, _mallum} = setup_player()

      # MeadowExpedition requires flame level 2, player starts at 1
      {:error, :insufficient_flame_level} =
        Mallums.send_on_quest(player.uid, "MeadowExpedition")
    end

    test "fails with no idle mallum" do
      {player, _mallum} = setup_player()

      # Put all mallums on quest
      mallums = Mallums.list_mallums(player.uid)

      Enum.each(mallums, fn m ->
        m
        |> PlayerMallum.changeset(%{state: "on_quest", assigned_quest_name: "SwampForage"})
        |> Repo.update!()
      end)

      {:error, :no_idle_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")
    end

    test "fails with unknown quest" do
      {player, _mallum} = setup_player()

      {:error, :unknown_quest} = Mallums.send_on_quest(player.uid, "FakeQuest")
    end
  end

  describe "check_quest/2" do
    test "with elapsed time transitions to quest_complete with rewards" do
      {player, _mallum} = setup_player()
      {:ok, quest_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")

      # Set start_time_utc far in the past (SwampForage is 5 min)
      past = DateTime.add(DateTime.utc_now(), -600, :second) |> DateTime.truncate(:second)

      quest_mallum
      |> Ecto.Changeset.change(start_time_utc: past)
      |> Repo.update!()

      {:ok, completed} = Mallums.check_quest(player.uid, quest_mallum.id)

      assert completed.state == "quest_complete"
      assert is_list(completed.pending_rewards)
      assert length(completed.pending_rewards) >= 1

      # Each reward should have item_key and count
      Enum.each(completed.pending_rewards, fn reward ->
        assert is_binary(reward["item_key"])
        assert is_integer(reward["count"])
        assert reward["count"] >= 1
      end)
    end

    test "before time returns unchanged mallum" do
      {player, _mallum} = setup_player()
      {:ok, quest_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")

      # Don't manipulate time — quest just started
      {:ok, still_questing} = Mallums.check_quest(player.uid, quest_mallum.id)
      assert still_questing.state == "on_quest"
    end
  end

  describe "collect_rewards/2" do
    test "adds seeds and resets mallum to idle" do
      {player, _mallum} = setup_player()
      {:ok, quest_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")

      # Set start_time_utc far in the past (SwampForage is 5 min)
      past = DateTime.add(DateTime.utc_now(), -600, :second) |> DateTime.truncate(:second)

      quest_mallum
      |> Ecto.Changeset.change(start_time_utc: past)
      |> Repo.update!()

      {:ok, completed} = Mallums.check_quest(player.uid, quest_mallum.id)
      assert completed.state == "quest_complete"

      {:ok, %{rewards: rewards}} = Mallums.collect_rewards(player.uid, completed.id)

      assert is_list(rewards)
      assert length(rewards) >= 1

      # Mallum should be idle now
      updated = Repo.get!(PlayerMallum, completed.id)
      assert updated.state == "idle"
      assert updated.assigned_quest_name == nil
      assert updated.pending_rewards == []

      # Check seeds were added to inventory (reward item_key is directly the item_key)
      inventory = Economy.list_inventory(player.uid)

      Enum.each(rewards, fn reward ->
        item = Enum.find(inventory, &(&1.item_key == reward["item_key"]))
        assert item != nil
        assert item.count >= reward["count"]
      end)
    end

    test "fails when not quest_complete" do
      {player, mallum} = setup_player()

      {:error, :not_quest_complete} = Mallums.collect_rewards(player.uid, mallum.id)
    end
  end

  describe "speed_up_quest/2" do
    test "consumes energy_drink, collects rewards, and resets to idle" do
      {player, _mallum} = setup_player()
      {:ok, quest_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")

      # Give the player energy_drinks
      Economy.upsert_item(player.uid, "energy_drink", 3)

      {:ok, completed} = Mallums.speed_up_quest(player.uid, quest_mallum.id)

      # Speed-up now atomically collects: state goes to idle, rewards distributed
      assert completed.state == "idle"
      assert completed.pending_rewards == []

      # energy_drink should be consumed (had 3, used 1 = 2)
      inventory = Economy.list_inventory(player.uid)
      drink = Enum.find(inventory, &(&1.item_key == "energy_drink"))
      assert drink.count == 2

      # Rewards should have been distributed to inventory
      reward_items = Enum.reject(inventory, &(&1.item_key == "energy_drink"))
      assert length(reward_items) >= 1
    end

    test "fails without energy_drink" do
      {player, _mallum} = setup_player()
      {:ok, quest_mallum} = Mallums.send_on_quest(player.uid, "SwampForage")

      # Spend all energy_drinks if any exist
      inventory = Economy.list_inventory(player.uid)
      drink = Enum.find(inventory, &(&1.item_key == "energy_drink"))
      if drink, do: Economy.spend_item(player.uid, "energy_drink", drink.count)

      {:error, :insufficient_items} = Mallums.speed_up_quest(player.uid, quest_mallum.id)
    end

    test "fails when not on quest" do
      {player, mallum} = setup_player()

      {:error, :not_on_quest} = Mallums.speed_up_quest(player.uid, mallum.id)
    end
  end

  describe "roll_rewards/1" do
    test "returns expected number of rewards" do
      config = Mallums.get_quest_configs() |> Map.fetch!("SwampForage")
      rewards = Mallums.roll_rewards(config)

      assert length(rewards) == config.reward_rolls

      Enum.each(rewards, fn reward ->
        assert is_binary(reward["item_key"])
        assert is_integer(reward["count"])
        assert reward["count"] >= 1
      end)
    end

    test "multi-roll quest returns correct number" do
      config = Mallums.get_quest_configs() |> Map.fetch!("DeepWoodsTrek")
      rewards = Mallums.roll_rewards(config)

      # DeepWoodsTrek has reward_rolls: 3
      assert length(rewards) == 3
    end
  end
end
