defmodule CampFire.Game.Plots do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerPlot, SeedConfig, GrowthRecipe}
  alias CampFire.Economy

  @water_cooldown_seconds 7200

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
    config = CampFire.ConfigCache.get("building_cost_config")
    costs = config["plot_costs"]
    idx = min(plot_count, length(costs) - 1)
    Enum.at(costs, idx)
  end

  def craft_plot(player_uid, grid_x, grid_y) do
    plot_count = count_plots(player_uid)
    cost = get_plot_cost(plot_count)

    Repo.transaction(fn ->
      case Economy.spend_mana(player_uid, cost["manaCost"]) do
        {:ok, _economy} -> :ok
        {:error, reason} -> Repo.rollback(reason)
      end

      harvest_costs = cost["harvestCosts"] || []

      Enum.each(harvest_costs, fn %{"itemName" => name, "count" => count} ->
        case Economy.spend_item(player_uid, name, count) do
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

  # --- Plant ---

  def plant(player_uid, plot_id, seed_name) do
    plot = Repo.get!(PlayerPlot, plot_id)

    cond do
      plot.player_uid != player_uid ->
        {:error, :not_owned}

      plot.state != "empty" ->
        {:error, :plot_not_empty}

      true ->
        case Economy.spend_seed(player_uid, seed_name, 1) do
          {:error, reason} ->
            {:error, reason}

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
            |> Repo.update()
        end
    end
  end

  # --- Water ---

  def water(player_uid, plot_id, vase_id) do
    alias CampFire.Game.Vases

    plot = Repo.get!(PlayerPlot, plot_id)
    now = DateTime.utc_now() |> DateTime.truncate(:second)

    cond do
      plot.player_uid != player_uid ->
        {:error, :not_owned}

      plot.state != "growing" ->
        {:error, :not_growing}

      plot.last_watered_utc != nil and
          DateTime.diff(now, plot.last_watered_utc, :second) < @water_cooldown_seconds ->
        {:error, :water_cooldown}

      true ->
        case Vases.use_water(vase_id, 1) do
          {:error, reason} ->
            {:error, reason}

          {:ok, _vase} ->
            plot
            |> PlayerPlot.changeset(%{
              water_count: plot.water_count + 1,
              last_watered_utc: now
            })
            |> Repo.update()
        end
    end
  end

  # --- Harvest ---

  def harvest(player_uid, plot_id) do
    plot = Repo.get!(PlayerPlot, plot_id)

    cond do
      plot.player_uid != player_uid ->
        {:error, :not_owned}

      plot.state != "mature" ->
        {:error, :not_mature}

      true ->
        seed_config =
          Repo.one!(from sc in SeedConfig, where: sc.seed_name == ^plot.seed_name)

        score = GrowthRecipe.evaluate(seed_config.recipe, plot.snapshots, plot.water_count)
        drops = GrowthRecipe.calculate_drops(score, seed_config.base_drops)
        item_name = "#{plot.seed_name}_harvest"

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
        |> Repo.update()

        {:ok, %{score: score, drops: drops, item_name: item_name}}
    end
  end

  # --- Maturity Check ---

  def check_maturity(plot_id) do
    plot = Repo.get!(PlayerPlot, plot_id)

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

  # --- Snapshot Recording ---

  def record_snapshot(plot_id, weather_data) do
    plot = Repo.get!(PlayerPlot, plot_id)

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

  # --- Skins ---

  def set_skin(player_uid, plot_id, skin_name) do
    plot = Repo.get!(PlayerPlot, plot_id)

    cond do
      plot.player_uid != player_uid ->
        {:error, :not_owned}

      skin_name not in (plot.unlocked_skins || []) ->
        {:error, :skin_not_unlocked}

      true ->
        plot
        |> PlayerPlot.changeset(%{skin_name: skin_name})
        |> Repo.update()
    end
  end

  # --- Testing ---

  def force_mature(plot_id) do
    plot = Repo.get!(PlayerPlot, plot_id)

    plot
    |> PlayerPlot.changeset(%{state: "mature"})
    |> Repo.update()
  end
end
