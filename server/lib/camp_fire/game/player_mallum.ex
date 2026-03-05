defmodule CampFire.Game.PlayerMallum do
  use Ecto.Schema
  import Ecto.Changeset

  @valid_states ~w(idle fetching_water on_quest quest_complete)

  schema "player_mallums" do
    field :player_uid, :string
    field :state, :string, default: "idle"
    field :assigned_quest_name, :string
    field :start_time_utc, :utc_datetime
    field :assigned_vase_id, :integer
    field :pending_rewards, {:array, :map}, default: []
    timestamps(type: :utc_datetime)
  end

  def changeset(mallum, attrs) do
    mallum
    |> cast(attrs, [
      :player_uid, :state, :assigned_quest_name, :start_time_utc,
      :assigned_vase_id, :pending_rewards
    ])
    |> validate_required([:player_uid, :state])
    |> validate_inclusion(:state, @valid_states)
  end
end
