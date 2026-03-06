defmodule CampFire.Game.Mallums do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.PlayerMallum
  alias CampFire.Economy

  # --- Config Helpers ---

  defp get_quest_config(quest_name) do
    case CampFire.ConfigCache.get("quest_configs") do
      nil ->
        nil

      quest_map ->
        case Map.get(quest_map, quest_name) do
          nil -> nil
          cached -> normalize_quest_config(cached)
        end
    end
  end

  defp get_all_quest_configs do
    case CampFire.ConfigCache.get("quest_configs") do
      nil -> %{}
      quest_map -> Map.new(quest_map, fn {k, v} -> {k, normalize_quest_config(v)} end)
    end
  end

  defp normalize_quest_config(cached) do
    rewards =
      (cached[:reward_pool] || cached["reward_pool"] || [])
      |> Enum.map(fn entry ->
        %{
          seed: entry["seed_name"] || entry["seed"] || entry[:seed_name] || entry[:seed],
          weight: entry["weight"] || entry[:weight],
          min: entry["min"] || entry[:min],
          max: entry["max"] || entry[:max]
        }
      end)

    %{
      duration_minutes: cached[:duration_minutes] || cached["duration_minutes"],
      flame_level: cached[:required_flame_level] || cached["required_flame_level"],
      reward_rolls: cached[:reward_rolls] || cached["reward_rolls"],
      rewards: rewards
    }
  end

  # --- Queries ---

  def list_mallums(player_uid) do
    from(m in PlayerMallum, where: m.player_uid == ^player_uid) |> Repo.all()
  end

  def get_quest_configs, do: get_all_quest_configs()

  # --- Quests ---

  def send_on_quest(player_uid, quest_name) do
    case get_quest_config(quest_name) do
      nil ->
        {:error, :unknown_quest}

      config ->
        economy = Economy.get_economy(player_uid)

        cond do
          economy == nil ->
            {:error, :player_not_found}

          economy.flame_level < config.flame_level ->
            {:error, :insufficient_flame_level}

          true ->
            case claim_idle_mallum(player_uid, quest_name) do
              {:error, reason} -> {:error, reason}
              {:ok, mallum} -> {:ok, mallum}
            end
        end
    end
  end

  def check_quest(player_uid, mallum_id) do
    with %PlayerMallum{} = mallum <- Repo.get(PlayerMallum, mallum_id),
         true <- mallum.player_uid == player_uid || {:error, :not_owned},
         true <- mallum.state == "on_quest" || {:error, :not_on_quest} do
      config = get_quest_config(mallum.assigned_quest_name)
      now = DateTime.utc_now() |> DateTime.truncate(:second)
      elapsed_minutes = DateTime.diff(now, mallum.start_time_utc, :second) / 60.0

      if elapsed_minutes >= config.duration_minutes do
        rewards = roll_rewards(config)

        mallum
        |> PlayerMallum.changeset(%{
          state: "quest_complete",
          pending_rewards: rewards
        })
        |> Repo.update()
      else
        {:ok, mallum}
      end
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  def collect_rewards(player_uid, mallum_id) do
    with %PlayerMallum{} = mallum <- Repo.get(PlayerMallum, mallum_id),
         true <- mallum.player_uid == player_uid || {:error, :not_owned},
         true <- mallum.state == "quest_complete" || {:error, :not_quest_complete},
         true <- mallum.pending_rewards != [] || {:error, :no_rewards} do
      Enum.each(mallum.pending_rewards, fn reward ->
        seed_name = reward["seed_name"]
        count = reward["count"]
        Economy.upsert_seed(player_uid, seed_name, count)
      end)

      rewards = mallum.pending_rewards

      mallum
      |> PlayerMallum.changeset(%{
        state: "idle",
        assigned_quest_name: nil,
        start_time_utc: nil,
        pending_rewards: []
      })
      |> Repo.update()

      {:ok, %{rewards: rewards}}
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  def speed_up_quest(player_uid, mallum_id) do
    with %PlayerMallum{} = mallum <- Repo.get(PlayerMallum, mallum_id),
         true <- mallum.player_uid == player_uid || {:error, :not_owned},
         true <- mallum.state == "on_quest" || {:error, :not_on_quest} do
      case Economy.spend_item(player_uid, "Speed_Potion", 1) do
        {:error, reason} ->
          {:error, reason}

        _ ->
          config = get_quest_config(mallum.assigned_quest_name)
          rewards = roll_rewards(config)

          mallum
          |> PlayerMallum.changeset(%{
            state: "quest_complete",
            pending_rewards: rewards
          })
          |> Repo.update()
      end
    else
      nil -> {:error, :not_found}
      {:error, _} = err -> err
    end
  end

  # --- Create ---

  def create_mallum(player_uid) do
    %PlayerMallum{}
    |> PlayerMallum.changeset(%{
      player_uid: player_uid,
      state: "idle"
    })
    |> Repo.insert()
  end

  # --- Reward Rolling ---

  def roll_rewards(config) do
    if config.rewards == [] do
      []
    else
      total_weight = Enum.reduce(config.rewards, 0, fn r, acc -> acc + r.weight end)

      Enum.map(1..config.reward_rolls, fn _ ->
        roll = :rand.uniform(total_weight)
        selected = pick_by_weight(config.rewards, roll, 0)
        count = Enum.random(selected.min..selected.max)
        %{"seed_name" => selected.seed, "count" => count}
      end)
    end
  end

  # --- Private Helpers ---

  defp claim_idle_mallum(player_uid, quest_name) do
    case Repo.one(
           from(m in PlayerMallum,
             where: m.player_uid == ^player_uid and m.state == "idle",
             limit: 1
           )
         ) do
      nil ->
        {:error, :no_idle_mallum}

      mallum ->
        now = DateTime.utc_now() |> DateTime.truncate(:second)

        mallum
        |> PlayerMallum.changeset(%{
          state: "on_quest",
          assigned_quest_name: quest_name,
          start_time_utc: now
        })
        |> Repo.update()
    end
  end

  defp pick_by_weight([reward | rest], roll, cumulative) do
    new_cumulative = cumulative + reward.weight

    if roll <= new_cumulative do
      reward
    else
      pick_by_weight(rest, roll, new_cumulative)
    end
  end

  defp pick_by_weight([], _roll, _cumulative) do
    # Fallback — should not happen if weights are correct
    raise "Weight calculation error: roll exceeded total weight"
  end
end
