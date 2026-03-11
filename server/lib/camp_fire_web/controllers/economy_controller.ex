defmodule CampFireWeb.EconomyController do
  use CampFireWeb, :controller
  alias CampFire.Economy

  def state(conn, _params) do
    player_uid = conn.assigns.current_player.uid

    case Economy.get_economy(player_uid) do
      nil ->
        conn |> put_status(404) |> json(%{error: "No economy record. Call POST /economy/init first."})

      economy ->
        {_economy, inventory} = Economy.get_full_state(player_uid)
        conn |> put_status(200) |> json(format_state(economy, inventory))
    end
  end

  def init(conn, _params) do
    player_uid = conn.assigns.current_player.uid

    if Economy.get_economy(player_uid) do
      conn |> put_status(409) |> json(%{error: "Economy already initialized"})
    else
      case Economy.init_economy(player_uid) do
        {:ok, economy} ->
          {_economy, inventory} = Economy.get_full_state(player_uid)
          conn |> put_status(201) |> json(format_state(economy, inventory))

        {:error, _changeset} ->
          conn |> put_status(422) |> json(%{error: "Failed to initialize economy"})
      end
    end
  end

  def collect_mana(conn, _params) do
    player_uid = conn.assigns.current_player.uid

    case Economy.collect_mana(player_uid) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{mana: economy.mana})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to collect mana"})
    end
  end

  def spend_mana(conn, %{"amount" => amount} = params) when is_number(amount) do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_mana(player_uid, amount, free_mode_opts(params)) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{mana: economy.mana})

      {:error, :insufficient_mana} ->
        conn |> put_status(422) |> json(%{error: "Insufficient mana"})
    end
  end

  def spend_mana(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'amount'"})
  end

  def spend_gems(conn, %{"amount" => amount} = params) when is_integer(amount) do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_gems(player_uid, amount, free_mode_opts(params)) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{gems: economy.gems})

      {:error, :insufficient_gems} ->
        conn |> put_status(422) |> json(%{error: "Insufficient gems"})
    end
  end

  def spend_gems(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'amount'"})
  end

  def add_gems(conn, %{"amount" => amount}) when is_integer(amount) and amount > 0 do
    player_uid = conn.assigns.current_player.uid

    case Economy.add_gems(player_uid, amount) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{gems: economy.gems})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to add gems"})
    end
  end

  def add_gems(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'amount'"})
  end

  def upgrade_flame(conn, %{"items" => items} = params) when is_list(items) do
    player_uid = conn.assigns.current_player.uid

    case Economy.upgrade_flame(player_uid, items, free_mode_opts(params)) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{flameLevel: economy.flame_level})

      {:error, :max_level} ->
        conn |> put_status(422) |> json(%{error: "Already at max flame level"})

      {:error, {:insufficient_items, name}} ->
        conn |> put_status(422) |> json(%{error: "Insufficient items: #{name}"})
    end
  end

  def upgrade_flame(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'items' array"})
  end

  def add_items(conn, %{"item_name" => name, "count" => count})
      when is_binary(name) and is_integer(count) and count > 0 do
    player_uid = conn.assigns.current_player.uid

    case Economy.upsert_item(player_uid, name, count) do
      {:ok, _} ->
        inventory = Economy.list_inventory(player_uid)
        conn |> put_status(200) |> json(%{inventory: format_inventory(inventory)})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to add item"})
    end
  end

  def add_items(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'item_name' (string) and 'count' (positive integer)"})
  end

  def spend_items(conn, %{"items" => items} = params) when is_list(items) do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_items(player_uid, items, free_mode_opts(params)) do
      {:ok, _} ->
        inventory = Economy.list_inventory(player_uid)
        conn |> put_status(200) |> json(%{inventory: format_inventory(inventory)})

      {:error, {:insufficient_items, name}} ->
        conn |> put_status(422) |> json(%{error: "Insufficient items: #{name}"})
    end
  end

  def spend_items(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'items' array"})
  end

  defp free_mode_opts(params) do
    if params["freeMode"], do: [free_mode: true], else: []
  end

  defp format_state(economy, inventory) do
    %{
      mana: economy.mana,
      gems: economy.gems,
      flameLevel: economy.flame_level,
      lastManaCollectUtc: DateTime.to_iso8601(economy.last_mana_collect_utc),
      inventory: format_inventory(inventory)
    }
  end

  defp format_inventory(inventory) do
    Enum.map(inventory, fn i -> %{itemName: i.item_name, count: i.count} end)
  end
end
