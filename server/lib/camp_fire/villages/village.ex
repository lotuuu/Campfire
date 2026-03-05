defmodule CampFire.Villages.Village do
  use Ecto.Schema
  import Ecto.Changeset

  schema "villages" do
    field :player_uid, :string
    field :snapshot, :map, default: %{}
    timestamps()
  end

  def changeset(village, attrs) do
    village
    |> cast(attrs, [:player_uid, :snapshot])
    |> validate_required([:player_uid, :snapshot])
    |> unique_constraint(:player_uid)
  end
end
