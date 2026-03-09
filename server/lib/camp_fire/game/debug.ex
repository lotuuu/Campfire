defmodule CampFire.Game.Debug do
  @moduledoc """
  Debug helpers for manipulating player state during development.

  These functions bypass normal game logic and directly modify the database.
  They should never be exposed in production builds.
  """

  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Economy.{PlayerEconomy, PlayerSeed, PlayerItem}

  alias CampFire.Game.{
    PlayerPlot,
    PlayerVase,
    PlayerGarden,
    PlayerMallum,
    PlayerBird,
    PlayerMallumHouse,
    PlayerState,
    Birds,
    Mallums
  }

  # ── Time Manipulation ────────────────────────────────────────

  @doc """
  Shift all player timestamps backward by `hours` so timers appear elapsed.

  Affects growing plots, filling vases, questing/fetching mallums, gardens,
  bird check hour, and mana collection timestamp.
  """
  def skip_time(player_uid, hours) when is_number(hours) and hours > 0 do
    seconds = trunc(hours * 3600)

    Repo.transaction(fn ->
      # Plots: shift plant_time_utc and last_watered_utc for growing plots
      from(p in PlayerPlot,
        where: p.player_uid == ^player_uid and p.state == "growing"
      )
      |> Repo.update_all(
        set: [
          plant_time_utc:
            dynamic([p], fragment("? - interval '1 second' * ?", p.plant_time_utc, ^seconds)),
          last_watered_utc:
            dynamic([p], fragment("? - interval '1 second' * ?", p.last_watered_utc, ^seconds))
        ]
      )

      # Vases: shift fill_start_time_utc for filling vases
      from(v in PlayerVase,
        where: v.player_uid == ^player_uid and v.state == "filling"
      )
      |> Repo.update_all(
        set: [
          fill_start_time_utc:
            dynamic([v], fragment("? - interval '1 second' * ?", v.fill_start_time_utc, ^seconds))
        ]
      )

      # Mallums on quest: shift start_time_utc
      from(m in PlayerMallum,
        where: m.player_uid == ^player_uid and m.state == "on_quest"
      )
      |> Repo.update_all(
        set: [
          start_time_utc:
            dynamic([m], fragment("? - interval '1 second' * ?", m.start_time_utc, ^seconds))
        ]
      )

      # Mallums fetching water: shift start_time_utc
      from(m in PlayerMallum,
        where: m.player_uid == ^player_uid and m.state == "fetching_water"
      )
      |> Repo.update_all(
        set: [
          start_time_utc:
            dynamic([m], fragment("? - interval '1 second' * ?", m.start_time_utc, ^seconds))
        ]
      )

      # Gardens: shift plant_time_utc and last_yield_time_utc
      from(g in PlayerGarden,
        where: g.player_uid == ^player_uid
      )
      |> Repo.update_all(
        set: [
          plant_time_utc:
            dynamic([g], fragment("? - interval '1 second' * ?", g.plant_time_utc, ^seconds)),
          last_yield_time_utc:
            dynamic([g], fragment("? - interval '1 second' * ?", g.last_yield_time_utc, ^seconds))
        ]
      )

      # Birds: shift last_bird_check_hour_utc in PlayerState.data
      case Repo.get(PlayerState, player_uid) do
        nil ->
          :ok

        state ->
          case Map.get(state.data || %{}, "last_bird_check_hour_utc") do
            nil ->
              :ok

            iso_str ->
              case DateTime.from_iso8601(iso_str) do
                {:ok, dt, _offset} ->
                  shifted = DateTime.add(dt, -seconds, :second)
                  new_data = Map.put(state.data, "last_bird_check_hour_utc", DateTime.to_iso8601(shifted))

                  state
                  |> PlayerState.changeset(%{data: new_data})
                  |> Repo.update!()

                _ ->
                  :ok
              end
          end
      end

      # Economy: shift last_mana_collect_utc backward
      from(e in PlayerEconomy,
        where: e.player_uid == ^player_uid
      )
      |> Repo.update_all(
        set: [
          last_mana_collect_utc:
            dynamic(
              [e],
              fragment("? - interval '1 second' * ?", e.last_mana_collect_utc, ^seconds)
            )
        ]
      )

      :ok
    end)
  end

  # ── Currency ─────────────────────────────────────────────────

  @doc "Set mana and/or gems directly. opts: [mana: float, gems: integer]"
  def set_currency(player_uid, opts) when is_list(opts) do
    updates =
      []
      |> maybe_add(:mana, Keyword.get(opts, :mana))
      |> maybe_add(:gems, Keyword.get(opts, :gems))

    if updates == [] do
      {:ok, :no_changes}
    else
      {count, results} =
        from(e in PlayerEconomy, where: e.player_uid == ^player_uid, select: e)
        |> Repo.update_all(set: updates)

      if count == 1, do: {:ok, hd(results)}, else: {:error, :not_found}
    end
  end

  defp maybe_add(list, _key, nil), do: list
  defp maybe_add(list, key, value), do: [{key, value} | list]

  # ── Inventory Grants ─────────────────────────────────────────

  @doc "Grant seeds to a player."
  def grant_seeds(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    Economy.upsert_seed(player_uid, seed_name, count)
  end

  @doc "Grant items to a player."
  def grant_items(player_uid, item_name, count) when is_integer(count) and count > 0 do
    Economy.upsert_item(player_uid, item_name, count)
  end

  # ── Bird Spawning ────────────────────────────────────────────

  @doc "Force-spawn a bird for debug purposes."
  def spawn_bird(player_uid) do
    case Repo.get(PlayerEconomy, player_uid) do
      nil -> {:error, :not_found}
      economy -> Birds.try_spawn_bird_public(player_uid, economy.flame_level)
    end
  end

  # ── Quest Completion ─────────────────────────────────────────

  @doc "Complete all in-progress quests immediately, rolling rewards."
  def complete_quests(player_uid) do
    mallums =
      from(m in PlayerMallum,
        where: m.player_uid == ^player_uid and m.state == "on_quest"
      )
      |> Repo.all()

    Enum.map(mallums, fn mallum ->
      config = Mallums.get_quest_config_public(mallum.assigned_quest_name)
      rewards = Mallums.roll_rewards(config)

      mallum
      |> PlayerMallum.changeset(%{
        state: "quest_complete",
        pending_rewards: rewards
      })
      |> Repo.update()
    end)
  end

  # ── Vase Fill ────────────────────────────────────────────────

  @doc "Instantly fill all filling vases and free their assigned mallums."
  def fill_vases(player_uid) do
    vases =
      from(v in PlayerVase,
        where: v.player_uid == ^player_uid and v.state == "filling"
      )
      |> Repo.all()

    Enum.map(vases, fn vase ->
      # Fill the vase
      vase
      |> PlayerVase.changeset(%{
        state: "full",
        current_water: vase.capacity,
        fill_start_time_utc: nil
      })
      |> Repo.update!()

      # Free any mallum assigned to this vase
      from(m in PlayerMallum,
        where:
          m.player_uid == ^player_uid and
            m.state == "fetching_water" and
            m.assigned_vase_id == ^vase.id
      )
      |> Repo.update_all(
        set: [state: "idle", assigned_vase_id: nil, start_time_utc: nil]
      )

      vase.id
    end)
  end

  # ── Plot Maturation ──────────────────────────────────────────

  @doc "Instantly mature all growing plots."
  def mature_plots(player_uid) do
    {count, _} =
      from(p in PlayerPlot,
        where: p.player_uid == ^player_uid and p.state == "growing"
      )
      |> Repo.update_all(set: [state: "mature"])

    {:ok, count}
  end

  # ── Flame Level ──────────────────────────────────────────────

  @doc "Set flame level directly."
  def set_flame_level(player_uid, level) when is_integer(level) and level > 0 do
    {count, results} =
      from(e in PlayerEconomy, where: e.player_uid == ^player_uid, select: e)
      |> Repo.update_all(set: [flame_level: level])

    if count == 1, do: {:ok, hd(results)}, else: {:error, :not_found}
  end

  # ── Clear Save ───────────────────────────────────────────────

  @doc "Delete all player data and re-initialize from scratch."
  def clear_save(player_uid) do
    Repo.transaction(fn ->
      from(p in PlayerPlot, where: p.player_uid == ^player_uid) |> Repo.delete_all()
      from(v in PlayerVase, where: v.player_uid == ^player_uid) |> Repo.delete_all()
      from(g in PlayerGarden, where: g.player_uid == ^player_uid) |> Repo.delete_all()
      from(m in PlayerMallum, where: m.player_uid == ^player_uid) |> Repo.delete_all()
      from(b in PlayerBird, where: b.player_uid == ^player_uid) |> Repo.delete_all()
      from(h in PlayerMallumHouse, where: h.player_uid == ^player_uid) |> Repo.delete_all()
      from(s in PlayerSeed, where: s.player_uid == ^player_uid) |> Repo.delete_all()
      from(i in PlayerItem, where: i.player_uid == ^player_uid) |> Repo.delete_all()
      from(e in PlayerEconomy, where: e.player_uid == ^player_uid) |> Repo.delete_all()
      from(s in PlayerState, where: s.player_uid == ^player_uid) |> Repo.delete_all()

      case Economy.init_economy(player_uid) do
        {:ok, economy} -> economy
        {:error, changeset} -> Repo.rollback(changeset)
      end
    end)
  end
end
