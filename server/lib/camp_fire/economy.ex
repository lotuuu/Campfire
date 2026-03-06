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
    case Repo.get(PlayerEconomy, player_uid) do
      nil ->
        {:error, :not_found}

      economy ->
        now = DateTime.utc_now() |> DateTime.truncate(:second)
        elapsed = max(DateTime.diff(now, economy.last_mana_collect_utc, :second), 0)

        mana_rate = @base_mana_per_second + (economy.flame_level - 1) * @mana_per_level
        earned = mana_rate * elapsed

        {1, [updated]} =
          from(e in PlayerEconomy, where: e.player_uid == ^player_uid, select: e)
          |> Repo.update_all(inc: [mana: earned], set: [last_mana_collect_utc: now])

        {:ok, updated}
    end
  end

  def spend_mana(player_uid, amount, opts \\ []) when is_number(amount) and amount > 0 do
    if opts[:free_mode] do
      case Repo.get(PlayerEconomy, player_uid) do
        nil -> {:error, :insufficient_mana}
        economy -> {:ok, economy}
      end
    else
      {count, results} =
        from(e in PlayerEconomy,
          where: e.player_uid == ^player_uid and e.mana >= ^amount,
          select: e
        )
        |> Repo.update_all(inc: [mana: -amount])

      if count == 1, do: {:ok, hd(results)}, else: {:error, :insufficient_mana}
    end
  end

  # --- Gems ---

  def add_gems(player_uid, amount) when is_integer(amount) and amount > 0 do
    {count, results} =
      from(e in PlayerEconomy, where: e.player_uid == ^player_uid, select: e)
      |> Repo.update_all(inc: [gems: amount])

    if count == 1, do: {:ok, hd(results)}, else: {:error, :not_found}
  end

  def spend_gems(player_uid, amount, opts \\ []) when is_integer(amount) and amount > 0 do
    if opts[:free_mode] do
      case Repo.get(PlayerEconomy, player_uid) do
        nil -> {:error, :insufficient_gems}
        economy -> {:ok, economy}
      end
    else
      {count, results} =
        from(e in PlayerEconomy,
          where: e.player_uid == ^player_uid and e.gems >= ^amount,
          select: e
        )
        |> Repo.update_all(inc: [gems: -amount])

      if count == 1, do: {:ok, hd(results)}, else: {:error, :insufficient_gems}
    end
  end

  # --- Flame ---

  def upgrade_flame(player_uid, required_items, opts \\ []) when is_list(required_items) do
    Repo.transaction(fn ->
      economy = Repo.get(PlayerEconomy, player_uid)

      cond do
        economy == nil -> Repo.rollback(:not_found)
        economy.flame_level >= @max_flame_level -> Repo.rollback(:max_level)
        true -> :ok
      end

      unless opts[:free_mode] do
        Enum.each(required_items, fn %{"item_name" => name, "count" => count} ->
          case spend_items_in_tx(player_uid, name, count) do
            :ok -> :ok
            {:error, reason} -> Repo.rollback(reason)
          end
        end)
      end

      now = DateTime.utc_now() |> DateTime.truncate(:second)

      {1, [updated]} =
        from(e in PlayerEconomy, where: e.player_uid == ^player_uid, select: e)
        |> Repo.update_all(inc: [flame_level: 1], set: [last_mana_collect_utc: now])

      updated
    end)
  end

  # --- Seeds ---

  def list_seeds(player_uid) do
    from(s in PlayerSeed, where: s.player_uid == ^player_uid) |> Repo.all()
  end

  def upsert_seed(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    %PlayerSeed{player_uid: player_uid, seed_name: seed_name, count: count}
    |> Repo.insert(
      on_conflict: [inc: [count: count]],
      conflict_target: [:player_uid, :seed_name],
      returning: true
    )
  end

  def spend_seed(player_uid, seed_name, count, opts \\ []) when is_integer(count) and count > 0 do
    if opts[:free_mode] do
      {:ok, :spent}
    else
      {updated, _} =
        from(s in PlayerSeed,
          where: s.player_uid == ^player_uid and s.seed_name == ^seed_name and s.count >= ^count
        )
        |> Repo.update_all(inc: [count: -count])

      if updated == 0 do
        {:error, :insufficient_seeds}
      else
        # Clean up zero-count rows
        from(s in PlayerSeed,
          where: s.player_uid == ^player_uid and s.seed_name == ^seed_name and s.count == 0
        )
        |> Repo.delete_all()

        {:ok, :spent}
      end
    end
  end

  # --- Items ---

  def list_items(player_uid) do
    from(i in PlayerItem, where: i.player_uid == ^player_uid) |> Repo.all()
  end

  def upsert_item(player_uid, item_name, count) when is_integer(count) and count > 0 do
    %PlayerItem{player_uid: player_uid, item_name: item_name, count: count}
    |> Repo.insert(
      on_conflict: [inc: [count: count]],
      conflict_target: [:player_uid, :item_name],
      returning: true
    )
  end

  def spend_item(player_uid, item_name, count, opts \\ []) when is_integer(count) and count > 0 do
    if opts[:free_mode] do
      {:ok, :spent}
    else
      {updated, _} =
        from(i in PlayerItem,
          where: i.player_uid == ^player_uid and i.item_name == ^item_name and i.count >= ^count
        )
        |> Repo.update_all(inc: [count: -count])

      if updated == 0 do
        {:error, :insufficient_items}
      else
        # Clean up zero-count rows
        from(i in PlayerItem,
          where: i.player_uid == ^player_uid and i.item_name == ^item_name and i.count == 0
        )
        |> Repo.delete_all()

        {:ok, :spent}
      end
    end
  end

  def spend_items(player_uid, items, opts \\ []) when is_list(items) do
    if opts[:free_mode] do
      {:ok, nil}
    else
      Repo.transaction(fn ->
        Enum.each(items, fn %{"item_name" => name, "count" => count} ->
          case spend_items_in_tx(player_uid, name, count) do
            :ok -> :ok
            {:error, reason} -> Repo.rollback(reason)
          end
        end)
      end)
    end
  end

  defp create_starter_buildings(player_uid) do
    alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerMallum, PlayerMallumHouse}

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

    %PlayerMallumHouse{}
    |> PlayerMallumHouse.changeset(%{
      player_uid: player_uid,
      grid_x: 1,
      grid_y: -1
    })
    |> Repo.insert!()

    mallum_house_config = CampFire.ConfigCache.get("mallum_house_config")
    mallums_per_house = (mallum_house_config && mallum_house_config["mallums_per_house"]) || 2

    for _ <- 1..mallums_per_house do
      %PlayerMallum{}
      |> PlayerMallum.changeset(%{
        player_uid: player_uid,
        state: "idle"
      })
      |> Repo.insert!()
    end
  end

  defp spend_items_in_tx(player_uid, item_name, count) do
    {updated, _} =
      from(i in PlayerItem,
        where: i.player_uid == ^player_uid and i.item_name == ^item_name and i.count >= ^count
      )
      |> Repo.update_all(inc: [count: -count])

    if updated == 0 do
      {:error, {:insufficient_items, item_name}}
    else
      from(i in PlayerItem,
        where: i.player_uid == ^player_uid and i.item_name == ^item_name and i.count == 0
      )
      |> Repo.delete_all()

      :ok
    end
  end
end
