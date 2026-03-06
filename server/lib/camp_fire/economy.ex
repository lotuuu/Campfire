defmodule CampFire.Economy do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy.{PlayerEconomy, PlayerSeed, PlayerItem}

  @base_mana_per_second 0.5
  @mana_per_level 0.3
  @max_flame_level 12

  # --- Init ---

  def get_economy(player_uid) do
    Repo.get(PlayerEconomy, player_uid)
  end

  def init_economy(player_uid) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)

    %PlayerEconomy{}
    |> PlayerEconomy.changeset(%{
      player_uid: player_uid,
      mana: 50.0,
      gems: 5,
      flame_level: 1,
      last_mana_collect_utc: now
    })
    |> Repo.insert()
    |> case do
      {:ok, economy} ->
        upsert_seed(player_uid, "Sprouts", 5)
        upsert_seed(player_uid, "Cress", 3)
        upsert_item(player_uid, "Speed_Potion", 3)
        create_starter_buildings(player_uid)
        {:ok, economy}

      {:error, changeset} ->
        {:error, changeset}
    end
  end

  # --- State ---

  def get_full_state(player_uid) do
    economy = Repo.get(PlayerEconomy, player_uid)
    seeds = list_seeds(player_uid)
    items = list_items(player_uid)
    {economy, seeds, items}
  end

  # --- Mana ---

  def collect_mana(player_uid) do
    economy = Repo.get!(PlayerEconomy, player_uid)
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    elapsed = max(DateTime.diff(now, economy.last_mana_collect_utc, :second), 0)

    mana_rate = @base_mana_per_second + (economy.flame_level - 1) * @mana_per_level
    earned = mana_rate * elapsed

    economy
    |> PlayerEconomy.changeset(%{mana: economy.mana + earned, last_mana_collect_utc: now})
    |> Repo.update()
  end

  def spend_mana(player_uid, amount) when is_number(amount) and amount > 0 do
    economy = Repo.get!(PlayerEconomy, player_uid)

    if economy.mana < amount do
      {:error, :insufficient_mana}
    else
      economy
      |> PlayerEconomy.changeset(%{mana: economy.mana - amount})
      |> Repo.update()
    end
  end

  # --- Gems ---

  def add_gems(player_uid, amount) when is_integer(amount) and amount > 0 do
    economy = Repo.get!(PlayerEconomy, player_uid)

    economy
    |> PlayerEconomy.changeset(%{gems: economy.gems + amount})
    |> Repo.update()
  end

  def spend_gems(player_uid, amount) when is_integer(amount) and amount > 0 do
    economy = Repo.get!(PlayerEconomy, player_uid)

    if economy.gems < amount do
      {:error, :insufficient_gems}
    else
      economy
      |> PlayerEconomy.changeset(%{gems: economy.gems - amount})
      |> Repo.update()
    end
  end

  # --- Flame ---

  def upgrade_flame(player_uid, required_items) when is_list(required_items) do
    Repo.transaction(fn ->
      economy = Repo.get!(PlayerEconomy, player_uid)

      if economy.flame_level >= @max_flame_level do
        Repo.rollback(:max_level)
      end

      Enum.each(required_items, fn %{"item_name" => name, "count" => count} ->
        case spend_items_in_tx(player_uid, name, count) do
          :ok -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end
      end)

      now = DateTime.utc_now() |> DateTime.truncate(:second)

      economy
      |> PlayerEconomy.changeset(%{flame_level: economy.flame_level + 1, last_mana_collect_utc: now})
      |> Repo.update!()
    end)
  end

  # --- Seeds ---

  def list_seeds(player_uid) do
    from(s in PlayerSeed, where: s.player_uid == ^player_uid) |> Repo.all()
  end

  def upsert_seed(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from s in PlayerSeed, where: s.player_uid == ^player_uid and s.seed_name == ^seed_name) do
      nil ->
        %PlayerSeed{}
        |> PlayerSeed.changeset(%{player_uid: player_uid, seed_name: seed_name, count: count})
        |> Repo.insert()

      existing ->
        existing
        |> PlayerSeed.changeset(%{count: existing.count + count})
        |> Repo.update()
    end
  end

  def spend_seed(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from s in PlayerSeed, where: s.player_uid == ^player_uid and s.seed_name == ^seed_name) do
      nil -> {:error, :insufficient_seeds}
      existing when existing.count < count -> {:error, :insufficient_seeds}
      existing when existing.count == count ->
        Repo.delete(existing)
        {:ok, :deleted}
      existing ->
        existing |> PlayerSeed.changeset(%{count: existing.count - count}) |> Repo.update()
    end
  end

  # --- Items ---

  def list_items(player_uid) do
    from(i in PlayerItem, where: i.player_uid == ^player_uid) |> Repo.all()
  end

  def upsert_item(player_uid, item_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from i in PlayerItem, where: i.player_uid == ^player_uid and i.item_name == ^item_name) do
      nil ->
        %PlayerItem{}
        |> PlayerItem.changeset(%{player_uid: player_uid, item_name: item_name, count: count})
        |> Repo.insert()

      existing ->
        existing
        |> PlayerItem.changeset(%{count: existing.count + count})
        |> Repo.update()
    end
  end

  def spend_item(player_uid, item_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from i in PlayerItem, where: i.player_uid == ^player_uid and i.item_name == ^item_name) do
      nil -> {:error, :insufficient_items}
      existing when existing.count < count -> {:error, :insufficient_items}
      existing when existing.count == count ->
        Repo.delete(existing)
        {:ok, :deleted}
      existing ->
        existing |> PlayerItem.changeset(%{count: existing.count - count}) |> Repo.update()
    end
  end

  def spend_items(player_uid, items) when is_list(items) do
    Repo.transaction(fn ->
      Enum.each(items, fn %{"item_name" => name, "count" => count} ->
        case spend_items_in_tx(player_uid, name, count) do
          :ok -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end
      end)
    end)
  end

  defp create_starter_buildings(player_uid) do
    alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerMallum}

    %PlayerPlot{}
    |> PlayerPlot.changeset(%{
      player_uid: player_uid,
      state: "empty",
      grid_x: -1,
      grid_y: 0
    })
    |> Repo.insert!()

    %PlayerVase{}
    |> PlayerVase.changeset(%{
      player_uid: player_uid,
      state: "full",
      capacity: 5,
      current_water: 5,
      grid_x: 0,
      grid_y: -1
    })
    |> Repo.insert!()

    %PlayerMallum{}
    |> PlayerMallum.changeset(%{
      player_uid: player_uid,
      state: "idle"
    })
    |> Repo.insert!()
  end

  defp spend_items_in_tx(player_uid, item_name, count) do
    case Repo.one(from i in PlayerItem, where: i.player_uid == ^player_uid and i.item_name == ^item_name) do
      nil -> {:error, {:insufficient_items, item_name}}
      existing when existing.count < count -> {:error, {:insufficient_items, item_name}}
      existing when existing.count == count ->
        Repo.delete!(existing)
        :ok
      existing ->
        existing |> PlayerItem.changeset(%{count: existing.count - count}) |> Repo.update!()
        :ok
    end
  end
end
