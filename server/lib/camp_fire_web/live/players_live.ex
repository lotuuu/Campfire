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
        if connected?(socket), do: schedule_refresh()
        {:noreply, assign(socket, detail: detail, viewing_uid: uid, editing_economy: false, economy_form: nil)}
    end
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, detail: nil, viewing_uid: nil, editing_economy: false, economy_form: nil)}
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
                    <td>{if p.seed_item_id, do: CampFire.Game.resolve_item_key!(p.seed_item_id), else: "-"}</td>
                    <td>{p.water_count}</td>
                  </tr>
                <% end %>
              </tbody>
            </table>
          <% else %>
            <p class="text-gray-400 text-sm">No plots</p>
          <% end %>
        </div>

        <%!-- Crop State (growing/mature plots) --%>
        <% active_plots = Enum.filter(@detail.plots, &(&1.state in ["growing", "mature"])) %>
        <%= if active_plots != [] do %>
          <div class="bg-white border rounded-lg p-4 col-span-2">
            <h3 class="font-semibold text-lg mb-3">Crop State</h3>
            <div class="space-y-3">
              <%= for p <- active_plots do %>
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
        <% end %>

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
            <div class="space-y-3">
              <%= for g <- @detail.gardens do %>
                <% info = enrich_garden(g) %>
                <div class={"border rounded-lg p-3 #{if g.mature, do: "border-green-300 bg-green-50", else: "border-blue-300 bg-blue-50"}"}>
                  <div class="flex items-center justify-between mb-2">
                    <span class="font-medium">{g.plant_name} <span class="text-gray-400 text-xs">({g.grid_x},{g.grid_y})</span></span>
                    <span class={"text-xs font-semibold px-2 py-0.5 rounded #{if g.mature, do: "bg-green-200 text-green-800", else: "bg-blue-200 text-blue-800"}"}>
                      {if g.mature, do: "MATURE", else: "GROWING"}
                    </span>
                  </div>

                  <%= if not g.mature do %>
                    <%!-- Growth progress --%>
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
                    <%!-- Yield info --%>
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

    # For mature plots, use actual harvest_preview (cached server result)
    # For growing plots, show projected range
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

    # Per-axis recipe breakdown
    axes = GrowthRecipe.evaluate_per_axis(seed_config.recipe, plot.snapshots, plot.water_count)

    # Raw snapshot averages
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
    phase = phases
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
end
