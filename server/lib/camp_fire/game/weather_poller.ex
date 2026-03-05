defmodule CampFire.Game.WeatherPoller do
  @moduledoc """
  GenServer that polls weather for all active locations every 15 minutes.
  Records snapshots on growing plots and processes rain effects.
  """

  use GenServer
  require Logger

  alias CampFire.Game.{Weather, Plots}

  @poll_interval_ms 15 * 60 * 1000

  # --- Client API ---

  def start_link(opts \\ []) do
    GenServer.start_link(__MODULE__, opts, name: __MODULE__)
  end

  # --- GenServer Callbacks ---

  @impl true
  def init(_opts) do
    schedule_poll()
    {:ok, %{}}
  end

  @impl true
  def handle_info(:poll, state) do
    poll_all_locations()
    schedule_poll()
    {:noreply, state}
  end

  # --- Private ---

  defp schedule_poll do
    Process.send_after(self(), :poll, @poll_interval_ms)
  end

  defp poll_all_locations do
    locations = Weather.active_locations()

    Enum.each(locations, fn {lat, lon} ->
      case Weather.get_or_fetch(lat, lon) do
        {:ok, cache} ->
          weather_data = cache.weather_data

          # Record snapshots on all growing plots at this location
          plots = Weather.growing_plots_at_location(lat, lon)

          Enum.each(plots, fn plot ->
            Plots.record_snapshot(plot.id, weather_data)
          end)

          # Process rain effects
          Weather.process_rain(lat, lon, weather_data, cache)

        {:error, reason} ->
          Logger.warning(
            "WeatherPoller: failed to fetch weather for (#{lat}, #{lon}): #{inspect(reason)}"
          )
      end
    end)
  rescue
    e ->
      Logger.error("WeatherPoller: unexpected error: #{Exception.message(e)}")
  end
end
