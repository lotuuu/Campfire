defmodule CampFire.TestHelpers do
  alias CampFire.Accounts

  def register_player do
    {:ok, player} = Accounts.register_player()
    player
  end

  def seed_quest_configs do
    quests = [
      {"SwampForage", %{quest_name: "SwampForage", duration_minutes: 5, required_flame_level: 1, reward_rolls: 2,
        reward_pool: [%{"seed_name" => "Basil", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Chamomile", "weight" => 2, "min" => 1, "max" => 2}]}},
      {"MeadowExpedition", %{quest_name: "MeadowExpedition", duration_minutes: 15, required_flame_level: 2, reward_rolls: 3,
        reward_pool: [%{"seed_name" => "Marigold", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Snowdrop", "weight" => 2, "min" => 1, "max" => 2}]}},
      {"DeepWoodsTrek", %{quest_name: "DeepWoodsTrek", duration_minutes: 60, required_flame_level: 3, reward_rolls: 3,
        reward_pool: [%{"seed_name" => "Mint", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Pansy", "weight" => 2, "min" => 1, "max" => 1}]}}
    ]

    # Write directly to ETS since ConfigCache GenServer can't see the test sandbox
    quest_map = Map.new(quests, fn {name, q} -> {name, q} end)
    :ets.insert(:config_cache, {"quest_configs", quest_map})
  end

  def seed_building_costs do
    config = %{
      "plot_costs" => [
        %{"mana_cost" => 150, "harvest_costs" => [%{"item_name" => "Sprouts_harvest", "count" => 1}]},
        %{"mana_cost" => 200, "harvest_costs" => [%{"item_name" => "Basil_harvest", "count" => 1}]},
        %{"mana_cost" => 260, "harvest_costs" => [%{"item_name" => "Basil_harvest", "count" => 2}]},
        %{"mana_cost" => 330, "harvest_costs" => [%{"item_name" => "Chamomile_harvest", "count" => 1}]}
      ],
      "vase_costs" => [
        %{"mana_cost" => 100, "harvest_costs" => [%{"item_name" => "Cress_harvest", "count" => 1}]},
        %{"mana_cost" => 120, "harvest_costs" => [%{"item_name" => "Basil_harvest", "count" => 2}]},
        %{"mana_cost" => 150, "harvest_costs" => [%{"item_name" => "Chamomile_harvest", "count" => 1}]}
      ]
    }

    :ets.insert(:config_cache, {"building_cost_config", config})
  end

  def seed_garden_configs do
    configs = %{
      "BerryBush" => %{
        "growth_duration_hours" => 24.0,
        "yield_item" => "Berry",
        "yield_amount" => 3,
        "yield_interval_hours" => 12.0,
        "mana_cost" => 30.0
      },
      "Oak" => %{
        "growth_duration_hours" => 48.0,
        "yield_item" => "Acorn",
        "yield_amount" => 2,
        "yield_interval_hours" => 24.0,
        "mana_cost" => 50.0
      }
    }

    :ets.insert(:config_cache, {"garden_configs", configs})
  end

  def auth_header(player) do
    [{"authorization", "Bearer #{player.auth_token}"}]
  end

  def authed_conn(conn, player) do
    Enum.reduce(auth_header(player), conn, fn {key, val}, conn ->
      Plug.Conn.put_req_header(conn, key, val)
    end)
  end
end
