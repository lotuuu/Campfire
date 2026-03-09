defmodule CampFire.Game.Birds do
  @moduledoc """
  Bird spawn and collection system.

  Birds spawn hourly with diminishing probability, land on free hex tiles,
  and carry seed rewards that players collect.
  """

  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerBird, PlayerState, GridValidation}
  alias CampFire.Economy
  alias CampFire.Economy.PlayerEconomy

  @spawn_base_chance 0.33
  @spawn_decay 0.5

  # ── Public API ──────────────────────────────────────────────

  @doc "List all birds for a player."
  def list_birds(player_uid) do
    from(b in PlayerBird, where: b.player_uid == ^player_uid, order_by: [asc: b.id])
    |> Repo.all()
  end

  @doc """
  Check and process bird spawns from last check hour to current hour.

  Walks each missed hour, rolling spawn chance with diminishing returns.
  Returns `{:ok, new_birds}` with newly spawned birds.
  """
  def check_spawns(player_uid) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    current_hour = truncate_to_hour(now)

    last_check = get_last_bird_check_hour(player_uid)

    # If no previous check, set to current hour (no retroactive spawns)
    last_check =
      case last_check do
        nil -> current_hour
        dt -> dt
      end

    # Walk hours from last_check to current_hour (exclusive of last_check, inclusive of current)
    hours = build_hour_range(last_check, current_hour)

    economy = Repo.get(PlayerEconomy, player_uid)

    if economy == nil do
      {:ok, []}
    else
    flame_level = economy.flame_level

    new_birds =
      Enum.reduce(hours, [], fn _hour, acc ->
        current_bird_count = count_birds(player_uid) + length(acc)
        spawn_chance = @spawn_base_chance * :math.pow(@spawn_decay, current_bird_count)

        if :rand.uniform() < spawn_chance do
          case try_spawn_bird(player_uid, flame_level, acc) do
            {:ok, bird} -> acc ++ [bird]
            :no_tile -> acc
            :no_seed -> acc
          end
        else
          acc
        end
      end)

    # Update last check hour
    set_last_bird_check_hour(player_uid, current_hour)

    {:ok, new_birds}
    end
  end

  @doc """
  Collect a bird: validate ownership, delete bird, grant seeds.

  Returns `{:ok, %{seed_name: ..., seed_count: ...}}` or `{:error, reason}`.
  """
  def collect_bird(player_uid, bird_id) do
    case Repo.get(PlayerBird, bird_id) do
      nil ->
        {:error, :bird_not_found}

      bird ->
        if bird.player_uid != player_uid do
          {:error, :not_owner}
        else
          Repo.transaction(fn ->
            Repo.delete!(bird)
            Economy.upsert_seed(player_uid, bird.seed_name, bird.seed_count)
            %{seed_name: bird.seed_name, seed_count: bird.seed_count}
          end)
        end
    end
  end

  @doc "Insert a bird record (used by check_spawns and tests)."
  def insert_bird(player_uid, gx, gy, seed_name, seed_count) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)

    %PlayerBird{}
    |> PlayerBird.changeset(%{
      player_uid: player_uid,
      grid_x: gx,
      grid_y: gy,
      seed_name: seed_name,
      seed_count: seed_count,
      spawned_at_utc: now
    })
    |> Repo.insert()
  end

  @doc "Force-spawn a bird (debug use). Returns {:ok, bird} or :no_tile / :no_seed."
  def try_spawn_bird_public(player_uid, flame_level) do
    try_spawn_bird(player_uid, flame_level, [])
  end

  # ── Private Helpers ────────────────────────────────────────

  defp count_birds(player_uid) do
    from(b in PlayerBird, where: b.player_uid == ^player_uid, select: count(b.id))
    |> Repo.one()
  end

  defp try_spawn_bird(player_uid, flame_level, pending_birds) do
    free_tiles = GridValidation.get_free_tiles(player_uid)

    # Also exclude tiles used by pending (not-yet-inserted) birds
    pending_coords = Enum.map(pending_birds, fn b -> {b.grid_x, b.grid_y} end)
    available = Enum.reject(free_tiles, fn tile -> tile in pending_coords end)

    if available == [] do
      :no_tile
    else
      {gx, gy} = Enum.random(available)

      case pick_seed(flame_level) do
        nil ->
          :no_seed

        {seed_name, seed_count} ->
          {:ok, bird} = do_insert_bird(player_uid, gx, gy, seed_name, seed_count)
          {:ok, bird}
      end
    end
  end

  defp pick_seed(flame_level) do
    seed_configs = CampFire.ConfigCache.get("seed_configs") || %{}

    eligible =
      seed_configs
      |> Enum.filter(fn {_name, config} ->
        tier = config["tier"] || Map.get(config, :tier, 0)
        tier <= flame_level
      end)

    if eligible == [] do
      nil
    else
      {seed_name, config} = Enum.random(eligible)
      tier = config["tier"] || Map.get(config, :tier, 0)
      base = max(1, flame_level - tier + 1)
      low = max(1, base - 1)
      high = base + 1
      seed_count = Enum.random(low..high)
      {seed_name, seed_count}
    end
  end

  defp do_insert_bird(player_uid, gx, gy, seed_name, seed_count) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)

    %PlayerBird{}
    |> PlayerBird.changeset(%{
      player_uid: player_uid,
      grid_x: gx,
      grid_y: gy,
      seed_name: seed_name,
      seed_count: seed_count,
      spawned_at_utc: now
    })
    |> Repo.insert()
  end

  defp truncate_to_hour(%DateTime{} = dt) do
    %{dt | minute: 0, second: 0, microsecond: {0, 0}}
  end

  defp build_hour_range(from_hour, to_hour) do
    from_unix = DateTime.to_unix(from_hour)
    to_unix = DateTime.to_unix(to_hour)

    if to_unix <= from_unix do
      []
    else
      # Start one hour after last check, up to and including current hour
      Stream.iterate(from_unix + 3600, &(&1 + 3600))
      |> Enum.take_while(&(&1 <= to_unix))
    end
  end

  defp get_last_bird_check_hour(player_uid) do
    case Repo.get(PlayerState, player_uid) do
      nil ->
        nil

      state ->
        case Map.get(state.data || %{}, "last_bird_check_hour_utc") do
          nil -> nil
          iso_str -> parse_datetime(iso_str)
        end
    end
  end

  defp set_last_bird_check_hour(player_uid, %DateTime{} = hour) do
    iso = DateTime.to_iso8601(hour)

    case Repo.get(PlayerState, player_uid) do
      nil ->
        %PlayerState{}
        |> PlayerState.changeset(%{
          player_uid: player_uid,
          data: %{"last_bird_check_hour_utc" => iso}
        })
        |> Repo.insert()

      state ->
        new_data = Map.put(state.data || %{}, "last_bird_check_hour_utc", iso)

        state
        |> PlayerState.changeset(%{data: new_data})
        |> Repo.update()
    end
  end

  defp parse_datetime(iso_str) when is_binary(iso_str) do
    case DateTime.from_iso8601(iso_str) do
      {:ok, dt, _offset} -> dt
      _ -> nil
    end
  end

  defp parse_datetime(_), do: nil
end
