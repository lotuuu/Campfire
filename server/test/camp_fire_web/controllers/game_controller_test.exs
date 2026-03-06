defmodule CampFireWeb.GameControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers
  alias CampFire.Economy
  alias CampFire.Game.{Vases, Plots, Birds, MallumHouses}
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
    seed_mallum_house_config()
    player = register_player()
    {:ok, _economy} = Economy.init_economy(player.uid)

    ensure_seed_config()
    seed_quest_configs()
    seed_building_costs()
    seed_garden_configs()
    seed_flame_config()
    seed_seed_configs()

    # Boost mana and provide harvest items for crafting
    economy = Economy.get_economy(player.uid)
    economy |> Ecto.Changeset.change(mana: 2000.0) |> Repo.update!()
    Economy.upsert_item(player.uid, "Sprouts_harvest", 10)
    Economy.upsert_item(player.uid, "Basil_harvest", 10)
    Economy.upsert_item(player.uid, "Cress_harvest", 10)
    Economy.upsert_item(player.uid, "Chamomile_harvest", 10)

    {:ok, _} = Economy.upsert_seed(player.uid, "Basil", 5)

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
      assert Map.has_key?(body, "mallumHouses")
      assert Map.has_key?(body, "cosmeticState")
      assert is_list(body["plots"])
      assert is_list(body["vases"])
      assert is_list(body["gardens"])
      assert is_list(body["mallums"])
      assert is_list(body["mallumHouses"])
    end

    test "includes birds in response", %{conn: conn} do
      {_player, conn} = setup_player(conn)
      conn = get(conn, "/game/state")
      body = json_response(conn, 200)
      assert Map.has_key?(body, "birds")
      assert is_list(body["birds"])
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

      conn = post(conn, "/game/plot/craft", %{gridX: 2, gridY: 0})
      body = json_response(conn, 201)

      assert body["state"] == "empty"
      assert body["gridX"] == 2
      assert body["gridY"] == 0
      assert body["id"] != nil
    end
  end

  describe "full plot lifecycle" do
    test "craft -> plant -> water -> harvest", %{conn: conn} do
      {player, conn} = setup_player(conn)

      # Craft plot
      conn1 = post(conn, "/game/plot/craft", %{gridX: 2, gridY: 0})
      plot = json_response(conn1, 201)
      plot_id = plot["id"]
      assert plot["state"] == "empty"

      # Craft vase and fill it with water
      conn2 = build_conn() |> authed_conn(player) |> post("/game/vase/craft", %{gridX: 0, gridY: 2})
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

      conn = post(conn, "/game/vase/craft", %{gridX: 2, gridY: 0})
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
      conn1 = post(conn, "/game/vase/craft", %{gridX: 2, gridY: 0})
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

      conn = post(conn, "/game/garden/plant", %{plantName: "BerryBush", gridX: 2, gridY: 0})
      body = json_response(conn, 201)

      assert body["plantName"] == "BerryBush"
      assert body["mature"] == false
      assert body["id"] != nil
    end

    test "fails with unknown plant", %{conn: conn} do
      {_player, conn} = setup_player(conn)

      conn = post(conn, "/game/garden/plant", %{plantName: "FakeTree", gridX: 0, gridY: 2})
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

  describe "POST /game/mallum-house/craft" do
    test "creates house and returns serialized response", %{conn: conn} do
      {_player, conn} = setup_player(conn)
      conn = post(conn, "/game/mallum-house/craft", %{gridX: 2, gridY: 0})
      body = json_response(conn, 201)
      assert body["gridX"] == 2
      assert body["gridY"] == 0
      assert body["id"] != nil
    end
  end

  describe "POST /game/bird/check" do
    test "returns new birds list", %{conn: conn} do
      {_player, conn} = setup_player(conn)
      conn = post(conn, "/game/bird/check")
      body = json_response(conn, 200)
      assert Map.has_key?(body, "newBirds")
      assert is_list(body["newBirds"])
    end
  end

  describe "POST /game/bird/collect" do
    test "collects bird and returns reward", %{conn: conn} do
      {player, conn} = setup_player(conn)
      {:ok, bird} = Birds.insert_bird(player.uid, 2, 0, "Basil", 2)
      conn = post(conn, "/game/bird/collect", %{birdId: bird.id})
      body = json_response(conn, 200)
      assert body["seedName"] == "Basil"
      assert body["seedCount"] == 2
    end
  end

  describe "POST /game/apotheke/craft" do
    test "crafts recipe and returns result", %{conn: conn} do
      {player, conn} = setup_player(conn)
      seed_recipe_configs()
      Economy.upsert_item(player.uid, "Basil_harvest", 5)
      conn = post(conn, "/game/apotheke/craft", %{recipeName: "Fertilizer"})
      body = json_response(conn, 200)
      assert body["resultItem"] == "Fertilizer"
      assert body["resultQuantity"] == 1
    end

    test "rejects unknown recipe", %{conn: conn} do
      {_player, conn} = setup_player(conn)
      seed_recipe_configs()
      conn = post(conn, "/game/apotheke/craft", %{recipeName: "FakeRecipe"})
      body = json_response(conn, 422)
      assert body["error"] =~ "unknown_recipe"
    end
  end

  describe "POST /game/mallum-house/set-skin" do
    test "unlocks and applies skin", %{conn: conn} do
      {player, conn} = setup_player(conn)
      seed_skin_configs()
      Economy.upsert_item(player.uid, "Basil_harvest", 10)
      houses = MallumHouses.list_houses(player.uid)
      house = List.first(houses)
      conn = post(conn, "/game/mallum-house/set-skin", %{houseId: house.id, skinName: "CozyHouse"})
      body = json_response(conn, 200)
      assert body["skinName"] == "CozyHouse"
    end
  end

  describe "entity cap validation" do
    test "craft_plot returns error when at cap", %{conn: conn} do
      {_player, conn} = setup_player(conn)
      seed_flame_config_with_low_cap()
      conn = post(conn, "/game/plot/craft", %{gridX: 2, gridY: 0})
      body = json_response(conn, 422)
      assert body["error"] =~ "entity_cap"
    end
  end

  describe "auth required" do
    test "rejects unauthenticated requests", %{conn: conn} do
      conn = get(conn, "/game/state")
      assert json_response(conn, 401)
    end
  end
end
