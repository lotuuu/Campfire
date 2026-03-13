defmodule CampFire.Game do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{Item, SeedConfig}

  def list_items do
    Repo.all(Item)
  end

  def list_items_by_category(category) do
    from(i in Item, where: i.category == ^category) |> Repo.all()
  end

  def get_item(item_key) do
    Repo.get(Item, item_key)
  end

  def get_item!(item_key) do
    Repo.get!(Item, item_key)
  end

  def get_seed_config!(seed_name) do
    Repo.get_by!(SeedConfig, seed_name: seed_name)
  end
end
