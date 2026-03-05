defmodule CampFire.Game.PlayerState do
  use Ecto.Schema
  import Ecto.Changeset

  @primary_key false
  schema "player_states" do
    field :player_uid, :string, primary_key: true
    field :data, :map, default: %{}
    timestamps(type: :utc_datetime)
  end

  def changeset(state, attrs) do
    state
    |> cast(attrs, [:player_uid, :data])
    |> validate_required([:player_uid])
    |> unique_constraint(:player_uid, name: :player_states_pkey)
  end
end
