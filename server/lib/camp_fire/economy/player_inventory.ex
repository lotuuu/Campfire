defmodule CampFire.Economy.PlayerInventory do
  use Ecto.Schema
  import Ecto.Changeset

  alias CampFire.Game.Item

  schema "player_inventory" do
    field :player_uid, :string
    belongs_to :item, Item
    field :count, :integer
  end

  def changeset(inventory, attrs) do
    inventory
    |> cast(attrs, [:player_uid, :item_id, :count])
    |> validate_required([:player_uid, :item_id, :count])
    |> validate_number(:count, greater_than: 0)
    |> unique_constraint([:player_uid, :item_id])
    |> foreign_key_constraint(:item_id)
  end
end
