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

IO.puts("Seeds complete.")
