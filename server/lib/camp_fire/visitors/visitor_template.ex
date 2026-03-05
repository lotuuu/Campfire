defmodule CampFire.Visitors.VisitorTemplate do
  use Ecto.Schema

  schema "visitor_templates" do
    field :visitor_id, :string
    field :name, :string
    field :portrait_id, :string
    field :type, :string
    field :flame_level_min, :integer, default: 1
    field :dialogue_pool, CampFire.JsonArray, default: []
    field :offer_pool, CampFire.JsonArray, default: []
    field :gift_pool, CampFire.JsonArray, default: []
    field :quest_pool, CampFire.JsonArray, default: []
    field :weight, :float, default: 1.0
  end
end
