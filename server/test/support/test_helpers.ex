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
        %{"manaCost" => 150, "harvestCosts" => [%{"itemName" => "Sprouts_harvest", "count" => 1}]},
        %{"manaCost" => 200, "harvestCosts" => [%{"itemName" => "Basil_harvest", "count" => 1}]},
        %{"manaCost" => 260, "harvestCosts" => [%{"itemName" => "Basil_harvest", "count" => 2}]},
        %{"manaCost" => 330, "harvestCosts" => [%{"itemName" => "Chamomile_harvest", "count" => 1}]}
      ],
      "vase_costs" => [
        %{"manaCost" => 100, "harvestCosts" => [%{"itemName" => "Cress_harvest", "count" => 1}]},
        %{"manaCost" => 120, "harvestCosts" => [%{"itemName" => "Basil_harvest", "count" => 2}]},
        %{"manaCost" => 150, "harvestCosts" => [%{"itemName" => "Chamomile_harvest", "count" => 1}]}
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

  def seed_flame_config do
    config = %{
      "base_mana_per_second" => 0.5,
      "mana_per_level" => 0.3,
      "max_flame_level" => 12,
      "entity_caps" => [8, 10, 13, 16, 20, 24, 28, 32, 36, 40, 45, 50],
      "grid_sizes" => [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7],
      "upgrade_recipes" => []
    }

    :ets.insert(:config_cache, {"flame_config", config})
  end

  def seed_flame_config_with_low_cap do
    config = %{
      "base_mana_per_second" => 0.5,
      "mana_per_level" => 0.3,
      "max_flame_level" => 12,
      "entity_caps" => [4, 5, 7, 10, 13, 16, 20, 24, 28, 32, 36, 40],
      "grid_sizes" => [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7],
      "upgrade_recipes" => []
    }

    :ets.insert(:config_cache, {"flame_config", config})
  end

  def seed_seed_configs do
    configs = %{
      "Basil" => %{"growth_duration_hours" => 2.0, "base_drops" => 3, "tier" => 1, "recipe" => %{}},
      "Sprouts" => %{"growth_duration_hours" => 0.5, "base_drops" => 2, "tier" => 0, "recipe" => %{}},
      "Cress" => %{"growth_duration_hours" => 1.0, "base_drops" => 2, "tier" => 0, "recipe" => %{}},
      "HarvestTest" => %{"growth_duration_hours" => 0.001, "base_drops" => 4, "tier" => 0, "recipe" => %{}},
      "SimpleSeed" => %{"growth_duration_hours" => 0.001, "base_drops" => 3, "tier" => 0, "recipe" => %{}},
      "SlowPlant" => %{"growth_duration_hours" => 9999.0, "base_drops" => 1, "tier" => 0, "recipe" => %{}}
    }

    :ets.insert(:config_cache, {"seed_configs", configs})
  end

  def seed_mallum_house_config do
    config = %{
      "mallums_per_house" => 2,
      "house_costs" => [
        %{"manaCost" => 200, "harvestCosts" => [%{"itemName" => "Basil_harvest", "count" => 1}]},
        %{"manaCost" => 350, "harvestCosts" => [%{"itemName" => "Chamomile_harvest", "count" => 2}]}
      ]
    }

    :ets.insert(:config_cache, {"mallum_house_config", config})
  end

  def seed_recipe_configs do
    recipes = %{
      "Fertilizer" => %{
        "ingredients" => [%{"item_name" => "Basil_harvest", "count" => 2}],
        "result_item" => "Fertilizer",
        "result_quantity" => 1,
        "category" => "consumable"
      },
      "Speed_Potion" => %{
        "ingredients" => [
          %{"item_name" => "Mint_harvest", "count" => 2},
          %{"item_name" => "Chamomile_harvest", "count" => 1}
        ],
        "result_item" => "Speed_Potion",
        "result_quantity" => 1,
        "category" => "consumable"
      }
    }

    :ets.insert(:config_cache, {"recipe_configs", recipes})
  end

  def seed_skin_configs do
    configs = %{
      "GreenPlot" => %{"building_type" => "plot", "cost_item_name" => "Basil_harvest", "cost_quantity" => 3},
      "BluePlot" => %{"building_type" => "plot", "cost_item_name" => "Chamomile_harvest", "cost_quantity" => 2},
      "FancyVase" => %{"building_type" => "vase", "cost_item_name" => "Basil_harvest", "cost_quantity" => 2},
      "CozyHouse" => %{"building_type" => "mallum_house", "cost_item_name" => "Basil_harvest", "cost_quantity" => 5}
    }

    :ets.insert(:config_cache, {"skin_configs", configs})
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
