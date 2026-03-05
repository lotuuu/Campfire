defmodule CampFire.Visitors.VisitorQuest do
  use Ecto.Schema
  import Ecto.Changeset

  schema "visitor_quests" do
    field :player_uid, :string
    field :visitor_id, :string
    field :request_item, :string
    field :request_count, :integer
    field :return_date_utc, :date
    field :reward, :map, default: %{}
    field :return_dialogue, {:array, :string}, default: []
    timestamps()
  end

  def changeset(quest, attrs) do
    quest
    |> cast(attrs, [:player_uid, :visitor_id, :request_item, :request_count, :return_date_utc, :reward, :return_dialogue])
    |> validate_required([:player_uid, :visitor_id, :request_item, :request_count, :return_date_utc])
  end
end
