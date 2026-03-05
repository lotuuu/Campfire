defmodule CampFireWeb.QuestsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin
  alias CampFire.Admin.QuestConfig

  def mount(_params, _session, socket) do
    {:ok, assign(socket, active_tab: :quests, quests: Admin.list_quests())}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    quest = Admin.get_quest!(id)

    form =
      quest
      |> QuestConfig.changeset(%{})
      |> to_form()

    reward_pool_json = Jason.encode!(quest.reward_pool || [], pretty: true)

    {:noreply,
     assign(socket,
       editing: quest,
       form: form,
       reward_pool_json: reward_pool_json
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil, reward_pool_json: nil)}
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
    quest = socket.assigns.editing
    reward_json = Map.get(params, "reward_pool_json", "[]")

    case Jason.decode(reward_json) do
      {:ok, reward_pool} ->
        attrs = Map.put(params, "reward_pool", reward_pool) |> Map.delete("reward_pool_json")

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

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid reward pool JSON")}
    end
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
              <label class="block text-sm font-medium text-gray-700">Reward Pool (JSON)</label>
              <textarea
                name="quest_config[reward_pool_json]"
                rows="6"
                class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"
              >{@reward_pool_json}</textarea>
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
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Quest Name</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Duration (min)</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Flame Lvl</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Reward Rolls</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Rewards</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for quest <- @quests do %>
            <tr class="hover:bg-gray-50">
              <td class="px-4 py-3 font-medium">{quest.quest_name}</td>
              <td class="px-4 py-3">{quest.duration_minutes}</td>
              <td class="px-4 py-3">{quest.required_flame_level}</td>
              <td class="px-4 py-3">{quest.reward_rolls}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{length(quest.reward_pool || [])} items</td>
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
end
