defmodule CampFireWeb.LogsLive do
  use CampFireWeb, :live_view

  alias CampFire.DebugLog

  def mount(_params, _session, socket) do
    if connected?(socket) do
      Phoenix.PubSub.subscribe(CampFire.PubSub, DebugLog.topic())
    end

    filters = %{level: nil, source: nil, category: nil, player_uid: nil}
    entries = DebugLog.list() |> Enum.reverse()

    {:ok,
     assign(socket,
       active_tab: :logs,
       filters: filters,
       entries: entries,
       paused: false
     )}
  end

  def handle_info(:logs_cleared, socket) do
    {:noreply, assign(socket, entries: [])}
  end

  def handle_info({:new_log_entry, entry}, socket) do
    if not socket.assigns.paused and matches_filters?(entry, socket.assigns.filters) do
      entries = socket.assigns.entries ++ [entry]
      entries = if length(entries) > 1000, do: Enum.drop(entries, 1), else: entries
      {:noreply, assign(socket, entries: entries)}
    else
      {:noreply, socket}
    end
  end

  def handle_event("filter", params, socket) do
    filters = %{
      level: blank_to_nil(params["level"]),
      source: blank_to_nil(params["source"]),
      category: blank_to_nil(params["category"]),
      player_uid: blank_to_nil(params["player_uid"])
    }

    filter_opts = build_filter_opts(filters)
    entries = DebugLog.list(filter_opts) |> Enum.reverse()
    {:noreply, assign(socket, filters: filters, entries: entries)}
  end

  def handle_event("clear_filters", _params, socket) do
    filters = %{level: nil, source: nil, category: nil, player_uid: nil}
    entries = DebugLog.list() |> Enum.reverse()
    {:noreply, assign(socket, filters: filters, entries: entries)}
  end

  def handle_event("toggle_pause", _params, socket) do
    {:noreply, assign(socket, paused: not socket.assigns.paused)}
  end

  def handle_event("clear_logs", _params, socket) do
    DebugLog.clear()
    {:noreply, socket}
  end

  defp blank_to_nil(""), do: nil
  defp blank_to_nil(nil), do: nil
  defp blank_to_nil(v), do: v

  defp build_filter_opts(filters) do
    opts = []
    opts = if filters.level, do: [{:level, String.to_existing_atom(filters.level)} | opts], else: opts
    opts = if filters.source, do: [{:source, String.to_existing_atom(filters.source)} | opts], else: opts
    opts = if filters.category, do: [{:category, filters.category} | opts], else: opts
    opts = if filters.player_uid, do: [{:player_uid, filters.player_uid} | opts], else: opts
    opts
  end

  defp matches_filters?(entry, filters) do
    (filters.level == nil or to_string(entry.level) == filters.level) and
      (filters.source == nil or to_string(entry.source) == filters.source) and
      (filters.category == nil or entry.category == filters.category) and
      (filters.player_uid == nil or
         (entry.player_uid != nil and String.starts_with?(entry.player_uid, filters.player_uid)))
  end

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-2xl font-bold">Debug Logs</h2>
        <div class="flex gap-2">
          <button
            phx-click="toggle_pause"
            class={"px-3 py-1 rounded text-sm font-medium #{if @paused, do: "bg-yellow-100 text-yellow-800", else: "bg-green-100 text-green-800"}"}
          >
            {if @paused, do: "Paused", else: "Live"}
          </button>
          <button phx-click="clear_filters" class="px-3 py-1 rounded text-sm bg-gray-100 text-gray-600 hover:bg-gray-200">
            Clear Filters
          </button>
          <button phx-click="clear_logs" data-confirm="Clear all log entries?" class="px-3 py-1 rounded text-sm bg-red-100 text-red-700 hover:bg-red-200">
            Clear Logs
          </button>
        </div>
      </div>

      <form phx-change="filter" class="flex gap-3 mb-4 items-end">
        <div>
          <label class="block text-xs text-gray-500 mb-1">Level</label>
          <select name="level" class="border rounded px-2 py-1 text-sm">
            <option value="">All</option>
            <option value="error" selected={@filters.level == "error"}>Error</option>
            <option value="warning" selected={@filters.level == "warning"}>Warning</option>
            <option value="info" selected={@filters.level == "info"}>Info</option>
          </select>
        </div>
        <div>
          <label class="block text-xs text-gray-500 mb-1">Source</label>
          <select name="source" class="border rounded px-2 py-1 text-sm">
            <option value="">All</option>
            <option value="server" selected={@filters.source == "server"}>Server</option>
            <option value="client" selected={@filters.source == "client"}>Client</option>
          </select>
        </div>
        <div>
          <label class="block text-xs text-gray-500 mb-1">Category</label>
          <select name="category" class="border rounded px-2 py-1 text-sm">
            <option value="">All</option>
            <option value="logger" selected={@filters.category == "logger"}>Logger</option>
            <option value="api" selected={@filters.category == "api"}>API</option>
            <option value="economy" selected={@filters.category == "economy"}>Economy</option>
            <option value="config" selected={@filters.category == "config"}>Config</option>
            <option value="client" selected={@filters.category == "client"}>Client</option>
          </select>
        </div>
        <div>
          <label class="block text-xs text-gray-500 mb-1">Player UID</label>
          <input
            type="text"
            name="player_uid"
            value={@filters.player_uid || ""}
            placeholder="Prefix match..."
            class="border rounded px-2 py-1 text-sm w-48"
            phx-debounce="300"
          />
        </div>
      </form>

      <div class="text-sm text-gray-500 mb-2">{length(@entries)} entries</div>

      <div class="bg-white border rounded-lg overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-50 border-b">
            <tr>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-36">Time</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-20">Level</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-20">Source</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-24">Category</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-28">Player</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium">Message</th>
            </tr>
          </thead>
          <tbody class="divide-y">
            <%= for entry <- @entries do %>
              <tr class="hover:bg-gray-50">
                <td class="px-3 py-2 font-mono text-xs text-gray-500">
                  {format_time(entry.timestamp)}
                </td>
                <td class="px-3 py-2">
                  <span class={level_badge(entry.level)}>{entry.level}</span>
                </td>
                <td class="px-3 py-2">
                  <span class={source_badge(entry.source)}>{entry.source}</span>
                </td>
                <td class="px-3 py-2 text-gray-600">{entry.category}</td>
                <td class="px-3 py-2">
                  <%= if entry.player_uid do %>
                    <.link navigate={"/admin/players/#{entry.player_uid}"} class="text-blue-600 hover:underline font-mono text-xs">
                      {String.slice(entry.player_uid, 0..7)}
                    </.link>
                  <% else %>
                    <span class="text-gray-400">-</span>
                  <% end %>
                </td>
                <td class="px-3 py-2 text-gray-800 truncate max-w-md" title={entry.message}>
                  {entry.message}
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      </div>

      <%= if @entries == [] do %>
        <div class="text-center text-gray-400 py-12">No log entries yet.</div>
      <% end %>
    </div>
    """
  end

  defp format_time(%DateTime{} = dt) do
    ms = dt.microsecond |> elem(0) |> div(1000)
    Calendar.strftime(dt, "%H:%M:%S") <> "." <> String.pad_leading("#{ms}", 3, "0")
  end
  defp format_time(_), do: "-"

  defp level_badge(:error), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-red-100 text-red-700"
  defp level_badge(:warning), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-yellow-100 text-yellow-700"
  defp level_badge(:info), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-700"
  defp level_badge(_), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-700"

  defp source_badge(:server), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-purple-100 text-purple-700"
  defp source_badge(:client), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-teal-100 text-teal-700"
  defp source_badge(_), do: "px-1.5 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-700"
end
