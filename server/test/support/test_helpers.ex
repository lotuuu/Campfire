defmodule CampFire.TestHelpers do
  alias CampFire.Accounts
  alias CampFire.Repo
  alias CampFire.Game.Item

  def register_player do
    {:ok, player} = Accounts.register_player()
    player
  end

  @doc """
  Seeds the items table with all known item keys so FK constraints pass.
  Must be called before any Economy.upsert_item / init_economy call.
  """
  def seed_items do
    plants = ~w(sprouts cress basil chamomile marigold snowdrop mint lavender pansy poppy jasmine rosemary dahlia moonflower)

    items =
      Enum.map(plants, fn p ->
        %{item_key: "#{p}_seed", display_name: "#{String.capitalize(p)} Seed", category: "seed"}
      end) ++
      Enum.map(plants, fn p ->
        %{item_key: p, display_name: String.capitalize(p), category: "harvest"}
      end) ++
      [
        %{item_key: "berry", display_name: "Berry", category: "harvest"},
        %{item_key: "acorn", display_name: "Acorn", category: "harvest"}
      ] ++
      ((plants -- ~w(sprouts cress)) |> Enum.map(fn p ->
        %{item_key: "#{p}_pigment", display_name: "#{String.capitalize(p)} Pigment", category: "pigment"}
      end)) ++
      [
        %{item_key: "speed_potion", display_name: "Speed Potion", category: "potion"},
        %{item_key: "hot_potion", display_name: "Hot Potion", category: "potion"},
        %{item_key: "cool_potion", display_name: "Cool Potion", category: "potion"},
        %{item_key: "wind_potion", display_name: "Wind Potion", category: "potion"},
        %{item_key: "calm_potion", display_name: "Calm Potion", category: "potion"},
        %{item_key: "humid_potion", display_name: "Humid Potion", category: "potion"},
        %{item_key: "dry_potion", display_name: "Dry Potion", category: "potion"},
        %{item_key: "sun_potion", display_name: "Sun Potion", category: "potion"},
        %{item_key: "shadow_potion", display_name: "Shadow Potion", category: "potion"},
        %{item_key: "rain_potion", display_name: "Rain Potion", category: "potion"},
        %{item_key: "impermeable_potion", display_name: "Impermeable Potion", category: "potion"},
        %{item_key: "moon_potion", display_name: "Moon Potion", category: "potion"},
        %{item_key: "fertilizer", display_name: "Fertilizer", category: "material"},
        %{item_key: "energy_drink", display_name: "Energy Drink", category: "consumable"},
        # Test-only items for seeds that don't exist in production
        %{item_key: "harvesttest_seed", display_name: "HarvestTest Seed", category: "seed"},
        %{item_key: "harvesttest", display_name: "HarvestTest", category: "harvest"},
        %{item_key: "simpleseed_seed", display_name: "SimpleSeed Seed", category: "seed"},
        %{item_key: "simpleseed", display_name: "SimpleSeed", category: "harvest"},
        %{item_key: "slowplant_seed", display_name: "SlowPlant Seed", category: "seed"},
        %{item_key: "slowplant", display_name: "SlowPlant", category: "harvest"},
        %{item_key: "rareseed_seed", display_name: "RareSeed Seed", category: "seed"},
        %{item_key: "rareseed", display_name: "RareSeed", category: "harvest"}
      ]

    now = NaiveDateTime.utc_now() |> NaiveDateTime.truncate(:second)

    entries =
      Enum.map(items, fn item ->
        Map.merge(item, %{inserted_at: now, updated_at: now})
      end)

    Repo.insert_all(Item, entries, on_conflict: :nothing, conflict_target: :item_key)
  end

  def seed_new_player_config do
    config = %{
      "mana" => 50,
      "gems" => 5,
      "starting_water" => 1,
      "items" => [
        %{"itemKey" => "sprouts_seed", "count" => 5},
        %{"itemKey" => "cress_seed", "count" => 3},
        %{"itemKey" => "speed_potion", "count" => 3}
      ]
    }

    :ets.insert(:config_cache, {"new_player_config", config})
  end

  def seed_quest_configs do
    quests = [
      {"SwampForage", %{quest_name: "SwampForage", duration_minutes: 5, required_flame_level: 1, reward_rolls: 2,
        reward_pool: [%{"itemKey" => "basil_seed", "weight" => 3, "min" => 1, "max" => 2}, %{"itemKey" => "chamomile_seed", "weight" => 2, "min" => 1, "max" => 2}]}},
      {"MeadowExpedition", %{quest_name: "MeadowExpedition", duration_minutes: 15, required_flame_level: 2, reward_rolls: 3,
        reward_pool: [%{"itemKey" => "marigold_seed", "weight" => 3, "min" => 1, "max" => 2}, %{"itemKey" => "snowdrop_seed", "weight" => 2, "min" => 1, "max" => 2}]}},
      {"DeepWoodsTrek", %{quest_name: "DeepWoodsTrek", duration_minutes: 60, required_flame_level: 3, reward_rolls: 3,
        reward_pool: [%{"itemKey" => "mint_seed", "weight" => 3, "min" => 1, "max" => 2}, %{"itemKey" => "pansy_seed", "weight" => 2, "min" => 1, "max" => 1}]}}
    ]

    # Write directly to ETS since ConfigCache GenServer can't see the test sandbox
    quest_map = Map.new(quests, fn {name, q} -> {name, q} end)
    :ets.insert(:config_cache, {"quest_configs", quest_map})
  end

  def seed_building_costs do
    config = %{
      "plot_costs" => [
        %{"manaCost" => 150, "harvestCosts" => [%{"itemKey" => "sprouts", "count" => 1}]},
        %{"manaCost" => 200, "harvestCosts" => [%{"itemKey" => "basil", "count" => 1}]},
        %{"manaCost" => 260, "harvestCosts" => [%{"itemKey" => "basil", "count" => 2}]},
        %{"manaCost" => 330, "harvestCosts" => [%{"itemKey" => "chamomile", "count" => 1}]}
      ],
      "vase_costs" => [
        %{"manaCost" => 100, "harvestCosts" => [%{"itemKey" => "cress", "count" => 1}]},
        %{"manaCost" => 120, "harvestCosts" => [%{"itemKey" => "basil", "count" => 2}]},
        %{"manaCost" => 150, "harvestCosts" => [%{"itemKey" => "chamomile", "count" => 1}]}
      ]
    }

    # Merge building costs into flame_config (building_cost_config was merged into flame_config)
    existing = case :ets.lookup(:config_cache, "flame_config") do
      [{"flame_config", val}] -> val
      _ -> %{}
    end
    :ets.insert(:config_cache, {"flame_config", Map.merge(existing, config)})
  end

  def seed_garden_configs do
    configs = %{
      "BerryBush" => %{
        "growth_duration_hours" => 24.0,
        "yield_item" => "berry",
        "yield_amount" => 3,
        "yield_interval_hours" => 12.0,
        "mana_cost" => 30.0
      },
      "Oak" => %{
        "growth_duration_hours" => 48.0,
        "yield_item" => "acorn",
        "yield_amount" => 2,
        "yield_interval_hours" => 24.0,
        "mana_cost" => 50.0
      }
    }

    :ets.insert(:config_cache, {"garden_configs", configs})
  end

  def seed_flame_config do
    config = %{
      "max_flame_level" => 12,
      "mana_rates" => [0.5, 1, 1.5, 2, 3, 4, 5, 7.5, 10, 12.5, 15, 20],
      "mana_caps" => [99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999],
      "entity_caps" => [8, 10, 13, 16, 20, 24, 28, 32, 36, 40, 45, 50],
      "grid_sizes" => [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7],
      "upgrade_recipes" => []
    }

    :ets.insert(:config_cache, {"flame_config", config})
  end

  def seed_flame_config_with_low_cap do
    config = %{
      "max_flame_level" => 12,
      "mana_rates" => [0.5, 1, 1.5, 2, 3, 4, 5, 7.5, 10, 12.5, 15, 20],
      "mana_caps" => [99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999, 99999],
      "entity_caps" => [4, 5, 7, 10, 13, 16, 20, 24, 28, 32, 36, 40],
      "grid_sizes" => [2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7],
      "upgrade_recipes" => []
    }

    :ets.insert(:config_cache, {"flame_config", config})
  end

  def seed_seed_configs do
    # Resolve item IDs from the items table (must call seed_items first)
    resolve_id = fn key ->
      case Repo.get_by(Item, item_key: key) do
        nil -> raise "Item #{key} not found — call seed_items() before seed_seed_configs()"
        item -> item.id
      end
    end

    make_config = fn harvest_key, seed_key, opts ->
      %{
        item_id: resolve_id.(seed_key),
        item_key: seed_key,
        harvest_item_id: resolve_id.(harvest_key),
        harvest_item_key: harvest_key,
        growth_duration_hours: opts[:growth_hours],
        min_drops: opts[:min_drops],
        max_drops: opts[:max_drops],
        tier: opts[:tier] || 0,
        recipe: opts[:recipe] || %{}
      }
    end

    configs = %{
      "basil" => make_config.("basil", "basil_seed", growth_hours: 2.0, min_drops: 1, max_drops: 4, tier: 1),
      "sprouts" => make_config.("sprouts", "sprouts_seed", growth_hours: 0.5, min_drops: 1, max_drops: 3, tier: 0),
      "cress" => make_config.("cress", "cress_seed", growth_hours: 1.0, min_drops: 1, max_drops: 3, tier: 0),
      "harvesttest" => make_config.("harvesttest", "harvesttest_seed", growth_hours: 0.001, min_drops: 2, max_drops: 8, tier: 0),
      "simpleseed" => make_config.("simpleseed", "simpleseed_seed", growth_hours: 0.001, min_drops: 2, max_drops: 6, tier: 0),
      "slowplant" => make_config.("slowplant", "slowplant_seed", growth_hours: 9999.0, min_drops: 1, max_drops: 3, tier: 0)
    }

    :ets.insert(:config_cache, {"seed_configs", configs})

    # Also seed by item_id for harvest/maturity lookups
    by_item_id = Map.new(configs, fn {_k, v} -> {v.item_id, v} end)
    :ets.insert(:config_cache, {"seed_configs_by_item_id", by_item_id})

    # Also seed item_key_to_id and item_id_to_key resolution maps
    items = Repo.all(Item)
    item_key_to_id = Map.new(items, fn i -> {i.item_key, i.id} end)
    item_id_to_key = Map.new(items, fn i -> {i.id, i.item_key} end)
    :ets.insert(:config_cache, {"item_key_to_id", item_key_to_id})
    :ets.insert(:config_cache, {"item_id_to_key", item_id_to_key})
  end

  def seed_plot_config do
    config = %{
      "water_cooldown_seconds" => 7200,
      "rain_water_cooldown_seconds" => 21600,
      "rain_trigger_minutes" => 15,
      "drop_spread_factor" => 0.3,
      "speed_item" => "speed_potion"
    }
    :ets.insert(:config_cache, {"plot_config", config})
  end

  def seed_mallum_house_config do
    config = %{
      "mallums_per_house" => 2,
      "quest_speed_item" => "energy_drink",
      "house_costs" => [
        %{"manaCost" => 200, "harvestCosts" => [%{"itemKey" => "basil", "count" => 1}]},
        %{"manaCost" => 350, "harvestCosts" => [%{"itemKey" => "chamomile", "count" => 2}]}
      ]
    }

    :ets.insert(:config_cache, {"mallum_house_config", config})
  end

  def seed_recipe_configs do
    recipes = %{
      "fertilizer" => %{
        "ingredients" => [%{"itemKey" => "basil", "count" => 2}],
        "result_item" => "fertilizer",
        "result_quantity" => 1,
        "category" => "consumable"
      },
      "speed_potion" => %{
        "ingredients" => [
          %{"itemKey" => "mint", "count" => 2},
          %{"itemKey" => "chamomile", "count" => 1}
        ],
        "result_item" => "speed_potion",
        "result_quantity" => 1,
        "category" => "consumable"
      }
    }

    :ets.insert(:config_cache, {"recipe_configs", recipes})
  end

  def seed_skin_configs do
    configs = %{
      "GreenPlot" => %{"building_type" => "plot", "cost_item_key" => "basil", "cost_quantity" => 3},
      "BluePlot" => %{"building_type" => "plot", "cost_item_key" => "chamomile", "cost_quantity" => 2},
      "FancyVase" => %{"building_type" => "vase", "cost_item_key" => "basil", "cost_quantity" => 2},
      "CozyHouse" => %{"building_type" => "mallum_house", "cost_item_key" => "basil", "cost_quantity" => 5}
    }

    :ets.insert(:config_cache, {"skin_configs", configs})
  end

  @doc """
  Returns a list of free hex positions for the given player.
  Useful when tests need to craft at a position not already occupied by starter buildings.
  """
  def free_positions(player_uid) do
    CampFire.Game.GridValidation.get_free_tiles(player_uid)
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
