defmodule CampFire.Game.Mallums do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.PlayerMallum
  alias CampFire.Economy

  @quest_configs %{
    "SwampForage" => %{
      duration_minutes: 30,
      flame_level: 1,
      reward_rolls: 1,
      rewards: [
        %{seed: "Sprouts", weight: 40, min: 1, max: 2},
        %{seed: "Cress", weight: 30, min: 1, max: 1},
        %{seed: "Basil", weight: 20, min: 1, max: 1},
        %{seed: "Mint", weight: 10, min: 1, max: 1}
      ]
    },
    "MeadowExpedition" => %{
      duration_minutes: 60,
      flame_level: 2,
      reward_rolls: 1,
      rewards: [
        %{seed: "Basil", weight: 30, min: 1, max: 2},
        %{seed: "Chamomile", weight: 25, min: 1, max: 1},
        %{seed: "Mint", weight: 25, min: 1, max: 1},
        %{seed: "Marigold", weight: 15, min: 1, max: 1},
        %{seed: "Sprouts", weight: 5, min: 1, max: 2}
      ]
    },
    "DeepWoodsTrek" => %{
      duration_minutes: 120,
      flame_level: 3,
      reward_rolls: 2,
      rewards: [
        %{seed: "Lavender", weight: 25, min: 1, max: 2},
        %{seed: "Rosemary", weight: 25, min: 1, max: 1},
        %{seed: "Chamomile", weight: 20, min: 1, max: 2},
        %{seed: "Poppy", weight: 20, min: 1, max: 1},
        %{seed: "Basil", weight: 10, min: 1, max: 2}
      ]
    },
    "MountainPass" => %{
      duration_minutes: 180,
      flame_level: 4,
      reward_rolls: 2,
      rewards: [
        %{seed: "Dahlia", weight: 25, min: 1, max: 1},
        %{seed: "Poppy", weight: 25, min: 1, max: 2},
        %{seed: "Lavender", weight: 20, min: 1, max: 2},
        %{seed: "Rosemary", weight: 15, min: 1, max: 1},
        %{seed: "Marigold", weight: 15, min: 1, max: 2}
      ]
    },
    "CrystalCavern" => %{
      duration_minutes: 240,
      flame_level: 5,
      reward_rolls: 2,
      rewards: [
        %{seed: "Jasmine", weight: 25, min: 1, max: 1},
        %{seed: "Dahlia", weight: 25, min: 1, max: 2},
        %{seed: "Moonflower", weight: 15, min: 1, max: 1},
        %{seed: "Lavender", weight: 20, min: 1, max: 2},
        %{seed: "Poppy", weight: 15, min: 1, max: 1}
      ]
    },
    "StarlitMarsh" => %{
      duration_minutes: 300,
      flame_level: 6,
      reward_rolls: 3,
      rewards: [
        %{seed: "Moonflower", weight: 25, min: 1, max: 1},
        %{seed: "Jasmine", weight: 25, min: 1, max: 2},
        %{seed: "Snowdrop", weight: 15, min: 1, max: 1},
        %{seed: "Dahlia", weight: 20, min: 1, max: 1},
        %{seed: "Rosemary", weight: 15, min: 1, max: 2}
      ]
    },
    "FrostpeakSummit" => %{
      duration_minutes: 360,
      flame_level: 7,
      reward_rolls: 3,
      rewards: [
        %{seed: "Snowdrop", weight: 30, min: 1, max: 2},
        %{seed: "Moonflower", weight: 25, min: 1, max: 1},
        %{seed: "Jasmine", weight: 20, min: 1, max: 2},
        %{seed: "Dahlia", weight: 15, min: 1, max: 1},
        %{seed: "Lavender", weight: 10, min: 1, max: 2}
      ]
    },
    "AncientGrove" => %{
      duration_minutes: 480,
      flame_level: 8,
      reward_rolls: 3,
      rewards: [
        %{seed: "Moonflower", weight: 25, min: 1, max: 2},
        %{seed: "Snowdrop", weight: 25, min: 1, max: 2},
        %{seed: "Jasmine", weight: 20, min: 1, max: 2},
        %{seed: "Dahlia", weight: 15, min: 1, max: 2},
        %{seed: "Rosemary", weight: 15, min: 1, max: 2}
      ]
    }
  }

  # --- Queries ---

  def list_mallums(player_uid) do
    from(m in PlayerMallum, where: m.player_uid == ^player_uid) |> Repo.all()
  end

  def get_quest_configs, do: @quest_configs

  # --- Quests ---

  def send_on_quest(player_uid, quest_name) do
    case Map.get(@quest_configs, quest_name) do
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
    mallum = Repo.get!(PlayerMallum, mallum_id)

    cond do
      mallum.player_uid != player_uid ->
        {:error, :not_owned}

      mallum.state != "on_quest" ->
        {:error, :not_on_quest}

      true ->
        config = Map.fetch!(@quest_configs, mallum.assigned_quest_name)
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
    end
  end

  def collect_rewards(player_uid, mallum_id) do
    mallum = Repo.get!(PlayerMallum, mallum_id)

    cond do
      mallum.player_uid != player_uid ->
        {:error, :not_owned}

      mallum.state != "quest_complete" ->
        {:error, :not_quest_complete}

      mallum.pending_rewards == [] ->
        {:error, :no_rewards}

      true ->
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
    end
  end

  def speed_up_quest(player_uid, mallum_id) do
    mallum = Repo.get!(PlayerMallum, mallum_id)

    cond do
      mallum.player_uid != player_uid ->
        {:error, :not_owned}

      mallum.state != "on_quest" ->
        {:error, :not_on_quest}

      true ->
        case Economy.spend_item(player_uid, "Speed_Potion", 1) do
          {:error, reason} ->
            {:error, reason}

          _ ->
            config = Map.fetch!(@quest_configs, mallum.assigned_quest_name)
            rewards = roll_rewards(config)

            mallum
            |> PlayerMallum.changeset(%{
              state: "quest_complete",
              pending_rewards: rewards
            })
            |> Repo.update()
        end
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
    total_weight = Enum.reduce(config.rewards, 0, fn r, acc -> acc + r.weight end)

    Enum.map(1..config.reward_rolls, fn _ ->
      roll = :rand.uniform(total_weight)
      selected = pick_by_weight(config.rewards, roll, 0)
      count = Enum.random(selected.min..selected.max)
      %{"seed_name" => selected.seed, "count" => count}
    end)
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
