defmodule CampFire.Economy.PlayerEconomy do
  use Ecto.Schema
  import Ecto.Changeset

  @primary_key false
  schema "player_economies" do
    field :player_uid, :string, primary_key: true
    field :mana, :float, default: 50.0
    field :gems, :integer, default: 5
    field :flame_level, :integer, default: 1
    field :last_mana_collect_utc, :utc_datetime
    timestamps(type: :utc_datetime)
  end

  def changeset(economy, attrs) do
    economy
    |> cast(attrs, [:player_uid, :mana, :gems, :flame_level, :last_mana_collect_utc])
    |> validate_required([:player_uid])
    |> validate_number(:mana, greater_than_or_equal_to: 0)
    |> validate_number(:gems, greater_than_or_equal_to: 0)
    |> validate_number(:flame_level, greater_than: 0)
    |> unique_constraint(:player_uid, name: :player_economies_pkey)
  end
end
