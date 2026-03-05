defmodule CampFire.Visitors.VisitorSchedule do
  use Ecto.Schema

  schema "visitor_schedule" do
    field :visitor_id, :string
    field :date, :date
    field :visit_number, :integer
    field :weather_condition, :string
    field :priority, :integer, default: 0
  end
end
