defmodule CampFireWeb.WeatherLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  @refresh_interval_ms 30_000

  def mount(_params, _session, socket) do
    if connected?(socket), do: Process.send_after(self(), :refresh, @refresh_interval_ms)

    {:ok, assign(socket, active_tab: :weather) |> load_data()}
  end

  def handle_info(:refresh, socket) do
    Process.send_after(self(), :refresh, @refresh_interval_ms)
    {:noreply, load_data(socket)}
  end

  defp load_data(socket) do
    caches = Admin.list_weather_caches()
    player_counts = Admin.weather_player_counts()

    # Merge player counts into cache entries by rounded lat/lon
    count_map =
      Map.new(player_counts, fn %{lat: lat, lon: lon, count: count} ->
        {round_key(lat, lon), count}
      end)

    entries =
      Enum.map(caches, fn cache ->
        key = round_key(cache.lat, cache.lon)
        players = Map.get(count_map, key, 0)
        %{cache: cache, players: players}
      end)

    assign(socket,
      entries: entries,
      active_locations: Admin.active_location_count(),
      total_caches: length(caches)
    )
  end

  defp round_key(lat, lon) do
    {Float.round(lat * 1.0, 2), Float.round(lon * 1.0, 2)}
  end

  def render(assigns) do
    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-4">Weather Polls</h2>

      <div class="grid grid-cols-3 gap-4 mb-6">
        <div class="bg-white border rounded-lg p-4 text-center">
          <div class="text-3xl font-bold text-blue-600">{@total_caches}</div>
          <div class="text-sm text-gray-500">Cached Locations</div>
        </div>
        <div class="bg-white border rounded-lg p-4 text-center">
          <div class="text-3xl font-bold text-green-600">{@active_locations}</div>
          <div class="text-sm text-gray-500">Active (growing plots)</div>
        </div>
        <div class="bg-white border rounded-lg p-4 text-center">
          <div class="text-3xl font-bold text-gray-600">30s</div>
          <div class="text-sm text-gray-500">Auto-refresh</div>
        </div>
      </div>

      <%= if @entries == [] do %>
        <p class="text-gray-500">No weather data cached yet.</p>
      <% else %>
        <div class="space-y-4">
          <%= for entry <- @entries do %>
            <div class="bg-white border rounded-lg p-4">
              <div class="flex justify-between items-start mb-3">
                <div>
                  <span class="font-mono text-sm text-gray-600">
                    ({entry.cache.lat}, {entry.cache.lon})
                  </span>
                  <span class="ml-2 text-sm text-gray-500">
                    {entry.players} player(s)
                  </span>
                </div>
                <div class="text-right text-sm text-gray-400">
                  Fetched: {format_time(entry.cache.fetched_at)}
                  <span class="ml-2">({cache_age(entry.cache.fetched_at)})</span>
                </div>
              </div>

              <%= if entry.cache.weather_data && map_size(entry.cache.weather_data) > 0 do %>
                <div class="grid grid-cols-4 gap-3 text-sm">
                  <div class="bg-red-50 rounded p-2 text-center">
                    <div class="text-lg font-semibold text-red-700">
                      {format_num(entry.cache.weather_data["temperature"])}&deg;C
                    </div>
                    <div class="text-xs text-gray-500">Temperature</div>
                  </div>
                  <div class="bg-blue-50 rounded p-2 text-center">
                    <div class="text-lg font-semibold text-blue-700">
                      {format_num(entry.cache.weather_data["humidity"])}%
                    </div>
                    <div class="text-xs text-gray-500">Humidity</div>
                  </div>
                  <div class="bg-teal-50 rounded p-2 text-center">
                    <div class="text-lg font-semibold text-teal-700">
                      {format_num(entry.cache.weather_data["wind_speed"])} m/s
                    </div>
                    <div class="text-xs text-gray-500">Wind</div>
                  </div>
                  <div class="bg-gray-50 rounded p-2 text-center">
                    <div class="text-lg font-semibold text-gray-700">
                      {format_num(entry.cache.weather_data["cloud_cover"])}%
                    </div>
                    <div class="text-xs text-gray-500">Clouds</div>
                  </div>
                </div>

                <div class="flex gap-4 mt-2 text-sm text-gray-600">
                  <span>
                    Condition:
                    <span class="font-medium">{entry.cache.weather_data["condition"] || "-"}</span>
                  </span>
                  <span>
                    Rain:
                    <span class={"font-medium #{if entry.cache.weather_data["is_raining"], do: "text-blue-600", else: "text-gray-400"}"}>
                      {if entry.cache.weather_data["is_raining"], do: "Yes", else: "No"}
                    </span>
                  </span>
                  <span>
                    Moon: <span class="font-medium">{format_num(entry.cache.weather_data["moon_phase"])}</span>
                  </span>
                  <%= if entry.cache.rain_start_utc do %>
                    <span>
                      Rain since: <span class="font-medium text-blue-600">{format_time(entry.cache.rain_start_utc)}</span>
                    </span>
                  <% end %>
                </div>
              <% else %>
                <p class="text-gray-400 text-sm">No weather data</p>
              <% end %>
            </div>
          <% end %>
        </div>
      <% end %>
    </div>
    """
  end

  defp format_time(nil), do: "-"

  defp format_time(%DateTime{} = dt) do
    Calendar.strftime(dt, "%Y-%m-%d %H:%M:%S")
  end

  defp format_time(_), do: "-"

  defp cache_age(nil), do: "-"

  defp cache_age(%DateTime{} = dt) do
    seconds = DateTime.diff(DateTime.utc_now(), dt, :second)

    cond do
      seconds < 60 -> "#{seconds}s ago"
      seconds < 3600 -> "#{div(seconds, 60)}m ago"
      true -> "#{div(seconds, 3600)}h ago"
    end
  end

  defp cache_age(_), do: "-"

  defp format_num(nil), do: "-"
  defp format_num(n) when is_float(n), do: :erlang.float_to_binary(n, decimals: 1)
  defp format_num(n), do: "#{n}"
end
