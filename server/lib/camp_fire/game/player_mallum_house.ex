defmodule CampFire.Game.PlayerMallumHouse do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_mallum_houses" do
    field :player_uid, :string
    field :grid_x, :integer
    field :grid_y, :integer
    field :skin_name, :string
    field :unlocked_skins, {:array, :string}, default: []
    timestamps()
  end

  def changeset(house, attrs) do
    house
    |> cast(attrs, [:player_uid, :grid_x, :grid_y, :skin_name, :unlocked_skins])
    |> validate_required([:player_uid, :grid_x, :grid_y])
  end
end
