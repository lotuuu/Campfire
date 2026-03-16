defmodule CampFire.Game.PlayerGarden do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_gardens" do
    field :player_uid, :string
    field :plant_name, :string
    field :plant_time_utc, :utc_datetime
    field :last_yield_time_utc, :utc_datetime
    field :mature, :boolean, default: false
    field :grid_x, :integer
    field :grid_y, :integer
    field :fertilized, :boolean, default: false
    timestamps(type: :utc_datetime)
  end

  def changeset(garden, attrs) do
    garden
    |> cast(attrs, [
      :player_uid, :plant_name, :plant_time_utc, :last_yield_time_utc,
      :mature, :grid_x, :grid_y, :fertilized
    ])
    |> validate_required([:player_uid, :plant_name, :plant_time_utc, :grid_x, :grid_y])
  end
end
