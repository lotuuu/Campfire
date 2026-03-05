defmodule CampFire.Admin.GardenConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "garden_configs" do
    field :plant_name, :string
    field :growth_duration_hours, :float
    field :yield_item, :string
    field :yield_amount, :integer, default: 1
    field :yield_interval_hours, :float
    field :water_required, :integer, default: 1
    field :mana_cost, :float, default: 0.0

    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:plant_name, :growth_duration_hours, :yield_item, :yield_amount, :yield_interval_hours, :water_required, :mana_cost])
    |> validate_required([:plant_name, :growth_duration_hours, :yield_item, :yield_interval_hours])
    |> unique_constraint(:plant_name)
  end
end
