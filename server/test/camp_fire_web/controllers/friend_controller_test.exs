defmodule CampFireWeb.FriendControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  setup %{conn: conn} do
    player = register_player()
    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player}
  end

  test "GET /friends returns empty list initially", %{conn: conn} do
    conn = get(conn, "/friends")
    assert json_response(conn, 200)["friends"] == []
  end

  test "full friend request flow", %{conn: conn, player: player} do
    other = register_player()

    # Send request
    conn1 = post(conn, "/friends/request", %{friendCode: other.friend_code})
    assert json_response(conn1, 201)["message"] == "Friend request sent"

    # Other player sees pending request
    other_conn = build_conn() |> authed_conn(other)
    conn2 = get(other_conn, "/friends/requests")
    [request] = json_response(conn2, 200)["requests"]
    assert request["from_uid"] == player.uid

    # Accept
    other_conn2 = build_conn() |> authed_conn(other)
    conn3 = post(other_conn2, "/friends/accept/#{request["id"]}")
    friends = json_response(conn3, 200)["friends"]
    assert length(friends) == 1

    # Both see each other
    conn4 = build_conn() |> authed_conn(player) |> get("/friends")
    assert length(json_response(conn4, 200)["friends"]) == 1
  end

  test "cannot friend yourself", %{conn: conn, player: player} do
    conn = post(conn, "/friends/request", %{friendCode: player.friend_code})
    assert json_response(conn, 400)["error"] =~ "yourself"
  end
end
