defmodule CampFireWeb.AuthControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  describe "POST /auth/register" do
    test "creates a new player", %{conn: conn} do
      conn = post(conn, "/auth/register")
      body = json_response(conn, 201)

      assert body["uid"]
      assert body["authToken"]
      assert body["friendCode"]
      assert body["displayName"] == "Camper"
    end
  end

  describe "PUT /auth/display-name" do
    test "updates display name with valid auth", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> put("/auth/display-name", %{displayName: "NewName"})

      assert json_response(conn, 200)["displayName"] == "NewName"
    end

    test "rejects invalid characters", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> put("/auth/display-name", %{displayName: "bad!name"})

      assert json_response(conn, 400)["error"]
    end

    test "rejects without auth", %{conn: conn} do
      conn = put(conn, "/auth/display-name", %{displayName: "Test"})
      assert json_response(conn, 401)
    end
  end
end
