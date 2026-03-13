defmodule CampFire.Game do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.Item

  # --- Items ---

  def list_items do
    Repo.all(Item)
  end

  def list_items_by_category(category) do
    from(i in Item, where: i.category == ^category) |> Repo.all()
  end

  def get_item(id) when is_integer(id) do
    Repo.get(Item, id)
  end

  def get_item!(id) when is_integer(id) do
    Repo.get!(Item, id)
  end

  def get_item_by_key(item_key) when is_binary(item_key) do
    Repo.get_by(Item, item_key: item_key)
  end

  def get_item_by_key!(item_key) when is_binary(item_key) do
    Repo.get_by!(Item, item_key: item_key)
  end

  @doc "Resolve an item_key string to its integer ID via ConfigCache (fast, cached)."
  def resolve_item_id!(item_key) when is_binary(item_key) do
    CampFire.ConfigCache.resolve_item_id!(item_key)
  end

  @doc "Resolve an integer item ID to its item_key string via ConfigCache (fast, cached)."
  def resolve_item_key!(item_id) when is_integer(item_id) do
    CampFire.ConfigCache.resolve_item_key!(item_id)
  end

  # --- Seed Configs ---

  @doc """
  Look up a seed config from ConfigCache by harvest_item_key (the plant slug, e.g. "sprouts").
  Returns a map with :item_id, :item_key, :harvest_item_id, :harvest_item_key, etc.
  Raises if not found.
  """
  def get_seed_config!(harvest_item_key) when is_binary(harvest_item_key) do
    seed_configs = CampFire.ConfigCache.get("seed_configs") || %{}

    case Map.get(seed_configs, harvest_item_key) do
      nil -> raise "Unknown seed config for harvest_item_key: #{harvest_item_key}"
      config -> config
    end
  end

  @doc """
  Look up a seed config from ConfigCache by seed item_id (the FK stored in plots/birds).
  Returns a map with :item_id, :item_key, :harvest_item_id, :harvest_item_key, etc.
  Raises if not found.
  """
  def get_seed_config_by_item_id!(item_id) when is_integer(item_id) do
    seed_configs = CampFire.ConfigCache.get("seed_configs_by_item_id") || %{}

    case Map.get(seed_configs, item_id) do
      nil -> raise "Unknown seed config for item_id: #{item_id}"
      config -> config
    end
  end
end
