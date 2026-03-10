# Server Push via Phoenix Channels

## Goal

Replace client-side `Update()` polling with server-pushed events over Phoenix Channels (WebSocket). The server decides when events happen and pushes them to connected clients in real time.

## Scope

### Events moving to server push (Category A — server-decided)

| Event | Current behavior | New behavior |
|-------|-----------------|--------------|
| Bird spawn | Client polls `GameService.CheckBirds()` hourly in `BirdManager.Update()` | Server computes hourly, pushes `bird:spawned` |
| Visitor arrival | Client checks time + fetches at 10 PM local in `VisitorManager.Update()` | Server schedules at 10 PM player-local, pushes `visitor:arrived` |
| Visitor departure | Client checks time in `VisitorManager.Update()` | Server schedules end of visitor window, pushes `visitor:departed` |
| Quest completion | Client checks timer every frame in `MallumManager.Update()`, then calls `GameService.CheckQuest()` | Server schedules timer expiry, pushes `quest:completed` |
| Vase fill completion | Client checks timer every frame in `VaseManager.Update()`, then calls `GameService.CheckVase()` | Server schedules timer expiry, pushes `vase:filled` |
| Gift received | Not yet implemented | Server pushes `gift:received` when another player sends a gift |

### Stays as local timers (Category B — client-local)

- **Mana accumulation** — continuous formula-based, visual only
- **Plot growth progress** — visual countdown, server confirms on harvest
- **Garden yield timers** — visual countdown, server confirms on collect

## Architecture

### Server: Phoenix Channel

**`PlayerChannel`** — one channel per authenticated player, topic `"player:{uid}"`.

#### Join flow

1. Client connects WebSocket to `/socket/player`
2. Sends `phx_join` with `{token: "<bearer_token>"}`
3. Channel authenticates token (same logic as REST auth plug)
4. On successful join, channel pushes `"sync"` event with current state:
   ```json
   {
     "visitor": { ... } | null,
     "birds": [ ... ],
     "quests": [ { "mallum_id": 1, "remaining_seconds": 3600 } ],
     "vases": [ { "vase_id": 1, "remaining_seconds": 600 } ]
   }
   ```
5. Channel schedules all pending timers using `Process.send_after`

#### Scheduled events

Each channel process (one per connected player) schedules timer messages:

- **Quest completion**: `Process.send_after(self(), {:quest_complete, mallum_id}, remaining_ms)`
- **Vase fill**: `Process.send_after(self(), {:vase_filled, vase_id}, remaining_ms)`
- **Visitor arrival**: `Process.send_after(self(), :visitor_arrive, ms_until_10pm_local)`
- **Visitor departure**: `Process.send_after(self(), :visitor_depart, ms_until_end_of_window)`
- **Bird check**: `Process.send_after(self(), :bird_check, ms_until_next_hour)`

When a `handle_info` fires:
1. Execute the server-side logic (e.g., roll visitor, complete quest, spawn bird)
2. Push the result to the client
3. Schedule the next occurrence if recurring (bird check → next hour)

#### Player timezone

Derived from stored `lat`/`lon` on the economy record. UTC offset computed from longitude: `offset_hours = round(lon / 15)`. Used to determine when 10 PM local time is in UTC for visitor scheduling.

#### External triggers

When a REST endpoint mutates state that affects a connected player (e.g., starting a quest, sending a vase to fill), it broadcasts to the channel:

```elixir
CampFireWeb.Endpoint.broadcast("player:#{uid}", "quest:started", %{mallum_id: id, duration_seconds: dur})
```

The channel's `handle_info` for this broadcast schedules the completion timer.

### Server: Socket & Routing

**`PlayerSocket`** — new socket module at `/socket/player`.

```elixir
# endpoint.ex
socket "/socket/player", CampFireWeb.PlayerSocket,
  websocket: [connect_info: [:peer_data]]

# player_socket.ex
defmodule CampFireWeb.PlayerSocket do
  use Phoenix.Socket
  channel "player:*", CampFireWeb.PlayerChannel

  def connect(%{"token" => token}, socket, _connect_info) do
    case verify_token(token) do
      {:ok, uid} -> {:ok, assign(socket, :uid, uid)}
      :error -> :error
    end
  end

  def id(socket), do: "player:#{socket.assigns.uid}"
end
```

### Client: ChannelService

**`ChannelService`** — new MonoBehaviour singleton in `Assets/Scripts/Services/`.

#### Responsibilities

1. Establish WebSocket connection to `ws(s)://{server}/socket/player/websocket`
2. Implement Phoenix Channel protocol: join, heartbeat (30s), push/receive
3. Auto-reconnect with exponential backoff (1s, 2s, 4s, 8s, 16s, 30s cap)
4. Parse incoming events and fire C# events
5. Re-join channel on reconnect (server re-sends sync state)

#### Implementation

Uses `System.Net.WebSockets.ClientWebSocket` (built-in, no external dependency).

Thin wrapper (~150 lines) that handles:
- Phoenix message format: `[join_ref, ref, topic, event, payload]`
- Heartbeat: send `["null", ref, "phoenix", "heartbeat", {}]` every 30s
- Message routing: parse event string, deserialize payload, invoke callback

#### Public API

```csharp
public class ChannelService : MonoBehaviour
{
    public static ChannelService Instance { get; private set; }

    public bool IsConnected { get; }

    // Events that managers subscribe to
    public event Action<VisitorPayload> OnVisitorArrived;
    public event Action OnVisitorDeparted;
    public event Action<BirdPayload> OnBirdSpawned;
    public event Action<int> OnQuestCompleted;      // mallum server ID
    public event Action<int> OnVaseFilled;           // vase server ID
    public event Action<GiftPayload> OnGiftReceived;
    public event Action<SyncPayload> OnSyncReceived;
}
```

#### Connection lifecycle

1. `ChannelService` waits for `SocialService.IsSignedIn` and `GameService.IsOnline`
2. Opens WebSocket with token in query params
3. Sends `phx_join` to `"player:{uid}"`
4. On join reply, processes `"sync"` payload
5. Listens for pushed events indefinitely
6. On disconnect: fires reconnect coroutine with backoff
7. On reconnect: re-joins, server sends fresh sync (catches up on missed events)

### Manager Changes

#### VisitorManager

- Remove `Update()` departure/arrival polling
- Subscribe to `ChannelService.OnVisitorArrived` and `OnVisitorDeparted`
- `OnVisitorArrived` callback: save visitor to `SaveData`, fire UI event
- `OnVisitorDeparted` callback: clear visitor from `SaveData`, fire UI event
- Keep `DebugKeepVisitor` flag (ignores `OnVisitorDeparted` when set)

#### BirdManager

- Remove server polling from `Update()`
- Subscribe to `ChannelService.OnBirdSpawned`
- Callback: add bird to `SaveData`, fire UI event
- Keep local fallback logic but only for offline edge cases (shouldn't happen since app is online-only)

#### MallumManager

- Remove quest timer checking from `Update()`
- Remove `GameService.CheckQuest()` calls
- Subscribe to `ChannelService.OnQuestCompleted`
- Callback: transition mallum to `QuestComplete`, roll rewards, fire UI event

#### VaseManager

- Remove fill timer checking from `Update()`
- Remove `GameService.CheckVase()` calls
- Subscribe to `ChannelService.OnVaseFilled`
- Callback: set vase state to `Full`, update water count, fire UI event

### Protocol Details

#### Message format (Phoenix standard)

```
Outgoing: [join_ref, ref, topic, event, payload]
Incoming: [join_ref, ref, topic, event, payload]
```

#### Events

| Direction | Event | Payload |
|-----------|-------|---------|
| Client → Server | `phx_join` | `{token: "..."}` |
| Server → Client | `phx_reply` | `{status: "ok", response: {sync: ...}}` |
| Server → Client | `visitor:arrived` | `{visitor_id, name, portrait_id, type, dialogue, ...}` |
| Server → Client | `visitor:departed` | `{}` |
| Server → Client | `bird:spawned` | `{bird_id, grid_x, grid_y, seed_name, seed_count}` |
| Server → Client | `quest:completed` | `{mallum_id, rewards: [...]}` |
| Server → Client | `vase:filled` | `{vase_id, current_water, capacity}` |
| Server → Client | `gift:received` | `{from_name, gift_type, gift_name, amount}` |
| Client → Server | `heartbeat` | `{}` |

### Error Handling

- **WebSocket disconnect**: Auto-reconnect with exponential backoff (1s → 30s cap)
- **Join failure** (bad token): Log error, do not retry (token refresh needed)
- **Missed events during disconnect**: Server sends full sync on rejoin — client replaces local state
- **Channel crash**: Phoenix auto-restarts channel process; client reconnects

### Testing

- **Server**: ExUnit tests for `PlayerChannel` — test join auth, sync payload, timer scheduling, event broadcasting
- **Client**: EditMode tests for message parsing and event routing (mock WebSocket)
- **Integration**: Manual testing via debug panel — connect indicator showing channel status

## Files

### New files

| File | Purpose |
|------|---------|
| `server/lib/camp_fire_web/channels/player_socket.ex` | Socket module, token auth |
| `server/lib/camp_fire_web/channels/player_channel.ex` | Channel logic, timers, event push |
| `server/lib/camp_fire/game/player_timezone.ex` | Longitude → UTC offset helper |
| `Assets/Scripts/Services/ChannelService.cs` | WebSocket client, Phoenix protocol, C# events |

### Modified files

| File | Change |
|------|--------|
| `server/lib/camp_fire_web/endpoint.ex` | Add player socket route |
| `server/lib/camp_fire_web/controllers/game_controller.ex` | Broadcast to channel on quest start, vase fill start |
| `Assets/Scripts/Managers/VisitorManager.cs` | Remove Update() polling, subscribe to channel events |
| `Assets/Scripts/Managers/BirdManager.cs` | Remove Update() server polling, subscribe to channel events |
| `Assets/Scripts/Managers/MallumManager.cs` | Remove quest timer polling, subscribe to channel events |
| `Assets/Scripts/Managers/VaseManager.cs` | Remove fill timer polling, subscribe to channel events |

### Deleted code (no new files needed)

- `GameService.CheckBirds()` — replaced by push
- `GameService.CheckQuest()` — replaced by push
- `GameService.CheckVase()` — replaced by push
- Bird hourly polling coroutine in `BirdManager`
- Visitor fetch logic in `VisitorManager.Update()`
