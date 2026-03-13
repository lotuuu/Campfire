defmodule CampFire.EconomyTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Economy

  setup do
    seed_items()
    seed_new_player_config()
    seed_flame_config()
    seed_mallum_house_config()
    :ok
  end

  describe "init_economy/1" do
    test "creates economy with default values" do
      player = register_player()
      {:ok, economy} = Economy.init_economy(player.uid)

      assert economy.mana == 50.0
      assert economy.gems == 5
      assert economy.flame_level == 1
      assert economy.last_mana_collect_utc
    end

    test "creates starting inventory" do
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      inventory = Economy.list_inventory(player.uid)

      sprouts = Enum.find(inventory, &(&1.item_key == "sprouts_seed"))
      cress = Enum.find(inventory, &(&1.item_key == "cress_seed"))
      potion = Enum.find(inventory, &(&1.item_key == "speed_potion"))

      assert sprouts.count == 5
      assert cress.count == 3
      assert potion.count == 3
    end

    test "rejects duplicate init" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:error, _} = Economy.init_economy(player.uid)
    end
  end

  describe "collect_mana/1" do
    test "accumulates mana based on flame level and time" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      economy = Economy.get_economy(player.uid)
      ten_seconds_ago = DateTime.add(DateTime.utc_now(), -10, :second) |> DateTime.truncate(:second)

      economy
      |> Ecto.Changeset.change(last_mana_collect_utc: ten_seconds_ago)
      |> CampFire.Repo.update!()

      {:ok, updated} = Economy.collect_mana(player.uid)
      # Level 1: 0.5 mana/sec * 10 sec = ~5.0 earned, starting 50 = ~55
      assert updated.mana >= 54.0 and updated.mana <= 56.0
    end
  end

  describe "spend_mana/2" do
    test "deducts mana when sufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, economy} = Economy.spend_mana(player.uid, 20.0)
      assert economy.mana == 30.0
    end

    test "rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_mana} = Economy.spend_mana(player.uid, 999.0)
    end
  end

  describe "gems" do
    test "add and spend gems" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      {:ok, economy} = Economy.add_gems(player.uid, 10)
      assert economy.gems == 15

      {:ok, economy} = Economy.spend_gems(player.uid, 5)
      assert economy.gems == 10
    end

    test "rejects overspend" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_gems} = Economy.spend_gems(player.uid, 100)
    end
  end

  describe "inventory (unified seeds + items)" do
    test "upsert adds to existing seed" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, item} = Economy.upsert_item(player.uid, "sprouts_seed", 3)
      assert item.count == 8
    end

    test "spend seed reduces count" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, _} = Economy.spend_item(player.uid, "sprouts_seed", 2)
      inventory = Economy.list_inventory(player.uid)
      sprouts = Enum.find(inventory, &(&1.item_key == "sprouts_seed"))
      assert sprouts.count == 3
    end

    test "spend deletes row when count reaches zero" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, :spent} = Economy.spend_item(player.uid, "sprouts_seed", 5)
      inventory = Economy.list_inventory(player.uid)
      assert Enum.find(inventory, &(&1.item_key == "sprouts_seed")) == nil
    end

    test "spend rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_items} = Economy.spend_item(player.uid, "sprouts_seed", 99)
    end

    test "upsert item adds to existing" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, item} = Economy.upsert_item(player.uid, "speed_potion", 2)
      assert item.count == 5
    end

    test "spend item reduces count" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, _} = Economy.spend_item(player.uid, "speed_potion", 1)
      inventory = Economy.list_inventory(player.uid)
      potion = Enum.find(inventory, &(&1.item_key == "speed_potion"))
      assert potion.count == 2
    end

    test "spend item rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_items} = Economy.spend_item(player.uid, "speed_potion", 99)
    end
  end

  describe "upgrade_flame/2" do
    test "consumes items and increments level" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, _} = Economy.upsert_item(player.uid, "sprouts", 5)

      {:ok, economy} = Economy.upgrade_flame(player.uid, [
        %{"itemKey" => "sprouts", "count" => 1}
      ])

      assert economy.flame_level == 2

      inventory = Economy.list_inventory(player.uid)
      sprouts = Enum.find(inventory, &(&1.item_key == "sprouts"))
      assert sprouts.count == 4
    end

    test "rejects when items insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      assert {:error, {:insufficient_items, "sprouts"}} =
               Economy.upgrade_flame(player.uid, [
                 %{"itemKey" => "sprouts", "count" => 1}
               ])
    end
  end
end
