defmodule CampFireWeb.VillageControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  setup %{conn: conn} do
    player = register_player()
    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player}
  end

  test "upsert and read own village", %{conn: conn, player: player} do
    snapshot = %{flameLevel: 3, plots: []}

    conn1 = put(conn, "/village", %{snapshot: snapshot})
    assert json_response(conn1, 200)["message"] == "Village updated"

    conn2 = build_conn() |> authed_conn(player) |> get("/village/#{player.uid}")
    body = json_response(conn2, 200)
    assert body["snapshot"]["flameLevel"] == 3
  end

  test "cannot read non-friend village", %{conn: conn} do
    other = register_player()
    conn = get(conn, "/village/#{other.uid}")
    assert json_response(conn, 403)
  end
end
