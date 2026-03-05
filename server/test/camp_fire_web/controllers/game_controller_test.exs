defmodule CampFireWeb.GameControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers
  alias CampFire.Economy
  alias CampFire.Game.{Mallums, Vases, Plots}
  alias CampFire.Repo

  defp ensure_seed_config do
    alias CampFire.Game.SeedConfig
    import Ecto.Query

    unless Repo.one(from sc in SeedConfig, where: sc.seed_name == "Basil") do
      %SeedConfig{}
      |> SeedConfig.changeset(%{
        seed_name: "Basil",
        growth_duration_hours: 0.001,
        base_drops: 2,
        recipe: %{}
      })
      |> Repo.insert!()
    end
  end

  defp setup_player(conn) do
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    ensure_seed_config()

    {:ok, _} = Economy.upsert_seed(player.uid, "Basil", 5)
    {:ok, _mallum} = Mallums.create_mallum(player.uid)

    {player, authed_conn(conn, player)}
  end

  describe "GET /game/state" do
    test "returns full state with all keys", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = get(conn, "/game/state")
      body = json_response(conn, 200)

      assert Map.has_key?(body, "economy")
      assert Map.has_key?(body, "plots")
      assert Map.has_key?(body, "vases")
      assert Map.has_key?(body, "gardens")
      assert Map.has_key?(body, "mallums")
      assert Map.has_key?(body, "cosmeticState")
      assert is_list(body["plots"])
      assert is_list(body["vases"])
      assert is_list(body["gardens"])
      assert is_list(body["mallums"])
    end
  end

  describe "PUT /game/state" do
    test "saves cosmetic data", %{conn: conn} do
      {player, conn} = setup_player(conn)

      conn = put(conn, "/game/state", %{data: %{theme: "dark", music: false}})
      body = json_response(conn, 200)
      assert body["ok"] == true

      # Verify it's persisted by reading state back
      conn2 = build_conn() |> authed_conn(player) |> get("/game/state")
      state = json_response(conn2, 200)
      assert state["cosmeticState"]["theme"] == "dark"
    end
  end

  describe "POST /game/plot/craft" do
    test "returns 201 with plot", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/plot/craft", %{gridX: 0, gridY: 0})
      body = json_response(conn, 201)

      assert body["state"] == "empty"
      assert body["gridX"] == 0
      assert body["gridY"] == 0
      assert body["id"] != nil
    end
  end

  describe "full plot lifecycle" do
    test "craft -> plant -> water -> harvest", %{conn: conn} do
      {player, conn} = setup_player(conn)

      # Craft plot
      conn1 = post(conn, "/game/plot/craft", %{gridX: 0, gridY: 0})
      plot = json_response(conn1, 201)
      plot_id = plot["id"]
      assert plot["state"] == "empty"

      # Craft vase and fill it with water
      conn2 = build_conn() |> authed_conn(player) |> post("/game/vase/craft", %{gridX: 1, gridY: 0})
      vase = json_response(conn2, 201)
      vase_id = vase["id"]

      # Directly set water on vase for testing
      {:ok, _} = Vases.set_water(vase_id, 5)

      # Plant seed
      conn3 = build_conn() |> authed_conn(player) |> post("/game/plot/plant", %{plotId: plot_id, seedName: "Basil"})
      planted = json_response(conn3, 200)
      assert planted["state"] == "growing"
      assert planted["seedName"] == "Basil"

      # Water plot
      conn4 = build_conn() |> authed_conn(player) |> post("/game/plot/water", %{plotId: plot_id, vaseId: vase_id})
      watered = json_response(conn4, 200)
      assert watered["waterCount"] == 1

      # Force mature for testing
      {:ok, _} = Plots.force_mature(plot_id)

      # Harvest
      conn5 = build_conn() |> authed_conn(player) |> post("/game/plot/harvest", %{plotId: plot_id})
      harvest = json_response(conn5, 200)
      assert harvest["itemName"] == "Basil_harvest"
      assert harvest["drops"] >= 1
      assert harvest["score"] >= 0.0
    end
  end

  describe "POST /game/vase/craft" do
    test "returns 201", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/vase/craft", %{gridX: 0, gridY: 0})
      body = json_response(conn, 201)

      assert body["state"] == "empty"
      assert body["capacity"] == 5
      assert body["currentWater"] == 0
    end
  end

  describe "POST /game/vase/fill" do
    test "starts filling", %{conn: conn} do
      {player, conn} = setup_player(conn)

      # Craft vase
      conn1 = post(conn, "/game/vase/craft", %{gridX: 0, gridY: 0})
      vase = json_response(conn1, 201)

      # Start fill
      conn2 = build_conn() |> authed_conn(player) |> post("/game/vase/fill", %{vaseId: vase["id"]})
      filling = json_response(conn2, 200)

      assert filling["state"] == "filling"
      assert filling["fillStartTimeUtc"] != nil
    end
  end

  describe "POST /game/garden/plant" do
    test "returns 201", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/garden/plant", %{plantName: "BerryBush", gridX: 0, gridY: 0})
      body = json_response(conn, 201)

      assert body["plantName"] == "BerryBush"
      assert body["mature"] == false
      assert body["id"] != nil
    end

    test "fails with unknown plant", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/garden/plant", %{plantName: "FakeTree", gridX: 0, gridY: 0})
      assert json_response(conn, 422)["error"] =~ "unknown_plant"
    end
  end

  describe "POST /game/quest/start" do
    test "starts quest", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/quest/start", %{questName: "SwampForage"})
      body = json_response(conn, 200)

      assert body["state"] == "on_quest"
      assert body["assignedQuestName"] == "SwampForage"
      assert body["startTimeUtc"] != nil
    end

    test "fails with insufficient flame level", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/quest/start", %{questName: "MeadowExpedition"})
      assert json_response(conn, 422)["error"] =~ "insufficient_flame_level"
    end
  end

  describe "POST /game/quest/speed-up" do
    test "with Speed_Potion completes quest", %{conn: conn} do
      {player, conn} = setup_player(conn)

      # Start quest
      conn1 = post(conn, "/game/quest/start", %{questName: "SwampForage"})
      mallum = json_response(conn1, 200)

      # Speed up
      conn2 = build_conn() |> authed_conn(player) |> post("/game/quest/speed-up", %{mallumId: mallum["id"]})
      sped_up = json_response(conn2, 200)

      assert sped_up["state"] == "quest_complete"
      assert is_list(sped_up["pendingRewards"])
    end
  end

  describe "POST /weather/location" do
    test "saves location", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/weather/location", %{lat: 37.7749, lon: -122.4194})
      body = json_response(conn, 200)
      assert body["ok"] == true
    end

    test "rejects invalid params", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/weather/location", %{lat: "not a number", lon: "bad"})
      assert json_response(conn, 400)["error"]
    end
  end

  describe "GET /weather/current" do
    test "returns 404 when no location", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = get(conn, "/weather/current")
      assert json_response(conn, 404)["error"] =~ "No location"
    end
  end

  describe "auth required" do
    test "rejects unauthenticated requests", %{conn: conn} do
      conn = get(conn, "/game/state")
      assert json_response(conn, 401)
    end
  end
end
