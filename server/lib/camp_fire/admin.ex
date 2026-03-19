defmodule CampFire.Admin do
  @moduledoc """
  Admin context providing CRUD operations for all game configuration types
  and player data queries for the admin dashboard.
  """

  import Ecto.Query
  import Ecto.Changeset

  alias CampFire.Repo
  alias CampFire.Admin.{QuestConfig, GardenConfig, GameConfig}
  alias CampFire.Game.{SeedConfig, PlayerPlot, PlayerVase, PlayerGarden, PlayerMallum, WeatherCache}
  alias CampFire.Economy.PlayerEconomy
  alias CampFire.Accounts.Player
  alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule}

  # ---------------------------------------------------------------------------
  # Seeds (SeedConfig)
  # ---------------------------------------------------------------------------

  def list_seeds do
    Repo.all(
      from s in SeedConfig,
        join: item in CampFire.Game.Item, on: item.id == s.item_id,
        join: harvest_item in CampFire.Game.Item, on: harvest_item.id == s.harvest_item_id,
        order_by: item.item_key,
        preload: [:item, :harvest_item]
    )
  end

  def get_seed!(id), do: Repo.get!(SeedConfig, id) |> Repo.preload([:item, :harvest_item])

  def create_seed(attrs) do
    %SeedConfig{}
    |> SeedConfig.changeset(attrs)
    |> Repo.insert()
  end

  def update_seed(%SeedConfig{} = seed, attrs) do
    seed
    |> SeedConfig.changeset(attrs)
    |> Repo.update()
  end

  def delete_seed(%SeedConfig{} = seed), do: Repo.delete(seed)

  # ---------------------------------------------------------------------------
  # Quests (QuestConfig)
  # ---------------------------------------------------------------------------

  def list_quests do
    Repo.all(from q in QuestConfig, order_by: [q.required_flame_level, q.quest_name])
  end

  def get_quest!(id), do: Repo.get!(QuestConfig, id)

  def create_quest(attrs) do
    %QuestConfig{}
    |> QuestConfig.changeset(attrs)
    |> Repo.insert()
  end

  def update_quest(%QuestConfig{} = quest, attrs) do
    quest
    |> QuestConfig.changeset(attrs)
    |> Repo.update()
  end

  def delete_quest(%QuestConfig{} = quest), do: Repo.delete(quest)

  # ---------------------------------------------------------------------------
  # Gardens (GardenConfig)
  # ---------------------------------------------------------------------------

  def list_gardens do
    Repo.all(from g in GardenConfig, order_by: g.plant_name)
  end

  def get_garden!(id), do: Repo.get!(GardenConfig, id)

  def create_garden(attrs) do
    %GardenConfig{}
    |> GardenConfig.changeset(attrs)
    |> Repo.insert()
  end

  def update_garden(%GardenConfig{} = garden, attrs) do
    garden
    |> GardenConfig.changeset(attrs)
    |> Repo.update()
  end

  def delete_garden(%GardenConfig{} = garden), do: Repo.delete(garden)

  # ---------------------------------------------------------------------------
  # Game Config (key-value store)
  # ---------------------------------------------------------------------------

  def list_game_configs do
    Repo.all(from c in GameConfig, order_by: c.key)
  end

  def get_game_config!(id), do: Repo.get!(GameConfig, id)

  def get_game_config_by_key(key) do
    Repo.get_by(GameConfig, key: key)
  end

  def upsert_game_config(key, value) do
    case get_game_config_by_key(key) do
      nil ->
        %GameConfig{}
        |> GameConfig.changeset(%{key: key, value: value})
        |> Repo.insert()

      existing ->
        existing
        |> GameConfig.changeset(%{value: value})
        |> Repo.update()
    end
  end

  # ---------------------------------------------------------------------------
  # Recipes (stored in game_configs as "recipe_configs" JSON map)
  # ---------------------------------------------------------------------------

  def list_recipes do
    case get_game_config_by_key("recipe_configs") do
      nil -> %{}
      config -> config.value || %{}
    end
  end

  def get_recipe(name) do
    Map.get(list_recipes(), name)
  end

  def upsert_recipe(name, recipe_data) do
    recipes = list_recipes()
    updated = Map.put(recipes, name, recipe_data)
    upsert_game_config("recipe_configs", updated)
  end

  def delete_recipe(name) do
    recipes = list_recipes() |> Map.delete(name)
    upsert_game_config("recipe_configs", recipes)
  end

  def rename_recipe(old_name, new_name, recipe_data) do
    recipes = list_recipes() |> Map.delete(old_name) |> Map.put(new_name, recipe_data)
    upsert_game_config("recipe_configs", recipes)
  end

  # ---------------------------------------------------------------------------
  # Skins (stored in game_configs as "skin_configs" JSON map)
  # ---------------------------------------------------------------------------

  def list_skins do
    case get_game_config_by_key("skin_configs") do
      nil -> %{}
      config -> config.value || %{}
    end
  end

  def get_skin(name) do
    Map.get(list_skins(), name)
  end

  def upsert_skin(name, skin_data) do
    skins = list_skins()
    updated = Map.put(skins, name, skin_data)
    upsert_game_config("skin_configs", updated)
  end

  def delete_skin(name) do
    skins = list_skins() |> Map.delete(name)
    upsert_game_config("skin_configs", skins)
  end

  def rename_skin(old_name, new_name, skin_data) do
    skins = list_skins() |> Map.delete(old_name) |> Map.put(new_name, skin_data)
    upsert_game_config("skin_configs", skins)
  end

  # ---------------------------------------------------------------------------
  # Visitors (VisitorTemplate)
  # ---------------------------------------------------------------------------

  def list_visitors do
    Repo.all(from v in VisitorTemplate, order_by: v.visitor_id)
  end

  def get_visitor!(id), do: Repo.get!(VisitorTemplate, id)

  def create_visitor(attrs) do
    %VisitorTemplate{}
    |> visitor_changeset(attrs)
    |> Repo.insert()
  end

  def update_visitor(%VisitorTemplate{} = visitor, attrs) do
    visitor
    |> visitor_changeset(attrs)
    |> Repo.update()
  end

  def delete_visitor(%VisitorTemplate{} = visitor), do: Repo.delete(visitor)

  def list_visitor_schedule do
    Repo.all(from s in VisitorSchedule, order_by: [desc: s.date])
  end

  defp visitor_changeset(%VisitorTemplate{} = template, attrs) do
    template
    |> cast(attrs, [
      :visitor_id, :name, :portrait_id, :type, :flame_level_min,
      :dialogue_pool, :offer_pool, :gift_pool, :quest_pool, :weight
    ])
    |> validate_required([:visitor_id, :name, :type])
    |> unique_constraint(:visitor_id)
  end

  # ---------------------------------------------------------------------------
  # Players
  # ---------------------------------------------------------------------------

  def search_players(query) when is_binary(query) and byte_size(query) > 0 do
    pattern = "%#{query}%"

    Repo.all(
      from p in Player,
        where:
          ilike(p.uid, ^pattern) or
            ilike(p.display_name, ^pattern) or
            ilike(p.friend_code, ^pattern),
        order_by: p.display_name,
        limit: 50
    )
  end

  def search_players(_) do
    Repo.all(
      from p in Player,
        order_by: [desc: p.updated_at],
        limit: 100
    )
  end

  def get_player_detail(uid) when is_binary(uid) do
    case Repo.get_by(Player, uid: uid) do
      nil ->
        nil

      player ->
        %{
          player: player,
          economy: Repo.get(PlayerEconomy, uid),
          inventory: CampFire.Economy.list_inventory(uid),
          plots: Repo.all(from p in PlayerPlot, where: p.player_uid == ^uid),
          vases: Repo.all(from v in PlayerVase, where: v.player_uid == ^uid),
          gardens: Repo.all(from g in PlayerGarden, where: g.player_uid == ^uid),
          mallums: Repo.all(from m in PlayerMallum, where: m.player_uid == ^uid)
        }
    end
  end

  def update_economy(uid, attrs) when is_binary(uid) do
    case Repo.get(PlayerEconomy, uid) do
      nil ->
        {:error, :not_found}

      economy ->
        economy
        |> PlayerEconomy.changeset(attrs)
        |> Repo.update()
    end
  end

  def update_player_name(uid, display_name) do
    case Repo.get_by(Player, uid: uid) do
      nil -> {:error, :not_found}
      player -> player |> change(%{display_name: display_name}) |> Repo.update()
    end
  end

  # ---------------------------------------------------------------------------
  # Inventory (admin)
  # ---------------------------------------------------------------------------

  def list_all_items do
    Repo.all(from i in CampFire.Game.Item, order_by: i.item_key)
  end

  def set_inventory_count(uid, item_key, count) do
    item = Repo.get_by!(CampFire.Game.Item, item_key: item_key)

    case Repo.get_by(CampFire.Economy.PlayerInventory, player_uid: uid, item_id: item.id) do
      nil when count > 0 ->
        %CampFire.Economy.PlayerInventory{}
        |> CampFire.Economy.PlayerInventory.changeset(%{player_uid: uid, item_id: item.id, count: count})
        |> Repo.insert()

      nil ->
        {:ok, nil}

      inv when count <= 0 ->
        Repo.delete(inv)

      inv ->
        inv |> CampFire.Economy.PlayerInventory.changeset(%{count: count}) |> Repo.update()
    end
  end

  def delete_inventory_item(uid, item_key) do
    item = Repo.get_by!(CampFire.Game.Item, item_key: item_key)

    case Repo.get_by(CampFire.Economy.PlayerInventory, player_uid: uid, item_id: item.id) do
      nil -> {:ok, nil}
      inv -> Repo.delete(inv)
    end
  end

  # ---------------------------------------------------------------------------
  # Player entities (admin CRUD)
  # ---------------------------------------------------------------------------

  def create_player_plot(attrs), do: %PlayerPlot{} |> PlayerPlot.changeset(attrs) |> Repo.insert()
  def update_plot(id, attrs), do: Repo.get!(PlayerPlot, id) |> PlayerPlot.changeset(attrs) |> Repo.update()
  def delete_plot(id), do: Repo.get!(PlayerPlot, id) |> Repo.delete()

  def create_player_vase(attrs), do: %PlayerVase{} |> PlayerVase.changeset(attrs) |> Repo.insert()
  def update_vase(id, attrs), do: Repo.get!(PlayerVase, id) |> PlayerVase.changeset(attrs) |> Repo.update()
  def delete_vase(id), do: Repo.get!(PlayerVase, id) |> Repo.delete()

  def create_player_garden(attrs), do: %PlayerGarden{} |> PlayerGarden.changeset(attrs) |> Repo.insert()
  def update_player_garden(id, attrs), do: Repo.get!(PlayerGarden, id) |> PlayerGarden.changeset(attrs) |> Repo.update()
  def delete_player_garden(id), do: Repo.get!(PlayerGarden, id) |> Repo.delete()

  def create_player_mallum(attrs), do: %PlayerMallum{} |> PlayerMallum.changeset(attrs) |> Repo.insert()
  def update_mallum(id, attrs), do: Repo.get!(PlayerMallum, id) |> PlayerMallum.changeset(attrs) |> Repo.update()
  def delete_mallum(id), do: Repo.get!(PlayerMallum, id) |> Repo.delete()

  # ---------------------------------------------------------------------------
  # Weather
  # ---------------------------------------------------------------------------

  def list_weather_caches do
    Repo.all(from w in WeatherCache, order_by: [desc: w.fetched_at])
  end

  def weather_player_counts do
    from(pe in PlayerEconomy,
      where: not is_nil(pe.lat) and not is_nil(pe.lon),
      group_by: [
        fragment("ROUND(CAST(? AS numeric), 2)", pe.lat),
        fragment("ROUND(CAST(? AS numeric), 2)", pe.lon)
      ],
      select: %{
        lat: fragment("ROUND(CAST(? AS numeric), 2)", pe.lat),
        lon: fragment("ROUND(CAST(? AS numeric), 2)", pe.lon),
        count: count(pe.player_uid)
      }
    )
    |> Repo.all()
  end

  def active_location_count do
    CampFire.Game.Weather.active_locations() |> length()
  end
end
