defmodule CampFire.Game.PlayerPlot do
  use Ecto.Schema
  import Ecto.Changeset

  alias CampFire.Game.Item

  @valid_states ~w(empty growing mature)

  schema "player_plots" do
    field :player_uid, :string
    belongs_to :seed_item, Item
    field :state, :string, default: "empty"
    field :plant_time_utc, :utc_datetime
    field :water_count, :integer, default: 0
    field :last_watered_utc, :utc_datetime
    field :snapshots, :map, default: %{}
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    field :fertilized, :boolean, default: false
    field :potions, {:array, :map}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(plot, attrs) do
    plot
    |> cast(attrs, [
      :player_uid, :seed_item_id, :state, :plant_time_utc, :water_count,
      :last_watered_utc, :snapshots, :grid_x, :grid_y, :skin_name, :unlocked_skins,
      :fertilized, :potions
    ])
    |> validate_required([:player_uid, :state, :grid_x, :grid_y])
    |> validate_inclusion(:state, @valid_states)
    |> foreign_key_constraint(:seed_item_id)
  end
end
