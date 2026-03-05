defmodule CampFire.Visitors.VisitorTemplate do
  use Ecto.Schema

  schema "visitor_templates" do
    field :visitor_id, :string
    field :name, :string
    field :portrait_id, :string
    field :type, :string
    field :flame_level_min, :integer, default: 1
    field :dialogue_pool, {:array, :map}, default: []
    field :offer_pool, {:array, :map}, default: []
    field :gift_pool, {:array, :map}, default: []
    field :quest_pool, {:array, :map}, default: []
    field :weight, :float, default: 1.0
  end
end
