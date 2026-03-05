defmodule CampFireWeb.VisitorController do
  use CampFireWeb, :controller
  alias CampFire.Visitors

  def tonight(conn, _params) do
    visitor = Visitors.get_tonight_visitor(conn.assigns.current_player.uid)
    json(conn, visitor)
  end

  def accept_quest(conn, params) do
    uid = conn.assigns.current_player.uid

    required = ["visitor_id", "request_item", "request_count", "return_days"]
    missing = Enum.filter(required, &(not Map.has_key?(params, &1)))

    if missing != [] do
      conn |> put_status(400) |> json(%{error: "#{Enum.join(missing, ", ")} required"})
    else
      case Visitors.accept_quest(uid, params) do
        {:ok, result} -> conn |> put_status(201) |> json(result)
        {:error, msg} -> conn |> put_status(500) |> json(%{error: msg})
      end
    end
  end

  def complete_quest(conn, %{"quest_id" => quest_id}) do
    uid = conn.assigns.current_player.uid

    case Visitors.complete_quest(uid, quest_id) do
      {:ok, reward} -> json(conn, %{reward: reward})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Quest not found"})
      {:error, msg} -> conn |> put_status(500) |> json(%{error: msg})
    end
  end

  def complete_quest(conn, _params) do
    conn |> put_status(400) |> json(%{error: "quest_id is required"})
  end
end
