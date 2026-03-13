defmodule CampFire.Game.Plots do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerPlot, PlayerVase, SeedConfig, GrowthRecipe, GridValidation}
  alias CampFire.Economy

  defp water_cooldown_seconds do
    config = CampFire.ConfigCache.get("plot_config") ||
      raise "plot_config not loaded in ConfigCache"
    config["water_cooldown_seconds"]
  end

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

  # --- Craft ---

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

  def craft_plot(player_uid, grid_x, grid_y, opts \\ []) do
    with :ok <- GridValidation.check_entity_cap(player_uid, opts),
         :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
      plot_count = count_plots(player_uid)

      case get_plot_cost(plot_count) do
        nil -> {:error, :config_not_loaded}
        cost ->
      Repo.transaction(fn ->
      case Economy.spend_mana(player_uid, cost["manaCost"], opts) do
        {:ok, _economy} -> :ok
        {:error, reason} -> Repo.rollback(reason)
      end

      harvest_costs = cost["harvestCosts"] || []

      Enum.each(harvest_costs, fn %{"itemName" => name, "count" => count} ->
        case Economy.spend_item(player_uid, name, count, opts) do
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

  def plant(player_uid, plot_id, seed_name, opts \\ []) do
    seed_configs = CampFire.ConfigCache.get("seed_configs") || %{}

    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- Map.has_key?(seed_configs, seed_name) || {:error, :unknown_seed},
         true <- plot.state == "empty" || {:error, :plot_not_empty} do
      Repo.transaction(fn ->
        case Economy.spend_item(player_uid, seed_name <> "_Seed", 1, opts) do
          {:error, reason} ->
            Repo.rollback(reason)

          _ ->
            now = DateTime.utc_now() |> DateTime.truncate(:second)

            plot
            |> PlayerPlot.changeset(%{
              seed_name: seed_name,
              state: "growing",
              plant_time_utc: now,
              water_count: 0,
              last_watered_utc: nil,
              snapshots: @empty_snapshots
            })
            |> Repo.update!()
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

  # --- Harvest ---

  def harvest(player_uid, plot_id) do
    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- plot.state == "mature" || {:error, :not_mature} do
      Repo.transaction(fn ->
        seed_config =
          Repo.one!(from sc in SeedConfig, where: sc.seed_name == ^plot.seed_name)

        score = GrowthRecipe.evaluate(seed_config.recipe, plot.snapshots, plot.water_count)
        drops = GrowthRecipe.calculate_drops(score, seed_config.min_drops, seed_config.max_drops)

        snapshot_count = get_in(plot.snapshots || %{}, [Access.key("snapshot_count", 0)])
        if snapshot_count == 0 do
          require Logger
          Logger.warning("Harvest with zero snapshots: player=#{player_uid} plot=#{plot_id} seed=#{plot.seed_name}")
        end
        item_name = plot.seed_name

        Economy.upsert_item(player_uid, item_name, drops)

        plot
        |> PlayerPlot.changeset(%{
          state: "empty",
          seed_name: nil,
          plant_time_utc: nil,
          water_count: 0,
          last_watered_utc: nil,
          snapshots: %{}
        })
        |> Repo.update!()

        %{score: score, drops: drops, item_name: item_name}
      end)
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Maturity Check ---

  def check_maturity(plot_id) do
    case Repo.get(PlayerPlot, plot_id) do
      nil ->
        {:error, :not_found}

      plot ->
        if plot.state == "growing" do
          seed_config =
            Repo.one!(from sc in SeedConfig, where: sc.seed_name == ^plot.seed_name)

          now = DateTime.utc_now() |> DateTime.truncate(:second)
          elapsed_hours = DateTime.diff(now, plot.plant_time_utc, :second) / 3600.0

          if elapsed_hours >= seed_config.growth_duration_hours do
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

  # --- Snapshot Recording ---

  def record_snapshot(plot_id, weather_data) do
    case Repo.get(PlayerPlot, plot_id) do
      nil ->
        {:error, :not_found}

      plot ->
        if plot.state == "growing" do
          snapshots = plot.snapshots || @empty_snapshots

          updated_snapshots = %{
            "temperatures" => (snapshots["temperatures"] || []) ++ [weather_data["temperature"] || 0.0],
            "wind_speeds" => (snapshots["wind_speeds"] || []) ++ [weather_data["wind_speed"] || 0.0],
            "humidities" => (snapshots["humidities"] || []) ++ [weather_data["humidity"] || 0.0],
            "cloud_covers" => (snapshots["cloud_covers"] || []) ++ [weather_data["cloud_cover"] || 0.0],
            "rain_snapshots" => (snapshots["rain_snapshots"] || []) ++ [if(weather_data["is_raining"], do: 1.0, else: 0.0)],
            "moon_phase_snapshots" => (snapshots["moon_phase_snapshots"] || []) ++ [weather_data["moon_phase"] || 0.0],
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

  def instant_finish(player_uid, plot_id) do
    with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
         true <- plot.player_uid == player_uid || {:error, :not_owned},
         true <- plot.state == "growing" || {:error, :not_growing},
         {:ok, _} <- CampFire.Economy.spend_item(player_uid, "Speed_Potion", 1) do
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
