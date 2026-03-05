defmodule CampFire.Economy.PlayerSeed do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_seeds" do
    field :player_uid, :string
    field :seed_name, :string
    field :count, :integer
  end

  def changeset(seed, attrs) do
    seed
    |> cast(attrs, [:player_uid, :seed_name, :count])
    |> validate_required([:player_uid, :seed_name, :count])
    |> validate_number(:count, greater_than: 0)
    |> unique_constraint([:player_uid, :seed_name])
  end
end
