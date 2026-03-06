defmodule CampFire.Game.GardensTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Game.Gardens
  alias CampFire.Economy

  defp setup_player(_context \\ %{}) do
    seed_garden_configs()
    seed_flame_config()
    seed_mallum_house_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)
    player
  end

  describe "plant/4" do
    test "creates garden with correct plant_time_utc" do
      player = setup_player()

      {:ok, garden} = Gardens.plant(player.uid, "BerryBush", 2, 0)

      assert garden.plant_name == "BerryBush"
      assert garden.plant_time_utc != nil
      assert garden.mature == false
      assert garden.grid_x == 2
      assert garden.grid_y == 0

      # BerryBush costs 30 mana, started with 50
      economy = Economy.get_economy(player.uid)
      assert economy.mana == 20.0
    end

    test "fails with unknown plant name" do
      player = setup_player()

      {:error, :unknown_plant} = Gardens.plant(player.uid, "MagicTree", 2, 0)
    end

    test "fails with insufficient mana" do
      player = setup_player()
      {:ok, _} = Economy.spend_mana(player.uid, 50.0)

      {:error, :insufficient_mana} = Gardens.plant(player.uid, "BerryBush", 2, 0)
    end
  end

  describe "check_and_collect/2" do
    test "on immature garden returns growing status" do
      player = setup_player()
      {:ok, garden} = Gardens.plant(player.uid, "BerryBush", 2, 0)

      {:ok, result} = Gardens.check_and_collect(player.uid, garden.id)

      assert result.status == :growing
    end

    test "on mature garden with elapsed yield interval adds items" do
      player = setup_player()
      {:ok, garden} = Gardens.plant(player.uid, "BerryBush", 2, 0)

      # Set plant_time_utc far in the past so it matures and yield interval passes
      # BerryBush: growth_hours=24, yield_interval_hours=12
      far_past = DateTime.add(DateTime.utc_now(), -48 * 3600, :second) |> DateTime.truncate(:second)

      garden
      |> Ecto.Changeset.change(plant_time_utc: far_past)
      |> Repo.update!()

      {:ok, result} = Gardens.check_and_collect(player.uid, garden.id)

      assert result.status == :collected
      assert result.item == "Berry"
      assert result.amount == 3

      # Check item was added to inventory
      items = Economy.list_items(player.uid)
      berry = Enum.find(items, &(&1.item_name == "Berry"))
      assert berry != nil
      assert berry.count == 3
    end

    test "mature garden within yield interval returns not_ready" do
      player = setup_player()
      {:ok, garden} = Gardens.plant(player.uid, "BerryBush", 2, 0)

      # Set plant_time_utc past growth time but within yield interval
      # BerryBush: growth_hours=24, yield_interval_hours=12
      # Set 25 hours ago: mature=yes, but yield interval (12h from plant) hasn't passed since last yield
      past = DateTime.add(DateTime.utc_now(), -25 * 3600, :second) |> DateTime.truncate(:second)

      garden
      |> Ecto.Changeset.change(plant_time_utc: past)
      |> Repo.update!()

      # First collect should work (25h > 12h yield interval from plant_time)
      {:ok, first} = Gardens.check_and_collect(player.uid, garden.id)
      assert first.status == :collected

      # Second collect immediately should return not_ready
      {:ok, second} = Gardens.check_and_collect(player.uid, garden.id)
      assert second.status == :not_ready
    end
  end
end
