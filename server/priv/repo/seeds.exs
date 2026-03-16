import Ecto.Query
alias CampFire.Repo
alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule}

# --- Item Definitions (seeded FIRST, before all configs) ---

alias CampFire.Game.Item

plants = ~w(sprouts cress basil chamomile marigold snowdrop mint lavender pansy poppy jasmine rosemary dahlia moonflower)

items =
  # Seeds
  Enum.map(plants, fn p ->
    %{item_key: "#{p}_seed", display_name: "#{String.capitalize(p)} Seed", category: "seed"}
  end) ++
  # Harvests
  Enum.map(plants, fn p ->
    %{item_key: p, display_name: String.capitalize(p), category: "harvest"}
  end) ++
  # Garden seeds
  [
    %{item_key: "berrybush_seed", display_name: "BerryBush Seed", category: "garden_seed"},
    %{item_key: "oak_seed", display_name: "Oak Seed", category: "garden_seed"}
  ] ++
  # Garden yields (not from plants list)
  [
    %{item_key: "berry", display_name: "Berry", category: "harvest"},
    %{item_key: "acorn", display_name: "Acorn", category: "harvest"}
  ] ++
  # Pigments (not all plants have pigment recipes — only tier 1+, so exclude sprouts and cress)
  ((plants -- ~w(sprouts cress))
  |> Enum.map(fn p ->
    %{item_key: "#{p}_pigment", display_name: "#{String.capitalize(p)} Pigment", category: "pigment"}
  end)) ++
  # Potions, materials, consumables
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
    %{item_key: "fertilizer", display_name: "Fertilizer", category: "consumable"},
    %{item_key: "energy_drink", display_name: "Energy Drink", category: "consumable"}
  ]

for item <- items do
  %Item{}
  |> Item.changeset(item)
  |> Repo.insert!(
    on_conflict: {:replace, [:display_name, :category, :sprite_key, :updated_at]},
    conflict_target: :item_key
  )
end

IO.puts("Items seeded: #{length(items)}")

# Build lookup map: item_key string -> %Item{} struct (with integer .id)
items_by_key = Item |> Repo.all() |> Map.new(fn i -> {i.item_key, i} end)

# Helper: look up integer ID by item_key string
item_id! = fn key ->
  case Map.get(items_by_key, key) do
    nil -> raise "Item '#{key}' not found — seed items before configs"
    item -> item.id
  end
end

# Helper: return item_key string (for use in config JSON values)
item_key! = fn key ->
  case Map.get(items_by_key, key) do
    nil -> raise "Item '#{key}' not found — seed items before configs"
    item -> item.item_key
  end
end

# --- Visitor Templates ---

templates = [
  %{
    visitor_id: "thorn_merchant",
    name: "Thorn",
    portrait_id: "thorn",
    type: "merchant",
    flame_level_min: 1,
    dialogue_pool: [
      [
        "The road was long, but your flame drew me in.",
        "I've got rare seeds from distant lands.",
        "Care to trade?"
      ],
      ["Ah, another campsite with good soil.", "Let's do business, shall we?"],
      [
        "I picked these up on the coast, past the marshes.",
        "They don't grow just anywhere.",
        "What have you got to offer in return?"
      ]
    ],
    offer_pool: [
      %{
        "costs" => [%{"itemKey" => "basil", "count" => 2}],
        "rewardItemKey" => "lavender_seed",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "chamomile", "count" => 3}],
        "rewardItemKey" => "mint_seed",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "mint", "count" => 2}],
        "rewardItemKey" => "rosemary_seed",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "lavender", "count" => 2}],
        "rewardItemKey" => "dahlia_seed",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "marigold", "count" => 3}],
        "rewardItemKey" => "hot_potion",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "snowdrop", "count" => 3}],
        "rewardItemKey" => "cool_potion",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemKey" => "mint", "count" => 3}],
        "rewardItemKey" => "humid_potion",
        "rewardCount" => 1
      }
    ],
    gift_pool: [],
    quest_pool: [],
    weight: 1.0
  },
  %{
    visitor_id: "willow_gifter",
    name: "Willow",
    portrait_id: "willow",
    type: "gifter",
    flame_level_min: 1,
    dialogue_pool: [
      ["Hello, dear! Your flame is so warm.", "I brought a little something for you."],
      [
        "What a lovely campsite you have here!",
        "Please, take this gift. It's the least I can do.",
        "May your garden flourish!"
      ],
      [
        "I was just passing through and felt your flame's warmth.",
        "Here, I hope this helps your garden grow."
      ]
    ],
    offer_pool: [],
    gift_pool: [
      %{"type" => "seed", "name" => "Chamomile Seed", "amount" => 2},
      %{"type" => "water", "name" => "Water", "amount" => 3},
      %{"type" => "seed", "name" => "Basil Seed", "amount" => 3},
      %{"type" => "item", "name" => "Basil", "amount" => 2},
      %{"type" => "item", "name" => "Hot Potion", "amount" => 1},
      %{"type" => "item", "name" => "Rain Potion", "amount" => 1},
      %{"type" => "item", "name" => "Sun Potion", "amount" => 1}
    ],
    quest_pool: [],
    weight: 1.5
  },
  %{
    visitor_id: "ember_quester",
    name: "Ember",
    portrait_id: "ember",
    type: "quester",
    flame_level_min: 2,
    dialogue_pool: [
      ["The stars told me to seek you out.", "I need something... rare. Can you help?"],
      [
        "I've been wandering for a long time, looking for the right campsite.",
        "Yours has the right energy. I have a request."
      ]
    ],
    offer_pool: [],
    gift_pool: [],
    quest_pool: [
      %{
        "request_item" => "lavender",
        "request_count" => 3,
        "return_days" => 7,
        "reward" => %{"type" => "seed", "name" => "Moonflower Seed", "count" => 2},
        "return_dialogue" => ["You found them!", "Here, take these rare seeds as thanks."]
      },
      %{
        "request_item" => "chamomile",
        "request_count" => 5,
        "return_days" => 5,
        "reward" => %{"type" => "seed", "name" => "Jasmine Seed", "count" => 1},
        "return_dialogue" => ["Perfect!", "I knew I could count on you."]
      },
      %{
        "request_item" => "lavender",
        "request_count" => 3,
        "return_days" => 7,
        "reward" => %{"type" => "item", "name" => "Moon Potion", "count" => 1},
        "return_dialogue" => ["Incredible! The moonlight guided me back.", "Here, this is special."]
      }
    ],
    weight: 0.8
  }
]

for t <- templates do
  Repo.insert!(
    %VisitorTemplate{
      visitor_id: t.visitor_id,
      name: t.name,
      portrait_id: t.portrait_id,
      type: t.type,
      flame_level_min: t.flame_level_min,
      dialogue_pool: t.dialogue_pool,
      offer_pool: t.offer_pool,
      gift_pool: t.gift_pool,
      quest_pool: t.quest_pool,
      weight: t.weight
    },
    on_conflict: :nothing,
    conflict_target: :visitor_id
  )
end

schedule = [
  %{visitor_id: "willow_gifter", visit_number: 1, priority: 10},
  %{visitor_id: "ember_quester", visit_number: 7, priority: 10}
]

for s <- schedule do
  exists =
    Repo.exists?(
      from(vs in VisitorSchedule,
        where: vs.visitor_id == ^s.visitor_id and vs.visit_number == ^s.visit_number
      )
    )

  unless exists do
    Repo.insert!(%VisitorSchedule{
      visitor_id: s.visitor_id,
      visit_number: s.visit_number,
      priority: s.priority
    })
  end
end

# --- Seed Configs (game balance data) ---

alias CampFire.Game.SeedConfig

defmodule RecipeHelper do
  def axis(ideal_min, ideal_max, tolerance, weight) do
    %{
      "enabled" => true,
      "ideal_min" => ideal_min,
      "ideal_max" => ideal_max,
      "tolerance" => tolerance,
      "weight" => weight
    }
  end
end

seed_configs = [
  %{
    item_id: item_id!.("sprouts_seed"),
    harvest_item_id: item_id!.("sprouts"),
    growth_duration_hours: 0.00278,
    min_drops: 1,
    max_drops: 4,
    tier: 0,
    recipe: %{
      "waterings" => RecipeHelper.axis(1, 1, 0, 1)
    }
  },
  %{
    item_id: item_id!.("cress_seed"),
    harvest_item_id: item_id!.("cress"),
    growth_duration_hours: 0.08333,
    min_drops: 1,
    max_drops: 3,
    tier: 0,
    recipe: %{
      "heat" => RecipeHelper.axis(10, 25, 0, 1),
      "humidity" => RecipeHelper.axis(50, 85, 0, 1),
      "waterings" => RecipeHelper.axis(1, 1, 0, 1)
    }
  },
  %{
    item_id: item_id!.("basil_seed"),
    harvest_item_id: item_id!.("basil"),
    growth_duration_hours: 1.0,
    min_drops: 1,
    max_drops: 4,
    tier: 1,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 30, 0, 1),
      "waterings" => RecipeHelper.axis(1, 1, 0, 0.5)
    }
  },
  %{
    item_id: item_id!.("chamomile_seed"),
    harvest_item_id: item_id!.("chamomile"),
    growth_duration_hours: 1.5,
    min_drops: 2,
    max_drops: 5,
    tier: 1,
    recipe: %{
      "heat" => RecipeHelper.axis(15, 25, 0, 1),
      "sunlight" => RecipeHelper.axis(50, 90, 0, 1)
    }
  },
  %{
    item_id: item_id!.("marigold_seed"),
    harvest_item_id: item_id!.("marigold"),
    growth_duration_hours: 2.0,
    min_drops: 2,
    max_drops: 6,
    tier: 2,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 35, 0, 1),
      "sunlight" => RecipeHelper.axis(60, 100, 0, 1),
      "waterings" => RecipeHelper.axis(1, 2, 0, 0.5)
    }
  },
  %{
    item_id: item_id!.("snowdrop_seed"),
    harvest_item_id: item_id!.("snowdrop"),
    growth_duration_hours: 2.5,
    min_drops: 2,
    max_drops: 6,
    tier: 2,
    recipe: %{
      "heat" => RecipeHelper.axis(0, 10, 0, 1.5),
      "humidity" => RecipeHelper.axis(50, 80, 0, 1),
      "waterings" => RecipeHelper.axis(1, 2, 0, 0.5)
    }
  },
  %{
    item_id: item_id!.("mint_seed"),
    harvest_item_id: item_id!.("mint"),
    growth_duration_hours: 3.0,
    min_drops: 2,
    max_drops: 8,
    tier: 3,
    recipe: %{
      "humidity" => RecipeHelper.axis(50, 80, 0, 1),
      "rain" => RecipeHelper.axis(0.2, 0.6, 0, 1.5),
      "waterings" => RecipeHelper.axis(1, 2, 0, 0.5)
    }
  },
  %{
    item_id: item_id!.("lavender_seed"),
    harvest_item_id: item_id!.("lavender"),
    growth_duration_hours: 5.0,
    min_drops: 2,
    max_drops: 9,
    tier: 4,
    recipe: %{
      "heat" => RecipeHelper.axis(25, 35, 0, 1.5),
      "wind" => RecipeHelper.axis(5, 15, 0, 1),
      "sunlight" => RecipeHelper.axis(70, 100, 0, 1.5)
    }
  },
  %{
    item_id: item_id!.("pansy_seed"),
    harvest_item_id: item_id!.("pansy"),
    growth_duration_hours: 6.0,
    min_drops: 2,
    max_drops: 8,
    tier: 3,
    recipe: %{
      "heat" => RecipeHelper.axis(5, 15, 0, 1.5),
      "sunlight" => RecipeHelper.axis(40, 80, 0, 1),
      "rain" => RecipeHelper.axis(0.2, 0.6, 0, 1)
    }
  },
  %{
    item_id: item_id!.("poppy_seed"),
    harvest_item_id: item_id!.("poppy"),
    growth_duration_hours: 8.0,
    min_drops: 3,
    max_drops: 10,
    tier: 5,
    recipe: %{
      "heat" => RecipeHelper.axis(15, 25, 0, 1),
      "humidity" => RecipeHelper.axis(40, 75, 0, 1),
      "rain" => RecipeHelper.axis(0.3, 0.7, 0, 1.5)
    }
  },
  %{
    item_id: item_id!.("jasmine_seed"),
    harvest_item_id: item_id!.("jasmine"),
    growth_duration_hours: 12.0,
    min_drops: 3,
    max_drops: 12,
    tier: 6,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 30, 0, 1),
      "humidity" => RecipeHelper.axis(60, 90, 0, 1.5),
      "waterings" => RecipeHelper.axis(2, 4, 0, 1)
    }
  },
  %{
    item_id: item_id!.("rosemary_seed"),
    harvest_item_id: item_id!.("rosemary"),
    growth_duration_hours: 18.0,
    min_drops: 3,
    max_drops: 14,
    tier: 7,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 35, 0, 1),
      "wind" => RecipeHelper.axis(5, 20, 0, 1),
      "sunlight" => RecipeHelper.axis(60, 100, 0, 1.5)
    }
  },
  %{
    item_id: item_id!.("dahlia_seed"),
    harvest_item_id: item_id!.("dahlia"),
    growth_duration_hours: 30.0,
    min_drops: 4,
    max_drops: 16,
    tier: 8,
    recipe: %{
      "heat" => RecipeHelper.axis(18, 28, 0, 1),
      "humidity" => RecipeHelper.axis(50, 80, 0, 1),
      "sunlight" => RecipeHelper.axis(50, 90, 0, 1),
      "waterings" => RecipeHelper.axis(3, 6, 0, 1)
    }
  },
  %{
    item_id: item_id!.("moonflower_seed"),
    harvest_item_id: item_id!.("moonflower"),
    growth_duration_hours: 48.0,
    min_drops: 5,
    max_drops: 20,
    tier: 9,
    recipe: %{
      "humidity" => RecipeHelper.axis(60, 90, 0, 1),
      "moon" => RecipeHelper.axis(4, 4, 0, 3),
      "waterings" => RecipeHelper.axis(3, 6, 0, 1)
    }
  }
]

replace_fields = [:growth_duration_hours, :min_drops, :max_drops, :tier, :recipe, :harvest_item_id, :updated_at]

for config <- seed_configs do
  %SeedConfig{}
  |> SeedConfig.changeset(config)
  |> Repo.insert!(
    on_conflict: {:replace, replace_fields},
    conflict_target: :item_id
  )
end

IO.puts("Seeds complete.")

# ── Admin Config Seeds ──

alias CampFire.Admin.{QuestConfig, GardenConfig, GameConfig}

# Quest configs (from Game.Mallums @quest_configs)
quests = [
  %{
    quest_name: "NearbyGathering",
    description: "Gather seeds from the area around camp.",
    duration_minutes: 1,
    required_flame_level: 1,
    reward_rolls: 3,
    reward_pool: [
      %{"itemKey" => "sprouts_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "cress_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2}
    ]
  },
  %{
    quest_name: "SwampForage",
    description: "Forage in the nearby swamp for useful seeds.",
    duration_minutes: 5,
    required_flame_level: 2,
    reward_rolls: 2,
    reward_pool: [
      %{"itemKey" => "cress_seed", "weight" => 4, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "basil_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "chamomile_seed", "weight" => 2, "minCount" => 1, "maxCount" => 2}
    ]
  },
  %{
    quest_name: "MeadowExpedition",
    description: "Explore the meadow for wildflowers.",
    duration_minutes: 15,
    required_flame_level: 3,
    reward_rolls: 3,
    reward_pool: [
      %{"itemKey" => "marigold_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "snowdrop_seed", "weight" => 2, "minCount" => 1, "maxCount" => 2}
    ]
  },
  %{
    quest_name: "DeepWoodsTrek",
    description: "Trek deep into the woods for rare finds.",
    duration_minutes: 60,
    required_flame_level: 4,
    reward_rolls: 3,
    reward_pool: [
      %{"itemKey" => "mint_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "pansy_seed", "weight" => 2, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "HighlandPass",
    description: "Cross the highland pass to find mountain herbs.",
    duration_minutes: 120,
    required_flame_level: 5,
    reward_rolls: 3,
    reward_pool: [
      %{"itemKey" => "lavender_seed", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "marigold_seed", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "DeepMarsh",
    description: "Navigate the deep marsh for exotic plants.",
    duration_minutes: 240,
    required_flame_level: 6,
    reward_rolls: 4,
    reward_pool: [
      %{"itemKey" => "poppy_seed", "weight" => 15, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "mint_seed", "weight" => 5, "minCount" => 1, "maxCount" => 1},
      %{"itemKey" => "berrybush_seed", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "MountainAscent",
    description: "Scale the mountain for high-altitude flora.",
    duration_minutes: 360,
    required_flame_level: 7,
    reward_rolls: 4,
    reward_pool: [
      %{"itemKey" => "jasmine_seed", "weight" => 15, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "lavender_seed", "weight" => 5, "minCount" => 1, "maxCount" => 1},
      %{"itemKey" => "oak_seed", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "MoonlitPath",
    description: "Follow the moonlit path through enchanted woods.",
    duration_minutes: 480,
    required_flame_level: 8,
    reward_rolls: 4,
    reward_pool: [
      %{"itemKey" => "rosemary_seed", "weight" => 15, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "pansy_seed", "weight" => 5, "minCount" => 1, "maxCount" => 1},
      %{"itemKey" => "berrybush_seed", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "AncientGrove",
    description: "Explore the ancient grove for legendary seeds.",
    duration_minutes: 720,
    required_flame_level: 9,
    reward_rolls: 5,
    reward_pool: [
      %{"itemKey" => "dahlia_seed", "weight" => 15, "minCount" => 1, "maxCount" => 2},
      %{"itemKey" => "moonflower_seed", "weight" => 5, "minCount" => 1, "maxCount" => 1},
      %{"itemKey" => "rosemary_seed", "weight" => 5, "minCount" => 1, "maxCount" => 1},
      %{"itemKey" => "oak_seed", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  }
]

for q <- quests do
  %QuestConfig{}
  |> QuestConfig.changeset(q)
  |> Repo.insert!(
    on_conflict:
      {:replace,
       [
         :description,
         :duration_minutes,
         :required_flame_level,
         :reward_rolls,
         :reward_pool,
         :updated_at
       ]},
    conflict_target: :quest_name
  )
end

# Garden configs
gardens = [
  %{
    plant_name: "BerryBush",
    growth_duration_hours: 24.0,
    yield_item: "berry",
    yield_amount: 3,
    yield_interval_hours: 12.0,
    water_required: 3,
    mana_cost: 0.0
  },
  %{
    plant_name: "Oak",
    growth_duration_hours: 48.0,
    yield_item: "acorn",
    yield_amount: 2,
    yield_interval_hours: 24.0,
    water_required: 5,
    mana_cost: 0.0
  }
]

for g <- gardens do
  %GardenConfig{}
  |> GardenConfig.changeset(g)
  |> Repo.insert!(on_conflict: :nothing, conflict_target: :plant_name)
end

# Game configs (economy constants)
game_configs = [
  %{
    key: "flame_config",
    value: %{
      "max_flame_level" => 12,
      "mana_rates" => [0.5, 1, 1.5, 2, 3, 4, 5, 7.5, 10, 12.5, 15, 20],
      "mana_caps" => [300, 500, 750, 1000, 1500, 2000, 3000, 4000, 5000, 7000, 9000, 12000],
      "entity_caps" => [5, 6, 7, 9, 12, 15, 18, 22, 26, 30, 35, 40],
      "grid_sizes" => [2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5],
      "upgrade_recipes" => [
        %{"ingredients" => [%{"itemKey" => item_key!.("sprouts"), "count" => 10}]},
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("sprouts"), "count" => 30},
            %{"itemKey" => item_key!.("cress"), "count" => 5}
          ]
        },
        %{"ingredients" => [%{"itemKey" => item_key!.("basil"), "count" => 5}]},
        %{"ingredients" => [%{"itemKey" => item_key!.("chamomile"), "count" => 5}]},
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("marigold"), "count" => 12},
            %{"itemKey" => item_key!.("snowdrop"), "count" => 8},
            %{"itemKey" => item_key!.("basil"), "count" => 8}
          ]
        },
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("mint"), "count" => 8},
            %{"itemKey" => item_key!.("pansy"), "count" => 4},
            %{"itemKey" => item_key!.("chamomile"), "count" => 8}
          ]
        },
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("lavender"), "count" => 22},
            %{"itemKey" => item_key!.("snowdrop"), "count" => 24},
            %{"itemKey" => item_key!.("basil"), "count" => 18}
          ]
        },
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("poppy"), "count" => 35},
            %{"itemKey" => item_key!.("pansy"), "count" => 30},
            %{"itemKey" => item_key!.("marigold"), "count" => 50}
          ]
        },
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("jasmine"), "count" => 60},
            %{"itemKey" => item_key!.("lavender"), "count" => 50},
            %{"itemKey" => item_key!.("poppy"), "count" => 60}
          ]
        },
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("rosemary"), "count" => 50},
            %{"itemKey" => item_key!.("jasmine"), "count" => 60},
            %{"itemKey" => item_key!.("lavender"), "count" => 55},
            %{"itemKey" => item_key!.("snowdrop"), "count" => 40}
          ]
        },
        %{
          "ingredients" => [
            %{"itemKey" => item_key!.("dahlia"), "count" => 50},
            %{"itemKey" => item_key!.("moonflower"), "count" => 30},
            %{"itemKey" => item_key!.("rosemary"), "count" => 60},
            %{"itemKey" => item_key!.("poppy"), "count" => 80},
            %{"itemKey" => item_key!.("basil"), "count" => 50}
          ]
        }
      ],
      "plot_costs" => [
        %{"manaCost" => 50, "harvestCosts" => [%{"itemKey" => item_key!.("cress"), "count" => 1}]},
        %{"manaCost" => 200, "harvestCosts" => [%{"itemKey" => item_key!.("cress"), "count" => 4}]},
        %{"manaCost" => 260, "harvestCosts" => [%{"itemKey" => item_key!.("basil"), "count" => 2}]},
        %{"manaCost" => 330, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 1}]},
        %{"manaCost" => 420, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 1}]},
        %{
          "manaCost" => 520,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("chamomile"), "count" => 1},
            %{"itemKey" => item_key!.("basil"), "count" => 1}
          ]
        },
        %{"manaCost" => 640, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 2}]},
        %{
          "manaCost" => 780,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("lavender"), "count" => 1},
            %{"itemKey" => item_key!.("chamomile"), "count" => 1}
          ]
        },
        %{
          "manaCost" => 940,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("mint"), "count" => 1},
            %{"itemKey" => item_key!.("lavender"), "count" => 1}
          ]
        },
        %{
          "manaCost" => 1120,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("mint"), "count" => 2},
            %{"itemKey" => item_key!.("chamomile"), "count" => 1}
          ]
        }
      ],
      "vase_costs" => [
        %{"manaCost" => 100, "harvestCosts" => [%{"itemKey" => item_key!.("cress"), "count" => 1}]},
        %{"manaCost" => 120, "harvestCosts" => [%{"itemKey" => item_key!.("basil"), "count" => 2}]},
        %{"manaCost" => 150, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 1}]},
        %{"manaCost" => 180, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 1}]},
        %{
          "manaCost" => 220,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("chamomile"), "count" => 1},
            %{"itemKey" => item_key!.("basil"), "count" => 1}
          ]
        },
        %{"manaCost" => 260, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 2}]},
        %{
          "manaCost" => 310,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("lavender"), "count" => 1},
            %{"itemKey" => item_key!.("basil"), "count" => 1}
          ]
        },
        %{
          "manaCost" => 370,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("lavender"), "count" => 1},
            %{"itemKey" => item_key!.("chamomile"), "count" => 1}
          ]
        }
      ],
      "garden_costs" => [
        %{"manaCost" => 550, "harvestCosts" => [%{"itemKey" => item_key!.("chamomile"), "count" => 3}]},
        %{"manaCost" => 850, "harvestCosts" => [%{"itemKey" => item_key!.("lavender"), "count" => 3}]},
        %{
          "manaCost" => 1200,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("mint"), "count" => 3},
            %{"itemKey" => item_key!.("chamomile"), "count" => 2}
          ]
        },
        %{
          "manaCost" => 1800,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("lavender"), "count" => 4},
            %{"itemKey" => item_key!.("mint"), "count" => 3}
          ]
        },
        %{
          "manaCost" => 2500,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("poppy"), "count" => 3},
            %{"itemKey" => item_key!.("lavender"), "count" => 4}
          ]
        }
      ]
    }
  },
  %{
    key: "vase_config",
    value: %{
      "craft_cost_mana" => 100,
      "default_capacity" => 5,
      "fill_duration_minutes" => 5,
      "capacity_tiers" => [5, 8, 12, 20],
      "upgrade_costs" => [400, 1000, 2000],
      "speed_item" => item_key!.("energy_drink")
    }
  },
  %{
    key: "mallum_house_config",
    value: %{
      "mallums_per_house" => 1,
      "quest_speed_item" => item_key!.("energy_drink"),
      "house_costs" => [
        %{"manaCost" => 10, "harvestCosts" => [%{"itemKey" => item_key!.("sprouts"), "count" => 1}]},
        %{"manaCost" => 100, "harvestCosts" => [%{"itemKey" => item_key!.("basil"), "count" => 2}]},
        %{"manaCost" => 500, "harvestCosts" => [%{"itemKey" => item_key!.("lavender"), "count" => 3}]},
        %{
          "manaCost" => 1000,
          "harvestCosts" => [
            %{"itemKey" => item_key!.("chamomile"), "count" => 2},
            %{"itemKey" => item_key!.("mint"), "count" => 2}
          ]
        }
      ]
    }
  },
  %{
    key: "bird_config",
    value: %{
      "spawn_base_chance" => 0.33,
      "spawn_decay" => 0.5
    }
  },
  %{
    key: "plot_config",
    value: %{
      "water_cooldown_seconds" => 7200,
      "rain_water_cooldown_seconds" => 21600,
      "rain_trigger_minutes" => 15,
      "drop_spread_factor" => 0.3,
      "speed_item" => item_key!.("speed_potion")
    }
  },
  %{
    key: "new_player_config",
    value: %{
      "mana" => 40,
      "gems" => 5,
      "starting_water" => 1,
      "items" => [
        %{"itemKey" => item_key!.("sprouts_seed"), "count" => 2},
        %{"itemKey" => item_key!.("speed_potion"), "count" => 3},
        %{"itemKey" => item_key!.("energy_drink"), "count" => 2}
      ]
    }
  },
  %{
    key: "recipe_configs",
    value: %{
      "basil_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("basil"), "count" => 3}],
        "result_item" => item_key!.("basil_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "chamomile_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("chamomile"), "count" => 3}],
        "result_item" => item_key!.("chamomile_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "dahlia_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("dahlia"), "count" => 3}],
        "result_item" => item_key!.("dahlia_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "jasmine_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("jasmine"), "count" => 3}],
        "result_item" => item_key!.("jasmine_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "lavender_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("lavender"), "count" => 3}],
        "result_item" => item_key!.("lavender_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "marigold_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("marigold"), "count" => 3}],
        "result_item" => item_key!.("marigold_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "mint_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("mint"), "count" => 3}],
        "result_item" => item_key!.("mint_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "moonflower_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("moonflower"), "count" => 3}],
        "result_item" => item_key!.("moonflower_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "pansy_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("pansy"), "count" => 3}],
        "result_item" => item_key!.("pansy_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "poppy_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("poppy"), "count" => 3}],
        "result_item" => item_key!.("poppy_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "rosemary_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("rosemary"), "count" => 3}],
        "result_item" => item_key!.("rosemary_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "snowdrop_pigment" => %{
        "ingredients" => [%{"itemKey" => item_key!.("snowdrop"), "count" => 3}],
        "result_item" => item_key!.("snowdrop_pigment"),
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "speed_potion" => %{
        "ingredients" => [
          %{"itemKey" => item_key!.("mint"), "count" => 4},
          %{"itemKey" => item_key!.("chamomile"), "count" => 3}
        ],
        "result_item" => item_key!.("speed_potion"),
        "result_quantity" => 1,
        "category" => "Consumable"
      },
      "fertilizer" => %{
        "ingredients" => [
          %{"itemKey" => item_key!.("berry"), "count" => 3},
          %{"itemKey" => item_key!.("acorn"), "count" => 1}
        ],
        "result_item" => item_key!.("fertilizer"),
        "result_quantity" => 1,
        "category" => "Consumable"
      }
    }
  },
  %{
    key: "skin_configs",
    value:
      Enum.reduce(
        ~w(basil chamomile dahlia jasmine lavender marigold mint moonflower pansy poppy rosemary snowdrop),
        %{},
        fn plant, acc ->
          pigment = "#{plant}_pigment"

          acc
          |> Map.put("#{plant}_plot", %{
            "building_type" => "plot",
            "cost_item_key" => item_key!.(pigment),
            "cost_quantity" => 1
          })
          |> Map.put("#{plant}_vase", %{
            "building_type" => "vase",
            "cost_item_key" => item_key!.(pigment),
            "cost_quantity" => 1
          })
          |> Map.put("#{plant}_house", %{
            "building_type" => "mallum_house",
            "cost_item_key" => item_key!.(pigment),
            "cost_quantity" => 1
          })
        end
      )
  }
]

for c <- game_configs do
  %GameConfig{}
  |> GameConfig.changeset(c)
  |> Repo.insert!(
    on_conflict: {:replace, [:value, :updated_at]},
    conflict_target: :key
  )
end

IO.puts("Admin config seeds complete.")
