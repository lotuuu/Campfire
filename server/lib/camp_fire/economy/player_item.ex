defmodule CampFire.Economy.PlayerItem do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_items" do
    field :player_uid, :string
    field :item_name, :string
    field :count, :integer
  end

  def changeset(item, attrs) do
    item
    |> cast(attrs, [:player_uid, :item_name, :count])
    |> validate_required([:player_uid, :item_name, :count])
    |> validate_number(:count, greater_than: 0)
    |> unique_constraint([:player_uid, :item_name])
  end
end
