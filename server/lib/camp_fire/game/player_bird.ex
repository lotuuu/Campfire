defmodule CampFire.Game.PlayerBird do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_birds" do
    field :player_uid, :string
    field :grid_x, :integer
    field :grid_y, :integer
    field :seed_name, :string
    field :seed_count, :integer
    field :spawned_at_utc, :utc_datetime
    timestamps()
  end

  def changeset(bird, attrs) do
    bird
    |> cast(attrs, [:player_uid, :grid_x, :grid_y, :seed_name, :seed_count, :spawned_at_utc])
    |> validate_required([:player_uid, :grid_x, :grid_y, :seed_name, :seed_count, :spawned_at_utc])
  end
end
