# Debug Log System Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a unified real-time debug logging system to the admin dashboard, capturing server-side and client-reported errors in an ETS ring buffer, streamed via PubSub to a filterable LiveView page.

**Architecture:** GenServer owns an ETS ring buffer (1000 entries). A custom Logger backend + explicit API instrumentation feed server errors. A new `/debug/log` endpoint accepts client reports. PubSub broadcasts every entry to the admin LiveView which renders a real-time, filterable log table.

**Tech Stack:** Elixir/Phoenix LiveView, ETS, Phoenix.PubSub, Tailwind CSS, C# Unity

**Spec:** `docs/superpowers/specs/2026-03-13-debug-log-system-design.md`

---

## File Structure

### New Files (Server)
- `server/lib/camp_fire/debug_log.ex` — GenServer + ETS ring buffer + Entry struct + public API
- `server/lib/camp_fire/debug_log/logger_backend.ex` — Custom Elixir Logger backend
- `server/lib/camp_fire_web/live/logs_live.ex` — Admin LiveView page

### Modified Files (Server)
- `server/lib/camp_fire/application.ex` — Add DebugLog to supervision tree (line 13, before ConfigCache)
- `server/config/config.exs` — Add Logger backend config (after line 28)
- `server/lib/camp_fire_web/router.ex` — Add `/debug/log` route (line 177) + `/admin/logs` route (line 53)
- `server/lib/camp_fire_web/controllers/debug_controller.ex` — Add `log` action
- `server/lib/camp_fire_web/components/layouts/admin.html.heex` — Add "Logs" sidebar entry (line 12)

### Modified Files (Unity)
- `Assets/Scripts/Services/DebugService.cs` — Add `LogRemoteError` method + queue + flush logic

---

## Task 1: DebugLog GenServer + Entry struct

**Files:**
- Create: `server/lib/camp_fire/debug_log.ex`

- [ ] **Step 1: Create the DebugLog module**

Create `server/lib/camp_fire/debug_log.ex` with:

```elixir
defmodule CampFire.DebugLog do
  use GenServer

  @table :debug_log_buffer
  @max_entries 1000

  # --- Entry struct ---

  defmodule Entry do
    @enforce_keys [:id, :timestamp, :level, :source, :category, :message]
    defstruct [:id, :timestamp, :level, :source, :category, :message, :player_uid, :metadata]
  end

  # --- Public API ---

  def start_link(_opts) do
    GenServer.start_link(__MODULE__, [], name: __MODULE__)
  end

  @doc "Insert a log entry. Fields: level, source, category, message, player_uid (optional), metadata (optional)"
  def log(attrs) when is_map(attrs) do
    GenServer.cast(__MODULE__, {:log, attrs})
  end

  @doc "Query entries. Options: level, source, category, player_uid (prefix match)"
  def list(opts \\ %{}) do
    @table
    |> :ets.tab2list()
    |> Enum.map(fn {_id, entry} -> entry end)
    |> Enum.filter(&matches?(&1, opts))
    |> Enum.sort_by(& &1.id)
  end

  # --- GenServer callbacks ---

  @impl true
  def init(_) do
    table = :ets.new(@table, [:named_table, :set, :public, read_concurrency: true])
    {:ok, %{table: table, counter: 0}}
  end

  @impl true
  def handle_cast({:log, attrs}, %{counter: counter} = state) do
    id = counter + 1

    entry = %Entry{
      id: id,
      timestamp: DateTime.utc_now(),
      level: attrs[:level] || attrs["level"] || :info,
      source: attrs[:source] || attrs["source"] || :server,
      category: attrs[:category] || attrs["category"] || "general",
      message: attrs[:message] || attrs["message"] || "",
      player_uid: attrs[:player_uid] || attrs["player_uid"],
      metadata: attrs[:metadata] || attrs["metadata"] || %{}
    }

    :ets.insert(@table, {id, entry})

    # Evict oldest if over capacity
    if id > @max_entries do
      :ets.delete(@table, id - @max_entries)
    end

    # Broadcast to subscribers
    Phoenix.PubSub.broadcast(CampFire.PubSub, "debug_log", {:new_log_entry, entry})

    {:noreply, %{state | counter: id}}
  end

  # --- Private ---

  defp matches?(entry, opts) do
    level_match?(entry, opts) and
      source_match?(entry, opts) and
      category_match?(entry, opts) and
      player_match?(entry, opts)
  end

  defp level_match?(_entry, %{level: nil}), do: true
  defp level_match?(_entry, %{level: ""}), do: true
  defp level_match?(entry, %{level: level}), do: to_string(entry.level) == to_string(level)
  defp level_match?(_entry, _), do: true

  defp source_match?(_entry, %{source: nil}), do: true
  defp source_match?(_entry, %{source: ""}), do: true
  defp source_match?(entry, %{source: source}), do: to_string(entry.source) == to_string(source)
  defp source_match?(_entry, _), do: true

  defp category_match?(_entry, %{category: nil}), do: true
  defp category_match?(_entry, %{category: ""}), do: true
  defp category_match?(entry, %{category: cat}), do: entry.category == cat
  defp category_match?(_entry, _), do: true

  defp player_match?(_entry, %{player_uid: nil}), do: true
  defp player_match?(_entry, %{player_uid: ""}), do: true
  defp player_match?(entry, %{player_uid: uid}) do
    entry.player_uid != nil and String.starts_with?(entry.player_uid, uid)
  end
  defp player_match?(_entry, _), do: true
end
```

- [ ] **Step 2: Add to supervision tree**

In `server/lib/camp_fire/application.ex`, add `CampFire.DebugLog` before `CampFire.ConfigCache` (line 13):

```elixir
    children = [
      CampFireWeb.Telemetry,
      CampFire.Repo,
      CampFire.DebugLog,        # <-- add this line
      CampFire.ConfigCache,
```

- [ ] **Step 3: Verify server compiles**

Run: `cd server && mix compile --warnings-as-errors`
Expected: Compiles with no errors

- [ ] **Step 4: Commit**

```
git add server/lib/camp_fire/debug_log.ex server/lib/camp_fire/application.ex
git commit -m "feat(admin): add DebugLog GenServer with ETS ring buffer"
```

---

## Task 2: Logger Backend

**Files:**
- Create: `server/lib/camp_fire/debug_log/logger_backend.ex`
- Modify: `server/config/config.exs` (after line 28)

- [ ] **Step 1: Create the Logger backend module**

Create `server/lib/camp_fire/debug_log/logger_backend.ex`:

```elixir
defmodule CampFire.DebugLog.LoggerBackend do
  @behaviour :gen_event

  @impl true
  def init(_) do
    {:ok, %{level: :warning}}
  end

  @impl true
  def handle_event({level, _gl, {Logger, message, _timestamp, metadata}}, state)
      when level in [:warning, :error] do
    # Don't re-log our own broadcasts
    unless metadata[:debug_log_skip] do
      CampFire.DebugLog.log(%{
        level: level,
        source: :server,
        category: "logger",
        message: IO.iodata_to_binary(message),
        player_uid: metadata[:player_uid],
        metadata: %{
          module: metadata[:module] |> inspect(),
          function: metadata[:function],
          file: metadata[:file],
          line: metadata[:line]
        }
      })
    end

    {:ok, state}
  end

  @impl true
  def handle_event(_event, state), do: {:ok, state}

  @impl true
  def handle_call({:configure, _opts}, state), do: {:ok, :ok, state}

  @impl true
  def handle_info(_msg, state), do: {:ok, state}

  @impl true
  def code_change(_old, state, _extra), do: {:ok, state}

  @impl true
  def terminate(_reason, _state), do: :ok
end
```

- [ ] **Step 2: Add Logger backend to config**

In `server/config/config.exs`, after line 28 (`metadata: [:request_id]`), add:

```elixir
config :logger, backends: [:console, CampFire.DebugLog.LoggerBackend]
```

- [ ] **Step 3: Verify server compiles**

Run: `cd server && mix compile --warnings-as-errors`
Expected: Compiles with no errors

- [ ] **Step 4: Commit**

```
git add server/lib/camp_fire/debug_log/logger_backend.ex server/config/config.exs
git commit -m "feat(admin): add Logger backend to capture warnings/errors into DebugLog"
```

---

## Task 3: Client log endpoint on DebugController

**Files:**
- Modify: `server/lib/camp_fire_web/controllers/debug_controller.ex` (add `log` action at end)
- Modify: `server/lib/camp_fire_web/router.ex` (add route at line 177)

- [ ] **Step 1: Add `log` action to DebugController**

In `server/lib/camp_fire_web/controllers/debug_controller.ex`, add before the final `end`:

```elixir
  def log(conn, %{"message" => message} = params) do
    uid = conn.assigns.current_player.uid

    level =
      case params["level"] do
        "warning" -> :warning
        "info" -> :info
        _ -> :error
      end

    CampFire.DebugLog.log(%{
      level: level,
      source: :client,
      category: params["category"] || "client",
      message: message,
      player_uid: uid,
      metadata: params["metadata"] || %{}
    })

    conn |> put_status(200) |> json(%{ok: true})
  end

  def log(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'message'"})
```

- [ ] **Step 2: Add route**

In `server/lib/camp_fire_web/router.ex`, add inside the `/debug` scope (after line 176):

```elixir
    post "/log", DebugController, :log
```

- [ ] **Step 3: Verify server compiles**

Run: `cd server && mix compile --warnings-as-errors`
Expected: Compiles with no errors

- [ ] **Step 4: Commit**

```
git add server/lib/camp_fire_web/controllers/debug_controller.ex server/lib/camp_fire_web/router.ex
git commit -m "feat(admin): add POST /debug/log endpoint for client error reporting"
```

---

## Task 4: Admin LiveView page (LogsLive)

**Files:**
- Create: `server/lib/camp_fire_web/live/logs_live.ex`
- Modify: `server/lib/camp_fire_web/router.ex` (add admin route, line 53)
- Modify: `server/lib/camp_fire_web/components/layouts/admin.html.heex` (add sidebar entry, line 12)

- [ ] **Step 1: Create LogsLive module**

Create `server/lib/camp_fire_web/live/logs_live.ex`:

```elixir
defmodule CampFireWeb.LogsLive do
  use CampFireWeb, :live_view

  alias CampFire.DebugLog

  def mount(_params, _session, socket) do
    if connected?(socket) do
      Phoenix.PubSub.subscribe(CampFire.PubSub, "debug_log")
    end

    filters = %{level: nil, source: nil, category: nil, player_uid: nil}
    entries = DebugLog.list(filters)

    {:ok,
     assign(socket,
       active_tab: :logs,
       filters: filters,
       entries: entries,
       paused: false
     )}
  end

  def handle_info({:new_log_entry, entry}, socket) do
    if matches_filters?(entry, socket.assigns.filters) and not socket.assigns.paused do
      entries = socket.assigns.entries ++ [entry]
      # Keep client-side list bounded too
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

    entries = DebugLog.list(filters)
    {:noreply, assign(socket, filters: filters, entries: entries)}
  end

  def handle_event("clear_filters", _params, socket) do
    filters = %{level: nil, source: nil, category: nil, player_uid: nil}
    entries = DebugLog.list(filters)
    {:noreply, assign(socket, filters: filters, entries: entries)}
  end

  def handle_event("toggle_pause", _params, socket) do
    {:noreply, assign(socket, paused: not socket.assigns.paused)}
  end

  defp blank_to_nil(""), do: nil
  defp blank_to_nil(nil), do: nil
  defp blank_to_nil(v), do: v

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
        </div>
      </div>

      <!-- Filters -->
      <form phx-change="filter" class="flex gap-3 mb-4 items-end">
        <div>
          <label class="block text-xs text-gray-500 mb-1">Level</label>
          <select name="level" class="border rounded px-2 py-1 text-sm" value={@filters.level || ""}>
            <option value="">All</option>
            <option value="error" selected={@filters.level == "error"}>Error</option>
            <option value="warning" selected={@filters.level == "warning"}>Warning</option>
            <option value="info" selected={@filters.level == "info"}>Info</option>
          </select>
        </div>
        <div>
          <label class="block text-xs text-gray-500 mb-1">Source</label>
          <select name="source" class="border rounded px-2 py-1 text-sm" value={@filters.source || ""}>
            <option value="">All</option>
            <option value="server" selected={@filters.source == "server"}>Server</option>
            <option value="client" selected={@filters.source == "client"}>Client</option>
          </select>
        </div>
        <div>
          <label class="block text-xs text-gray-500 mb-1">Category</label>
          <select name="category" class="border rounded px-2 py-1 text-sm" value={@filters.category || ""}>
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

      <!-- Entry count -->
      <div class="text-sm text-gray-500 mb-2">{length(@entries)} entries</div>

      <!-- Log table -->
      <div class="bg-white border rounded-lg overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-50 border-b">
            <tr>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-40">Time</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-20">Level</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-20">Source</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-24">Category</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium w-32">Player</th>
              <th class="text-left px-3 py-2 text-gray-500 font-medium">Message</th>
            </tr>
          </thead>
          <tbody id="log-entries" phx-update="stream" class="divide-y">
            <%= for entry <- @entries do %>
              <tr id={"entry-#{entry.id}"} class="hover:bg-gray-50">
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
    Calendar.strftime(dt, "%H:%M:%S.") <> String.pad_leading("#{dt.microsecond |> elem(0) |> div(1000)}", 3, "0")
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
```

- [ ] **Step 2: Add admin route**

In `server/lib/camp_fire_web/router.ex`, add inside the admin auth scope (after line 53, the sprites line):

```elixir
    live "/logs", LogsLive, :index
```

- [ ] **Step 3: Add sidebar entry**

In `server/lib/camp_fire_web/components/layouts/admin.html.heex`, add after the Sprites link (line 12):

```heex
    <.link navigate="/admin/logs" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :logs, do: "bg-gray-800 text-white", else: ""}"}>Logs</.link>
```

- [ ] **Step 4: Verify server compiles**

Run: `cd server && mix compile --warnings-as-errors`
Expected: Compiles with no errors

- [ ] **Step 5: Commit**

```
git add server/lib/camp_fire_web/live/logs_live.ex server/lib/camp_fire_web/router.ex server/lib/camp_fire_web/components/layouts/admin.html.heex
git commit -m "feat(admin): add real-time debug logs LiveView page with filters"
```

---

## Task 5: Explicit API error instrumentation

**Files:**
- Create: `server/lib/camp_fire_web/plugs/debug_log_errors.ex`
- Modify: `server/lib/camp_fire_web/router.ex` (add plug to pipelines)

- [ ] **Step 1: Create error-logging Plug**

Create `server/lib/camp_fire_web/plugs/debug_log_errors.ex`:

```elixir
defmodule CampFireWeb.Plugs.DebugLogErrors do
  @behaviour Plug

  import Plug.Conn

  @impl true
  def init(opts), do: opts

  @impl true
  def call(conn, _opts) do
    register_before_send(conn, fn conn ->
      if conn.status >= 400 do
        player_uid =
          case conn.assigns do
            %{current_player: %{uid: uid}} -> uid
            _ -> nil
          end

        level = if conn.status >= 500, do: :error, else: :warning

        CampFire.DebugLog.log(%{
          level: level,
          source: :server,
          category: "api",
          message: "#{conn.method} #{conn.request_path} → #{conn.status}",
          player_uid: player_uid,
          metadata: %{
            status: conn.status,
            method: conn.method,
            path: conn.request_path,
            params: inspect(conn.params, limit: 200)
          }
        })
      end

      conn
    end)
  end
end
```

- [ ] **Step 2: Add plug to API pipeline**

In `server/lib/camp_fire_web/router.ex`, add the plug inside the `:api` pipeline (after line 15):

```elixir
    plug CampFireWeb.Plugs.DebugLogErrors
```

- [ ] **Step 3: Verify server compiles**

Run: `cd server && mix compile --warnings-as-errors`
Expected: Compiles with no errors

- [ ] **Step 4: Commit**

```
git add server/lib/camp_fire_web/plugs/debug_log_errors.ex server/lib/camp_fire_web/router.ex
git commit -m "feat(admin): add API error instrumentation plug for DebugLog"
```

---

## Task 6: Unity client remote error reporting

**Files:**
- Modify: `Assets/Scripts/Services/DebugService.cs`

- [ ] **Step 1: Add remote logging to DebugService**

In `Assets/Scripts/Services/DebugService.cs`, add after the `Awake()` method (line 19) and before `SkipTime`:

```csharp
        // --- Remote Debug Logging ---
        private static readonly float FlushIntervalSeconds = 5f;
        private static readonly int MaxQueueSize = 50;

        private readonly System.Collections.Generic.List<RemoteLogEntry> _logQueue = new();
        private float _lastFlushTime;

        [Serializable]
        private class RemoteLogEntry
        {
            public string level;
            public string message;
            public string category;
            public string metadata;
        }

        [Serializable]
        private class RemoteLogBatch
        {
            public string level;
            public string message;
            public string category;
            public string metadata;
        }

        public void LogRemoteError(string message, string category = "client", Dictionary<string, string> metadata = null)
        {
            QueueRemoteLog("error", message, category, metadata);
        }

        public void LogRemoteWarning(string message, string category = "client", Dictionary<string, string> metadata = null)
        {
            QueueRemoteLog("warning", message, category, metadata);
        }

        private void QueueRemoteLog(string level, string message, string category, Dictionary<string, string> metadata)
        {
            if (_logQueue.Count >= MaxQueueSize)
                _logQueue.RemoveAt(0);

            var metaJson = "{}";
            if (metadata != null && metadata.Count > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var kv in metadata)
                    parts.Add($"\"{kv.Key}\":\"{kv.Value.Replace("\"", "\\\"")}\"");
                metaJson = "{" + string.Join(",", parts) + "}";
            }

            _logQueue.Add(new RemoteLogEntry
            {
                level = level,
                message = message,
                category = category,
                metadata = metaJson
            });
        }

        private void Update()
        {
            if (_logQueue.Count > 0 && Time.realtimeSinceStartup - _lastFlushTime >= FlushIntervalSeconds)
            {
                _lastFlushTime = Time.realtimeSinceStartup;
                FlushLogQueue();
            }
        }

        private async void FlushLogQueue()
        {
            // Drain queue
            var batch = new System.Collections.Generic.List<RemoteLogEntry>(_logQueue);
            _logQueue.Clear();

            foreach (var entry in batch)
            {
                var json = $"{{\"level\":\"{entry.level}\",\"message\":\"{entry.message.Replace("\"", "\\\"")}\",\"category\":\"{entry.category}\",\"metadata\":{entry.metadata}}}";
                await PostQuiet("/debug/log", json);
            }
        }
```

Also add `using System.Collections.Generic;` to the top imports if not already present.

- [ ] **Step 2: Verify Unity compiles**

Check Unity console for compilation errors (use MCP `read_console` or check in editor).

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/Services/DebugService.cs
git commit -m "feat(debug): add remote error reporting to DebugService for admin log dashboard"
```

---

## Task 7: Integration smoke test

- [ ] **Step 1: Start the server and verify the logs page loads**

Run: `cd server && mix phx.server`

Navigate to `http://localhost:4000/admin/logs` (after logging in). Verify:
- Page loads with empty log table
- Filters are visible
- "Live" button shows green
- "Logs" sidebar entry is highlighted

- [ ] **Step 2: Trigger a test log entry**

From a terminal, send a client log:
```bash
curl -X POST http://localhost:4000/debug/log \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <test-token>" \
  -d '{"level":"error","message":"Test error from curl","category":"client","metadata":{"test":true}}'
```

Verify the entry appears in real-time on the logs page.

- [ ] **Step 3: Test Logger backend**

Trigger a Logger.warning somewhere (e.g., invalid config cache refresh) and verify it appears in the logs page with source "server" and category "logger".

- [ ] **Step 4: Test filters**

Verify level, source, category, and player UID filters work correctly. Verify "Clear Filters" resets all.
