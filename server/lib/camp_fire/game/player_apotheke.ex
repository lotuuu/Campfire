defmodule CampFire.Game.PlayerApotheke do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_apothekes" do
    field :player_uid, :string
    field :grid_x, :integer, default: 1
    field :grid_y, :integer, default: 0
    timestamps(type: :utc_datetime)
  end

  def changeset(apotheke, attrs) do
    apotheke
    |> cast(attrs, [:player_uid, :grid_x, :grid_y])
    |> validate_required([:player_uid, :grid_x, :grid_y])
    |> unique_constraint(:player_uid)
  end
end
