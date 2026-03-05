defmodule CampFire.Game.WeatherCache do
  use Ecto.Schema
  import Ecto.Changeset

  schema "weather_cache" do
    field :lat, :float
    field :lon, :float
    field :weather_data, :map, default: %{}
    field :rain_start_utc, :utc_datetime
    field :last_rain_effect_utc, :utc_datetime
    field :fetched_at, :utc_datetime
    timestamps(type: :utc_datetime)
  end

  def changeset(cache, attrs) do
    cache
    |> cast(attrs, [:lat, :lon, :weather_data, :rain_start_utc, :last_rain_effect_utc, :fetched_at])
    |> validate_required([:lat, :lon, :fetched_at])
    |> unique_constraint([:lat, :lon])
  end
end
