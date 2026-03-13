# Debug Log System Design

**Date:** 2026-03-13

## Summary

A unified real-time debug logging system visible in the Phoenix admin dashboard. Captures both server-side errors (via Elixir Logger backend + explicit instrumentation) and client-reported errors (via dedicated API endpoint). Stored in an ETS ring buffer, streamed to the admin UI via PubSub, filterable by user/level/source.

## Architecture

### Components

1. **`CampFire.DebugLog`** — Core GenServer owning an ETS ring buffer (~1000 entries). Provides `log/1` to insert entries and `list/1` to query with filters. Broadcasts each new entry via `Phoenix.PubSub` on topic `"debug_log"`.

2. **`CampFire.DebugLog.Entry`** — Struct representing a single log entry:
   - `id` — monotonic integer (insertion order)
   - `timestamp` — UTC datetime
   - `level` — `:error | :warning | :info`
   - `source` — `:server | :client`
   - `category` — string, e.g. `"api"`, `"economy"`, `"config"`, `"logger"`, `"client"`
   - `player_uid` — string or nil (nil for system-level logs)
   - `message` — human-readable summary
   - `metadata` — map of arbitrary extra data (request path, stacktrace, etc.)

3. **`CampFire.DebugLog.LoggerBackend`** — Custom Elixir Logger backend. Forwards all `warning` and `error` level Logger messages into `DebugLog.log/1` with `source: :server, category: "logger"`. Extracts `player_uid` from Logger metadata if present.

4. **Explicit instrumentation** — Key code paths annotated to log structured entries with player context:
   - API Plug/middleware: logs 4xx/5xx responses with player UID from conn assigns, `category: "api"`
   - Economy context: logs transaction failures with player UID, `category: "economy"`
   - ConfigCache: logs refresh failures, `category: "config"`

5. **`POST /api/debug/log`** — Authenticated endpoint for Unity client to report errors. Accepts JSON body:
   ```json
   {
     "level": "error",
     "message": "Failed to deserialize config response",
     "category": "client",
     "metadata": {"endpoint": "/api/game/configs", "status_code": 200, "error": "..."}
   }
   ```
   Player UID extracted from bearer token auth (already on conn). Rate-limited to prevent flooding.

6. **`CampFireWeb.Live.LogsLive`** — Admin LiveView page at `/admin/logs`. Subscribes to `"debug_log"` PubSub topic on mount. Shows a scrolling log table with:
   - Timestamp, level (color-coded), source badge, category, player UID (clickable link to player page), message
   - Expandable row for metadata details
   - Filter controls: level dropdown, source dropdown, player UID text input, category dropdown
   - Filters applied both to incoming stream and to initial ETS query
   - Auto-scroll to bottom (with pause when user scrolls up)
   - Clear filters button

### Data Flow

```
Server error paths ──→ Logger.error/warning ──→ LoggerBackend ──→ DebugLog.log()
                                                                        │
Explicit instrumentation (API, Economy, Config) ────────────────→ DebugLog.log()
                                                                        │
Unity client ──→ POST /api/debug/log ──→ DebugLogController ───→ DebugLog.log()
                                                                        │
                                                                        ▼
                                                              ETS ring buffer
                                                                        │
                                                              PubSub broadcast
                                                                        │
                                                                        ▼
                                                              LogsLive (admin UI)
```

### ETS Ring Buffer

- Named table `:debug_log_buffer`
- Keys are monotonic integers from a counter in GenServer state
- On insert: if size > 1000, delete oldest entry (key = current - 1000)
- `list/1` does `ets.tab2list` + sort + filter in-process (1000 entries is trivial)

### Filter Behavior

Filters are applied as an AND combination. Empty filter fields match everything. The player UID filter does a prefix match so you can type partial UIDs.

Real-time entries that don't match current filters are silently dropped (not shown). Changing filters re-queries ETS to backfill matching historical entries.

## Unity Client Side

A lightweight `DebugLogService` singleton that:
- Catches failed HTTP responses from `GameService`/`EconomyService` and posts them to `/api/debug/log`
- Exposes `DebugLogService.LogError(message, category, metadata)` for manual reporting
- Batches or debounces to avoid flooding (max 1 request per 5 seconds, queues intermediate entries)

## Out of Scope

- Log persistence across server restarts (ETS is ephemeral)
- Log export/download
- Alerting/notifications
- Audit trail for admin actions (separate concern)

## File Locations

- `server/lib/camp_fire/debug_log.ex` — GenServer + ETS buffer
- `server/lib/camp_fire/debug_log/entry.ex` — Entry struct
- `server/lib/camp_fire/debug_log/logger_backend.ex` — Logger backend
- `server/lib/camp_fire_web/live/logs_live.ex` — Admin LiveView
- `server/lib/camp_fire_web/controllers/debug_log_controller.ex` — Client endpoint
- `Assets/Scripts/Services/DebugLogService.cs` — Unity client service
