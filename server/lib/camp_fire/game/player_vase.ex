defmodule CampFire.Game.PlayerVase do
  use Ecto.Schema
  import Ecto.Changeset

  @valid_states ~w(empty filling full)

  schema "player_vases" do
    field :player_uid, :string
    field :capacity, :integer, default: 5
    field :current_water, :integer, default: 0
    field :state, :string, default: "empty"
    field :fill_start_time_utc, :utc_datetime
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(vase, attrs) do
    vase
    |> cast(attrs, [
      :player_uid, :capacity, :current_water, :state, :fill_start_time_utc,
      :grid_x, :grid_y, :skin_name, :unlocked_skins
    ])
    |> validate_required([:player_uid, :state, :grid_x, :grid_y])
    |> validate_inclusion(:state, @valid_states)
    |> validate_number(:capacity, greater_than: 0)
    |> validate_number(:current_water, greater_than_or_equal_to: 0)
  end
end
