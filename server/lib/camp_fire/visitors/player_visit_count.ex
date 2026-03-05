defmodule CampFire.Visitors.PlayerVisitCount do
  use Ecto.Schema

  @primary_key {:player_uid, :string, []}
  schema "player_visit_counts" do
    field :count, :integer, default: 0
    field :last_visit_date, :date
  end
end
