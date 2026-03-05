defmodule CampFireWeb.GiftController do
  use CampFireWeb, :controller
  alias CampFire.Gifts
  alias CampFire.Social

  def send_gift(conn, %{"toUid" => to_uid, "items" => items}) do
    from_uid = conn.assigns.current_player.uid

    if not Social.are_friends?(from_uid, to_uid) do
      conn |> put_status(403) |> json(%{error: "Not friends with this player"})
    else
      case Gifts.send_gift(from_uid, to_uid, items) do
        {:ok, gift} ->
          conn |> put_status(201) |> json(%{giftId: gift.id, createdAt: gift.inserted_at})

        {:error, msg} when is_binary(msg) ->
          conn |> put_status(400) |> json(%{error: msg})

        {:error, _} ->
          conn |> put_status(500) |> json(%{error: "Failed to send gift"})
      end
    end
  end

  def send_gift(conn, _params) do
    conn |> put_status(400) |> json(%{error: "toUid and items are required"})
  end

  def index(conn, _params) do
    gifts = Gifts.pending_gifts(conn.assigns.current_player.uid)
    json(conn, %{gifts: gifts})
  end

  def claim(conn, %{"gift_id" => gift_id}) do
    case Gifts.claim_gift(gift_id, conn.assigns.current_player.uid) do
      {:ok, items} -> json(conn, %{items: items})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Gift not found or already claimed"})
      {:error, _} -> conn |> put_status(500) |> json(%{error: "Failed to claim gift"})
    end
  end
end
