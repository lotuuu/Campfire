defmodule CampFireWeb.GameController do
  use CampFireWeb, :controller

  alias CampFire.Economy
  alias CampFire.Game.{Plots, Vases, Gardens, Mallums, MallumHouses, Birds, Weather, PlayerState, Apotheke, Skins}
  alias CampFire.Repo

  alias CampFire.ConfigCache

  # ── Config sync ────────────────────────────────────────────

  def get_configs(conn, _params) do
    seed_configs = ConfigCache.get("seed_configs") || %{}
    quest_configs = ConfigCache.get("quest_configs") || %{}
    garden_configs = ConfigCache.get("garden_configs") || %{}
    flame_config = ConfigCache.get("flame_config") || %{}
    vase_config = ConfigCache.get("vase_config") || %{}
    mallum_house_config = ConfigCache.get("mallum_house_config") || %{}
    building_cost_config = ConfigCache.get("building_cost_config") || %{}
    skin_configs = ConfigCache.get("skin_configs") || %{}

    seeds =
      Map.new(seed_configs, fn {name, s} ->
        {name,
         %{
           seedName: s.seed_name,
           growthDurationHours: s.growth_duration_hours,
           minDrops: s.min_drops,
           maxDrops: s.max_drops,
           tier: s.tier,
           recipe: s.recipe
         }}
      end)

    quests =
      Map.new(quest_configs, fn {name, q} ->
        {name,
         %{
           questName: q.quest_name,
           durationMinutes: q.duration_minutes,
           requiredFlameLevel: q.required_flame_level,
           rewardRolls: q.reward_rolls,
           rewardPool: q.reward_pool
         }}
      end)

    gardens =
      Map.new(garden_configs, fn {name, g} ->
        {name,
         %{
           plantName: g.plant_name,
           growthDurationHours: g.growth_duration_hours,
           yieldItem: g.yield_item,
           yieldAmount: g.yield_amount,
           yieldIntervalHours: g.yield_interval_hours,
           waterRequired: g.water_required,
           manaCost: g.mana_cost
         }}
      end)

    conn
    |> put_status(200)
    |> json(%{
      seeds: seeds,
      quests: quests,
      gardens: gardens,
      flameConfig: serialize_game_config(flame_config),
      vaseConfig: serialize_game_config(vase_config),
      mallumHouseConfig: serialize_game_config(mallum_house_config),
      buildingCostConfig: serialize_game_config(building_cost_config),
      recipes: ConfigCache.get("recipe_configs") || %{},
      skins: skin_configs
    })
  end

  # game_configs values are already stored with camelCase keys in the DB
  defp serialize_game_config(config) when is_map(config), do: config
  defp serialize_game_config(_), do: %{}

  # ── State sync ──────────────────────────────────────────────

  def get_state(conn, _params) do
    uid = conn.assigns.current_player.uid

    # Lazy-evaluate timers: mature growing plots, complete filling vases
    # Use single fetch + in-place updates to avoid N+1 re-fetch
    plots =
      Plots.list_plots(uid)
      |> Enum.map(fn p ->
        if p.state == "growing" do
          case Plots.check_maturity(p.id) do
            {:ok, updated} -> updated
            _ -> p
          end
        else
          p
        end
      end)

    vases =
      Vases.list_vases(uid)
      |> Enum.map(fn v ->
        if v.state == "filling" do
          case Vases.check_fill(uid, v.id) do
            {:ok, updated} -> updated
            _ -> v
          end
        else
          v
        end
      end)
    gardens = Gardens.list_gardens(uid)
    mallums = Mallums.list_mallums(uid)
    houses = MallumHouses.list_houses(uid)
    birds = Birds.list_birds(uid)

    {economy, seeds, items} =
      case Economy.get_economy(uid) do
        nil -> {nil, [], []}
        _eco -> Economy.get_full_state(uid)
      end

    player_state =
      case Repo.get(PlayerState, uid) do
        nil -> %{}
        ps -> ps.data || %{}
      end

    weather_data =
      if economy && economy.lat && economy.lon do
        case Weather.get_or_fetch(economy.lat, economy.lon) do
          {:ok, cache} -> cache.weather_data
          _ -> nil
        end
      else
        nil
      end

    economy_json =
      if economy do
        %{
          mana: economy.mana,
          gems: economy.gems,
          flameLevel: economy.flame_level,
          lastManaCollectUtc: DateTime.to_iso8601(economy.last_mana_collect_utc),
          seeds: Enum.map(seeds, fn s -> %{seedName: s.seed_name, count: s.count} end),
          items: Enum.map(items, fn i -> %{itemName: i.item_name, count: i.count} end)
        }
      else
        nil
      end

    conn
    |> put_status(200)
    |> json(%{
      economy: economy_json,
      plots: Enum.map(plots, &serialize_plot/1),
      vases: Enum.map(vases, &serialize_vase/1),
      gardens: Enum.map(gardens, &serialize_garden/1),
      mallums: Enum.map(mallums, &serialize_mallum/1),
      mallumHouses: Enum.map(houses, &serialize_mallum_house/1),
      birds: Enum.map(birds, &serialize_bird/1),
      cosmeticState: player_state,
      weather: weather_data
    })
  end

  def save_state(conn, %{"data" => data}) do
    uid = conn.assigns.current_player.uid

    case Repo.get(PlayerState, uid) do
      nil ->
        %PlayerState{}
        |> PlayerState.changeset(%{player_uid: uid, data: data})
        |> Repo.insert()

      existing ->
        existing
        |> PlayerState.changeset(%{data: data})
        |> Repo.update()
    end
    |> case do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, _} -> conn |> put_status(422) |> json(%{error: "Failed to save state"})
    end
  end

  def save_state(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'data' field"})
  end

  # ── Plots ───────────────────────────────────────────────────

  def list_plots(conn, _params) do
    uid = conn.assigns.current_player.uid
    plots = Plots.list_plots(uid)
    conn |> put_status(200) |> json(%{plots: Enum.map(plots, &serialize_plot/1)})
  end

  def craft_plot(conn, %{"gridX" => x, "gridY" => y}) do
    uid = conn.assigns.current_player.uid

    case Plots.craft_plot(uid, x, y) do
      {:ok, plot} ->
        conn |> put_status(201) |> json(serialize_plot(plot))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def craft_plot(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'gridX' and 'gridY'"})
  end

  def plant_seed(conn, %{"plotId" => plot_id, "seedName" => seed_name}) do
    uid = conn.assigns.current_player.uid

    case Plots.plant(uid, plot_id, seed_name) do
      {:ok, plot} ->
        conn |> put_status(200) |> json(serialize_plot(plot))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def plant_seed(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'plotId' and 'seedName'"})
  end

  def water_plot(conn, %{"plotId" => plot_id, "vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid

    case Plots.water(uid, plot_id, vase_id) do
      {:ok, plot} ->
        conn |> put_status(200) |> json(serialize_plot(plot))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def water_plot(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'plotId' and 'vaseId'"})
  end

  def harvest_plot(conn, %{"plotId" => plot_id}) do
    uid = conn.assigns.current_player.uid

    # Lazy eval maturity first
    Plots.check_maturity(plot_id)

    case Plots.harvest(uid, plot_id) do
      {:ok, %{score: score, drops: drops, item_name: item_name}} ->
        conn |> put_status(200) |> json(%{score: score, drops: drops, itemName: item_name})

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def harvest_plot(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'plotId'"})
  end

  def set_plot_skin(conn, %{"plotId" => plot_id, "skinName" => skin}) do
    uid = conn.assigns.current_player.uid

    case Skins.apply_skin(uid, :plot, plot_id, skin) do
      {:ok, plot} ->
        conn |> put_status(200) |> json(serialize_plot(plot))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def set_plot_skin(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'plotId' and 'skinName'"})
  end

  # ── Vases ───────────────────────────────────────────────────

  def list_vases(conn, _params) do
    uid = conn.assigns.current_player.uid
    vases = Vases.list_vases(uid)
    conn |> put_status(200) |> json(%{vases: Enum.map(vases, &serialize_vase/1)})
  end

  def craft_vase(conn, %{"gridX" => x, "gridY" => y}) do
    uid = conn.assigns.current_player.uid

    case Vases.craft_vase(uid, x, y) do
      {:ok, vase} ->
        conn |> put_status(201) |> json(serialize_vase(vase))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def craft_vase(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'gridX' and 'gridY'"})
  end

  def fill_vase(conn, %{"vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid

    case Vases.start_fill(uid, vase_id) do
      {:ok, vase} ->
        conn |> put_status(200) |> json(serialize_vase(vase))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def fill_vase(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'vaseId'"})
  end

  def check_vase(conn, %{"vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid

    case Vases.check_fill(uid, vase_id) do
      {:ok, vase} ->
        conn |> put_status(200) |> json(serialize_vase(vase))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def check_vase(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'vaseId'"})
  end

  def set_vase_skin(conn, %{"vaseId" => vase_id, "skinName" => skin}) do
    uid = conn.assigns.current_player.uid

    case Skins.apply_skin(uid, :vase, vase_id, skin) do
      {:ok, vase} ->
        conn |> put_status(200) |> json(serialize_vase(vase))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def set_vase_skin(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'vaseId' and 'skinName'"})
  end

  def instant_finish_vase(conn, %{"vaseId" => vase_id}) do
    uid = conn.assigns.current_player.uid

    case Vases.instant_finish(uid, vase_id) do
      {:ok, vase} ->
        conn |> put_status(200) |> json(serialize_vase(vase))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def instant_finish_vase(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'vaseId'"})
  end

  # ── Gardens ─────────────────────────────────────────────────

  def list_gardens(conn, _params) do
    uid = conn.assigns.current_player.uid
    gardens = Gardens.list_gardens(uid)
    conn |> put_status(200) |> json(%{gardens: Enum.map(gardens, &serialize_garden/1)})
  end

  def plant_garden(conn, %{"plantName" => name, "gridX" => x, "gridY" => y}) do
    uid = conn.assigns.current_player.uid

    case Gardens.plant(uid, name, x, y) do
      {:ok, garden} ->
        conn |> put_status(201) |> json(serialize_garden(garden))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def plant_garden(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'plantName', 'gridX', and 'gridY'"})
  end

  def collect_garden(conn, %{"gardenId" => garden_id}) do
    uid = conn.assigns.current_player.uid

    case Gardens.check_and_collect(uid, garden_id) do
      {:ok, %{status: :collected, garden: garden, item: item, amount: amount}} ->
        conn
        |> put_status(200)
        |> json(%{
          status: "collected",
          garden: serialize_garden(garden),
          item: item,
          amount: amount
        })

      {:ok, %{status: status, garden: garden}} ->
        conn
        |> put_status(200)
        |> json(%{status: Atom.to_string(status), garden: serialize_garden(garden)})

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def collect_garden(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'gardenId'"})
  end

  # ── Quests ──────────────────────────────────────────────────

  def start_quest(conn, %{"questName" => quest_name}) do
    uid = conn.assigns.current_player.uid

    case Mallums.send_on_quest(uid, quest_name) do
      {:ok, mallum} ->
        conn |> put_status(200) |> json(serialize_mallum(mallum))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def start_quest(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'questName'"})
  end

  def check_quest(conn, %{"mallumId" => mallum_id}) do
    uid = conn.assigns.current_player.uid

    case Mallums.check_quest(uid, mallum_id) do
      {:ok, mallum} ->
        conn |> put_status(200) |> json(serialize_mallum(mallum))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def check_quest(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'mallumId'"})
  end

  def collect_quest(conn, %{"mallumId" => mallum_id}) do
    uid = conn.assigns.current_player.uid

    # Capture rewards before collect_rewards resets the mallum
    case Mallums.collect_rewards(uid, mallum_id) do
      {:ok, %{rewards: rewards}} ->
        conn |> put_status(200) |> json(%{rewards: rewards})

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def collect_quest(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'mallumId'"})
  end

  def speed_up_quest(conn, %{"mallumId" => mallum_id}) do
    uid = conn.assigns.current_player.uid

    case Mallums.speed_up_quest(uid, mallum_id) do
      {:ok, mallum} ->
        conn |> put_status(200) |> json(serialize_mallum(mallum))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def speed_up_quest(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'mallumId'"})
  end

  # ── Mallum Houses ─────────────────────────────────────────────

  def craft_mallum_house(conn, %{"gridX" => gx, "gridY" => gy}) do
    uid = conn.assigns.current_player.uid

    case MallumHouses.craft_house(uid, gx, gy) do
      {:ok, house} -> conn |> put_status(201) |> json(serialize_mallum_house(house))
      {:error, reason} -> conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def craft_mallum_house(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'gridX' and 'gridY'"})
  end

  def set_mallum_house_skin(conn, %{"houseId" => house_id, "skinName" => skin}) do
    uid = conn.assigns.current_player.uid

    case Skins.apply_skin(uid, :mallum_house, house_id, skin) do
      {:ok, house} ->
        conn |> put_status(200) |> json(serialize_mallum_house(house))

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def set_mallum_house_skin(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'houseId' and 'skinName'"})
  end

  # ── Apotheke ────────────────────────────────────────────────

  def craft_apotheke(conn, %{"recipeName" => recipe_name}) do
    uid = conn.assigns.current_player.uid

    case Apotheke.craft(uid, recipe_name) do
      {:ok, result} ->
        conn |> put_status(200) |> json(%{resultItem: result.result_item, resultQuantity: result.result_quantity})

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def craft_apotheke(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'recipeName'"})
  end

  # ── Birds ───────────────────────────────────────────────────

  def check_birds(conn, _params) do
    uid = conn.assigns.current_player.uid

    case Birds.check_spawns(uid) do
      {:ok, new_birds} ->
        conn |> put_status(200) |> json(%{newBirds: Enum.map(new_birds, &serialize_bird/1)})
    end
  end

  def collect_bird(conn, %{"birdId" => bird_id}) do
    uid = conn.assigns.current_player.uid

    case Birds.collect_bird(uid, bird_id) do
      {:ok, reward} ->
        conn |> put_status(200) |> json(%{seedName: reward.seed_name, seedCount: reward.seed_count})

      {:error, reason} ->
        conn |> put_status(422) |> json(%{error: format_error(reason)})
    end
  end

  def collect_bird(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'birdId'"})
  end

  # ── Weather ─────────────────────────────────────────────────

  def submit_location(conn, %{"lat" => lat, "lon" => lon})
      when is_number(lat) and is_number(lon) do
    uid = conn.assigns.current_player.uid

    case Weather.update_player_location(uid, lat, lon) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, _} -> conn |> put_status(422) |> json(%{error: "Failed to update location"})
    end
  end

  def submit_location(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'lat' and 'lon'"})
  end

  def current_weather(conn, _params) do
    uid = conn.assigns.current_player.uid
    economy = Economy.get_economy(uid)

    cond do
      economy == nil ->
        conn |> put_status(404) |> json(%{error: "No economy record"})

      economy.lat == nil or economy.lon == nil ->
        conn |> put_status(404) |> json(%{error: "No location set"})

      true ->
        case Weather.get_or_fetch(economy.lat, economy.lon) do
          {:ok, cache} ->
            conn |> put_status(200) |> json(%{weather: cache.weather_data})

          {:error, :no_api_key} ->
            conn |> put_status(503) |> json(%{error: "Weather service unavailable"})

          {:error, _} ->
            conn |> put_status(503) |> json(%{error: "Weather service error"})
        end
    end
  end

  # ── Serializers ─────────────────────────────────────────────

  defp serialize_plot(plot) do
    %{
      id: plot.id,
      seedName: plot.seed_name,
      state: plot.state,
      plantTimeUtc: format_datetime(plot.plant_time_utc),
      waterCount: plot.water_count,
      lastWateredUtc: format_datetime(plot.last_watered_utc),
      snapshots: plot.snapshots,
      gridX: plot.grid_x,
      gridY: plot.grid_y,
      skinName: plot.skin_name,
      unlockedSkins: plot.unlocked_skins
    }
  end

  defp serialize_vase(vase) do
    %{
      id: vase.id,
      capacity: vase.capacity,
      currentWater: vase.current_water,
      state: vase.state,
      fillStartTimeUtc: format_datetime(vase.fill_start_time_utc),
      gridX: vase.grid_x,
      gridY: vase.grid_y,
      skinName: vase.skin_name,
      unlockedSkins: vase.unlocked_skins
    }
  end

  defp serialize_garden(garden) do
    %{
      id: garden.id,
      plantName: garden.plant_name,
      plantTimeUtc: format_datetime(garden.plant_time_utc),
      lastYieldTimeUtc: format_datetime(garden.last_yield_time_utc),
      mature: garden.mature,
      gridX: garden.grid_x,
      gridY: garden.grid_y
    }
  end

  defp serialize_mallum(mallum) do
    %{
      id: mallum.id,
      state: mallum.state,
      assignedQuestName: mallum.assigned_quest_name,
      startTimeUtc: format_datetime(mallum.start_time_utc),
      assignedVaseId: mallum.assigned_vase_id,
      pendingRewards: mallum.pending_rewards
    }
  end

  defp serialize_mallum_house(house) do
    %{
      id: house.id,
      gridX: house.grid_x,
      gridY: house.grid_y,
      skinName: house.skin_name,
      unlockedSkins: house.unlocked_skins || []
    }
  end

  defp serialize_bird(bird) do
    %{
      id: bird.id,
      gridX: bird.grid_x,
      gridY: bird.grid_y,
      seedName: bird.seed_name,
      seedCount: bird.seed_count,
      spawnedAtUtc: format_datetime(bird.spawned_at_utc)
    }
  end

  defp format_datetime(nil), do: nil
  defp format_datetime(%DateTime{} = dt), do: DateTime.to_iso8601(dt)

  defp format_error(reason) when is_atom(reason), do: Atom.to_string(reason)
  defp format_error(reason) when is_binary(reason), do: reason
  defp format_error({:insufficient_items, name}), do: "Insufficient items: #{name}"
  defp format_error(reason), do: inspect(reason)
end
