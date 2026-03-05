defmodule CampFire.Admin do
  @moduledoc """
  Admin context providing CRUD operations for all game configuration types
  and player data queries for the admin dashboard.
  """

  import Ecto.Query
  import Ecto.Changeset

  alias CampFire.Repo
  alias CampFire.Admin.{QuestConfig, GardenConfig, GameConfig}
  alias CampFire.Game.{SeedConfig, PlayerPlot, PlayerVase, PlayerGarden, PlayerMallum}
  alias CampFire.Economy.{PlayerEconomy, PlayerSeed, PlayerItem}
  alias CampFire.Accounts.Player
  alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule}

  # ---------------------------------------------------------------------------
  # Seeds (SeedConfig)
  # ---------------------------------------------------------------------------

  def list_seeds do
    Repo.all(from s in SeedConfig, order_by: s.seed_name)
  end

  def get_seed!(id), do: Repo.get!(SeedConfig, id)

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
    Repo.all(from q in QuestConfig, order_by: q.quest_name)
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
          seeds: Repo.all(from s in PlayerSeed, where: s.player_uid == ^uid),
          items: Repo.all(from i in PlayerItem, where: i.player_uid == ^uid),
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
end
