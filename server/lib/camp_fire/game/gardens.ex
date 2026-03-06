defmodule CampFire.Game.Gardens do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerGarden, GridValidation}
  alias CampFire.Economy

  # --- Config Helpers ---

  defp get_plant_config(plant_name) do
    garden_map = CampFire.ConfigCache.get("garden_configs")

    case Map.get(garden_map, plant_name) do
      nil -> nil
      cached -> normalize_plant_config(cached)
    end
  end

  defp get_all_plant_configs do
    garden_map = CampFire.ConfigCache.get("garden_configs")
    Map.new(garden_map, fn {k, v} -> {k, normalize_plant_config(v)} end)
  end

  defp normalize_plant_config(cached) do
    %{
      growth_hours: cached[:growth_duration_hours] || cached["growth_duration_hours"],
      yield_item: cached[:yield_item] || cached["yield_item"],
      yield_amount: cached[:yield_amount] || cached["yield_amount"],
      yield_interval_hours: cached[:yield_interval_hours] || cached["yield_interval_hours"],
      mana_cost: cached[:mana_cost] || cached["mana_cost"]
    }
  end

  # --- Queries ---

  def list_gardens(player_uid) do
    from(g in PlayerGarden, where: g.player_uid == ^player_uid) |> Repo.all()
  end

  def get_plant_configs, do: get_all_plant_configs()

  # --- Plant ---

  def plant(player_uid, plant_name, grid_x, grid_y) do
    with {:config, config} when config != nil <- {:config, get_plant_config(plant_name)},
         :ok <- GridValidation.check_entity_cap(player_uid),
         :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
      Repo.transaction(fn ->
          case Economy.spend_mana(player_uid, config.mana_cost) do
            {:ok, _economy} -> :ok
            {:error, reason} -> Repo.rollback(reason)
          end

          now = DateTime.utc_now() |> DateTime.truncate(:second)

          %PlayerGarden{}
          |> PlayerGarden.changeset(%{
            player_uid: player_uid,
            plant_name: plant_name,
            plant_time_utc: now,
            mature: false,
            grid_x: grid_x,
            grid_y: grid_y
          })
          |> Repo.insert!()
        end)
    else
      {:config, nil} -> {:error, :unknown_plant}
      error -> error
    end
  end

  # --- Check & Collect ---

  def check_and_collect(player_uid, garden_id) do
    garden = Repo.get!(PlayerGarden, garden_id)

    cond do
      garden.player_uid != player_uid ->
        {:error, :not_owned}

      true ->
        config = get_plant_config(garden.plant_name)
        now = DateTime.utc_now() |> DateTime.truncate(:second)

        # Check maturity
        garden =
          if not garden.mature do
            elapsed_hours = DateTime.diff(now, garden.plant_time_utc, :second) / 3600.0

            if elapsed_hours >= config.growth_hours do
              garden
              |> PlayerGarden.changeset(%{mature: true})
              |> Repo.update!()
            else
              garden
            end
          else
            garden
          end

        if not garden.mature do
          {:ok, %{status: :growing, garden: garden}}
        else
          # Check yield interval
          reference_time = garden.last_yield_time_utc || garden.plant_time_utc
          elapsed_since_yield = DateTime.diff(now, reference_time, :second) / 3600.0

          if elapsed_since_yield >= config.yield_interval_hours do
            Economy.upsert_item(player_uid, config.yield_item, config.yield_amount)

            updated_garden =
              garden
              |> PlayerGarden.changeset(%{last_yield_time_utc: now})
              |> Repo.update!()

            {:ok,
             %{
               status: :collected,
               garden: updated_garden,
               item: config.yield_item,
               amount: config.yield_amount
             }}
          else
            {:ok, %{status: :not_ready, garden: garden}}
          end
        end
    end
  end
end
