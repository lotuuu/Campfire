defmodule CampFire.Admin.GameConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "game_configs" do
    field :key, :string
    field :value, :map, default: %{}

    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:key, :value])
    |> validate_required([:key, :value])
    |> unique_constraint(:key)
  end
end
