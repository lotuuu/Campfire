defmodule CampFire.Economy do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy.{PlayerEconomy, PlayerInventory}

  defp flame_config! do
    CampFire.ConfigCache.get("flame_config") ||
      raise "flame_config not loaded in ConfigCache"
  end

  defp max_flame_level, do: flame_config!()["max_flame_level"]

  defp mana_rate(flame_level) do
    rates = flame_config!()["mana_rates"]
    index = max(flame_level - 1, 0) |> min(length(rates) - 1)
    Enum.at(rates, index)
  end

  defp mana_cap(flame_level) do
    caps = flame_config!()["mana_caps"]
    index = max(flame_level - 1, 0) |> min(length(caps) - 1)
    Enum.at(caps, index)
  end

  # --- Init ---

  def get_economy(player_uid) do
    Repo.get(PlayerEconomy, player_uid)
  end

  def init_economy(player_uid) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    npc = CampFire.ConfigCache.get("new_player_config") || %{}

    %PlayerEconomy{}
    |> PlayerEconomy.changeset(%{
      player_uid: player_uid,
      mana: (npc["mana"] || 50) * 1.0,
      gems: npc["gems"] || 5,
      flame_level: 1,
      last_mana_collect_utc: now
    })
    |> Repo.insert()
    |> case do
      {:ok, economy} ->
        for item <- npc["items"] || [] do
          upsert_item(player_uid, item["itemKey"], item["count"])
        end

        create_starter_buildings(player_uid, npc)
        {:ok, economy}

      {:error, changeset} ->
        {:error, changeset}
    end
  end

  # --- State ---

  def get_full_state(player_uid) do
    economy = Repo.get(PlayerEconomy, player_uid)
    inventory = list_inventory(player_uid)
    {economy, inventory}
  end

  # --- Mana ---

  def collect_mana(player_uid) do
    case Repo.get(PlayerEconomy, player_uid) do
      nil ->
        {:error, :not_found}

      economy ->
        now = DateTime.utc_now() |> DateTime.truncate(:second)
        elapsed = max(DateTime.diff(now, economy.last_mana_collect_utc, :second), 0)

        mana_rate = mana_rate(economy.flame_level)
        earned = mana_rate * elapsed
        cap = mana_cap(economy.flame_level)
        capped_mana = min(economy.mana + earned, cap)

        {1, [updated]} =
          from(e in PlayerEconomy, where: e.player_uid == ^player_uid, select: e)
          |> Repo.update_all(set: [mana: capped_mana, last_mana_collect_utc: now])

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
      # Flush accumulated passive mana into the stored value before spending
      collect_mana(player_uid)

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
        economy.flame_level >= max_flame_level() -> Repo.rollback(:max_level)
        true -> :ok
      end

      unless opts[:free_mode] do
        Enum.each(required_items, fn %{"itemKey" => name, "count" => count} ->
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

  # --- Inventory ---

  def list_inventory(player_uid) do
    from(i in PlayerInventory, where: i.player_uid == ^player_uid) |> Repo.all()
  end

  def upsert_item(player_uid, item_key, count) when is_integer(count) and count > 0 do
    %PlayerInventory{player_uid: player_uid, item_key: item_key, count: count}
    |> Repo.insert(
      on_conflict: [inc: [count: count]],
      conflict_target: [:player_uid, :item_key],
      returning: true
    )
  end

  def spend_item(player_uid, item_key, count, opts \\ []) when is_integer(count) and count > 0 do
    if opts[:free_mode] do
      {:ok, :spent}
    else
      {updated, _} =
        from(i in PlayerInventory,
          where: i.player_uid == ^player_uid and i.item_key == ^item_key and i.count >= ^count
        )
        |> Repo.update_all(inc: [count: -count])

      if updated == 0 do
        {:error, :insufficient_items}
      else
        # Clean up zero-count rows
        from(i in PlayerInventory,
          where: i.player_uid == ^player_uid and i.item_key == ^item_key and i.count == 0
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
        Enum.each(items, fn %{"itemKey" => name, "count" => count} ->
          case spend_items_in_tx(player_uid, name, count) do
            :ok -> :ok
            {:error, reason} -> Repo.rollback(reason)
          end
        end)
      end)
    end
  end

  defp create_starter_buildings(player_uid, npc) do
    alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerApotheke}

    # Pick 3 random distinct hex positions (excluding flame at 0,0)
    grid_radius = starter_grid_radius()
    positions = non_center_hex_positions(grid_radius) |> Enum.shuffle() |> Enum.take(3)
    [plot_pos, vase_pos, apotheke_pos] = positions

    %PlayerPlot{}
    |> PlayerPlot.changeset(%{
      player_uid: player_uid,
      state: "empty",
      grid_x: elem(plot_pos, 0),
      grid_y: elem(plot_pos, 1)
    })
    |> Repo.insert!()

    vase_config = CampFire.ConfigCache.get("vase_config")
    vase_capacity = (vase_config && vase_config["default_capacity"]) || 5
    starting_water = npc["starting_water"] || 1
    vase_state = if starting_water > 0, do: "full", else: "empty"

    %PlayerVase{}
    |> PlayerVase.changeset(%{
      player_uid: player_uid,
      state: vase_state,
      capacity: vase_capacity,
      current_water: starting_water,
      grid_x: elem(vase_pos, 0),
      grid_y: elem(vase_pos, 1)
    })
    |> Repo.insert!()

    %PlayerApotheke{}
    |> PlayerApotheke.changeset(%{
      player_uid: player_uid,
      grid_x: elem(apotheke_pos, 0),
      grid_y: elem(apotheke_pos, 1)
    })
    |> Repo.insert!()
  end

  defp starter_grid_radius do
    case CampFire.ConfigCache.get("flame_config") do
      nil -> 2
      config -> Enum.at(config["grid_sizes"] || [], 0, 2)
    end
  end

  defp non_center_hex_positions(radius) do
    for q <- -radius..radius,
        r <- -radius..radius,
        {q, r} != {0, 0},
        max(abs(q), max(abs(r), abs(q + r))) <= radius,
        do: {q, r}
  end

  defp spend_items_in_tx(player_uid, item_key, count) do
    {updated, _} =
      from(i in PlayerInventory,
        where: i.player_uid == ^player_uid and i.item_key == ^item_key and i.count >= ^count
      )
      |> Repo.update_all(inc: [count: -count])

    if updated == 0 do
      {:error, {:insufficient_items, item_key}}
    else
      from(i in PlayerInventory,
        where: i.player_uid == ^player_uid and i.item_key == ^item_key and i.count == 0
      )
      |> Repo.delete_all()

      :ok
    end
  end
end
