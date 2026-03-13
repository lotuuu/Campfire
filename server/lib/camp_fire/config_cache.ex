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

  @doc "Resolve an item_key string to its integer ID. Raises on unknown key."
  def resolve_item_id!(item_key) do
    map = get("item_key_to_id") || %{}
    case Map.get(map, item_key) do
      nil -> raise "Unknown item key: #{item_key}"
      id -> id
    end
  end

  @doc "Resolve an integer item ID to its item_key string. Raises on unknown ID."
  def resolve_item_key!(item_id) do
    map = get("item_id_to_key") || %{}
    case Map.get(map, item_id) do
      nil -> raise "Unknown item id: #{item_id}"
      key -> key
    end
  end

  @impl true
  def init(_) do
    table = :ets.new(@table, [:named_table, :set, :public, read_concurrency: true])

    case load_all() do
      :ok -> :ok
      {:error, reason} ->
        require Logger
        Logger.warning("ConfigCache failed to load: #{inspect(reason)}, will retry in 5s")
        Process.send_after(self(), :retry_load, 5_000)
    end

    {:ok, %{table: table}}
  end

  @impl true
  def handle_info(:retry_load, state) do
    case load_all() do
      :ok -> :ok
      {:error, reason} ->
        require Logger
        Logger.warning("ConfigCache retry failed: #{inspect(reason)}, retrying in 5s")
        Process.send_after(self(), :retry_load, 5_000)
    end

    {:noreply, state}
  end

  @impl true
  def handle_cast(:refresh, state) do
    load_all()
    {:noreply, state}
  end

  defp load_all do
    try do
      load_all!()
      :ok
    rescue
      e -> {:error, e}
    end
  end

  defp load_all! do
    require Logger
    configs = CampFire.Repo.all(CampFire.Admin.GameConfig)

    configs =
      if configs == [] do
        Logger.info("ConfigCache: no game configs found, running seeds...")

        try do
          seed_path = Application.app_dir(:camp_fire, "priv/repo/seeds.exs")
          Code.eval_file(seed_path)
          CampFire.Repo.all(CampFire.Admin.GameConfig)
        rescue
          e ->
            Logger.error("ConfigCache: seed failed: #{inspect(e)}")
            raise "No game configs and seeding failed"
        end
      else
        configs
      end

    for config <- configs do
      :ets.insert(@table, {config.key, config.value})
    end

    quests = CampFire.Repo.all(CampFire.Admin.QuestConfig)

    quest_map =
      Map.new(quests, fn q ->
        {q.quest_name,
         %{
           quest_name: q.quest_name,
           description: q.description || "",
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

    # --- Items: load all, build lookup maps ---
    items = CampFire.Game.list_items()

    items_map =
      Map.new(items, fn i ->
        {i.item_key,
         %{
           "id" => i.id,
           "displayName" => i.display_name,
           "category" => i.category,
           "spriteKey" => i.sprite_key
         }}
      end)

    :ets.insert(@table, {"items", items_map})

    # Key-to-ID and ID-to-key resolution maps
    item_key_to_id = Map.new(items, fn i -> {i.item_key, i.id} end)
    item_id_to_key = Map.new(items, fn i -> {i.id, i.item_key} end)
    :ets.insert(@table, {"item_key_to_id", item_key_to_id})
    :ets.insert(@table, {"item_id_to_key", item_id_to_key})

    # --- Seed configs: keyed by harvest item_key (the "plant slug") ---
    import Ecto.Query
    seeds = CampFire.Repo.all(
      from sc in CampFire.Game.SeedConfig,
        join: seed_item in CampFire.Game.Item, on: seed_item.id == sc.item_id,
        join: harvest_item in CampFire.Game.Item, on: harvest_item.id == sc.harvest_item_id,
        select: %{
          item_id: sc.item_id,
          item_key: seed_item.item_key,
          harvest_item_id: sc.harvest_item_id,
          harvest_item_key: harvest_item.item_key,
          growth_duration_hours: sc.growth_duration_hours,
          min_drops: sc.min_drops,
          max_drops: sc.max_drops,
          tier: sc.tier,
          recipe: sc.recipe
        }
    )

    # Keyed by harvest item_key (plant slug) — e.g., "sprouts" => %{...}
    seed_map =
      Map.new(seeds, fn s ->
        {s.harvest_item_key, s}
      end)

    :ets.insert(@table, {"seed_configs", seed_map})

    # Also key by item_id for plot harvest lookups
    seed_map_by_item_id =
      Map.new(seeds, fn s -> {s.item_id, s} end)

    :ets.insert(@table, {"seed_configs_by_item_id", seed_map_by_item_id})

    sprite_manifest = CampFire.SpriteManifest.build()
    :ets.insert(@table, {"sprite_manifest", sprite_manifest})
  end
end
