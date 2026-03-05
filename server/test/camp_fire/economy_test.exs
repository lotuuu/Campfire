defmodule CampFire.EconomyTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Economy

  describe "init_economy/1" do
    test "creates economy with default values" do
      player = register_player()
      {:ok, economy} = Economy.init_economy(player.uid)

      assert economy.mana == 50.0
      assert economy.gems == 5
      assert economy.flame_level == 1
      assert economy.last_mana_collect_utc
    end

    test "creates starting seeds and items" do
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      seeds = Economy.list_seeds(player.uid)
      items = Economy.list_items(player.uid)

      sprouts = Enum.find(seeds, &(&1.seed_name == "Sprouts"))
      cress = Enum.find(seeds, &(&1.seed_name == "Cress"))
      potion = Enum.find(items, &(&1.item_name == "Speed_Potion"))

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

  describe "seeds" do
    test "upsert adds to existing" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, seed} = Economy.upsert_seed(player.uid, "Sprouts", 3)
      assert seed.count == 8
    end

    test "spend reduces count" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, _} = Economy.spend_seed(player.uid, "Sprouts", 2)
      seeds = Economy.list_seeds(player.uid)
      sprouts = Enum.find(seeds, &(&1.seed_name == "Sprouts"))
      assert sprouts.count == 3
    end

    test "spend deletes row when count reaches zero" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, :deleted} = Economy.spend_seed(player.uid, "Sprouts", 5)
      seeds = Economy.list_seeds(player.uid)
      assert Enum.find(seeds, &(&1.seed_name == "Sprouts")) == nil
    end

    test "spend rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_seeds} = Economy.spend_seed(player.uid, "Sprouts", 99)
    end
  end

  describe "items" do
    test "upsert adds to existing" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, item} = Economy.upsert_item(player.uid, "Speed_Potion", 2)
      assert item.count == 5
    end

    test "spend reduces count" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, _} = Economy.spend_item(player.uid, "Speed_Potion", 1)
      items = Economy.list_items(player.uid)
      potion = Enum.find(items, &(&1.item_name == "Speed_Potion"))
      assert potion.count == 2
    end

    test "spend rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_items} = Economy.spend_item(player.uid, "Speed_Potion", 99)
    end
  end

  describe "upgrade_flame/2" do
    test "consumes items and increments level" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, _} = Economy.upsert_item(player.uid, "Sprouts_harvest", 5)

      {:ok, economy} = Economy.upgrade_flame(player.uid, [
        %{"item_name" => "Sprouts_harvest", "count" => 1}
      ])

      assert economy.flame_level == 2

      items = Economy.list_items(player.uid)
      sprouts = Enum.find(items, &(&1.item_name == "Sprouts_harvest"))
      assert sprouts.count == 4
    end

    test "rejects when items insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      assert {:error, {:insufficient_items, "Sprouts_harvest"}} =
               Economy.upgrade_flame(player.uid, [
                 %{"item_name" => "Sprouts_harvest", "count" => 1}
               ])
    end
  end
end
