defmodule CampFire.ConfigCache do
  use GenServer

  @table :config_cache

  def start_link(_opts) do
    GenServer.start_link(__MODULE__, [], name: __MODULE__)
  end

  def get(key) do
    case :ets.lookup(@table, key) do
      [{^key, value}] -> value
      [] -> nil
    end
  end

  def refresh do
    GenServer.cast(__MODULE__, :refresh)
  end

  @impl true
  def init(_) do
    table = :ets.new(@table, [:named_table, :set, :public, read_concurrency: true])
    load_all()
    {:ok, %{table: table}}
  end

  @impl true
  def handle_cast(:refresh, state) do
    load_all()
    {:noreply, state}
  end

  defp load_all do
    configs = CampFire.Repo.all(CampFire.Admin.GameConfig)

    for config <- configs do
      :ets.insert(@table, {config.key, config.value})
    end

    quests = CampFire.Repo.all(CampFire.Admin.QuestConfig)

    quest_map =
      Map.new(quests, fn q ->
        {q.quest_name,
         %{
           quest_name: q.quest_name,
           duration_minutes: q.duration_minutes,
           required_flame_level: q.required_flame_level,
           reward_rolls: q.reward_rolls,
           reward_pool: q.reward_pool
         }}
      end)

    :ets.insert(@table, {"quest_configs", quest_map})

    gardens = CampFire.Repo.all(CampFire.Admin.GardenConfig)

    garden_map =
      Map.new(gardens, fn g ->
        {g.plant_name,
         %{
           plant_name: g.plant_name,
           growth_duration_hours: g.growth_duration_hours,
           yield_item: g.yield_item,
           yield_amount: g.yield_amount,
           yield_interval_hours: g.yield_interval_hours,
           water_required: g.water_required,
           mana_cost: g.mana_cost
         }}
      end)

    :ets.insert(@table, {"garden_configs", garden_map})

    seeds = CampFire.Repo.all(CampFire.Game.SeedConfig)

    seed_map =
      Map.new(seeds, fn s ->
        {s.seed_name,
         %{
           seed_name: s.seed_name,
           growth_duration_hours: s.growth_duration_hours,
           min_drops: s.min_drops,
           max_drops: s.max_drops,
           tier: s.tier,
           recipe: s.recipe
         }}
      end)

    :ets.insert(@table, {"seed_configs", seed_map})

    sprite_manifest = CampFire.SpriteManifest.build()
    :ets.insert(@table, {"sprite_manifest", sprite_manifest})
  end
end
