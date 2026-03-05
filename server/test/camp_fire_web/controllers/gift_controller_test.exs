defmodule CampFireWeb.GiftControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers
  alias CampFire.Social

  setup %{conn: conn} do
    player = register_player()
    friend = register_player()

    # Make them friends directly via context
    :ok = Social.send_request(player.uid, friend.uid)
    [req] = Social.pending_requests(friend.uid)
    {:ok, _friends} = Social.accept_request(req.id, friend.uid)

    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player, friend: friend}
  end

  test "send and claim gift flow", %{conn: conn, friend: friend} do
    # Send gift
    conn1 = post(conn, "/gifts/send", %{toUid: friend.uid, items: [%{type: "seed", name: "Basil"}]})
    body = json_response(conn1, 201)
    assert body["giftId"]

    # Friend sees pending gift
    friend_conn = build_conn() |> authed_conn(friend)
    conn2 = get(friend_conn, "/gifts")
    [gift] = json_response(conn2, 200)["gifts"]

    # Claim
    friend_conn2 = build_conn() |> authed_conn(friend)
    conn3 = post(friend_conn2, "/gifts/claim/#{gift["id"]}")
    assert json_response(conn3, 200)["items"]
  end

  test "cannot send to non-friend", %{conn: conn} do
    stranger = register_player()
    conn = post(conn, "/gifts/send", %{toUid: stranger.uid, items: [%{type: "seed"}]})
    assert json_response(conn, 403)
  end
end
