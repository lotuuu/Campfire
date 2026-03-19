defmodule CampFireWeb.PlayersLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin
  alias CampFire.Game.GrowthRecipe

  def mount(_params, _session, socket) do
    {:ok,
     assign(socket,
       active_tab: :players,
       search_query: "",
       search_results: Admin.search_players(nil),
       detail: nil,
       editing_economy: false,
       economy_form: nil,
       editing: nil,
       edit_form: nil,
       all_items: []
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
        if connected?(socket), do: schedule_refresh()

        {:noreply,
         assign(socket,
           detail: detail,
           viewing_uid: uid,
           editing_economy: false,
           economy_form: nil,
           editing: nil,
           edit_form: nil,
           all_items: Admin.list_all_items()
         )}
    end
  end

  def handle_params(_params, _uri, socket) do
    {:noreply,
     assign(socket,
       detail: nil,
       viewing_uid: nil,
       editing_economy: false,
       economy_form: nil,
       editing: nil,
       edit_form: nil
     )}
  end

  def handle_info(:refresh, socket) do
    case socket.assigns[:viewing_uid] do
      nil ->
        {:noreply, socket}

      uid ->
        detail = Admin.get_player_detail(uid)
        schedule_refresh()
        {:noreply, assign(socket, detail: detail)}
    end
  end

  defp schedule_refresh, do: Process.send_after(self(), :refresh, 2_000)

  # ---------------------------------------------------------------------------
  # Search events
  # ---------------------------------------------------------------------------

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

  # ---------------------------------------------------------------------------
  # Generic cancel
  # ---------------------------------------------------------------------------

  def handle_event("cancel_edit", _params, socket) do
    {:noreply, assign(socket, editing: nil, edit_form: nil)}
  end

  # ---------------------------------------------------------------------------
  # Economy editing (existing pattern)
  # ---------------------------------------------------------------------------

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

  # ---------------------------------------------------------------------------
  # Player rename
  # ---------------------------------------------------------------------------

  def handle_event("edit_player", _params, socket) do
    name = socket.assigns.detail.player.display_name
    form = %{"display_name" => name} |> to_form(as: "player")
    {:noreply, assign(socket, editing: {:player}, edit_form: form)}
  end

  def handle_event("save_player", %{"player" => params}, socket) do
    uid = socket.assigns.detail.player.uid

    case Admin.update_player_name(uid, params["display_name"]) do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Player renamed") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to rename player")}
    end
  end

  # ---------------------------------------------------------------------------
  # Inventory editing
  # ---------------------------------------------------------------------------

  def handle_event("edit_inventory", %{"key" => key}, socket) do
    inv = Enum.find(socket.assigns.detail.inventory, &(&1.item_key == key))
    form = %{"item_key" => key, "count" => (inv && inv.count) || 0} |> to_form(as: "inventory")
    {:noreply, assign(socket, editing: {:inventory, key}, edit_form: form)}
  end

  def handle_event("add_inventory", _params, socket) do
    form = %{"item_key" => "", "count" => 1} |> to_form(as: "inventory")
    {:noreply, assign(socket, editing: {:inventory, :new}, edit_form: form)}
  end

  def handle_event("save_inventory", %{"inventory" => params}, socket) do
    uid = socket.assigns.detail.player.uid
    item_key = params["item_key"]
    count = parse_int(params["count"])

    case Admin.set_inventory_count(uid, item_key, count) do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Inventory updated") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to update inventory")}
    end
  end

  def handle_event("delete_inventory", %{"key" => key}, socket) do
    uid = socket.assigns.detail.player.uid

    case Admin.delete_inventory_item(uid, key) do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Item removed") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to remove item")}
    end
  end

  # ---------------------------------------------------------------------------
  # Plot editing
  # ---------------------------------------------------------------------------

  def handle_event("edit_plot", %{"id" => id}, socket) do
    id = parse_int(id)
    plot = Enum.find(socket.assigns.detail.plots, &(&1.id == id))
    seed_key = if plot.seed_item_id, do: CampFire.Game.resolve_item_key!(plot.seed_item_id), else: ""

    form =
      %{
        "state" => plot.state,
        "seed_key" => seed_key,
        "water_count" => plot.water_count,
        "grid_x" => plot.grid_x,
        "grid_y" => plot.grid_y,
        "fertilized" => to_string(plot.fertilized)
      }
      |> to_form(as: "plot")

    {:noreply, assign(socket, editing: {:plot, id}, edit_form: form)}
  end

  def handle_event("add_plot", _params, socket) do
    form =
      %{"state" => "empty", "seed_key" => "", "water_count" => 0, "grid_x" => 0, "grid_y" => 0, "fertilized" => "false"}
      |> to_form(as: "plot")

    {:noreply, assign(socket, editing: {:plot, :new}, edit_form: form)}
  end

  def handle_event("save_plot", %{"plot" => params}, socket) do
    uid = socket.assigns.detail.player.uid
    seed_item_id = resolve_seed_item_id(params["seed_key"])

    attrs = %{
      state: params["state"],
      seed_item_id: seed_item_id,
      water_count: parse_int(params["water_count"]),
      grid_x: parse_int(params["grid_x"]),
      grid_y: parse_int(params["grid_y"]),
      fertilized: params["fertilized"] == "true"
    }

    result =
      case socket.assigns.editing do
        {:plot, :new} ->
          Admin.create_player_plot(Map.put(attrs, :player_uid, uid))

        {:plot, id} ->
          Admin.update_plot(id, attrs)
      end

    case result do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Plot saved") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save plot")}
    end
  end

  def handle_event("delete_plot", %{"id" => id}, socket) do
    uid = socket.assigns.detail.player.uid
    Admin.delete_plot(parse_int(id))
    detail = Admin.get_player_detail(uid)
    {:noreply, socket |> put_flash(:info, "Plot deleted") |> assign(detail: detail, editing: nil, edit_form: nil)}
  end

  # ---------------------------------------------------------------------------
  # Vase editing
  # ---------------------------------------------------------------------------

  def handle_event("edit_vase", %{"id" => id}, socket) do
    id = parse_int(id)
    vase = Enum.find(socket.assigns.detail.vases, &(&1.id == id))

    form =
      %{
        "state" => vase.state,
        "current_water" => vase.current_water,
        "capacity" => vase.capacity,
        "grid_x" => vase.grid_x,
        "grid_y" => vase.grid_y
      }
      |> to_form(as: "vase")

    {:noreply, assign(socket, editing: {:vase, id}, edit_form: form)}
  end

  def handle_event("add_vase", _params, socket) do
    form =
      %{"state" => "empty", "current_water" => 0, "capacity" => 5, "grid_x" => 0, "grid_y" => 0}
      |> to_form(as: "vase")

    {:noreply, assign(socket, editing: {:vase, :new}, edit_form: form)}
  end

  def handle_event("save_vase", %{"vase" => params}, socket) do
    uid = socket.assigns.detail.player.uid

    attrs = %{
      state: params["state"],
      current_water: parse_int(params["current_water"]),
      capacity: parse_int(params["capacity"]),
      grid_x: parse_int(params["grid_x"]),
      grid_y: parse_int(params["grid_y"])
    }

    result =
      case socket.assigns.editing do
        {:vase, :new} -> Admin.create_player_vase(Map.put(attrs, :player_uid, uid))
        {:vase, id} -> Admin.update_vase(id, attrs)
      end

    case result do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Vase saved") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save vase")}
    end
  end

  def handle_event("delete_vase", %{"id" => id}, socket) do
    uid = socket.assigns.detail.player.uid
    Admin.delete_vase(parse_int(id))
    detail = Admin.get_player_detail(uid)
    {:noreply, socket |> put_flash(:info, "Vase deleted") |> assign(detail: detail, editing: nil, edit_form: nil)}
  end

  # ---------------------------------------------------------------------------
  # Garden editing
  # ---------------------------------------------------------------------------

  def handle_event("edit_garden", %{"id" => id}, socket) do
    id = parse_int(id)
    garden = Enum.find(socket.assigns.detail.gardens, &(&1.id == id))

    form =
      %{
        "plant_name" => garden.plant_name,
        "mature" => to_string(garden.mature),
        "grid_x" => garden.grid_x,
        "grid_y" => garden.grid_y,
        "fertilized" => to_string(garden.fertilized)
      }
      |> to_form(as: "garden")

    {:noreply, assign(socket, editing: {:garden, id}, edit_form: form)}
  end

  def handle_event("add_garden", _params, socket) do
    form =
      %{"plant_name" => "", "mature" => "false", "grid_x" => 0, "grid_y" => 0, "fertilized" => "false"}
      |> to_form(as: "garden")

    {:noreply, assign(socket, editing: {:garden, :new}, edit_form: form)}
  end

  def handle_event("save_garden", %{"garden" => params}, socket) do
    uid = socket.assigns.detail.player.uid

    attrs = %{
      plant_name: params["plant_name"],
      mature: params["mature"] == "true",
      grid_x: parse_int(params["grid_x"]),
      grid_y: parse_int(params["grid_y"]),
      fertilized: params["fertilized"] == "true",
      plant_time_utc: DateTime.utc_now() |> DateTime.truncate(:second)
    }

    result =
      case socket.assigns.editing do
        {:garden, :new} -> Admin.create_player_garden(Map.put(attrs, :player_uid, uid))
        {:garden, id} -> Admin.update_player_garden(id, Map.delete(attrs, :plant_time_utc))
      end

    case result do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Garden saved") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save garden")}
    end
  end

  def handle_event("delete_garden", %{"id" => id}, socket) do
    uid = socket.assigns.detail.player.uid
    Admin.delete_player_garden(parse_int(id))
    detail = Admin.get_player_detail(uid)
    {:noreply, socket |> put_flash(:info, "Garden deleted") |> assign(detail: detail, editing: nil, edit_form: nil)}
  end

  # ---------------------------------------------------------------------------
  # Mallum editing
  # ---------------------------------------------------------------------------

  def handle_event("edit_mallum", %{"id" => id}, socket) do
    id = parse_int(id)
    mallum = Enum.find(socket.assigns.detail.mallums, &(&1.id == id))

    form =
      %{
        "state" => mallum.state,
        "assigned_quest_name" => mallum.assigned_quest_name || ""
      }
      |> to_form(as: "mallum")

    {:noreply, assign(socket, editing: {:mallum, id}, edit_form: form)}
  end

  def handle_event("add_mallum", _params, socket) do
    form = %{"state" => "idle", "assigned_quest_name" => ""} |> to_form(as: "mallum")
    {:noreply, assign(socket, editing: {:mallum, :new}, edit_form: form)}
  end

  def handle_event("save_mallum", %{"mallum" => params}, socket) do
    uid = socket.assigns.detail.player.uid
    quest = if params["assigned_quest_name"] == "", do: nil, else: params["assigned_quest_name"]

    attrs = %{state: params["state"], assigned_quest_name: quest}

    result =
      case socket.assigns.editing do
        {:mallum, :new} -> Admin.create_player_mallum(Map.put(attrs, :player_uid, uid))
        {:mallum, id} -> Admin.update_mallum(id, attrs)
      end

    case result do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Mallum saved") |> assign(detail: detail, editing: nil, edit_form: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save mallum")}
    end
  end

  def handle_event("clear_mallum_rewards", %{"id" => id}, socket) do
    uid = socket.assigns.detail.player.uid
    Admin.update_mallum(parse_int(id), %{pending_rewards: []})
    detail = Admin.get_player_detail(uid)
    {:noreply, socket |> put_flash(:info, "Rewards cleared") |> assign(detail: detail)}
  end

  def handle_event("delete_mallum", %{"id" => id}, socket) do
    uid = socket.assigns.detail.player.uid
    Admin.delete_mallum(parse_int(id))
    detail = Admin.get_player_detail(uid)
    {:noreply, socket |> put_flash(:info, "Mallum deleted") |> assign(detail: detail, editing: nil, edit_form: nil)}
  end

  # ---------------------------------------------------------------------------
  # Render
  # ---------------------------------------------------------------------------

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
        {render_player_section(assigns)}
        {render_economy_section(assigns)}
        {render_inventory_section(assigns)}
        {render_plots_section(assigns)}

        <%!-- Crop State (growing/mature plots) --%>
        <% active_plots = Enum.filter(@detail.plots, &(&1.state in ["growing", "mature"])) %>
        <%= if active_plots != [] do %>
          {render_crop_state(assign(assigns, :active_plots, active_plots))}
        <% end %>

        {render_vases_section(assigns)}
        {render_gardens_section(assigns)}
        {render_mallums_section(assigns)}
      </div>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Player section
  # ---------------------------------------------------------------------------

  defp render_player_section(assigns) do
    ~H"""
    <div class="bg-white border rounded-lg p-4">
      <div class="flex justify-between items-center mb-3">
        <h3 class="font-semibold text-lg">Player</h3>
        <%= if @editing != {:player} do %>
          <button phx-click="edit_player" class="text-blue-600 hover:underline text-sm">Edit</button>
        <% end %>
      </div>

      <%= if @editing == {:player} do %>
        <.form for={@edit_form} phx-submit="save_player" class="space-y-3">
          <div>
            <label class="block text-sm text-gray-500">Display Name</label>
            <input type="text" name="player[display_name]" value={@edit_form[:display_name].value} class="w-full border rounded px-3 py-1" />
          </div>
          <dl class="space-y-1 text-sm">
            <div class="flex"><dt class="w-32 text-gray-500">UID</dt><dd class="font-mono text-xs">{@detail.player.uid}</dd></div>
            <div class="flex"><dt class="w-32 text-gray-500">Friend Code</dt><dd class="font-mono">{@detail.player.friend_code}</dd></div>
          </dl>
          <div class="flex gap-2">
            <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            <button type="button" phx-click="cancel_edit" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
          </div>
        </.form>
      <% else %>
        <dl class="space-y-1 text-sm">
          <div class="flex"><dt class="w-32 text-gray-500">Name</dt><dd>{@detail.player.display_name}</dd></div>
          <div class="flex"><dt class="w-32 text-gray-500">UID</dt><dd class="font-mono text-xs">{@detail.player.uid}</dd></div>
          <div class="flex"><dt class="w-32 text-gray-500">Friend Code</dt><dd class="font-mono">{@detail.player.friend_code}</dd></div>
        </dl>
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Economy section
  # ---------------------------------------------------------------------------

  defp render_economy_section(assigns) do
    ~H"""
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
    """
  end

  # ---------------------------------------------------------------------------
  # Inventory section
  # ---------------------------------------------------------------------------

  defp render_inventory_section(assigns) do
    ~H"""
    <div class="bg-white border rounded-lg p-4">
      <div class="flex justify-between items-center mb-3">
        <h3 class="font-semibold text-lg">Inventory ({length(@detail.inventory)})</h3>
        <button phx-click="add_inventory" class="text-blue-600 hover:underline text-sm">+ Add Item</button>
      </div>

      <%!-- Add/Edit form --%>
      <%= if match?({:inventory, _}, @editing) do %>
        <.form for={@edit_form} phx-submit="save_inventory" class="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg space-y-3">
          <div>
            <label class="block text-sm text-gray-500">Item</label>
            <%= if @editing == {:inventory, :new} do %>
              <select name="inventory[item_key]" class="w-full border rounded px-3 py-1">
                <option value="">-- Select item --</option>
                <%= for item <- @all_items do %>
                  <option value={item.item_key} selected={@edit_form[:item_key].value == item.item_key}>
                    {item.item_key} ({item.category})
                  </option>
                <% end %>
              </select>
            <% else %>
              <input type="text" name="inventory[item_key]" value={@edit_form[:item_key].value} readonly class="w-full border rounded px-3 py-1 bg-gray-100" />
            <% end %>
          </div>
          <div>
            <label class="block text-sm text-gray-500">Count</label>
            <input type="number" name="inventory[count]" value={@edit_form[:count].value} min="0" class="w-full border rounded px-3 py-1" />
          </div>
          <div class="flex gap-2">
            <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            <button type="button" phx-click="cancel_edit" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
          </div>
        </.form>
      <% end %>

      <%= if @detail.inventory != [] do %>
        <table class="w-full text-sm">
          <thead><tr>
            <th class="text-left text-gray-500">Item</th>
            <th class="text-left text-gray-500">Count</th>
            <th class="text-right text-gray-500">Actions</th>
          </tr></thead>
          <tbody>
            <%= for i <- @detail.inventory do %>
              <tr class="hover:bg-gray-50">
                <td>{i.item_key}</td>
                <td>{i.count}</td>
                <td class="text-right">
                  <button phx-click="edit_inventory" phx-value-key={i.item_key} class="text-blue-600 hover:underline text-xs mr-2">Edit</button>
                  <button phx-click="delete_inventory" phx-value-key={i.item_key} data-confirm={"Delete #{i.item_key}?"} class="text-red-600 hover:underline text-xs">Delete</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% else %>
        <p class="text-gray-400 text-sm">No inventory</p>
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Plots section
  # ---------------------------------------------------------------------------

  defp render_plots_section(assigns) do
    seed_items = Enum.filter(assigns.all_items, &(&1.category == "seed"))
    assigns = assign(assigns, :seed_items, seed_items)

    ~H"""
    <div class="bg-white border rounded-lg p-4">
      <div class="flex justify-between items-center mb-3">
        <h3 class="font-semibold text-lg">Plots ({length(@detail.plots)})</h3>
        <button phx-click="add_plot" class="text-blue-600 hover:underline text-sm">+ Add Plot</button>
      </div>

      <%!-- Add/Edit form --%>
      <%= if match?({:plot, _}, @editing) do %>
        <.form for={@edit_form} phx-submit="save_plot" class="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg space-y-3">
          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-sm text-gray-500">State</label>
              <select name="plot[state]" class="w-full border rounded px-3 py-1">
                <%= for s <- ["empty", "growing", "mature"] do %>
                  <option value={s} selected={@edit_form[:state].value == s}>{s}</option>
                <% end %>
              </select>
            </div>
            <div>
              <label class="block text-sm text-gray-500">Seed</label>
              <select name="plot[seed_key]" class="w-full border rounded px-3 py-1">
                <option value="">-- None --</option>
                <%= for item <- @seed_items do %>
                  <option value={item.item_key} selected={@edit_form[:seed_key].value == item.item_key}>{item.item_key}</option>
                <% end %>
              </select>
            </div>
            <div>
              <label class="block text-sm text-gray-500">Water Count</label>
              <input type="number" name="plot[water_count]" value={@edit_form[:water_count].value} min="0" class="w-full border rounded px-3 py-1" />
            </div>
          </div>
          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-sm text-gray-500">Grid X</label>
              <input type="number" name="plot[grid_x]" value={@edit_form[:grid_x].value} class="w-full border rounded px-3 py-1" />
            </div>
            <div>
              <label class="block text-sm text-gray-500">Grid Y</label>
              <input type="number" name="plot[grid_y]" value={@edit_form[:grid_y].value} class="w-full border rounded px-3 py-1" />
            </div>
            <div class="flex items-end">
              <label class="flex items-center gap-2 text-sm">
                <input type="hidden" name="plot[fertilized]" value="false" />
                <input type="checkbox" name="plot[fertilized]" value="true" checked={@edit_form[:fertilized].value == "true"} />
                Fertilized
              </label>
            </div>
          </div>
          <div class="flex gap-2">
            <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            <button type="button" phx-click="cancel_edit" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
          </div>
        </.form>
      <% end %>

      <%= if @detail.plots != [] do %>
        <table class="w-full text-sm">
          <thead><tr>
            <th class="text-left text-gray-500">Grid</th>
            <th class="text-left text-gray-500">State</th>
            <th class="text-left text-gray-500">Seed</th>
            <th class="text-left text-gray-500">Water</th>
            <th class="text-right text-gray-500">Actions</th>
          </tr></thead>
          <tbody>
            <%= for p <- @detail.plots do %>
              <tr class={"hover:bg-gray-50 #{if @editing == {:plot, p.id}, do: "bg-blue-50"}"}>
                <td>({p.grid_x},{p.grid_y})</td>
                <td>{p.state}</td>
                <td>{if p.seed_item_id, do: CampFire.Game.resolve_item_key!(p.seed_item_id), else: "-"}</td>
                <td>{p.water_count}</td>
                <td class="text-right">
                  <button phx-click="edit_plot" phx-value-id={p.id} class="text-blue-600 hover:underline text-xs mr-2">Edit</button>
                  <button phx-click="delete_plot" phx-value-id={p.id} data-confirm="Delete this plot?" class="text-red-600 hover:underline text-xs">Delete</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% else %>
        <p class="text-gray-400 text-sm">No plots</p>
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Crop State (read-only, unchanged)
  # ---------------------------------------------------------------------------

  defp render_crop_state(assigns) do
    ~H"""
    <div class="bg-white border rounded-lg p-4 col-span-2">
      <h3 class="font-semibold text-lg mb-3">Crop State</h3>
      <div class="space-y-3">
        <%= for p <- @active_plots do %>
          <% info = enrich_plot(p) %>
          <div class={"border rounded-lg p-3 #{if p.state == "mature", do: "border-green-300 bg-green-50", else: "border-yellow-300 bg-yellow-50"}"}>
            <div class="flex items-center justify-between mb-2">
              <span class="font-medium">{info.seed_name} <span class="text-gray-400 text-xs">({p.grid_x},{p.grid_y})</span></span>
              <span class={"text-xs font-semibold px-2 py-0.5 rounded #{if p.state == "mature", do: "bg-green-200 text-green-800", else: "bg-yellow-200 text-yellow-800"}"}>
                {String.upcase(p.state)}
              </span>
            </div>

            <%!-- Progress bar --%>
            <div class="mb-2">
              <div class="flex justify-between text-xs text-gray-500 mb-1">
                <span>Growth: {Float.round(info.progress * 100, 1)}%</span>
                <span>{info.remaining_text}</span>
              </div>
              <div class="w-full bg-gray-200 rounded-full h-2">
                <div class={"h-2 rounded-full #{if info.progress >= 1.0, do: "bg-green-500", else: "bg-yellow-500"}"} style={"width: #{min(info.progress * 100, 100)}%"}></div>
              </div>
            </div>

            <%!-- Stats row --%>
            <div class="grid grid-cols-4 gap-2 text-xs">
              <div>
                <span class="text-gray-500 block">Waterings</span>
                <span class="font-medium">{p.water_count}</span>
              </div>
              <div>
                <span class="text-gray-500 block">Water Cooldown</span>
                <span class="font-medium">{info.water_cooldown_text}</span>
              </div>
              <div>
                <span class="text-gray-500 block">Recipe Score</span>
                <span class="font-medium">{Float.round(info.recipe_score * 100, 1)}%</span>
              </div>
              <div>
                <span class="text-gray-500 block">{info.drops_label}</span>
                <span class="font-medium">{info.drops_text}</span>
              </div>
            </div>

            <%!-- Recipe Axes --%>
            <%= if info.axes != [] do %>
              <div class="mt-3 border-t pt-2">
                <div class="text-xs font-medium text-gray-600 mb-1">Recipe Breakdown</div>
                <div class="space-y-1">
                  <%= for ax <- info.axes do %>
                    <div class="flex items-center gap-2 text-xs">
                      <span class="w-20 text-gray-500 capitalize">{ax.axis}</span>
                      <div class="flex-1 bg-gray-200 rounded-full h-1.5">
                        <div class={"h-1.5 rounded-full #{cond do
                          ax.score >= 0.8 -> "bg-green-500"
                          ax.score >= 0.5 -> "bg-yellow-500"
                          true -> "bg-red-400"
                        end}"} style={"width: #{ax.score * 100}%"}></div>
                      </div>
                      <span class="w-10 text-right font-mono">{Float.round(ax.score * 100, 0)}%</span>
                      <span class="text-gray-400 w-48 text-right">
                        actual: {ax.actual} | ideal: {ax.ideal_min}-{ax.ideal_max} +/-{ax.tolerance}
                      </span>
                    </div>
                  <% end %>
                </div>
              </div>
            <% end %>

            <%!-- Snapshot Summary --%>
            <div class="mt-2 border-t pt-2">
              <div class="text-xs text-gray-500">
                <span class="font-medium">{info.snapshot_count} snapshot(s)</span>
                <%= if info.snapshot_summary do %>
                  <span class="ml-2">
                    | Temp: {info.snapshot_summary.avg_temp}C
                    | Wind: {info.snapshot_summary.avg_wind}m/s
                    | Humidity: {info.snapshot_summary.avg_humidity}%
                    | Cloud: {info.snapshot_summary.avg_cloud}%
                    | Rain: {info.snapshot_summary.rain_pct}%
                    | Moon: {info.snapshot_summary.moon}
                  </span>
                <% end %>
              </div>
            </div>
          </div>
        <% end %>
      </div>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Vases section
  # ---------------------------------------------------------------------------

  defp render_vases_section(assigns) do
    ~H"""
    <div class="bg-white border rounded-lg p-4">
      <div class="flex justify-between items-center mb-3">
        <h3 class="font-semibold text-lg">Vases ({length(@detail.vases)})</h3>
        <button phx-click="add_vase" class="text-blue-600 hover:underline text-sm">+ Add Vase</button>
      </div>

      <%= if match?({:vase, _}, @editing) do %>
        <.form for={@edit_form} phx-submit="save_vase" class="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg space-y-3">
          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-sm text-gray-500">State</label>
              <select name="vase[state]" class="w-full border rounded px-3 py-1">
                <%= for s <- ["empty", "filling", "full"] do %>
                  <option value={s} selected={@edit_form[:state].value == s}>{s}</option>
                <% end %>
              </select>
            </div>
            <div>
              <label class="block text-sm text-gray-500">Current Water</label>
              <input type="number" name="vase[current_water]" value={@edit_form[:current_water].value} min="0" class="w-full border rounded px-3 py-1" />
            </div>
            <div>
              <label class="block text-sm text-gray-500">Capacity</label>
              <input type="number" name="vase[capacity]" value={@edit_form[:capacity].value} min="1" class="w-full border rounded px-3 py-1" />
            </div>
          </div>
          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-sm text-gray-500">Grid X</label>
              <input type="number" name="vase[grid_x]" value={@edit_form[:grid_x].value} class="w-full border rounded px-3 py-1" />
            </div>
            <div>
              <label class="block text-sm text-gray-500">Grid Y</label>
              <input type="number" name="vase[grid_y]" value={@edit_form[:grid_y].value} class="w-full border rounded px-3 py-1" />
            </div>
          </div>
          <div class="flex gap-2">
            <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            <button type="button" phx-click="cancel_edit" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
          </div>
        </.form>
      <% end %>

      <%= if @detail.vases != [] do %>
        <table class="w-full text-sm">
          <thead><tr>
            <th class="text-left text-gray-500">Grid</th>
            <th class="text-left text-gray-500">State</th>
            <th class="text-left text-gray-500">Water</th>
            <th class="text-left text-gray-500">Capacity</th>
            <th class="text-right text-gray-500">Actions</th>
          </tr></thead>
          <tbody>
            <%= for v <- @detail.vases do %>
              <tr class={"hover:bg-gray-50 #{if @editing == {:vase, v.id}, do: "bg-blue-50"}"}>
                <td>({v.grid_x},{v.grid_y})</td>
                <td>{v.state}</td>
                <td>{v.current_water}</td>
                <td>{v.capacity}</td>
                <td class="text-right">
                  <button phx-click="edit_vase" phx-value-id={v.id} class="text-blue-600 hover:underline text-xs mr-2">Edit</button>
                  <button phx-click="delete_vase" phx-value-id={v.id} data-confirm="Delete this vase?" class="text-red-600 hover:underline text-xs">Delete</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% else %>
        <p class="text-gray-400 text-sm">No vases</p>
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Gardens section
  # ---------------------------------------------------------------------------

  defp render_gardens_section(assigns) do
    ~H"""
    <div class="bg-white border rounded-lg p-4">
      <div class="flex justify-between items-center mb-3">
        <h3 class="font-semibold text-lg">Gardens ({length(@detail.gardens)})</h3>
        <button phx-click="add_garden" class="text-blue-600 hover:underline text-sm">+ Add Garden</button>
      </div>

      <%= if match?({:garden, _}, @editing) do %>
        <.form for={@edit_form} phx-submit="save_garden" class="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg space-y-3">
          <div class="grid grid-cols-3 gap-3">
            <div>
              <label class="block text-sm text-gray-500">Plant Name</label>
              <input type="text" name="garden[plant_name]" value={@edit_form[:plant_name].value} class="w-full border rounded px-3 py-1" placeholder="e.g. Apple Tree" />
            </div>
            <div>
              <label class="block text-sm text-gray-500">Grid X</label>
              <input type="number" name="garden[grid_x]" value={@edit_form[:grid_x].value} class="w-full border rounded px-3 py-1" />
            </div>
            <div>
              <label class="block text-sm text-gray-500">Grid Y</label>
              <input type="number" name="garden[grid_y]" value={@edit_form[:grid_y].value} class="w-full border rounded px-3 py-1" />
            </div>
          </div>
          <div class="flex gap-4">
            <label class="flex items-center gap-2 text-sm">
              <input type="hidden" name="garden[mature]" value="false" />
              <input type="checkbox" name="garden[mature]" value="true" checked={@edit_form[:mature].value == "true"} />
              Mature
            </label>
            <label class="flex items-center gap-2 text-sm">
              <input type="hidden" name="garden[fertilized]" value="false" />
              <input type="checkbox" name="garden[fertilized]" value="true" checked={@edit_form[:fertilized].value == "true"} />
              Fertilized
            </label>
          </div>
          <div class="flex gap-2">
            <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            <button type="button" phx-click="cancel_edit" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
          </div>
        </.form>
      <% end %>

      <%= if @detail.gardens != [] do %>
        <div class="space-y-3">
          <%= for g <- @detail.gardens do %>
            <% info = enrich_garden(g) %>
            <div class={"border rounded-lg p-3 #{if g.mature, do: "border-green-300 bg-green-50", else: "border-blue-300 bg-blue-50"} #{if @editing == {:garden, g.id}, do: "ring-2 ring-blue-400"}"}>
              <div class="flex items-center justify-between mb-2">
                <span class="font-medium">{g.plant_name} <span class="text-gray-400 text-xs">({g.grid_x},{g.grid_y})</span></span>
                <div class="flex items-center gap-2">
                  <span class={"text-xs font-semibold px-2 py-0.5 rounded #{if g.mature, do: "bg-green-200 text-green-800", else: "bg-blue-200 text-blue-800"}"}>
                    {if g.mature, do: "MATURE", else: "GROWING"}
                  </span>
                  <button phx-click="edit_garden" phx-value-id={g.id} class="text-blue-600 hover:underline text-xs">Edit</button>
                  <button phx-click="delete_garden" phx-value-id={g.id} data-confirm="Delete this garden?" class="text-red-600 hover:underline text-xs">Delete</button>
                </div>
              </div>

              <%= if not g.mature do %>
                <div class="mb-2">
                  <div class="flex justify-between text-xs text-gray-500 mb-1">
                    <span>Growth: {Float.round(info.progress * 100, 1)}%</span>
                    <span>{info.remaining_text}</span>
                  </div>
                  <div class="w-full bg-gray-200 rounded-full h-2">
                    <div class="h-2 rounded-full bg-blue-500" style={"width: #{min(info.progress * 100, 100)}%"}></div>
                  </div>
                </div>
              <% else %>
                <div class="grid grid-cols-3 gap-2 text-xs">
                  <div>
                    <span class="text-gray-500 block">Yields</span>
                    <span class="font-medium">{info.yield_item} x{info.yield_amount}</span>
                  </div>
                  <div>
                    <span class="text-gray-500 block">Interval</span>
                    <span class="font-medium">{info.yield_interval_text}</span>
                  </div>
                  <div>
                    <span class="text-gray-500 block">Next Yield</span>
                    <span class="font-medium">{info.next_yield_text}</span>
                  </div>
                </div>
              <% end %>
            </div>
          <% end %>
        </div>
      <% else %>
        <p class="text-gray-400 text-sm">No gardens</p>
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Mallums section
  # ---------------------------------------------------------------------------

  defp render_mallums_section(assigns) do
    ~H"""
    <div class="bg-white border rounded-lg p-4">
      <div class="flex justify-between items-center mb-3">
        <h3 class="font-semibold text-lg">Mallums ({length(@detail.mallums)})</h3>
        <button phx-click="add_mallum" class="text-blue-600 hover:underline text-sm">+ Add Mallum</button>
      </div>

      <%= if match?({:mallum, _}, @editing) do %>
        <.form for={@edit_form} phx-submit="save_mallum" class="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg space-y-3">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm text-gray-500">State</label>
              <select name="mallum[state]" class="w-full border rounded px-3 py-1">
                <%= for s <- ["idle", "fetching_water", "on_quest", "quest_complete"] do %>
                  <option value={s} selected={@edit_form[:state].value == s}>{s}</option>
                <% end %>
              </select>
            </div>
            <div>
              <label class="block text-sm text-gray-500">Assigned Quest</label>
              <input type="text" name="mallum[assigned_quest_name]" value={@edit_form[:assigned_quest_name].value} class="w-full border rounded px-3 py-1" placeholder="Quest name or empty" />
            </div>
          </div>
          <div class="flex gap-2">
            <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            <button type="button" phx-click="cancel_edit" class="bg-gray-300 px-3 py-1 rounded text-sm">Cancel</button>
          </div>
        </.form>
      <% end %>

      <%= if @detail.mallums != [] do %>
        <table class="w-full text-sm">
          <thead><tr>
            <th class="text-left text-gray-500">State</th>
            <th class="text-left text-gray-500">Quest</th>
            <th class="text-left text-gray-500">Rewards</th>
            <th class="text-right text-gray-500">Actions</th>
          </tr></thead>
          <tbody>
            <%= for m <- @detail.mallums do %>
              <% reward_count = length(m.pending_rewards || []) %>
              <tr class={"hover:bg-gray-50 #{if @editing == {:mallum, m.id}, do: "bg-blue-50"}"}>
                <td>{m.state}</td>
                <td>{m.assigned_quest_name || "-"}</td>
                <td>
                  {reward_count} pending
                  <%= if reward_count > 0 do %>
                    <button phx-click="clear_mallum_rewards" phx-value-id={m.id} data-confirm="Clear all pending rewards?" class="text-orange-600 hover:underline text-xs ml-1">Clear</button>
                  <% end %>
                </td>
                <td class="text-right">
                  <button phx-click="edit_mallum" phx-value-id={m.id} class="text-blue-600 hover:underline text-xs mr-2">Edit</button>
                  <button phx-click="delete_mallum" phx-value-id={m.id} data-confirm="Delete this mallum?" class="text-red-600 hover:underline text-xs">Delete</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      <% else %>
        <p class="text-gray-400 text-sm">No mallums</p>
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Helpers
  # ---------------------------------------------------------------------------

  defp resolve_seed_item_id(""), do: nil
  defp resolve_seed_item_id(nil), do: nil

  defp resolve_seed_item_id(item_key) do
    case CampFire.Repo.get_by(CampFire.Game.Item, item_key: item_key) do
      nil -> nil
      item -> item.id
    end
  end

  defp enrich_plot(plot) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    seed_config = CampFire.Game.get_seed_config_by_item_id!(plot.seed_item_id)
    seed_name = seed_config.harvest_item_key

    growth_seconds = seed_config.growth_duration_hours * 3600
    elapsed = if plot.plant_time_utc, do: DateTime.diff(now, plot.plant_time_utc, :second), else: 0
    progress = if growth_seconds > 0, do: min(elapsed / growth_seconds, 1.0), else: 1.0

    remaining = max(growth_seconds - elapsed, 0)
    remaining_text = if remaining <= 0, do: "Ready!", else: format_duration(remaining)

    # Water cooldown
    water_cooldown = water_cooldown_seconds()

    water_remaining =
      if plot.last_watered_utc do
        since_water = DateTime.diff(now, plot.last_watered_utc, :second)
        max(water_cooldown - since_water, 0)
      else
        0
      end

    water_cooldown_text = if water_remaining <= 0, do: "Ready", else: format_duration(water_remaining)

    # Recipe score & drops
    recipe_score = GrowthRecipe.evaluate(seed_config.recipe, plot.snapshots, plot.water_count)

    {drops_label, drops_text} =
      if plot.state == "mature" do
        case CampFire.Game.Plots.harvest_preview(plot.player_uid, plot.id) do
          {:ok, %{drops: drops, score: score}} ->
            {"Drops", "#{drops} (#{Float.round(score * 100, 1)}%)"}

          _ ->
            {"Drops", "error"}
        end
      else
        config = CampFire.ConfigCache.get("plot_config") || %{}
        spread_factor = config["drop_spread_factor"] || 0.3
        center = seed_config.min_drops + recipe_score * (seed_config.max_drops - seed_config.min_drops)
        spread = (seed_config.max_drops - seed_config.min_drops) * spread_factor
        low = max(seed_config.min_drops, round(center - spread))
        high = min(seed_config.max_drops, round(center + spread))
        {"Projected Drops", "#{low}-#{high}"}
      end

    snapshot_count = get_in(plot.snapshots || %{}, [Access.key("snapshot_count", 0)])

    axes = GrowthRecipe.evaluate_per_axis(seed_config.recipe, plot.snapshots, plot.water_count)

    snapshots = plot.snapshots || %{}

    snapshot_summary =
      if snapshot_count > 0 do
        %{
          avg_temp: safe_avg(snapshots["temperatures"]),
          avg_wind: safe_avg(snapshots["wind_speeds"]),
          avg_humidity: safe_avg(snapshots["humidities"]),
          avg_cloud: safe_avg(snapshots["cloud_covers"]),
          rain_pct: safe_rain_pct(snapshots["rain_snapshots"], snapshot_count),
          moon: dominant_moon_phase(snapshots["moon_phase_snapshots"])
        }
      else
        nil
      end

    %{
      seed_name: seed_name,
      progress: progress,
      remaining_text: remaining_text,
      water_cooldown_text: water_cooldown_text,
      recipe_score: recipe_score,
      drops_label: drops_label,
      drops_text: drops_text,
      snapshot_count: snapshot_count,
      axes: axes,
      snapshot_summary: snapshot_summary
    }
  end

  defp enrich_garden(garden) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    garden_configs = CampFire.ConfigCache.get("garden_configs") || %{}
    config = Map.get(garden_configs, garden.plant_name, %{})

    growth_hours = config[:growth_duration_hours] || 1.0
    growth_seconds = growth_hours * 3600

    elapsed = if garden.plant_time_utc, do: DateTime.diff(now, garden.plant_time_utc, :second), else: 0
    progress = if growth_seconds > 0, do: min(elapsed / growth_seconds, 1.0), else: 1.0
    remaining = max(growth_seconds - elapsed, 0)
    remaining_text = if remaining <= 0, do: "Ready!", else: format_duration(remaining)

    yield_interval_hours = config[:yield_interval_hours] || 1.0
    yield_interval_text = format_duration(yield_interval_hours * 3600)

    next_yield_text =
      if garden.mature && garden.last_yield_time_utc do
        since_yield = DateTime.diff(now, garden.last_yield_time_utc, :second)
        remaining_yield = max(yield_interval_hours * 3600 - since_yield, 0)
        if remaining_yield <= 0, do: "Ready!", else: format_duration(remaining_yield)
      else
        if garden.mature, do: "Ready!", else: "-"
      end

    %{
      progress: progress,
      remaining_text: remaining_text,
      yield_item: config[:yield_item] || "?",
      yield_amount: config[:yield_amount] || 0,
      yield_interval_text: yield_interval_text,
      next_yield_text: next_yield_text
    }
  end

  defp water_cooldown_seconds do
    config = CampFire.ConfigCache.get("plot_config") || %{}
    config["water_cooldown_seconds"] || 300
  end

  defp safe_avg(nil), do: 0.0
  defp safe_avg([]), do: 0.0
  defp safe_avg(list), do: Float.round(Enum.sum(list) / length(list), 1)

  defp safe_rain_pct(nil, _count), do: 0.0
  defp safe_rain_pct(_, 0), do: 0.0

  defp safe_rain_pct(rain_snapshots, count) do
    rain_count = Enum.count(rain_snapshots, &(&1 >= 1.0))
    Float.round(rain_count / count * 100, 0)
  end

  defp dominant_moon_phase(nil), do: "-"
  defp dominant_moon_phase([]), do: "-"

  defp dominant_moon_phase(phases) do
    phase =
      phases
      |> Enum.map(&round/1)
      |> Enum.frequencies()
      |> Enum.max_by(fn {_phase, count} -> count end)
      |> elem(0)

    case phase do
      0 -> "New Moon"
      1 -> "Waxing Crescent"
      2 -> "First Quarter"
      3 -> "Waxing Gibbous"
      4 -> "Full Moon"
      5 -> "Waning Gibbous"
      6 -> "Last Quarter"
      7 -> "Waning Crescent"
      _ -> "Phase #{phase}"
    end
  end

  defp format_duration(seconds) when is_float(seconds), do: format_duration(trunc(seconds))
  defp format_duration(seconds) when seconds < 60, do: "#{seconds}s"

  defp format_duration(seconds) when seconds < 3600 do
    m = div(seconds, 60)
    s = rem(seconds, 60)
    if s > 0, do: "#{m}m #{s}s", else: "#{m}m"
  end

  defp format_duration(seconds) do
    h = div(seconds, 3600)
    m = div(rem(seconds, 3600), 60)
    if m > 0, do: "#{h}h #{m}m", else: "#{h}h"
  end

  defp format_datetime(nil), do: "-"

  defp format_datetime(%NaiveDateTime{} = dt) do
    Calendar.strftime(dt, "%Y-%m-%d %H:%M")
  end

  defp format_datetime(%DateTime{} = dt) do
    Calendar.strftime(dt, "%Y-%m-%d %H:%M")
  end

  defp format_datetime(_), do: "-"

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
end
