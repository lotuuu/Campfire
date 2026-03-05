defmodule CampFireWeb.VillageController do
  use CampFireWeb, :controller
  alias CampFire.Villages
  alias CampFire.Social

  def upsert(conn, %{"snapshot" => snapshot}) when is_map(snapshot) do
    case Villages.upsert_snapshot(conn.assigns.current_player.uid, snapshot) do
      {:ok, _} -> json(conn, %{message: "Village updated"})
      {:error, :too_large} -> conn |> put_status(413) |> json(%{error: "Village snapshot too large (max 100KB)"})
      {:error, _} -> conn |> put_status(500) |> json(%{error: "Failed to update village"})
    end
  end

  def upsert(conn, _params) do
    conn |> put_status(400) |> json(%{error: "snapshot must be a JSON object"})
  end

  def show(conn, %{"uid" => uid}) do
    current_uid = conn.assigns.current_player.uid

    if uid != current_uid and not Social.are_friends?(current_uid, uid) do
      conn |> put_status(403) |> json(%{error: "Not friends with this player"})
    else
      result = Villages.get_snapshot(uid)
      json(conn, %{snapshot: result.snapshot, updatedAt: result.updated_at})
    end
  end
end
