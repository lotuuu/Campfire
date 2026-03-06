defmodule CampFire.Game.SeedConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "seed_configs" do
    field :seed_name, :string
    field :growth_duration_hours, :float
    field :min_drops, :integer
    field :max_drops, :integer
    field :tier, :integer, default: 1
    field :recipe, :map, default: %{}
    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:seed_name, :growth_duration_hours, :min_drops, :max_drops, :tier, :recipe])
    |> validate_required([:seed_name, :growth_duration_hours, :min_drops, :max_drops])
    |> validate_number(:growth_duration_hours, greater_than: 0)
    |> validate_number(:min_drops, greater_than: 0)
    |> validate_number(:max_drops, greater_than: 0)
    |> unique_constraint(:seed_name)
  end
end
