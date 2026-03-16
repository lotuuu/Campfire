defmodule CampFire.Game.Item do
  use Ecto.Schema
  import Ecto.Changeset

  schema "items" do
    field :item_key, :string
    field :display_name, :string
    field :category, :string
    field :sprite_key, :string

    timestamps()
  end

  @valid_categories ~w(seed harvest pigment potion material consumable garden_seed)

  def changeset(item, attrs) do
    item
    |> cast(attrs, [:item_key, :display_name, :category, :sprite_key])
    |> validate_required([:item_key, :display_name, :category])
    |> validate_inclusion(:category, @valid_categories)
    |> unique_constraint(:item_key)
  end
end
