import Ecto.Query
alias CampFire.Repo
alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule}

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
        "costs" => [%{"itemName" => "Basil Leaf", "count" => 2}],
        "rewardSeedName" => "Lavender",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemName" => "Chamomile Petal", "count" => 3}],
        "rewardSeedName" => "Mint",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemName" => "Mint Leaf", "count" => 2}],
        "rewardSeedName" => "Rosemary",
        "rewardCount" => 1
      },
      %{
        "costs" => [%{"itemName" => "Lavender Petal", "count" => 2}],
        "rewardSeedName" => "Dahlia",
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
      %{"type" => "seed", "name" => "Chamomile", "amount" => 2},
      %{"type" => "water", "amount" => 3},
      %{"type" => "seed", "name" => "Basil", "amount" => 3},
      %{"type" => "item", "name" => "Basil Leaf", "amount" => 2}
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
        "request_item" => "Lavender Petal",
        "request_count" => 3,
        "return_days" => 7,
        "reward" => %{"type" => "seed", "name" => "Moonflower", "count" => 2},
        "return_dialogue" => ["You found them!", "Here, take these rare seeds as thanks."]
      },
      %{
        "request_item" => "Chamomile Petal",
        "request_count" => 5,
        "return_days" => 5,
        "reward" => %{"type" => "seed", "name" => "Jasmine", "count" => 1},
        "return_dialogue" => ["Perfect!", "I knew I could count on you."]
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
    seed_name: "Sprouts",
    growth_duration_hours: 0.00278,
    min_drops: 1,
    max_drops: 4,
    tier: 0,
    recipe: %{
      "waterings" => RecipeHelper.axis(1, 1, 1, 1)
    }
  },
  %{
    seed_name: "Cress",
    growth_duration_hours: 0.08333,
    min_drops: 1,
    max_drops: 3,
    tier: 0,
    recipe: %{
      "heat" => RecipeHelper.axis(10, 25, 15, 1),
      "humidity" => RecipeHelper.axis(50, 85, 15, 1),
      "waterings" => RecipeHelper.axis(1, 1, 1, 1)
    }
  },
  %{
    seed_name: "Basil",
    growth_duration_hours: 1.0,
    min_drops: 1,
    max_drops: 4,
    tier: 1,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 30, 10, 1),
      "waterings" => RecipeHelper.axis(1, 1, 1, 0.5)
    }
  },
  %{
    seed_name: "Chamomile",
    growth_duration_hours: 1.5,
    min_drops: 2,
    max_drops: 5,
    tier: 1,
    recipe: %{
      "heat" => RecipeHelper.axis(15, 25, 10, 1),
      "sunlight" => RecipeHelper.axis(50, 90, 20, 1)
    }
  },
  %{
    seed_name: "Marigold",
    growth_duration_hours: 2.0,
    min_drops: 2,
    max_drops: 6,
    tier: 2,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 35, 10, 1),
      "sunlight" => RecipeHelper.axis(60, 100, 20, 1),
      "waterings" => RecipeHelper.axis(1, 2, 1, 0.5)
    }
  },
  %{
    seed_name: "Snowdrop",
    growth_duration_hours: 2.5,
    min_drops: 2,
    max_drops: 6,
    tier: 2,
    recipe: %{
      "heat" => RecipeHelper.axis(0, 10, 10, 1.5),
      "humidity" => RecipeHelper.axis(50, 80, 20, 1),
      "waterings" => RecipeHelper.axis(1, 2, 1, 0.5)
    }
  },
  %{
    seed_name: "Mint",
    growth_duration_hours: 3.0,
    min_drops: 2,
    max_drops: 8,
    tier: 3,
    recipe: %{
      "humidity" => RecipeHelper.axis(50, 80, 20, 1),
      "rain" => RecipeHelper.axis(0.2, 0.6, 0.3, 1.5),
      "waterings" => RecipeHelper.axis(1, 2, 1, 0.5)
    }
  },
  %{
    seed_name: "Lavender",
    growth_duration_hours: 5.0,
    min_drops: 2,
    max_drops: 9,
    tier: 4,
    recipe: %{
      "heat" => RecipeHelper.axis(25, 35, 10, 1.5),
      "wind" => RecipeHelper.axis(5, 15, 5, 1),
      "sunlight" => RecipeHelper.axis(70, 100, 20, 1.5)
    }
  },
  %{
    seed_name: "Pansy",
    growth_duration_hours: 6.0,
    min_drops: 2,
    max_drops: 8,
    tier: 3,
    recipe: %{
      "heat" => RecipeHelper.axis(5, 15, 10, 1.5),
      "sunlight" => RecipeHelper.axis(40, 80, 20, 1),
      "rain" => RecipeHelper.axis(0.2, 0.6, 0.3, 1)
    }
  },
  %{
    seed_name: "Poppy",
    growth_duration_hours: 8.0,
    min_drops: 3,
    max_drops: 10,
    tier: 5,
    recipe: %{
      "heat" => RecipeHelper.axis(15, 25, 10, 1),
      "humidity" => RecipeHelper.axis(40, 75, 20, 1),
      "rain" => RecipeHelper.axis(0.3, 0.7, 0.3, 1.5)
    }
  },
  %{
    seed_name: "Jasmine",
    growth_duration_hours: 12.0,
    min_drops: 3,
    max_drops: 12,
    tier: 6,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 30, 10, 1),
      "humidity" => RecipeHelper.axis(60, 90, 20, 1.5),
      "waterings" => RecipeHelper.axis(2, 4, 2, 1)
    }
  },
  %{
    seed_name: "Rosemary",
    growth_duration_hours: 18.0,
    min_drops: 3,
    max_drops: 14,
    tier: 7,
    recipe: %{
      "heat" => RecipeHelper.axis(20, 35, 10, 1),
      "wind" => RecipeHelper.axis(5, 20, 5, 1),
      "sunlight" => RecipeHelper.axis(60, 100, 20, 1.5)
    }
  },
  %{
    seed_name: "Dahlia",
    growth_duration_hours: 30.0,
    min_drops: 4,
    max_drops: 16,
    tier: 8,
    recipe: %{
      "heat" => RecipeHelper.axis(18, 28, 8, 1),
      "humidity" => RecipeHelper.axis(50, 80, 20, 1),
      "sunlight" => RecipeHelper.axis(50, 90, 20, 1),
      "waterings" => RecipeHelper.axis(3, 6, 2, 1)
    }
  },
  %{
    seed_name: "Moonflower",
    growth_duration_hours: 48.0,
    min_drops: 5,
    max_drops: 20,
    tier: 9,
    recipe: %{
      "humidity" => RecipeHelper.axis(60, 90, 20, 1),
      "moon" => RecipeHelper.axis(4, 4, 0, 3),
      "waterings" => RecipeHelper.axis(3, 6, 2, 1)
    }
  }
]

replace_fields = [:growth_duration_hours, :min_drops, :max_drops, :tier, :recipe, :updated_at]

for config <- seed_configs do
  %SeedConfig{}
  |> SeedConfig.changeset(config)
  |> Repo.insert!(
    on_conflict: {:replace, replace_fields},
    conflict_target: :seed_name
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
      %{"seed" => "Sprouts", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Cress", "weight" => 3, "minCount" => 1, "maxCount" => 2}
    ]
  },
  %{
    quest_name: "SwampForage",
    description: "Forage in the nearby swamp for useful seeds.",
    duration_minutes: 5,
    required_flame_level: 2,
    reward_rolls: 2,
    reward_pool: [
      %{"seed" => "Cress", "weight" => 4, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Basil", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Chamomile", "weight" => 2, "minCount" => 1, "maxCount" => 2}
    ]
  },
  %{
    quest_name: "MeadowExpedition",
    description: "Explore the meadow for wildflowers.",
    duration_minutes: 15,
    required_flame_level: 3,
    reward_rolls: 3,
    reward_pool: [
      %{"seed" => "Marigold", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Snowdrop", "weight" => 2, "minCount" => 1, "maxCount" => 2}
    ]
  },
  %{
    quest_name: "DeepWoodsTrek",
    description: "Trek deep into the woods for rare finds.",
    duration_minutes: 60,
    required_flame_level: 4,
    reward_rolls: 3,
    reward_pool: [
      %{"seed" => "Mint", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Pansy", "weight" => 2, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "HighlandPass",
    description: "Cross the highland pass to find mountain herbs.",
    duration_minutes: 120,
    required_flame_level: 5,
    reward_rolls: 3,
    reward_pool: [
      %{"seed" => "Lavender", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Marigold", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "DeepMarsh",
    description: "Navigate the deep marsh for exotic plants.",
    duration_minutes: 240,
    required_flame_level: 6,
    reward_rolls: 4,
    reward_pool: [
      %{"seed" => "Poppy", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Mint", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "MountainAscent",
    description: "Scale the mountain for high-altitude flora.",
    duration_minutes: 360,
    required_flame_level: 7,
    reward_rolls: 4,
    reward_pool: [
      %{"seed" => "Jasmine", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Lavender", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "MoonlitPath",
    description: "Follow the moonlit path through enchanted woods.",
    duration_minutes: 480,
    required_flame_level: 8,
    reward_rolls: 4,
    reward_pool: [
      %{"seed" => "Rosemary", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Pansy", "weight" => 1, "minCount" => 1, "maxCount" => 1}
    ]
  },
  %{
    quest_name: "AncientGrove",
    description: "Explore the ancient grove for legendary seeds.",
    duration_minutes: 720,
    required_flame_level: 9,
    reward_rolls: 5,
    reward_pool: [
      %{"seed" => "Dahlia", "weight" => 3, "minCount" => 1, "maxCount" => 2},
      %{"seed" => "Moonflower", "weight" => 1, "minCount" => 1, "maxCount" => 1},
      %{"seed" => "Rosemary", "weight" => 1, "minCount" => 1, "maxCount" => 1}
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
    yield_item: "Berry",
    yield_amount: 3,
    yield_interval_hours: 12.0,
    water_required: 3,
    mana_cost: 0.0
  },
  %{
    plant_name: "Oak",
    growth_duration_hours: 48.0,
    yield_item: "Acorn",
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
        %{"ingredients" => [%{"itemName" => "Sprouts", "count" => 10}]},
        %{
          "ingredients" => [
            %{"itemName" => "Sprouts", "count" => 30},
            %{"itemName" => "Cress", "count" => 5}
          ]
        },
        %{"ingredients" => [%{"itemName" => "Basil", "count" => 5}]},
        %{"ingredients" => [%{"itemName" => "Chamomile", "count" => 5}]},
        %{
          "ingredients" => [
            %{"itemName" => "Marigold", "count" => 12},
            %{"itemName" => "Snowdrop", "count" => 8},
            %{"itemName" => "Basil", "count" => 8}
          ]
        },
        %{
          "ingredients" => [
            %{"itemName" => "Mint", "count" => 8},
            %{"itemName" => "Pansy", "count" => 4},
            %{"itemName" => "Chamomile", "count" => 8}
          ]
        },
        %{
          "ingredients" => [
            %{"itemName" => "Lavender", "count" => 22},
            %{"itemName" => "Snowdrop", "count" => 24},
            %{"itemName" => "Basil", "count" => 18}
          ]
        },
        %{
          "ingredients" => [
            %{"itemName" => "Poppy", "count" => 35},
            %{"itemName" => "Pansy", "count" => 30},
            %{"itemName" => "Marigold", "count" => 50}
          ]
        },
        %{
          "ingredients" => [
            %{"itemName" => "Jasmine", "count" => 60},
            %{"itemName" => "Lavender", "count" => 50},
            %{"itemName" => "Poppy", "count" => 60}
          ]
        },
        %{
          "ingredients" => [
            %{"itemName" => "Rosemary", "count" => 50},
            %{"itemName" => "Jasmine", "count" => 60},
            %{"itemName" => "Lavender", "count" => 55},
            %{"itemName" => "Snowdrop", "count" => 40}
          ]
        },
        %{
          "ingredients" => [
            %{"itemName" => "Dahlia", "count" => 50},
            %{"itemName" => "Moonflower", "count" => 30},
            %{"itemName" => "Rosemary", "count" => 60},
            %{"itemName" => "Poppy", "count" => 80},
            %{"itemName" => "Basil", "count" => 50}
          ]
        }
      ],
      "plot_costs" => [
        %{"manaCost" => 50, "harvestCosts" => [%{"itemName" => "Cress", "count" => 1}]},
        %{"manaCost" => 200, "harvestCosts" => [%{"itemName" => "Cress", "count" => 4}]},
        %{"manaCost" => 260, "harvestCosts" => [%{"itemName" => "Basil", "count" => 2}]},
        %{"manaCost" => 330, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 1}]},
        %{"manaCost" => 420, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 1}]},
        %{
          "manaCost" => 520,
          "harvestCosts" => [
            %{"itemName" => "Chamomile", "count" => 1},
            %{"itemName" => "Basil", "count" => 1}
          ]
        },
        %{"manaCost" => 640, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 2}]},
        %{
          "manaCost" => 780,
          "harvestCosts" => [
            %{"itemName" => "Lavender", "count" => 1},
            %{"itemName" => "Chamomile", "count" => 1}
          ]
        },
        %{
          "manaCost" => 940,
          "harvestCosts" => [
            %{"itemName" => "Mint", "count" => 1},
            %{"itemName" => "Lavender", "count" => 1}
          ]
        },
        %{
          "manaCost" => 1120,
          "harvestCosts" => [
            %{"itemName" => "Mint", "count" => 2},
            %{"itemName" => "Chamomile", "count" => 1}
          ]
        }
      ],
      "vase_costs" => [
        %{"manaCost" => 100, "harvestCosts" => [%{"itemName" => "Cress", "count" => 1}]},
        %{"manaCost" => 120, "harvestCosts" => [%{"itemName" => "Basil", "count" => 2}]},
        %{"manaCost" => 150, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 1}]},
        %{"manaCost" => 180, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 1}]},
        %{
          "manaCost" => 220,
          "harvestCosts" => [
            %{"itemName" => "Chamomile", "count" => 1},
            %{"itemName" => "Basil", "count" => 1}
          ]
        },
        %{"manaCost" => 260, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 2}]},
        %{
          "manaCost" => 310,
          "harvestCosts" => [
            %{"itemName" => "Lavender", "count" => 1},
            %{"itemName" => "Basil", "count" => 1}
          ]
        },
        %{
          "manaCost" => 370,
          "harvestCosts" => [
            %{"itemName" => "Lavender", "count" => 1},
            %{"itemName" => "Chamomile", "count" => 1}
          ]
        }
      ],
      "garden_costs" => [
        %{"manaCost" => 550, "harvestCosts" => [%{"itemName" => "Chamomile", "count" => 3}]},
        %{"manaCost" => 850, "harvestCosts" => [%{"itemName" => "Lavender", "count" => 3}]},
        %{
          "manaCost" => 1200,
          "harvestCosts" => [
            %{"itemName" => "Mint", "count" => 3},
            %{"itemName" => "Chamomile", "count" => 2}
          ]
        },
        %{
          "manaCost" => 1800,
          "harvestCosts" => [
            %{"itemName" => "Lavender", "count" => 4},
            %{"itemName" => "Mint", "count" => 3}
          ]
        },
        %{
          "manaCost" => 2500,
          "harvestCosts" => [
            %{"itemName" => "Poppy", "count" => 3},
            %{"itemName" => "Lavender", "count" => 4}
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
      "speed_item" => "Energy_Drink"
    }
  },
  %{
    key: "mallum_house_config",
    value: %{
      "mallums_per_house" => 1,
      "quest_speed_item" => "Energy_Drink",
      "house_costs" => [
        %{"manaCost" => 10, "harvestCosts" => [%{"itemName" => "Sprouts", "count" => 1}]},
        %{"manaCost" => 100, "harvestCosts" => [%{"itemName" => "Basil", "count" => 2}]},
        %{"manaCost" => 500, "harvestCosts" => [%{"itemName" => "Lavender", "count" => 3}]},
        %{
          "manaCost" => 1000,
          "harvestCosts" => [
            %{"itemName" => "Chamomile", "count" => 2},
            %{"itemName" => "Mint", "count" => 2}
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
      "speed_item" => "Speed_Potion"
    }
  },
  %{
    key: "new_player_config",
    value: %{
      "mana" => 40,
      "gems" => 5,
      "starting_water" => 1,
      "seeds" => [%{"name" => "Sprouts", "count" => 2}],
      "items" => [
        %{"name" => "Speed_Potion", "count" => 3},
        %{"name" => "Energy_Drink", "count" => 2}
      ]
    }
  },
  %{
    key: "recipe_configs",
    value: %{
      "Basil_Pigment" => %{
        "ingredients" => [%{"item_name" => "Basil", "count" => 3}],
        "result_item" => "Basil_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Chamomile_Pigment" => %{
        "ingredients" => [%{"item_name" => "Chamomile", "count" => 3}],
        "result_item" => "Chamomile_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Dahlia_Pigment" => %{
        "ingredients" => [%{"item_name" => "Dahlia", "count" => 3}],
        "result_item" => "Dahlia_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Jasmine_Pigment" => %{
        "ingredients" => [%{"item_name" => "Jasmine", "count" => 3}],
        "result_item" => "Jasmine_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Lavender_Pigment" => %{
        "ingredients" => [%{"item_name" => "Lavender", "count" => 3}],
        "result_item" => "Lavender_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Marigold_Pigment" => %{
        "ingredients" => [%{"item_name" => "Marigold", "count" => 3}],
        "result_item" => "Marigold_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Mint_Pigment" => %{
        "ingredients" => [%{"item_name" => "Mint", "count" => 3}],
        "result_item" => "Mint_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Moonflower_Pigment" => %{
        "ingredients" => [%{"item_name" => "Moonflower", "count" => 3}],
        "result_item" => "Moonflower_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Pansy_Pigment" => %{
        "ingredients" => [%{"item_name" => "Pansy", "count" => 3}],
        "result_item" => "Pansy_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Poppy_Pigment" => %{
        "ingredients" => [%{"item_name" => "Poppy", "count" => 3}],
        "result_item" => "Poppy_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Rosemary_Pigment" => %{
        "ingredients" => [%{"item_name" => "Rosemary", "count" => 3}],
        "result_item" => "Rosemary_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Snowdrop_Pigment" => %{
        "ingredients" => [%{"item_name" => "Snowdrop", "count" => 3}],
        "result_item" => "Snowdrop_Pigment",
        "result_quantity" => 1,
        "category" => "Pigment"
      },
      "Speed_Potion" => %{
        "ingredients" => [
          %{"item_name" => "Mint", "count" => 4},
          %{"item_name" => "Chamomile", "count" => 3}
        ],
        "result_item" => "Speed_Potion",
        "result_quantity" => 1,
        "category" => "Potion"
      },
      "Fertilizer" => %{
        "ingredients" => [
          %{"item_name" => "Berry", "count" => 3},
          %{"item_name" => "Acorn", "count" => 1}
        ],
        "result_item" => "Fertilizer",
        "result_quantity" => 1,
        "category" => "Material"
      }
    }
  },
  %{
    key: "skin_configs",
    value:
      Enum.reduce(
        ~w(Basil Chamomile Dahlia Jasmine Lavender Marigold Mint Moonflower Pansy Poppy Rosemary Snowdrop),
        %{},
        fn plant, acc ->
          pigment = "#{plant}_Pigment"

          acc
          |> Map.put("#{plant}_plot", %{
            "building_type" => "plot",
            "cost_item_name" => pigment,
            "cost_quantity" => 1
          })
          |> Map.put("#{plant}_vase", %{
            "building_type" => "vase",
            "cost_item_name" => pigment,
            "cost_quantity" => 1
          })
          |> Map.put("#{plant}_house", %{
            "building_type" => "mallum_house",
            "cost_item_name" => pigment,
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
