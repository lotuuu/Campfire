defmodule CampFireWeb.VisitorsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok,
     assign(socket,
       active_tab: :visitors,
       sub_tab: :templates,
       visitors: Admin.list_visitors(),
       schedule: Admin.list_visitor_schedule(),
       editing: nil,
       form: nil,
       dialogue_pool_json: "[]",
       offer_pool_json: "[]",
       gift_pool_json: "[]",
       quest_pool_json: "[]"
     )}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    visitor = Admin.get_visitor!(id)

    form =
      %{}
      |> Map.put("visitor_id", visitor.visitor_id)
      |> Map.put("name", visitor.name)
      |> Map.put("type", visitor.type)
      |> Map.put("weight", visitor.weight)
      |> Map.put("flame_level_min", visitor.flame_level_min)
      |> Map.put("portrait_id", visitor.portrait_id)
      |> to_form(as: "visitor")

    {:noreply,
     assign(socket,
       sub_tab: :templates,
       editing: visitor,
       form: form,
       dialogue_pool_json: Jason.encode!(visitor.dialogue_pool || [], pretty: true),
       offer_pool_json: Jason.encode!(visitor.offer_pool || [], pretty: true),
       gift_pool_json: Jason.encode!(visitor.gift_pool || [], pretty: true),
       quest_pool_json: Jason.encode!(visitor.quest_pool || [], pretty: true)
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil)}
  end

  def handle_event("switch_tab", %{"tab" => tab}, socket) do
    {:noreply, assign(socket, sub_tab: String.to_existing_atom(tab), editing: nil, form: nil)}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/visitors/#{id}/edit")}
  end

  def handle_event("cancel", _params, socket) do
    {:noreply, push_patch(socket, to: "/admin/visitors")}
  end

  def handle_event("new", _params, socket) do
    attrs = %{
      visitor_id: "visitor_#{System.unique_integer([:positive])}",
      name: "New Visitor",
      type: "wanderer",
      weight: 1.0,
      flame_level_min: 1,
      dialogue_pool: [],
      offer_pool: [],
      gift_pool: [],
      quest_pool: []
    }

    case Admin.create_visitor(attrs) do
      {:ok, visitor} ->
        {:noreply,
         socket
         |> assign(visitors: Admin.list_visitors())
         |> push_patch(to: "/admin/visitors/#{visitor.id}/edit")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to create visitor")}
    end
  end

  def handle_event("save", %{"visitor" => params}, socket) do
    visitor = socket.assigns.editing

    with {:ok, dialogue} <- Jason.decode(Map.get(params, "dialogue_pool_json", "[]")),
         {:ok, offer} <- Jason.decode(Map.get(params, "offer_pool_json", "[]")),
         {:ok, gift} <- Jason.decode(Map.get(params, "gift_pool_json", "[]")),
         {:ok, quest} <- Jason.decode(Map.get(params, "quest_pool_json", "[]")) do
      attrs =
        params
        |> Map.drop(["dialogue_pool_json", "offer_pool_json", "gift_pool_json", "quest_pool_json"])
        |> Map.put("dialogue_pool", dialogue)
        |> Map.put("offer_pool", offer)
        |> Map.put("gift_pool", gift)
        |> Map.put("quest_pool", quest)

      case Admin.update_visitor(visitor, attrs) do
        {:ok, _} ->
          {:noreply,
           socket
           |> put_flash(:info, "Visitor updated")
           |> assign(visitors: Admin.list_visitors())
           |> push_patch(to: "/admin/visitors")}

        {:error, _changeset} ->
          {:noreply, put_flash(socket, :error, "Failed to update visitor")}
      end
    else
      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid JSON in one of the pool fields")}
    end
  end

  def handle_event("delete", %{"id" => id}, socket) do
    visitor = Admin.get_visitor!(id)

    case Admin.delete_visitor(visitor) do
      {:ok, _} ->
        {:noreply,
         socket
         |> put_flash(:info, "Visitor deleted")
         |> assign(visitors: Admin.list_visitors())
         |> push_patch(to: "/admin/visitors")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete visitor")}
    end
  end

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Visitors</h2>
        <%= if @sub_tab == :templates do %>
          <button phx-click="new" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
            + New Visitor
          </button>
        <% end %>
      </div>

      <div class="flex gap-2 mb-6">
        <button
          phx-click="switch_tab"
          phx-value-tab="templates"
          class={"px-4 py-2 rounded #{if @sub_tab == :templates, do: "bg-gray-900 text-white", else: "bg-gray-200"}"}
        >
          Templates
        </button>
        <button
          phx-click="switch_tab"
          phx-value-tab="schedule"
          class={"px-4 py-2 rounded #{if @sub_tab == :schedule, do: "bg-gray-900 text-white", else: "bg-gray-200"}"}
        >
          Schedule
        </button>
      </div>

      <%= if @sub_tab == :templates do %>
        <%= if @editing do %>
          <div class="bg-white border rounded-lg p-6 mb-6">
            <h3 class="text-lg font-semibold mb-4">Edit: {@editing.name}</h3>
            <.form for={@form} phx-submit="save" class="space-y-4">
              <div class="grid grid-cols-3 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700">Visitor ID</label>
                  <input type="text" name="visitor[visitor_id]" value={@form[:visitor_id].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Name</label>
                  <input type="text" name="visitor[name]" value={@form[:name].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Type</label>
                  <select name="visitor[type]" class="mt-1 block w-full border rounded px-3 py-2">
                    <option value="wanderer" selected={@form[:type].value == "wanderer"}>wanderer</option>
                    <option value="merchant" selected={@form[:type].value == "merchant"}>merchant</option>
                    <option value="quest_giver" selected={@form[:type].value == "quest_giver"}>quest_giver</option>
                  </select>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Weight</label>
                  <input type="number" step="0.1" name="visitor[weight]" value={@form[:weight].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Min Flame Level</label>
                  <input type="number" name="visitor[flame_level_min]" value={@form[:flame_level_min].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Portrait ID</label>
                  <input type="text" name="visitor[portrait_id]" value={@form[:portrait_id].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
              </div>

              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Dialogue Pool (JSON)</label>
                  <div id="dialogue-editor" phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
                    <div class="json-toolbar">
                      <button type="button" data-action="format">Format</button>
                      <button type="button" data-action="minify">Minify</button>
                    </div>
                    <textarea name="visitor[dialogue_pool_json]" rows="6" class="mt-1 block w-full border rounded px-3 py-2">{@dialogue_pool_json}</textarea>
                    <div class="json-error-msg"></div>
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Offer Pool (JSON)</label>
                  <div id="offer-editor" phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
                    <div class="json-toolbar">
                      <button type="button" data-action="format">Format</button>
                      <button type="button" data-action="minify">Minify</button>
                    </div>
                    <textarea name="visitor[offer_pool_json]" rows="6" class="mt-1 block w-full border rounded px-3 py-2">{@offer_pool_json}</textarea>
                    <div class="json-error-msg"></div>
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Gift Pool (JSON)</label>
                  <div id="gift-editor" phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
                    <div class="json-toolbar">
                      <button type="button" data-action="format">Format</button>
                      <button type="button" data-action="minify">Minify</button>
                    </div>
                    <textarea name="visitor[gift_pool_json]" rows="6" class="mt-1 block w-full border rounded px-3 py-2">{@gift_pool_json}</textarea>
                    <div class="json-error-msg"></div>
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Quest Pool (JSON)</label>
                  <div id="quest-pool-editor" phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
                    <div class="json-toolbar">
                      <button type="button" data-action="format">Format</button>
                      <button type="button" data-action="minify">Minify</button>
                    </div>
                    <textarea name="visitor[quest_pool_json]" rows="6" class="mt-1 block w-full border rounded px-3 py-2">{@quest_pool_json}</textarea>
                    <div class="json-error-msg"></div>
                  </div>
                </div>
              </div>

              <div class="flex gap-2">
                <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
                <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
                <button type="button" phx-click="delete" phx-value-id={@editing.id} data-confirm="Delete this visitor?" class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto">Delete</button>
              </div>
            </.form>
          </div>
        <% end %>

        <table class="w-full bg-white border rounded-lg">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Visitor ID</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Name</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Type</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Weight</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Min Flame</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
            </tr>
          </thead>
          <tbody class="divide-y">
            <%= for v <- @visitors do %>
              <tr class="hover:bg-gray-50">
                <td class="px-4 py-3 font-mono text-sm">{v.visitor_id}</td>
                <td class="px-4 py-3 font-medium">{v.name}</td>
                <td class="px-4 py-3">{v.type}</td>
                <td class="px-4 py-3">{v.weight}</td>
                <td class="px-4 py-3">{v.flame_level_min}</td>
                <td class="px-4 py-3">
                  <button phx-click="edit" phx-value-id={v.id} class="text-blue-600 hover:underline">Edit</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% end %>

      <%= if @sub_tab == :schedule do %>
        <table class="w-full bg-white border rounded-lg">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Date</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Visitor ID</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Visit #</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Weather</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Priority</th>
            </tr>
          </thead>
          <tbody class="divide-y">
            <%= for s <- @schedule do %>
              <tr class="hover:bg-gray-50">
                <td class="px-4 py-3">{s.date}</td>
                <td class="px-4 py-3 font-mono text-sm">{s.visitor_id}</td>
                <td class="px-4 py-3">{s.visit_number}</td>
                <td class="px-4 py-3">{s.weather_condition}</td>
                <td class="px-4 py-3">{s.priority}</td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% end %>
    </div>
    """
  end
end
