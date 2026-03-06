defmodule CampFireWeb.QuestsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin
  alias CampFire.Admin.QuestConfig

  def mount(_params, _session, socket) do
    seed_names = Admin.list_seeds() |> Enum.map(& &1.seed_name) |> Enum.sort()
    {:ok,
     socket
     |> assign(active_tab: :quests, quests: Admin.list_quests(), seed_names: seed_names)
     |> allow_upload(:icon,
       accept: ~w(.png),
       max_file_size: 512_000,
       max_entries: 1
     )}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    quest = Admin.get_quest!(id)

    form =
      quest
      |> QuestConfig.changeset(%{})
      |> to_form()

    rewards = reward_pool_to_editable(quest.reward_pool)

    {:noreply,
     assign(socket,
       editing: quest,
       form: form,
       rewards: rewards
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil, rewards: [])}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/quests/#{id}/edit")}
  end

  def handle_event("cancel", _params, socket) do
    {:noreply, push_patch(socket, to: "/admin/quests")}
  end

  def handle_event("new", _params, socket) do
    case Admin.create_quest(%{
           quest_name: "NewQuest_#{System.unique_integer([:positive])}",
           duration_minutes: 60,
           required_flame_level: 1,
           reward_rolls: 1,
           reward_pool: []
         }) do
      {:ok, quest} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> assign(quests: Admin.list_quests())
         |> push_patch(to: "/admin/quests/#{quest.id}/edit")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to create quest")}
    end
  end

  def handle_event("save", %{"quest_config" => params}, socket) do
    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      quest = socket.assigns.editing
      key = "quests/#{String.downcase(quest.quest_name)}"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)

    quest = socket.assigns.editing
    rewards = socket.assigns.rewards

    reward_pool =
      Enum.map(rewards, fn r ->
        %{
          "seed" => r.seed,
          "weight" => parse_number(r.weight),
          "minCount" => parse_int(r.min_count),
          "maxCount" => parse_int(r.max_count)
        }
      end)

    attrs = Map.put(params, "reward_pool", reward_pool)

    case Admin.update_quest(quest, attrs) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Quest updated")
         |> assign(quests: Admin.list_quests())
         |> push_patch(to: "/admin/quests")}

      {:error, changeset} ->
        {:noreply, assign(socket, form: to_form(changeset))}
    end
  end

  def handle_event("add_reward", _params, socket) do
    rewards = socket.assigns.rewards ++ [%{seed: "", weight: "1", min_count: "1", max_count: "1"}]
    {:noreply, assign(socket, rewards: rewards)}
  end

  def handle_event("remove_reward", %{"index" => index}, socket) do
    idx = String.to_integer(index)
    rewards = List.delete_at(socket.assigns.rewards, idx)
    {:noreply, assign(socket, rewards: rewards)}
  end

  def handle_event("update_reward", %{"index" => index, "field" => field, "value" => value}, socket) do
    idx = String.to_integer(index)
    field_atom = String.to_existing_atom(field)
    rewards = List.update_at(socket.assigns.rewards, idx, &Map.put(&1, field_atom, value))
    {:noreply, assign(socket, rewards: rewards)}
  end

  def handle_event("delete", %{"id" => id}, socket) do
    quest = Admin.get_quest!(id)

    case Admin.delete_quest(quest) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Quest deleted")
         |> assign(quests: Admin.list_quests())
         |> push_patch(to: "/admin/quests")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete quest")}
    end
  end

  defp parse_number(val) when is_binary(val) do
    case Float.parse(val) do
      {f, _} -> f
      :error -> 0
    end
  end

  defp parse_number(val) when is_number(val), do: val
  defp parse_number(_), do: 0

  defp parse_int(val) when is_binary(val) do
    case Integer.parse(val) do
      {i, _} -> i
      :error -> 0
    end
  end

  defp parse_int(val) when is_integer(val), do: val
  defp parse_int(_), do: 0

  defp reward_pool_to_editable(reward_pool) do
    Enum.map(reward_pool || [], fn r ->
      %{
        seed: to_string(r["seed"] || ""),
        weight: to_string(r["weight"] || 1),
        min_count: to_string(r["minCount"] || 1),
        max_count: to_string(r["maxCount"] || 1)
      }
    end)
  end

  defp total_weight(rewards) do
    Enum.reduce(rewards, 0.0, fn r, acc -> acc + parse_number(r.weight) end)
  end

  defp probability_pct(weight, total) when total > 0, do: Float.round(parse_number(weight) / total * 100, 1)
  defp probability_pct(_, _), do: 0.0

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Quests</h2>
        <button phx-click="new" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New Quest
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white border rounded-lg p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.quest_name}</h3>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center overflow-hidden">
              <img
                src={CampFire.Sprites.sprite_url("quests/#{String.downcase(@editing.quest_name)}")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
          <.form for={@form} phx-submit="save" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Quest Name</label>
                <input
                  type="text"
                  name="quest_config[quest_name]"
                  value={@form[:quest_name].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Duration (minutes)</label>
                <input
                  type="number"
                  name="quest_config[duration_minutes]"
                  value={@form[:duration_minutes].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Required Flame Level</label>
                <input
                  type="number"
                  name="quest_config[required_flame_level]"
                  value={@form[:required_flame_level].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Reward Rolls</label>
                <input
                  type="number"
                  name="quest_config[reward_rolls]"
                  value={@form[:reward_rolls].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
            </div>

            <div>
              <div class="flex justify-between items-center mb-2">
                <label class="block text-sm font-medium text-gray-700">Reward Pool</label>
                <button
                  type="button"
                  phx-click="add_reward"
                  class="text-sm bg-green-100 text-green-700 px-3 py-1 rounded hover:bg-green-200"
                >
                  + Add Reward
                </button>
              </div>

              <%= if @rewards == [] do %>
                <p class="text-sm text-gray-400 italic py-4 text-center">No rewards yet. Click "+ Add Reward" to add one.</p>
              <% else %>
                <div class="border rounded-lg overflow-hidden">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-50">
                      <tr>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Seed Name</th>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 w-24">Weight</th>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 w-20">Min</th>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 w-20">Max</th>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 w-28">Probability</th>
                        <th class="px-3 py-2 w-10"></th>
                      </tr>
                    </thead>
                    <tbody class="divide-y">
                      <% total = total_weight(@rewards) %>
                      <%= for {reward, idx} <- Enum.with_index(@rewards) do %>
                        <% pct = probability_pct(reward.weight, total) %>
                        <tr class="hover:bg-gray-50">
                          <td class="px-3 py-2">
                            <select
                              id={"reward-#{idx}-seed"}
                              phx-hook="RewardInput"
                              data-index={idx}
                              data-field="seed"
                              class="w-full border rounded px-2 py-1 text-sm"
                            >
                              <option value="">-- select seed --</option>
                              <%= for name <- @seed_names do %>
                                <option value={name} selected={name == reward.seed}>{name}</option>
                              <% end %>
                            </select>
                          </td>
                          <td class="px-3 py-2">
                            <input
                              id={"reward-#{idx}-weight"}
                              type="number"
                              value={reward.weight}
                              phx-hook="RewardInput"
                              data-index={idx}
                              data-field="weight"
                              step="0.1"
                              min="0"
                              class="w-full border rounded px-2 py-1 text-sm"
                            />
                          </td>
                          <td class="px-3 py-2">
                            <input
                              id={"reward-#{idx}-min_count"}
                              type="number"
                              value={reward.min_count}
                              phx-hook="RewardInput"
                              data-index={idx}
                              data-field="min_count"
                              min="0"
                              class="w-full border rounded px-2 py-1 text-sm"
                            />
                          </td>
                          <td class="px-3 py-2">
                            <input
                              id={"reward-#{idx}-max_count"}
                              type="number"
                              value={reward.max_count}
                              phx-hook="RewardInput"
                              data-index={idx}
                              data-field="max_count"
                              min="0"
                              class="w-full border rounded px-2 py-1 text-sm"
                            />
                          </td>
                          <td class="px-3 py-2">
                            <div class="flex items-center gap-2">
                              <div class="flex-1 bg-gray-200 rounded-full h-2">
                                <div class="bg-blue-500 rounded-full h-2" style={"width: #{pct}%"}></div>
                              </div>
                              <span class="text-xs text-gray-500 w-12 text-right">{pct}%</span>
                            </div>
                          </td>
                          <td class="px-3 py-2">
                            <button
                              type="button"
                              phx-click="remove_reward"
                              phx-value-index={idx}
                              class="text-red-400 hover:text-red-600 text-lg leading-none"
                              title="Remove"
                            >
                              &times;
                            </button>
                          </td>
                        </tr>
                      <% end %>
                    </tbody>
                  </table>
                </div>
              <% end %>
            </div>

            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
              <button
                type="button"
                phx-click="delete"
                phx-value-id={@editing.id}
                data-confirm="Delete this quest?"
                class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto"
              >
                Delete
              </button>
            </div>
          </.form>
        </div>
      <% end %>

      <table class="w-full bg-white border rounded-lg">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-4 py-3 w-12"></th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Quest Name</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Duration</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Flame Lvl</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Rolls</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Reward Pool</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for quest <- @quests do %>
            <% pool = quest.reward_pool || [] %>
            <% tw = Enum.reduce(pool, 0.0, fn r, acc -> acc + (r["weight"] || 0) end) %>
            <tr class="hover:bg-gray-50 align-top">
              <td class="px-4 py-3">
                <img src={CampFire.Sprites.sprite_url("quests/#{String.downcase(quest.quest_name)}")}
                  class="w-8 h-8 object-contain" onerror="this.style.display='none'" />
              </td>
              <td class="px-4 py-3 font-medium">{quest.quest_name}</td>
              <td class="px-4 py-3">{format_duration(quest.duration_minutes)}</td>
              <td class="px-4 py-3">{quest.required_flame_level}</td>
              <td class="px-4 py-3">{quest.reward_rolls}</td>
              <td class="px-4 py-3">
                <%= if pool == [] do %>
                  <span class="text-gray-400 text-sm italic">none</span>
                <% else %>
                  <div class="space-y-1">
                    <%= for r <- pool do %>
                      <% pct = if tw > 0, do: Float.round((r["weight"] || 0) / tw * 100, 1), else: 0.0 %>
                      <div class="flex items-center gap-2 text-sm">
                        <span class="font-medium w-24 truncate" title={r["seed"]}>{r["seed"]}</span>
                        <div class="flex-1 bg-gray-200 rounded-full h-1.5 max-w-[80px]">
                          <div class="bg-blue-500 rounded-full h-1.5" style={"width: #{pct}%"}></div>
                        </div>
                        <span class="text-gray-500 text-xs w-10 text-right">{pct}%</span>
                        <span class="text-gray-400 text-xs">({r["minCount"]}-{r["maxCount"]})</span>
                      </div>
                    <% end %>
                  </div>
                <% end %>
              </td>
              <td class="px-4 py-3">
                <button phx-click="edit" phx-value-id={quest.id} class="text-blue-600 hover:underline">Edit</button>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp format_duration(minutes) when minutes < 60, do: "#{minutes}m"
  defp format_duration(minutes) when rem(minutes, 60) == 0, do: "#{div(minutes, 60)}h"
  defp format_duration(minutes), do: "#{div(minutes, 60)}h #{rem(minutes, 60)}m"
end
