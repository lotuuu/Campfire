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
      ["The road was long, but your flame drew me in.", "I've got rare seeds from distant lands.", "Care to trade?"],
      ["Ah, another campsite with good soil.", "Let's do business, shall we?"],
      ["I picked these up on the coast, past the marshes.", "They don't grow just anywhere.", "What have you got to offer in return?"]
    ],
    offer_pool: [
      %{"costs" => [%{"itemName" => "Basil Leaf", "count" => 2}], "rewardSeedName" => "Lavender", "rewardCount" => 1},
      %{"costs" => [%{"itemName" => "Chamomile Petal", "count" => 3}], "rewardSeedName" => "Mint", "rewardCount" => 1},
      %{"costs" => [%{"itemName" => "Mint Leaf", "count" => 2}], "rewardSeedName" => "Rosemary", "rewardCount" => 1},
      %{"costs" => [%{"itemName" => "Lavender Petal", "count" => 2}], "rewardSeedName" => "Dahlia", "rewardCount" => 1}
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
      ["What a lovely campsite you have here!", "Please, take this gift. It's the least I can do.", "May your garden flourish!"],
      ["I was just passing through and felt your flame's warmth.", "Here, I hope this helps your garden grow."]
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
      ["I've been wandering for a long time, looking for the right campsite.", "Yours has the right energy. I have a request."]
    ],
    offer_pool: [],
    gift_pool: [],
    quest_pool: [
      %{
        "request_item" => "Lavender Petal", "request_count" => 3, "return_days" => 7,
        "reward" => %{"type" => "seed", "name" => "Moonflower", "count" => 2},
        "return_dialogue" => ["You found them!", "Here, take these rare seeds as thanks."]
      },
      %{
        "request_item" => "Chamomile Petal", "request_count" => 5, "return_days" => 5,
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
      from vs in VisitorSchedule,
        where: vs.visitor_id == ^s.visitor_id and vs.visit_number == ^s.visit_number
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
  %{seed_name: "Sprouts", growth_duration_hours: 0.00833, base_drops: 1, mana_cost: 0.0, recipe: %{
    "humidity" => RecipeHelper.axis(40, 80, 20, 1),
    "waterings" => RecipeHelper.axis(1, 1, 1, 0.5)
  }},
  %{seed_name: "Cress", growth_duration_hours: 0.08333, base_drops: 1, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(10, 25, 15, 1),
    "humidity" => RecipeHelper.axis(50, 85, 15, 1)
  }},
  %{seed_name: "Basil", growth_duration_hours: 1.0, base_drops: 1, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(20, 30, 10, 1),
    "waterings" => RecipeHelper.axis(1, 1, 1, 0.5)
  }},
  %{seed_name: "Chamomile", growth_duration_hours: 1.5, base_drops: 2, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(15, 25, 10, 1),
    "sunlight" => RecipeHelper.axis(50, 90, 20, 1)
  }},
  %{seed_name: "Marigold", growth_duration_hours: 2.0, base_drops: 2, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(20, 35, 10, 1),
    "sunlight" => RecipeHelper.axis(60, 100, 20, 1),
    "waterings" => RecipeHelper.axis(1, 2, 1, 0.5)
  }},
  %{seed_name: "Snowdrop", growth_duration_hours: 2.5, base_drops: 2, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(0, 10, 10, 1.5),
    "humidity" => RecipeHelper.axis(50, 80, 20, 1),
    "waterings" => RecipeHelper.axis(1, 2, 1, 0.5)
  }},
  %{seed_name: "Mint", growth_duration_hours: 3.0, base_drops: 3, mana_cost: 0.0, recipe: %{
    "humidity" => RecipeHelper.axis(50, 80, 20, 1),
    "rain" => RecipeHelper.axis(0.2, 0.6, 0.3, 1.5),
    "waterings" => RecipeHelper.axis(1, 2, 1, 0.5)
  }},
  %{seed_name: "Lavender", growth_duration_hours: 5.0, base_drops: 3, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(25, 35, 10, 1.5),
    "wind" => RecipeHelper.axis(5, 15, 5, 1),
    "sunlight" => RecipeHelper.axis(70, 100, 20, 1.5)
  }},
  %{seed_name: "Pansy", growth_duration_hours: 6.0, base_drops: 3, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(5, 15, 10, 1.5),
    "sunlight" => RecipeHelper.axis(40, 80, 20, 1),
    "rain" => RecipeHelper.axis(0.2, 0.6, 0.3, 1)
  }},
  %{seed_name: "Poppy", growth_duration_hours: 8.0, base_drops: 4, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(15, 25, 10, 1),
    "humidity" => RecipeHelper.axis(40, 75, 20, 1),
    "rain" => RecipeHelper.axis(0.3, 0.7, 0.3, 1.5)
  }},
  %{seed_name: "Jasmine", growth_duration_hours: 12.0, base_drops: 4, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(20, 30, 10, 1),
    "humidity" => RecipeHelper.axis(60, 90, 20, 1.5),
    "waterings" => RecipeHelper.axis(2, 4, 2, 1)
  }},
  %{seed_name: "Rosemary", growth_duration_hours: 18.0, base_drops: 5, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(20, 35, 10, 1),
    "wind" => RecipeHelper.axis(5, 20, 5, 1),
    "sunlight" => RecipeHelper.axis(60, 100, 20, 1.5)
  }},
  %{seed_name: "Dahlia", growth_duration_hours: 30.0, base_drops: 6, mana_cost: 0.0, recipe: %{
    "heat" => RecipeHelper.axis(18, 28, 8, 1),
    "humidity" => RecipeHelper.axis(50, 80, 20, 1),
    "sunlight" => RecipeHelper.axis(50, 90, 20, 1),
    "waterings" => RecipeHelper.axis(3, 6, 2, 1)
  }},
  %{seed_name: "Moonflower", growth_duration_hours: 48.0, base_drops: 8, mana_cost: 0.0, recipe: %{
    "humidity" => RecipeHelper.axis(60, 90, 20, 1),
    "moon" => RecipeHelper.axis(4, 4, 0, 3),
    "waterings" => RecipeHelper.axis(3, 6, 2, 1)
  }}
]

replace_fields = [:growth_duration_hours, :base_drops, :mana_cost, :recipe, :updated_at]

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
  %{quest_name: "SwampForage", duration_minutes: 5, required_flame_level: 1, reward_rolls: 2,
    reward_pool: [%{"seed_name" => "Basil", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Chamomile", "weight" => 2, "min" => 1, "max" => 2}]},
  %{quest_name: "MeadowExpedition", duration_minutes: 15, required_flame_level: 2, reward_rolls: 3,
    reward_pool: [%{"seed_name" => "Marigold", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Snowdrop", "weight" => 2, "min" => 1, "max" => 2}]},
  %{quest_name: "DeepWoodsTrek", duration_minutes: 60, required_flame_level: 3, reward_rolls: 3,
    reward_pool: [%{"seed_name" => "Mint", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Pansy", "weight" => 2, "min" => 1, "max" => 1}]},
  %{quest_name: "HighlandPass", duration_minutes: 120, required_flame_level: 4, reward_rolls: 3,
    reward_pool: [%{"seed_name" => "Lavender", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Marigold", "weight" => 1, "min" => 1, "max" => 1}]},
  %{quest_name: "DeepMarsh", duration_minutes: 240, required_flame_level: 5, reward_rolls: 4,
    reward_pool: [%{"seed_name" => "Poppy", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Mint", "weight" => 1, "min" => 1, "max" => 1}]},
  %{quest_name: "MountainAscent", duration_minutes: 360, required_flame_level: 6, reward_rolls: 4,
    reward_pool: [%{"seed_name" => "Jasmine", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Lavender", "weight" => 1, "min" => 1, "max" => 1}]},
  %{quest_name: "MoonlitPath", duration_minutes: 480, required_flame_level: 7, reward_rolls: 4,
    reward_pool: [%{"seed_name" => "Rosemary", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Pansy", "weight" => 1, "min" => 1, "max" => 1}]},
  %{quest_name: "AncientGrove", duration_minutes: 720, required_flame_level: 8, reward_rolls: 5,
    reward_pool: [%{"seed_name" => "Dahlia", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Moonflower", "weight" => 1, "min" => 1, "max" => 1}, %{"seed_name" => "Rosemary", "weight" => 1, "min" => 1, "max" => 1}]}
]

for q <- quests do
  %QuestConfig{}
  |> QuestConfig.changeset(q)
  |> Repo.insert!(on_conflict: :nothing, conflict_target: :quest_name)
end

# Garden configs
gardens = [
  %{plant_name: "BerryBush", growth_duration_hours: 24.0, yield_item: "Berry", yield_amount: 3, yield_interval_hours: 12.0, water_required: 3, mana_cost: 0.0},
  %{plant_name: "Oak", growth_duration_hours: 48.0, yield_item: "Acorn", yield_amount: 2, yield_interval_hours: 24.0, water_required: 5, mana_cost: 0.0}
]

for g <- gardens do
  %GardenConfig{}
  |> GardenConfig.changeset(g)
  |> Repo.insert!(on_conflict: :nothing, conflict_target: :plant_name)
end

# Game configs (economy constants)
game_configs = [
  %{key: "flame_config", value: %{
    "base_mana_per_second" => 0.5,
    "mana_per_level" => 0.3,
    "max_flame_level" => 12,
    "entity_caps" => [6, 6, 8, 8, 12, 15, 18, 22, 26, 30, 35, 40],
    "grid_sizes" => [2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5]
  }},
  %{key: "vase_config", value: %{
    "craft_cost_mana" => 100,
    "default_capacity" => 5,
    "fill_duration_minutes" => 30,
    "capacity_tiers" => [5, 8, 12, 20],
    "upgrade_costs" => [75, 200, 500]
  }},
  %{key: "mallum_house_config", value: %{
    "mallums_per_house" => 1,
    "house_costs" => [
      %{"mana" => 15, "harvests" => []},
      %{"mana" => 30, "harvests" => [%{"item" => "Basil_harvest", "count" => 2}]},
      %{"mana" => 60, "harvests" => [%{"item" => "Lavender_harvest", "count" => 3}]},
      %{"mana" => 100, "harvests" => [%{"item" => "Chamomile_harvest", "count" => 2}, %{"item" => "Mint_harvest", "count" => 2}]}
    ]
  }}
]

for c <- game_configs do
  %GameConfig{}
  |> GameConfig.changeset(c)
  |> Repo.insert!(on_conflict: :nothing, conflict_target: :key)
end

IO.puts("Admin config seeds complete.")
