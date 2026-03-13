defmodule CampFire.Game.Item do
  use Ecto.Schema
  import Ecto.Changeset

  @primary_key {:item_key, :string, autogenerate: false}

  schema "items" do
    field :display_name, :string
    field :category, :string
    field :sprite_key, :string

    timestamps()
  end

  @valid_categories ~w(seed harvest pigment potion material consumable)

  def changeset(item, attrs) do
    item
    |> cast(attrs, [:item_key, :display_name, :category, :sprite_key])
    |> validate_required([:item_key, :display_name, :category])
    |> validate_inclusion(:category, @valid_categories)
    |> unique_constraint(:item_key, name: :items_pkey)
  end
end
