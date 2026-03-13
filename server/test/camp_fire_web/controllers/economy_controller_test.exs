defmodule CampFireWeb.EconomyControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers
  alias CampFire.Economy

  setup do
    seed_items()
    seed_new_player_config()
    seed_flame_config()
    seed_mallum_house_config()
    :ok
  end

  describe "GET /economy/state" do
    test "returns 404 when not initialized", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> get("/economy/state")

      assert json_response(conn, 404)["error"]
    end

    test "returns full state when initialized", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> get("/economy/state")

      body = json_response(conn, 200)
      assert body["mana"] == 50.0
      assert body["gems"] == 5
      assert body["flameLevel"] == 1
      assert is_list(body["inventory"])
    end
  end

  describe "POST /economy/init" do
    test "initializes economy with defaults", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/init")

      body = json_response(conn, 201)
      assert body["mana"] == 50.0
      assert body["gems"] == 5
      assert body["flameLevel"] == 1
      assert is_list(body["inventory"])
      # 3 distinct items: sprouts_seed, cress_seed, speed_potion
      assert length(body["inventory"]) == 3
    end

    test "rejects double init", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/init")

      assert json_response(conn, 409)["error"]
    end
  end

  describe "POST /economy/spend-mana" do
    test "deducts mana", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/spend-mana", %{amount: 20.0})

      body = json_response(conn, 200)
      assert body["mana"] == 30.0
    end

    test "rejects when insufficient", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/spend-mana", %{amount: 999.0})

      assert json_response(conn, 422)["error"] =~ "Insufficient"
    end
  end

  describe "POST /economy/add-items and spend-items" do
    test "adds and spends items", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn1 =
        conn
        |> authed_conn(player)
        |> post("/economy/add-items", %{item_key: "basil_seed", count: 5})

      body = json_response(conn1, 200)
      basil = Enum.find(body["inventory"], &(&1["itemKey"] == "basil_seed"))
      assert basil["count"] == 5

      conn2 =
        build_conn()
        |> authed_conn(player)
        |> post("/economy/spend-items", %{items: [%{itemKey: "basil_seed", count: 2}]})

      body = json_response(conn2, 200)
      basil = Enum.find(body["inventory"], &(&1["itemKey"] == "basil_seed"))
      assert basil["count"] == 3
    end
  end

  describe "POST /economy/upgrade-flame" do
    test "upgrades flame level", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)
      Economy.upsert_item(player.uid, "sprouts", 5)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/upgrade-flame", %{items: [%{itemKey: "sprouts", count: 1}]})

      body = json_response(conn, 200)
      assert body["flameLevel"] == 2
    end
  end

  describe "auth required" do
    test "rejects unauthenticated requests", %{conn: conn} do
      conn = get(conn, "/economy/state")
      assert json_response(conn, 401)
    end
  end
end
