defmodule CampFireWeb.DebugController do
  use CampFireWeb, :controller
  alias CampFire.Game.Debug

  def skip_time(conn, %{"hours" => hours}) when is_number(hours) do
    uid = conn.assigns.current_player.uid
    case Debug.skip_time(uid, hours) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true, hours: hours})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end
  def skip_time(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'hours' (number)"})

  def set_currency(conn, params) do
    uid = conn.assigns.current_player.uid
    opts = []
    opts = if is_number(params["mana"]), do: Keyword.put(opts, :mana, params["mana"] / 1), else: opts
    opts = if is_integer(params["gems"]), do: Keyword.put(opts, :gems, params["gems"]), else: opts
    case Debug.set_currency(uid, opts) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end

  def grant_seeds(conn, %{"seedName" => name, "count" => count}) when is_integer(count) do
    uid = conn.assigns.current_player.uid
    Debug.grant_seeds(uid, name, count)
    conn |> put_status(200) |> json(%{ok: true})
  end
  def grant_seeds(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'seedName' and 'count'"})

  def grant_items(conn, %{"itemKey" => key, "count" => count}) when is_integer(count) do
    uid = conn.assigns.current_player.uid
    Debug.grant_items(uid, key, count)
    conn |> put_status(200) |> json(%{ok: true})
  end
  def grant_items(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'itemKey' and 'count'"})

  def spawn_bird(conn, _params) do
    uid = conn.assigns.current_player.uid
    case Debug.spawn_bird(uid) do
      {:ok, bird} -> conn |> put_status(200) |> json(%{ok: true, birdId: bird.id})
      :no_tile -> conn |> put_status(422) |> json(%{error: "no_free_tile"})
      :no_seed -> conn |> put_status(422) |> json(%{error: "no_eligible_seed"})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end

  def complete_quests(conn, _params) do
    uid = conn.assigns.current_player.uid
    {:ok, count} = Debug.complete_quests(uid)
    conn |> put_status(200) |> json(%{ok: true, completed: count})
  end

  def fill_vases(conn, _params) do
    uid = conn.assigns.current_player.uid
    {:ok, count} = Debug.fill_vases(uid)
    conn |> put_status(200) |> json(%{ok: true, filled: count})
  end

  def mature_plots(conn, _params) do
    uid = conn.assigns.current_player.uid
    {:ok, count} = Debug.mature_plots(uid)
    conn |> put_status(200) |> json(%{ok: true, matured: count})
  end

  def set_flame_level(conn, %{"level" => level}) when is_integer(level) do
    uid = conn.assigns.current_player.uid
    case Debug.set_flame_level(uid, level) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end
  def set_flame_level(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'level' (integer)"})

  def clear_save(conn, _params) do
    uid = conn.assigns.current_player.uid
    case Debug.clear_save(uid) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end

  def log(conn, %{"message" => message} = params) do
    uid = conn.assigns.current_player.uid

    level =
      case params["level"] do
        "warning" -> :warning
        "info" -> :info
        _ -> :error
      end

    CampFire.DebugLog.log(%{
      level: level,
      source: :client,
      category: params["category"] || "client",
      message: message,
      player_uid: uid,
      metadata: params["metadata"] || %{}
    })

    conn |> put_status(200) |> json(%{ok: true})
  end

  def log(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'message'"})
end
