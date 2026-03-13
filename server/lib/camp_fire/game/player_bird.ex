defmodule CampFire.Game.PlayerBird do
  use Ecto.Schema
  import Ecto.Changeset

  alias CampFire.Game.Item

  schema "player_birds" do
    field :player_uid, :string
    field :grid_x, :integer
    field :grid_y, :integer
    belongs_to :seed_item, Item
    field :seed_count, :integer
    field :spawned_at_utc, :utc_datetime
    timestamps()
  end

  def changeset(bird, attrs) do
    bird
    |> cast(attrs, [:player_uid, :grid_x, :grid_y, :seed_item_id, :seed_count, :spawned_at_utc])
    |> validate_required([:player_uid, :grid_x, :grid_y, :seed_item_id, :seed_count, :spawned_at_utc])
    |> foreign_key_constraint(:seed_item_id)
  end
end
