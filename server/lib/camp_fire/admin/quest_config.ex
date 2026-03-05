defmodule CampFire.Admin.QuestConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "quest_configs" do
    field :quest_name, :string
    field :duration_minutes, :integer
    field :required_flame_level, :integer, default: 1
    field :reward_rolls, :integer, default: 1
    field :reward_pool, {:array, :map}, default: []

    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:quest_name, :duration_minutes, :required_flame_level, :reward_rolls, :reward_pool])
    |> validate_required([:quest_name, :duration_minutes])
    |> unique_constraint(:quest_name)
  end
end
