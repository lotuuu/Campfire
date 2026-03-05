defmodule CampFire.Visitors do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule, VisitorQuest}
  alias CampFire.Villages

  def get_tonight_visitor(uid) do
    today = Date.utc_today()
    visit_number = increment_visit_count(uid, today)
    flame_level = get_flame_level(uid)

    with nil <- get_date_visitor(today),
         nil <- get_milestone_visitor(visit_number),
         nil <- get_weather_visitor(),
         nil <- get_quest_return(uid, today),
         nil <- get_random_visitor(flame_level) do
      %{visitor_type: nil, message: "No visitors available tonight"}
    else
      visitor -> visitor
    end
  end

  def accept_quest(uid, params) do
    return_date = Date.utc_today() |> Date.add(params["return_days"])

    %VisitorQuest{}
    |> VisitorQuest.changeset(%{
      player_uid: uid,
      visitor_id: params["visitor_id"],
      request_item: params["request_item"],
      request_count: params["request_count"],
      return_date_utc: return_date,
      reward: params["reward"] || %{},
      return_dialogue: params["return_dialogue"] || []
    })
    |> Repo.insert()
    |> case do
      {:ok, quest} ->
        {:ok, %{quest_id: quest.id, return_date: Date.to_iso8601(quest.return_date_utc)}}
      {:error, _changeset} ->
        {:error, "Failed to accept quest"}
    end
  end

  def complete_quest(uid, quest_id) do
    today = Date.utc_today()

    query =
      from(q in VisitorQuest,
        where: q.id == ^quest_id and q.player_uid == ^uid and q.return_date_utc <= ^today
      )

    case Repo.one(query) do
      nil -> {:error, :not_found}
      quest ->
        Repo.delete!(quest)
        {:ok, quest.reward}
    end
  end

  # --- Private helpers ---

  defp increment_visit_count(uid, today) do
    Repo.query!(
      """
      INSERT INTO player_visit_counts (player_uid, count, last_visit_date)
      VALUES ($1, 1, $2)
      ON CONFLICT (player_uid) DO UPDATE
        SET count = CASE
          WHEN player_visit_counts.last_visit_date < $2
            THEN player_visit_counts.count + 1
          ELSE player_visit_counts.count
        END,
        last_visit_date = $2
      RETURNING count
      """,
      [uid, today]
    ).rows
    |> hd()
    |> hd()
  end

  defp get_flame_level(uid) do
    case Villages.get_snapshot(uid) do
      %{snapshot: %{"flameLevel" => level}} -> level
      _ -> 1
    end
  end

  defp get_date_visitor(today) do
    query =
      from(vs in VisitorSchedule,
        join: vt in VisitorTemplate, on: vt.visitor_id == vs.visitor_id,
        where: vs.date == ^today,
        order_by: [desc: vs.priority],
        limit: 1,
        select: vt
      )

    case Repo.one(query) do
      nil -> nil
      template -> build_visitor_payload(template)
    end
  end

  defp get_milestone_visitor(visit_number) do
    query =
      from(vs in VisitorSchedule,
        join: vt in VisitorTemplate, on: vt.visitor_id == vs.visitor_id,
        where: vs.visit_number == ^visit_number,
        order_by: [desc: vs.priority],
        limit: 1,
        select: vt
      )

    case Repo.one(query) do
      nil -> nil
      template -> build_visitor_payload(template)
    end
  end

  defp get_weather_visitor, do: nil

  defp get_quest_return(uid, today) do
    query =
      from(q in VisitorQuest,
        where: q.player_uid == ^uid and q.return_date_utc <= ^today,
        order_by: [asc: q.return_date_utc],
        limit: 1
      )

    case Repo.one(query) do
      nil ->
        nil

      quest ->
        template = Repo.one(from vt in VisitorTemplate, where: vt.visitor_id == ^quest.visitor_id)

        base =
          if template do
            build_visitor_payload(template)
          else
            %{
              visitor_type: "quester",
              visitor_id: quest.visitor_id,
              name: quest.visitor_id,
              portrait_id: nil,
              dialogue: []
            }
          end

        Map.merge(base, %{
          visitor_type: "quester",
          dialogue: quest.return_dialogue || [],
          quest: %{
            quest_id: quest.id,
            is_return: true,
            reward: quest.reward
          }
        })
    end
  end

  defp get_random_visitor(flame_level) do
    templates =
      from(vt in VisitorTemplate, where: vt.flame_level_min <= ^flame_level)
      |> Repo.all()

    case templates do
      [] -> nil
      list -> list |> weighted_random() |> build_visitor_payload()
    end
  end

  defp weighted_random(templates) do
    total = Enum.reduce(templates, 0.0, fn t, acc -> acc + t.weight end)
    roll = :rand.uniform() * total

    Enum.reduce_while(templates, roll, fn t, remaining ->
      remaining = remaining - t.weight
      if remaining <= 0, do: {:halt, t}, else: {:cont, remaining}
    end)
    |> case do
      %VisitorTemplate{} = t -> t
      _ -> List.last(templates)
    end
  end

  defp build_visitor_payload(template) do
    base = %{
      visitor_type: template.type,
      visitor_id: template.visitor_id,
      name: template.name,
      portrait_id: template.portrait_id,
      dialogue: roll_dialogue(template.dialogue_pool)
    }

    case template.type do
      "merchant" -> Map.put(base, :offers, template.offer_pool)
      "gifter" -> Map.put(base, :gift, roll_gift(template.gift_pool))
      "quester" -> Map.put(base, :quest, roll_quest(template.quest_pool))
      _ -> base
    end
  end

  defp roll_dialogue(pool) when is_list(pool) and length(pool) > 0 do
    pick = Enum.random(pool)
    if is_list(pick), do: pick, else: [pick]
  end
  defp roll_dialogue(_), do: []

  defp roll_gift(pool) when is_list(pool) and length(pool) > 0, do: Enum.random(pool)
  defp roll_gift(_), do: nil

  defp roll_quest(pool) when is_list(pool) and length(pool) > 0, do: Enum.random(pool)
  defp roll_quest(_), do: nil
end
