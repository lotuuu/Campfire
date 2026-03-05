defmodule CampFireWeb.VisitorControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  setup %{conn: conn} do
    alias CampFire.Repo
    alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule}

    Repo.insert!(%VisitorTemplate{
      visitor_id: "willow_gifter", name: "Willow", portrait_id: "willow",
      type: "gifter", flame_level_min: 1, weight: 1.5,
      dialogue_pool: [%{"lines" => ["Hello!", "Take this gift."]}],
      offer_pool: [], gift_pool: [%{"type" => "water", "amount" => 3}], quest_pool: []
    }, on_conflict: :nothing, conflict_target: :visitor_id)

    Repo.insert!(%VisitorTemplate{
      visitor_id: "ember_quester", name: "Ember", portrait_id: "ember",
      type: "quester", flame_level_min: 2, weight: 0.8,
      dialogue_pool: [%{"lines" => ["I need help."]}],
      offer_pool: [], gift_pool: [],
      quest_pool: [%{"request_item" => "Lavender Petal", "request_count" => 3, "return_days" => 7,
        "reward" => %{"type" => "seed", "name" => "Moonflower", "count" => 2},
        "return_dialogue" => ["Thanks!"]}]
    }, on_conflict: :nothing, conflict_target: :visitor_id)

    Repo.insert!(%VisitorSchedule{visitor_id: "willow_gifter", visit_number: 1, priority: 10})

    player = register_player()
    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player}
  end

  test "GET /visitors/tonight returns a visitor", %{conn: conn} do
    conn = get(conn, "/visitors/tonight")
    body = json_response(conn, 200)
    # First visit should get Willow (milestone visit #1)
    assert body["visitor_id"] == "willow_gifter"
    assert body["name"] == "Willow"
  end

  test "quest accept and complete flow", %{conn: conn, player: player} do
    conn1 = post(conn, "/visitors/quest/accept", %{
      visitor_id: "ember_quester",
      request_item: "Lavender Petal",
      request_count: 3,
      return_days: 0
    })
    body = json_response(conn1, 201)
    quest_id = body["quest_id"]
    assert quest_id

    # Complete immediately (return_days: 0)
    conn2 = build_conn() |> authed_conn(player) |> post("/visitors/quest/complete", %{quest_id: quest_id})
    assert json_response(conn2, 200)["reward"]
  end
end
