defmodule CampFire.Game.Vases do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerVase, PlayerMallum, GridValidation}
  alias CampFire.Economy

  @default_capacity 5
  @fill_seconds_per_unit 60

  # --- Queries ---

  def list_vases(player_uid) do
    from(v in PlayerVase, where: v.player_uid == ^player_uid) |> Repo.all()
  end

  # --- Craft ---

  def count_vases(player_uid) do
    from(v in PlayerVase, where: v.player_uid == ^player_uid, select: count(v.id))
    |> Repo.one()
  end

  defp get_vase_cost(vase_count) do
    case CampFire.ConfigCache.get("building_cost_config") do
      nil -> nil
      config ->
        costs = config["vase_costs"]
        idx = min(vase_count, length(costs) - 1)
        Enum.at(costs, idx)
    end
  end

  def craft_vase(player_uid, grid_x, grid_y) do
    with :ok <- GridValidation.check_entity_cap(player_uid),
         :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
      vase_count = count_vases(player_uid)

      case get_vase_cost(vase_count) do
        nil -> {:error, :config_not_loaded}
        cost ->
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

      %PlayerVase{}
      |> PlayerVase.changeset(%{
        player_uid: player_uid,
        state: "empty",
        capacity: @default_capacity,
        current_water: 0,
        grid_x: grid_x,
        grid_y: grid_y
      })
      |> Repo.insert!()
      end)
      end
    end
  end

  # --- Fill ---

  def start_fill(player_uid, vase_id) do
    with %PlayerVase{} = vase <- Repo.get(PlayerVase, vase_id),
         true <- vase.player_uid == player_uid || {:error, :not_owned},
         true <- vase.state != "filling" || {:error, :already_filling} do
      case claim_idle_mallum_for_water(player_uid, vase_id) do
        {:error, reason} ->
          {:error, reason}

        {:ok, _mallum} ->
          now = DateTime.utc_now() |> DateTime.truncate(:second)

          vase
          |> PlayerVase.changeset(%{
            state: "filling",
            fill_start_time_utc: now
          })
          |> Repo.update()
      end
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  def check_fill(player_uid, vase_id) do
    with %PlayerVase{} = vase <- Repo.get(PlayerVase, vase_id),
         true <- vase.player_uid == player_uid || {:error, :not_owned},
         true <- vase.state == "filling" || {:error, :not_filling} do
      now = DateTime.utc_now() |> DateTime.truncate(:second)
      elapsed = DateTime.diff(now, vase.fill_start_time_utc, :second)
      required = vase.capacity * @fill_seconds_per_unit

      if elapsed >= required do
        # Free the mallum assigned to this vase
        free_mallum_for_vase(player_uid, vase_id)

        vase
        |> PlayerVase.changeset(%{
          state: "full",
          current_water: vase.capacity,
          fill_start_time_utc: nil
        })
        |> Repo.update()
      else
        {:ok, vase}
      end
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Instant Finish ---

  def instant_finish(player_uid, vase_id) do
    alias CampFire.Economy

    with %PlayerVase{} = vase <- Repo.get(PlayerVase, vase_id),
         true <- vase.player_uid == player_uid || {:error, :not_owned},
         true <- vase.state == "filling" || {:error, :not_filling},
         {:ok, _} <- Economy.spend_item(player_uid, "Speed_Potion", 1) do
      # Free the mallum assigned to this vase
      free_mallum_for_vase(player_uid, vase_id)

      vase
      |> PlayerVase.changeset(%{
        state: "full",
        current_water: vase.capacity,
        fill_start_time_utc: nil
      })
      |> Repo.update()
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Water Usage ---

  def use_water(vase_id, amount) when is_integer(amount) and amount > 0 do
    case Repo.get(PlayerVase, vase_id) do
      nil ->
        {:error, :not_found}

      vase ->
        if vase.current_water < amount do
          {:error, :insufficient_water}
        else
          new_water = vase.current_water - amount
          new_state = if new_water == 0, do: "empty", else: vase.state

          vase
          |> PlayerVase.changeset(%{current_water: new_water, state: new_state})
          |> Repo.update()
        end
    end
  end

  def set_water(vase_id, amount) when is_integer(amount) and amount >= 0 do
    case Repo.get(PlayerVase, vase_id) do
      nil ->
        {:error, :not_found}

      vase ->
        new_state = if amount == 0, do: "empty", else: "full"

        vase
        |> PlayerVase.changeset(%{current_water: amount, state: new_state})
        |> Repo.update()
    end
  end

  # --- Rain ---

  def rain_fill_all(player_uid) do
    vases = from(v in PlayerVase, where: v.player_uid == ^player_uid) |> Repo.all()

    Enum.each(vases, fn vase ->
      vase
      |> PlayerVase.changeset(%{
        current_water: vase.capacity,
        state: "full",
        fill_start_time_utc: nil
      })
      |> Repo.update!()
    end)

    # Free all mallums fetching water
    from(m in PlayerMallum,
      where: m.player_uid == ^player_uid and m.state == "fetching_water"
    )
    |> Repo.all()
    |> Enum.each(fn mallum ->
      mallum
      |> PlayerMallum.changeset(%{state: "idle", assigned_vase_id: nil})
      |> Repo.update!()
    end)

    :ok
  end

  # --- Skins ---

  def set_skin(player_uid, vase_id, skin_name) do
    with %PlayerVase{} = vase <- Repo.get(PlayerVase, vase_id),
         true <- vase.player_uid == player_uid || {:error, :not_owned},
         true <- skin_name in (vase.unlocked_skins || []) || {:error, :skin_not_unlocked} do
      vase
      |> PlayerVase.changeset(%{skin_name: skin_name})
      |> Repo.update()
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Private Helpers ---

  defp claim_idle_mallum_for_water(player_uid, vase_id) do
    Repo.transaction(fn ->
      case Repo.one(
             from(m in PlayerMallum,
               where: m.player_uid == ^player_uid and m.state == "idle",
               limit: 1,
               lock: "FOR UPDATE SKIP LOCKED"
             )
           ) do
        nil ->
          Repo.rollback(:no_idle_mallum)

        mallum ->
          now = DateTime.utc_now() |> DateTime.truncate(:second)

          mallum
          |> PlayerMallum.changeset(%{
            state: "fetching_water",
            assigned_vase_id: vase_id,
            start_time_utc: now
          })
          |> Repo.update!()
      end
    end)
  end

  defp free_mallum_for_vase(player_uid, vase_id) do
    case Repo.one(
           from(m in PlayerMallum,
             where:
               m.player_uid == ^player_uid and
                 m.state == "fetching_water" and
                 m.assigned_vase_id == ^vase_id,
             limit: 1
           )
         ) do
      nil -> :ok
      mallum ->
        mallum
        |> PlayerMallum.changeset(%{state: "idle", assigned_vase_id: nil, start_time_utc: nil})
        |> Repo.update!()
    end
  end
end
