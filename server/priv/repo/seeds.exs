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
  %{seed_name: "Sprouts", growth_duration_hours: 0.0167, base_drops: 1, mana_cost: 0.0, recipe: %{}},
  %{seed_name: "Cress", growth_duration_hours: 0.5, base_drops: 1, mana_cost: 0.0, recipe: %{}},
  %{seed_name: "Basil", growth_duration_hours: 2.0, base_drops: 2, mana_cost: 5.0, recipe: %{
    "heat" => RecipeHelper.axis(18, 30, 10, 1),
    "waterings" => RecipeHelper.axis(2, 5, 2, 1)
  }},
  %{seed_name: "Mint", growth_duration_hours: 3.0, base_drops: 2, mana_cost: 8.0, recipe: %{
    "humidity" => RecipeHelper.axis(50, 80, 20, 1),
    "waterings" => RecipeHelper.axis(3, 6, 2, 1)
  }},
  %{seed_name: "Chamomile", growth_duration_hours: 4.0, base_drops: 2, mana_cost: 10.0, recipe: %{
    "heat" => RecipeHelper.axis(15, 25, 8, 1),
    "sunlight" => RecipeHelper.axis(30, 80, 20, 1)
  }},
  %{seed_name: "Lavender", growth_duration_hours: 6.0, base_drops: 3, mana_cost: 15.0, recipe: %{
    "heat" => RecipeHelper.axis(20, 35, 10, 1),
    "wind" => RecipeHelper.axis(5, 20, 10, 0.5),
    "waterings" => RecipeHelper.axis(2, 4, 2, 0.5)
  }},
  %{seed_name: "Rosemary", growth_duration_hours: 8.0, base_drops: 3, mana_cost: 20.0, recipe: %{
    "heat" => RecipeHelper.axis(15, 30, 10, 1),
    "humidity" => RecipeHelper.axis(30, 60, 15, 0.8)
  }},
  %{seed_name: "Marigold", growth_duration_hours: 5.0, base_drops: 2, mana_cost: 12.0, recipe: %{
    "sunlight" => RecipeHelper.axis(20, 60, 20, 1),
    "heat" => RecipeHelper.axis(18, 32, 10, 0.8)
  }},
  %{seed_name: "Poppy", growth_duration_hours: 7.0, base_drops: 3, mana_cost: 18.0, recipe: %{
    "rain" => RecipeHelper.axis(0.2, 0.6, 0.3, 1),
    "wind" => RecipeHelper.axis(0, 15, 10, 0.5)
  }},
  %{seed_name: "Dahlia", growth_duration_hours: 10.0, base_drops: 4, mana_cost: 25.0, recipe: %{
    "heat" => RecipeHelper.axis(20, 30, 8, 1),
    "humidity" => RecipeHelper.axis(40, 70, 15, 0.8),
    "waterings" => RecipeHelper.axis(4, 8, 3, 0.7)
  }},
  %{seed_name: "Jasmine", growth_duration_hours: 12.0, base_drops: 4, mana_cost: 30.0, recipe: %{
    "heat" => RecipeHelper.axis(22, 35, 8, 1),
    "moon" => RecipeHelper.axis(4, 6, 2, 0.8),
    "humidity" => RecipeHelper.axis(50, 80, 15, 0.6)
  }},
  %{seed_name: "Moonflower", growth_duration_hours: 16.0, base_drops: 5, mana_cost: 40.0, recipe: %{
    "moon" => RecipeHelper.axis(6, 8, 2, 1.5),
    "humidity" => RecipeHelper.axis(60, 90, 15, 0.8),
    "heat" => RecipeHelper.axis(10, 22, 8, 0.5)
  }},
  %{seed_name: "Snowdrop", growth_duration_hours: 14.0, base_drops: 4, mana_cost: 35.0, recipe: %{
    "heat" => RecipeHelper.axis(-5, 10, 8, 1.2),
    "rain" => RecipeHelper.axis(0.3, 0.8, 0.2, 0.8)
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
