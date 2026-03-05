defmodule CampFireWeb.FriendController do
  use CampFireWeb, :controller
  alias CampFire.Social
  alias CampFire.Accounts

  def index(conn, _params) do
    friends = Social.list_friends(conn.assigns.current_player.uid)
    json(conn, %{friends: friends})
  end

  def create_request(conn, %{"friendCode" => friend_code}) do
    uid = conn.assigns.current_player.uid

    case Accounts.get_player_by_friend_code(friend_code) do
      nil ->
        conn |> put_status(404) |> json(%{error: "Player not found"})

      target ->
        case Social.send_request(uid, target.uid) do
          :ok -> conn |> put_status(201) |> json(%{message: "Friend request sent"})
          {:error, msg} -> conn |> put_status(400) |> json(%{error: msg})
        end
    end
  end

  def create_request(conn, _params) do
    conn |> put_status(400) |> json(%{error: "friendCode is required"})
  end

  def pending_requests(conn, _params) do
    requests = Social.pending_requests(conn.assigns.current_player.uid)
    json(conn, %{requests: requests})
  end

  def accept(conn, %{"request_id" => request_id}) do
    case Social.accept_request(request_id, conn.assigns.current_player.uid) do
      {:ok, friends} -> json(conn, %{friends: friends})
      {:error, msg} -> conn |> put_status(400) |> json(%{error: msg})
    end
  end

  def decline(conn, %{"request_id" => request_id}) do
    case Social.decline_request(request_id, conn.assigns.current_player.uid) do
      :ok -> json(conn, %{message: "Friend request declined"})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Friend request not found"})
    end
  end

  def remove(conn, %{"friend_uid" => friend_uid}) do
    case Social.remove_friend(conn.assigns.current_player.uid, friend_uid) do
      :ok -> json(conn, %{message: "Friend removed"})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Friend not found"})
    end
  end
end
