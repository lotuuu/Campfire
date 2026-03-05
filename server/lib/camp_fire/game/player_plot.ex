defmodule CampFire.Game.PlayerPlot do
  use Ecto.Schema
  import Ecto.Changeset

  @valid_states ~w(empty growing mature)

  schema "player_plots" do
    field :player_uid, :string
    field :seed_name, :string
    field :state, :string, default: "empty"
    field :plant_time_utc, :utc_datetime
    field :water_count, :integer, default: 0
    field :last_watered_utc, :utc_datetime
    field :snapshots, :map, default: %{}
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(plot, attrs) do
    plot
    |> cast(attrs, [
      :player_uid, :seed_name, :state, :plant_time_utc, :water_count,
      :last_watered_utc, :snapshots, :grid_x, :grid_y, :skin_name, :unlocked_skins
    ])
    |> validate_required([:player_uid, :state, :grid_x, :grid_y])
    |> validate_inclusion(:state, @valid_states)
  end
end
