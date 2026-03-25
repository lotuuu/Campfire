defmodule CampFire.Game.Plots do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerPlot, PlayerVase, GrowthRecipe, GridValidation}
  alias CampFire.Economy

  defp water_cooldown_seconds do
    config = CampFire.ConfigCache.get("plot_config") ||
      raise "plot_config not loaded in ConfigCache"
    config["water_cooldown_seconds"]
  end

  @potion_types %{
    "hot_potion" => %{"type" => "hot", "value" => 15},
    "cool_potion" => %{"type" => "cool", "value" => -15},
    "wind_potion" => %{"type" => "wind", "value" => 10},
    "calm_potion" => %{"type" => "calm", "value" => -10},
    "humid_potion" => %{"type" => "humid", "value" => 30},
    "dry_potion" => %{"type" => "dry", "value" => -30},
    "sun_potion" => %{"type" => "sun"},
    "shadow_potion" => %{"type" => "shadow"},
    "rain_potion" => %{"type" => "rain"},
    "impermeable_potion" => %{"type" => "impermeable"},
    "moon_potion" => %{"type" => "moon"}
  }

  @empty_snapshots %{
    "temperatures" => [],
    "wind_speeds" => [],
    "humidities" => [],
    "cloud_covers" => [],
    "rain_snapshots" => [],
    "moon_phase_snapshots" => [],
    "snapshot_count" => 0
  }

  # --- Queries ---

  def list_plots(player_uid) do
    from(p in PlayerPlot, where: p.player_uid == ^player_uid) |> Repo.all()
  end

  # --- Build ---

  def count_plots(player_uid) do
    from(p in PlayerPlot, where: p.player_uid == ^player_uid, select: count(p.id))
    |> Repo.one()
  end

  defp get_plot_cost(plot_count) do
    case CampFire.ConfigCache.get("flame_config") do
      nil -> nil
      config ->
        costs = config["plot_costs"]
        idx = min(plot_count, length(costs) - 1)
        Enum.at(costs, idx)
    end
  end

  def build_plot(player_uid, grid_x, grid_y, opts \\ []) do
    with :ok <- GridValidation.check_entity_cap(player_uid, opts),
         :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
      # Subtract 1 for the free starter plot so cost index is based on purchased plots
      plot_count = max(count_plots(player_uid) - 1, 0)

      case get_plot_cost(plot_count) do
        nil -> {:error, :config_not_loaded}
        cost ->
      Repo.transaction(fn ->
      case Economy.spend_mana(player_uid, cost["manaCost"], opts) do
        {:ok, _economy} -> :ok
        {:error, reason} -> Repo.rollback(reason)
      end

      harvest_costs = cost["harvestCosts"] || []

      Enum.each(harvest_costs, fn %{"itemKey" => key, "count" => count} ->
        case Economy.spend_item(player_uid, key, count, opts) do
          {:ok, _} -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end
      end)

      %PlayerPlot{}
      |> PlayerPlot.changeset(%{
        player_uid: player_uid,
        state: "empty",
        grid_x: grid_x,
        grid_y: grid_y
      })
      |> Repo.insert!()
      end)
      end
    end
  end

  # --- Plant ---

  def plant(player_uid, plot_id, plant_key, opts \\ []) do
    seed_configs = CampFire.ConfigCache.get("seed_configs") || %{}

    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- Map.has_key?(seed_configs, plant_key) || {:error, :unknown_seed},
         true <- plot.state == "empty" || {:error, :plot_not_empty} do
      seed_config = CampFire.Game.get_seed_config!(plant_key)

      Repo.transaction(fn ->
        case Economy.spend_item(player_uid, seed_config.item_key, 1, opts) do
          {:error, reason} ->
            Repo.rollback(reason)

          _ ->
            now = DateTime.utc_now() |> DateTime.truncate(:second)

            planted =
              plot
              |> PlayerPlot.changeset(%{
                seed_item_id: seed_config.item_id,
                state: "growing",
                plant_time_utc: now,
                water_count: 0,
                last_watered_utc: nil,
                snapshots: @empty_snapshots,
                potions: []
              })
              |> Repo.update!()

            # Record initial weather snapshot so short-lived plants get at least one
            record_initial_snapshot(player_uid, planted.id)

            planted
        end
      end)
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Water ---

  def water(player_uid, plot_id, vase_id) do
    alias CampFire.Game.Vases

    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         %PlayerVase{} = vase <- Repo.get(PlayerVase, vase_id) || {:error, :vase_not_found} do
      now = DateTime.utc_now() |> DateTime.truncate(:second)

      cond do
        plot.player_uid != player_uid ->
          {:error, :not_owned}

        vase.player_uid != player_uid ->
          {:error, :not_owned}

        plot.state != "growing" ->
          {:error, :not_growing}

        plot.last_watered_utc != nil and
            DateTime.diff(now, plot.last_watered_utc, :second) < water_cooldown_seconds() ->
          {:error, :water_cooldown}

        true ->
          Repo.transaction(fn ->
            case Vases.use_water(vase_id, 1) do
              {:error, reason} ->
                Repo.rollback(reason)

              {:ok, _vase} ->
                plot
                |> PlayerPlot.changeset(%{
                  water_count: plot.water_count + 1,
                  last_watered_utc: now
                })
                |> Repo.update!()
            end
          end)
      end
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Fertilize ---

  def fertilize(player_uid, plot_id, opts \\ []) do
    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- plot.state == "growing" || {:error, :not_growing},
         true <- not plot.fertilized || {:error, :already_fertilized} do
      case Economy.spend_item(player_uid, "fertilizer", 1, opts) do
        {:ok, _} ->
          plot
          |> PlayerPlot.changeset(%{fertilized: true})
          |> Repo.update()

        {:error, reason} ->
          {:error, reason}
      end
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Apply Potion ---

  def apply_potion(player_uid, plot_id, potion_item_key, opts \\ []) do
    case Map.get(@potion_types, potion_item_key) do
      nil ->
        {:error, :unknown_potion}

      potion_entry ->
        with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
             true <- plot.player_uid == player_uid || {:error, :not_owned},
             true <- plot.state == "growing" || {:error, :not_growing},
             {:ok, _} <- Economy.spend_item(player_uid, potion_item_key, 1, opts) do
          updated_potions = (plot.potions || []) ++ [potion_entry]

          plot
          |> PlayerPlot.changeset(%{potions: updated_potions})
          |> Repo.update()
        else
          nil -> {:error, :not_found}
          {:error, _} = err -> err
        end
    end
  end

  # --- Harvest ---

  def harvest(player_uid, plot_id) do
    check_maturity(plot_id)

    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- plot.state == "mature" || {:error, {:not_mature, plot.state}} do
      Repo.transaction(fn ->
        seed_config = CampFire.Game.get_seed_config_by_item_id!(plot.seed_item_id)

        score = GrowthRecipe.evaluate(seed_config.recipe, plot.snapshots, plot.water_count)
        base_drops = GrowthRecipe.calculate_drops(score, seed_config.min_drops, seed_config.max_drops)
        bonus_drops = if plot.fertilized, do: max(ceil(base_drops * 0.5), 1), else: 0
        drops = base_drops + bonus_drops

        snapshot_count = get_in(plot.snapshots || %{}, [Access.key("snapshot_count", 0)])
        if snapshot_count == 0 do
          require Logger
          Logger.warning("Harvest with zero snapshots: player=#{player_uid} plot=#{plot_id} seed_item_id=#{plot.seed_item_id}")
        end
        harvest_item_key = seed_config.harvest_item_key

        case Economy.upsert_item(player_uid, harvest_item_key, drops) do
          {:ok, _} -> :ok
          {:error, reason} -> Repo.rollback({:upsert_failed, harvest_item_key, reason})
        end

        plot
        |> PlayerPlot.changeset(%{
          state: "empty",
          seed_item_id: nil,
          plant_time_utc: nil,
          water_count: 0,
          last_watered_utc: nil,
          snapshots: %{},
          fertilized: false,
          potions: []
        })
        |> Repo.update!()

        %{score: score, drops: drops, bonus_drops: bonus_drops, harvest_item_key: harvest_item_key}
      end)
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Harvest Preview ---

  @doc """
  Returns the harvest result (score, drops, item key) without actually harvesting.
  Runs check_maturity first to ensure server-side state is up to date.
  """
  def harvest_preview(player_uid, plot_id) do
    check_maturity(plot_id)

    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- plot.state == "mature" || {:error, {:not_mature, plot.state}} do
      seed_config = CampFire.Game.get_seed_config_by_item_id!(plot.seed_item_id)
      score = GrowthRecipe.evaluate(seed_config.recipe, plot.snapshots, plot.water_count)
      base_drops = GrowthRecipe.calculate_drops(score, seed_config.min_drops, seed_config.max_drops)
      bonus_drops = if plot.fertilized, do: max(ceil(base_drops * 0.5), 1), else: 0
      drops = base_drops + bonus_drops

      {:ok, %{score: score, drops: drops, bonus_drops: bonus_drops, harvest_item_key: seed_config.harvest_item_key}}
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Maturity Check ---

  # Server matures plots 4 seconds early so the client (which detects maturity
  # based on its own timer) can pre-fetch harvest results before the player taps.
  @maturity_buffer_seconds 4

  def check_maturity(plot_id) do
    case Repo.get(PlayerPlot, plot_id) do
      nil ->
        {:error, :not_found}

      plot ->
        if plot.state == "growing" do
          seed_config = CampFire.Game.get_seed_config_by_item_id!(plot.seed_item_id)

          now = DateTime.utc_now() |> DateTime.truncate(:second)
          elapsed_seconds = DateTime.diff(now, plot.plant_time_utc, :second)
          required_seconds = max(trunc(seed_config.growth_duration_hours * 3600) - @maturity_buffer_seconds, 0)

          if elapsed_seconds >= required_seconds do
            plot
            |> PlayerPlot.changeset(%{state: "mature"})
            |> Repo.update()
          else
            {:ok, plot}
          end
        else
          {:ok, plot}
        end
    end
  end

  # --- Potion Weather Modifier ---

  defp apply_potions(weather_data, [], _plot), do: weather_data

  defp apply_potions(weather_data, potions, plot) do
    Enum.reduce(potions, weather_data, fn potion, wd ->
      case potion["type"] do
        "hot" -> Map.update(wd, "temperature", 0.0, &(&1 + potion["value"]))
        "cool" -> Map.update(wd, "temperature", 0.0, &(&1 + potion["value"]))
        "wind" -> Map.update(wd, "wind_speed", 0.0, &max(&1 + potion["value"], 0.0))
        "calm" -> Map.update(wd, "wind_speed", 0.0, &max(&1 + potion["value"], 0.0))
        "humid" -> Map.update(wd, "humidity", 0.0, &min(max(&1 + potion["value"], 0.0), 100.0))
        "dry" -> Map.update(wd, "humidity", 0.0, &min(max(&1 + potion["value"], 0.0), 100.0))
        "sun" -> Map.put(wd, "cloud_cover", 0.0)
        "shadow" -> Map.put(wd, "cloud_cover", 100.0)
        "rain" -> Map.put(wd, "is_raining", true)
        "impermeable" -> Map.put(wd, "is_raining", false)
        "moon" ->
          seed_config = CampFire.Game.get_seed_config_by_item_id!(plot.seed_item_id)
          moon_axis = get_in(seed_config.recipe, ["moon"])
          if is_map(moon_axis) and moon_axis["enabled"] do
            Map.put(wd, "moon_phase", moon_axis["ideal_min"])
          else
            wd
          end
        _ -> wd
      end
    end)
  end

  # --- Snapshot Recording ---

  defp record_initial_snapshot(player_uid, plot_id) do
    economy = Economy.get_economy(player_uid)

    if economy && economy.lat && economy.lon do
      case CampFire.Game.Weather.get_or_fetch(economy.lat, economy.lon) do
        {:ok, cache} -> record_snapshot(plot_id, cache.weather_data)
        _ -> :ok
      end
    end
  end

  def record_snapshot(plot_id, weather_data) do
    case Repo.get(PlayerPlot, plot_id) do
      nil ->
        {:error, :not_found}

      plot ->
        if plot.state == "growing" do
          effective_weather = apply_potions(weather_data, plot.potions || [], plot)
          snapshots = plot.snapshots || @empty_snapshots

          updated_snapshots = %{
            "temperatures" => (snapshots["temperatures"] || []) ++ [effective_weather["temperature"] || 0.0],
            "wind_speeds" => (snapshots["wind_speeds"] || []) ++ [effective_weather["wind_speed"] || 0.0],
            "humidities" => (snapshots["humidities"] || []) ++ [effective_weather["humidity"] || 0.0],
            "cloud_covers" => (snapshots["cloud_covers"] || []) ++ [effective_weather["cloud_cover"] || 0.0],
            "rain_snapshots" => (snapshots["rain_snapshots"] || []) ++ [if(effective_weather["is_raining"], do: 1.0, else: 0.0)],
            "moon_phase_snapshots" => (snapshots["moon_phase_snapshots"] || []) ++ [effective_weather["moon_phase"] || 0.0],
            "snapshot_count" => (snapshots["snapshot_count"] || 0) + 1
          }

          plot
          |> PlayerPlot.changeset(%{snapshots: updated_snapshots})
          |> Repo.update()
        else
          {:ok, plot}
        end
    end
  end

  # --- Skins ---

  def set_skin(player_uid, plot_id, skin_name) do
    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- skin_name in (plot.unlocked_skins || []) || {:error, :skin_not_unlocked} do
      plot
      |> PlayerPlot.changeset(%{skin_name: skin_name})
      |> Repo.update()
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Instant Finish ---

  defp speed_item do
    config = CampFire.ConfigCache.get("plot_config") ||
      raise "plot_config not loaded in ConfigCache"
    config["speed_item"]
  end

  def instant_finish(player_uid, plot_id, opts \\ []) do
    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- plot.state == "growing" || {:error, :not_growing},
         {:ok, _} <- CampFire.Economy.spend_item(player_uid, speed_item(), 1, opts) do
      plot
      |> PlayerPlot.changeset(%{state: "mature"})
      |> Repo.update()
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Testing ---

  def force_mature(plot_id) do
    case Repo.get(PlayerPlot, plot_id) do
      nil -> {:error, :not_found}
      plot ->
        plot
        |> PlayerPlot.changeset(%{state: "mature"})
        |> Repo.update()
    end
  end
end
