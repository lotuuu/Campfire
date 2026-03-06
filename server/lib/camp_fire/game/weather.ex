defmodule CampFire.Game.Weather do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{WeatherCache, MoonPhase, Vases}
  alias CampFire.Economy.PlayerEconomy

  @cache_ttl_seconds 900
  @rain_effect_threshold_seconds 900

  # --- Public API ---

  def get_or_fetch(lat, lon) do
    lat = Float.round(lat * 1.0, 2)
    lon = Float.round(lon * 1.0, 2)

    case get_cache(lat, lon) do
      %WeatherCache{} = cache ->
        now = DateTime.utc_now() |> DateTime.truncate(:second)
        age = DateTime.diff(now, cache.fetched_at, :second)

        if age < @cache_ttl_seconds do
          {:ok, cache}
        else
          fetch_and_cache(lat, lon, cache)
        end

      nil ->
        fetch_and_cache(lat, lon, nil)
    end
  end

  def update_player_location(player_uid, lat, lon) do
    case Repo.get(PlayerEconomy, player_uid) do
      nil ->
        {:error, :not_found}

      economy ->
        economy
        |> PlayerEconomy.changeset(%{lat: lat, lon: lon})
        |> Repo.update()
    end
  end

  def active_locations do
    from(pe in PlayerEconomy,
      join: pp in CampFire.Game.PlayerPlot,
      on: pp.player_uid == pe.player_uid,
      where: pp.state == "growing" and not is_nil(pe.lat) and not is_nil(pe.lon),
      select: {pe.lat, pe.lon},
      distinct: true
    )
    |> Repo.all()
  end

  def growing_plots_at_location(lat, lon) do
    lat_rounded = Float.round(lat * 1.0, 2)
    lon_rounded = Float.round(lon * 1.0, 2)

    from(pp in CampFire.Game.PlayerPlot,
      join: pe in PlayerEconomy,
      on: pp.player_uid == pe.player_uid,
      where:
        pp.state == "growing" and
          fragment("ROUND(CAST(? AS numeric), 2)", pe.lat) == ^lat_rounded and
          fragment("ROUND(CAST(? AS numeric), 2)", pe.lon) == ^lon_rounded,
      select: pp
    )
    |> Repo.all()
  end

  def process_rain(lat, lon, weather_data, cache) do
    is_raining = weather_data["is_raining"] || false
    now = DateTime.utc_now() |> DateTime.truncate(:second)

    if is_raining do
      # Set rain_start_utc if not set
      cache =
        if cache.rain_start_utc == nil do
          cache
          |> WeatherCache.changeset(%{rain_start_utc: now})
          |> Repo.update!()
        else
          cache
        end

      # Check if rain has persisted for 15+ minutes
      rain_duration = DateTime.diff(now, cache.rain_start_utc, :second)

      should_apply =
        rain_duration >= @rain_effect_threshold_seconds and
          (cache.last_rain_effect_utc == nil or
             DateTime.compare(cache.last_rain_effect_utc, cache.rain_start_utc) == :lt)

      if should_apply do
        # Fill all vases for players at this location
        player_uids = players_at_location(lat, lon)

        Enum.each(player_uids, fn uid ->
          Vases.rain_fill_all(uid)
        end)

        cache
        |> WeatherCache.changeset(%{last_rain_effect_utc: now})
        |> Repo.update!()
      end

      :ok
    else
      # Not raining — clear rain_start_utc
      if cache.rain_start_utc != nil do
        cache
        |> WeatherCache.changeset(%{rain_start_utc: nil})
        |> Repo.update!()
      end

      :ok
    end
  end

  # --- Private Helpers ---

  defp get_cache(lat, lon) do
    Repo.one(
      from(w in WeatherCache,
        where:
          fragment("ROUND(CAST(? AS numeric), 2)", w.lat) == ^lat and
            fragment("ROUND(CAST(? AS numeric), 2)", w.lon) == ^lon,
        limit: 1
      )
    )
  end

  defp fetch_and_cache(lat, lon, existing_cache) do
    api_key = Application.get_env(:camp_fire, :owm_api_key, "")

    if api_key == "" do
      {:error, :no_api_key}
    else
      case fetch_owm(lat, lon, api_key) do
        {:ok, weather_data} ->
          now = DateTime.utc_now() |> DateTime.truncate(:second)

          attrs = %{
            lat: lat,
            lon: lon,
            weather_data: weather_data,
            fetched_at: now
          }

          cache =
            case existing_cache do
              nil ->
                %WeatherCache{}
                |> WeatherCache.changeset(attrs)
                |> Repo.insert!()

              cache ->
                cache
                |> WeatherCache.changeset(attrs)
                |> Repo.update!()
            end

          {:ok, cache}

        {:error, reason} ->
          {:error, reason}
      end
    end
  end

  defp fetch_owm(lat, lon, api_key) do
    url =
      "https://api.openweathermap.org/data/2.5/weather?lat=#{lat}&lon=#{lon}&units=metric&appid=#{api_key}"

    case Req.get(url) do
      {:ok, %Req.Response{status: 200, body: body}} ->
        weather_data = parse_owm_response(body)
        {:ok, weather_data}

      {:ok, %Req.Response{status: status}} ->
        {:error, {:owm_error, status}}

      {:error, reason} ->
        {:error, {:http_error, reason}}
    end
  end

  defp parse_owm_response(body) do
    temperature = get_in(body, ["main", "temp"]) || 0.0
    humidity = get_in(body, ["main", "humidity"]) || 0.0
    wind_speed = get_in(body, ["wind", "speed"]) || 0.0
    cloud_cover = get_in(body, ["clouds", "all"]) || 0.0

    weather_list = body["weather"] || []
    condition = if weather_list != [], do: hd(weather_list)["main"] || "", else: ""
    is_raining = condition in ["Rain", "Thunderstorm", "Drizzle"]

    moon_phase = MoonPhase.calculate()

    %{
      "temperature" => temperature * 1.0,
      "humidity" => humidity * 1.0,
      "wind_speed" => wind_speed * 1.0,
      "cloud_cover" => cloud_cover * 1.0,
      "condition" => condition,
      "is_raining" => is_raining,
      "moon_phase" => moon_phase * 1.0
    }
  end

  defp players_at_location(lat, lon) do
    lat_rounded = Float.round(lat * 1.0, 2)
    lon_rounded = Float.round(lon * 1.0, 2)

    from(pe in PlayerEconomy,
      where:
        fragment("ROUND(CAST(? AS numeric), 2)", pe.lat) == ^lat_rounded and
          fragment("ROUND(CAST(? AS numeric), 2)", pe.lon) == ^lon_rounded,
      select: pe.player_uid
    )
    |> Repo.all()
  end
end
