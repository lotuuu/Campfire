defmodule CampFireWeb.PlayersLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok,
     assign(socket,
       active_tab: :players,
       search_query: "",
       search_results: Admin.search_players(nil),
       detail: nil,
       editing_economy: false,
       economy_form: nil
     )}
  end

  def handle_params(%{"uid" => uid}, _uri, socket) do
    case Admin.get_player_detail(uid) do
      nil ->
        {:noreply,
         socket
         |> put_flash(:error, "Player not found")
         |> push_patch(to: "/admin/players")}

      detail ->
        {:noreply, assign(socket, detail: detail, editing_economy: false, economy_form: nil)}
    end
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, detail: nil, editing_economy: false, economy_form: nil)}
  end

  def handle_event("search", %{"query" => query}, socket) do
    results = Admin.search_players(query)
    {:noreply, assign(socket, search_query: query, search_results: results)}
  end

  def handle_event("view", %{"uid" => uid}, socket) do
    {:noreply, push_patch(socket, to: "/admin/players/#{uid}")}
  end

  def handle_event("back", _params, socket) do
    {:noreply, push_patch(socket, to: "/admin/players")}
  end

  def handle_event("edit_economy", _params, socket) do
    econ = socket.assigns.detail.economy

    form =
      %{
        "mana" => (econ && econ.mana) || 0,
        "gems" => (econ && econ.gems) || 0,
        "flame_level" => (econ && econ.flame_level) || 1
      }
      |> to_form(as: "economy")

    {:noreply, assign(socket, editing_economy: true, economy_form: form)}
  end

  def handle_event("cancel_economy", _params, socket) do
    {:noreply, assign(socket, editing_economy: false, economy_form: nil)}
  end

  def handle_event("save_economy", %{"economy" => params}, socket) do
    uid = socket.assigns.detail.player.uid

    attrs = %{
      mana: parse_float(params["mana"]),
      gems: parse_int(params["gems"]),
      flame_level: parse_int(params["flame_level"])
    }

    case Admin.update_economy(uid, attrs) do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)

        {:noreply,
         socket
         |> put_flash(:info, "Economy updated")
         |> assign(detail: detail, editing_economy: false, economy_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to update economy")}
    end
  end

  defp parse_float(val) when is_binary(val) do
    case Float.parse(val) do
      {f, _} -> f
      :error -> 0.0
    end
  end

  defp parse_float(val), do: val

  defp parse_int(val) when is_binary(val) do
    case Integer.parse(val) do
      {i, _} -> i
      :error -> 0
    end
  end

  defp parse_int(val), do: val

  def render(assigns) do
    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-6">Players</h2>

      <%= if @detail do %>
        {render_detail(assigns)}
      <% else %>
        {render_search(assigns)}
      <% end %>
    </div>
    """
  end

  defp render_search(assigns) do
    ~H"""
    <div>
      <form phx-change="search" class="mb-6">
        <input
          type="text"
          name="query"
          value={@search_query}
          placeholder="Search by name, friend code, or UID..."
          phx-debounce="300"
          class="w-full border rounded px-4 py-2 text-lg"
          autofocus
        />
      </form>

      <%= if @search_results != [] do %>
        <table class="w-full bg-white border rounded-lg">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Display Name</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Friend Code</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">UID</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Last Online</th>
              <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
            </tr>
          </thead>
          <tbody class="divide-y">
            <%= for player <- @search_results do %>
              <tr class="hover:bg-gray-50">
                <td class="px-4 py-3 font-medium">{player.display_name}</td>
                <td class="px-4 py-3 font-mono text-sm">{player.friend_code}</td>
                <td class="px-4 py-3 font-mono text-xs text-gray-500">{String.slice(player.uid, 0, 12)}...</td>
                <td class="px-4 py-3 text-sm text-gray-500">{format_datetime(player.updated_at)}</td>
                <td class="px-4 py-3">
                  <button phx-click="view" phx-value-uid={player.uid} class="text-blue-600 hover:underline">View</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% else %>
        <%= if @search_query != "" do %>
          <p class="text-gray-500">No players found.</p>
        <% end %>
      <% end %>
    </div>
    """
  end

  defp render_detail(assigns) do
    ~H"""
    <div>
      <button phx-click="back" class="text-blue-600 hover:underline mb-4">&larr; Back to search</button>

      <div class="grid grid-cols-2 gap-6">
        <%!-- Player info --%>
        <div class="bg-white border rounded-lg p-4">
          <h3 class="font-semibold text-lg mb-3">Player</h3>
          <dl class="space-y-1 text-sm">
            <div class="flex"><dt class="w-32 text-gray-500">Name</dt><dd>{@detail.player.display_name}</dd></div>
            <div class="flex"><dt class="w-32 text-gray-500">UID</dt><dd class="font-mono text-xs">{@detail.player.uid}</dd></div>
            <div class="flex"><dt class="w-32 text-gray-500">Friend Code</dt><dd class="font-mono">{@detail.player.friend_code}</dd></div>
          </dl>
        </div>

        <%!-- Economy --%>
        <div class="bg-white border rounded-lg p-4">
          <div class="flex justify-between items-center mb-3">
            <h3 class="font-semibold text-lg">Economy</h3>
            <%= if not @editing_economy do %>
              <button phx-click="edit_economy" class="text-blue-600 hover:underline text-sm">Edit</button>
            <% end %>
          </div>

          <%= if @editing_economy do %>
            <.form for={@economy_form} phx-submit="save_economy" class="space-y-3">
              <div>
                <label class="block text-sm text-gray-500">Mana</label>
                <input type="number" step="0.1" name="economy[mana]" value={@economy_form[:mana].value} class="w-full border rounded px-3 py-1" />
              </div>
              <div>
                <label class="block text-sm text-gray-500">Gems</label>
                <input type="number" name="economy[gems]" value={@economy_form[:gems].value} class="w-full border rounded px-3 py-1" />
              </div>
              <div>
                <label class="block text-sm text-gray-500">Flame Level</label>
                <input type="number" name="economy[flame_level]" value={@economy_form[:flame_level].value} class="w-full border rounded px-3 py-1" />
              </div>
              <div class="flex gap-2">
                <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
                <button type="button" phx-click="cancel_economy" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
              </div>
            </.form>
          <% else %>
            <%= if @detail.economy do %>
              <dl class="space-y-1 text-sm">
                <div class="flex"><dt class="w-32 text-gray-500">Mana</dt><dd>{@detail.economy.mana}</dd></div>
                <div class="flex"><dt class="w-32 text-gray-500">Gems</dt><dd>{@detail.economy.gems}</dd></div>
                <div class="flex"><dt class="w-32 text-gray-500">Flame Level</dt><dd>{@detail.economy.flame_level}</dd></div>
              </dl>
            <% else %>
              <p class="text-gray-400 text-sm">No economy data</p>
            <% end %>
          <% end %>
        </div>

        <%!-- Inventory --%>
        <div class="bg-white border rounded-lg p-4">
          <h3 class="font-semibold text-lg mb-3">Inventory</h3>
          <%= if @detail.inventory != [] do %>
            <table class="w-full text-sm">
              <thead><tr><th class="text-left text-gray-500">Item</th><th class="text-left text-gray-500">Count</th></tr></thead>
              <tbody>
                <%= for i <- @detail.inventory do %>
                  <tr><td>{i.item_key}</td><td>{i.count}</td></tr>
                <% end %>
              </tbody>
            </table>
          <% else %>
            <p class="text-gray-400 text-sm">No inventory</p>
          <% end %>
        </div>

        <%!-- Plots --%>
        <div class="bg-white border rounded-lg p-4">
          <h3 class="font-semibold text-lg mb-3">Plots ({length(@detail.plots)})</h3>
          <%= if @detail.plots != [] do %>
            <table class="w-full text-sm">
              <thead><tr>
                <th class="text-left text-gray-500">Grid</th>
                <th class="text-left text-gray-500">State</th>
                <th class="text-left text-gray-500">Seed</th>
                <th class="text-left text-gray-500">Water</th>
              </tr></thead>
              <tbody>
                <%= for p <- @detail.plots do %>
                  <tr>
                    <td>({p.grid_x},{p.grid_y})</td>
                    <td>{p.state}</td>
                    <td>{p.seed_name || "-"}</td>
                    <td>{p.water_count}</td>
                  </tr>
                <% end %>
              </tbody>
            </table>
          <% else %>
            <p class="text-gray-400 text-sm">No plots</p>
          <% end %>
        </div>

        <%!-- Vases --%>
        <div class="bg-white border rounded-lg p-4">
          <h3 class="font-semibold text-lg mb-3">Vases ({length(@detail.vases)})</h3>
          <%= if @detail.vases != [] do %>
            <table class="w-full text-sm">
              <thead><tr>
                <th class="text-left text-gray-500">Grid</th>
                <th class="text-left text-gray-500">State</th>
                <th class="text-left text-gray-500">Water</th>
                <th class="text-left text-gray-500">Capacity</th>
              </tr></thead>
              <tbody>
                <%= for v <- @detail.vases do %>
                  <tr>
                    <td>({v.grid_x},{v.grid_y})</td>
                    <td>{v.state}</td>
                    <td>{v.current_water}</td>
                    <td>{v.capacity}</td>
                  </tr>
                <% end %>
              </tbody>
            </table>
          <% else %>
            <p class="text-gray-400 text-sm">No vases</p>
          <% end %>
        </div>

        <%!-- Gardens --%>
        <div class="bg-white border rounded-lg p-4">
          <h3 class="font-semibold text-lg mb-3">Gardens ({length(@detail.gardens)})</h3>
          <%= if @detail.gardens != [] do %>
            <table class="w-full text-sm">
              <thead><tr>
                <th class="text-left text-gray-500">Grid</th>
                <th class="text-left text-gray-500">Plant</th>
                <th class="text-left text-gray-500">Mature</th>
              </tr></thead>
              <tbody>
                <%= for g <- @detail.gardens do %>
                  <tr>
                    <td>({g.grid_x},{g.grid_y})</td>
                    <td>{g.plant_name}</td>
                    <td>{if g.mature, do: "Yes", else: "No"}</td>
                  </tr>
                <% end %>
              </tbody>
            </table>
          <% else %>
            <p class="text-gray-400 text-sm">No gardens</p>
          <% end %>
        </div>

        <%!-- Mallums --%>
        <div class="bg-white border rounded-lg p-4">
          <h3 class="font-semibold text-lg mb-3">Mallums ({length(@detail.mallums)})</h3>
          <%= if @detail.mallums != [] do %>
            <table class="w-full text-sm">
              <thead><tr>
                <th class="text-left text-gray-500">State</th>
                <th class="text-left text-gray-500">Quest</th>
                <th class="text-left text-gray-500">Rewards</th>
              </tr></thead>
              <tbody>
                <%= for m <- @detail.mallums do %>
                  <tr>
                    <td>{m.state}</td>
                    <td>{m.assigned_quest_name || "-"}</td>
                    <td>{length(m.pending_rewards || [])} pending</td>
                  </tr>
                <% end %>
              </tbody>
            </table>
          <% else %>
            <p class="text-gray-400 text-sm">No mallums</p>
          <% end %>
        </div>
      </div>
    </div>
    """
  end

  defp format_datetime(nil), do: "-"

  defp format_datetime(%NaiveDateTime{} = dt) do
    Calendar.strftime(dt, "%Y-%m-%d %H:%M")
  end

  defp format_datetime(%DateTime{} = dt) do
    Calendar.strftime(dt, "%Y-%m-%d %H:%M")
  end

  defp format_datetime(_), do: "-"
end
