defmodule CampFire.Economy.PlayerInventory do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_inventory" do
    field :player_uid, :string
    field :item_key, :string
    field :count, :integer
  end

  def changeset(inventory, attrs) do
    inventory
    |> cast(attrs, [:player_uid, :item_key, :count])
    |> validate_required([:player_uid, :item_key, :count])
    |> validate_number(:count, greater_than: 0)
    |> unique_constraint([:player_uid, :item_key])
  end
end
