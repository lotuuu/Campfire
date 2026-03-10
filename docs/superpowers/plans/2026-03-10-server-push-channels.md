# Server Push via Phoenix Channels — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace client-side `Update()` polling with server-pushed events over Phoenix Channels (WebSocket) for birds, visitors, quests, and vases.

**Architecture:** Server creates a `PlayerChannel` per connected player. The channel schedules timers via `Process.send_after` and pushes events when they fire. The Unity client connects via `ClientWebSocket`, implements the Phoenix Channel protocol, and routes events to manager callbacks. Managers drop their `Update()` polling loops and subscribe to channel events instead.

**Tech Stack:** Elixir/Phoenix Channels (server), `System.Net.WebSockets.ClientWebSocket` (client), Phoenix wire protocol JSON arrays.

**Spec:** `docs/superpowers/specs/2026-03-10-server-push-channels-design.md`

---

## File Structure

### New Server Files

| File | Responsibility |
|------|---------------|
| `server/lib/camp_fire_web/channels/player_socket.ex` | WebSocket endpoint, token auth on connect |
| `server/lib/camp_fire_web/channels/player_channel.ex` | Channel join, sync push, timer scheduling, event broadcasting |
| `server/lib/camp_fire/game/player_timezone.ex` | `offset_hours_from_lon/1` — longitude → UTC offset |

### New Client Files

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Services/ChannelService.cs` | WebSocket connection, Phoenix protocol, reconnect, C# events |

### Modified Server Files

| File | Change |
|------|--------|
| `server/lib/camp_fire_web/endpoint.ex` | Add `socket "/socket/player"` route |
| `server/lib/camp_fire_web/controllers/game_controller.ex` | Broadcast to channel on quest start, vase fill start |

### Modified Client Files

| File | Change |
|------|--------|
| `Assets/Scripts/Managers/BirdManager.cs` | Remove `Update()` server polling, subscribe to channel |
| `Assets/Scripts/Managers/VaseManager.cs` | Remove `Update()` fill checking, subscribe to channel |
| `Assets/Scripts/Managers/MallumManager.cs` | Remove `Update()` quest/water polling, subscribe to channel |
| `Assets/Scripts/Managers/VisitorManager.cs` | Remove `Update()` arrival/departure polling, subscribe to channel |

### New Test Files

| File | Tests |
|------|-------|
| `server/test/camp_fire_web/channels/player_channel_test.exs` | Join auth, sync payload, timer events |
| `server/test/camp_fire/game/player_timezone_test.exs` | Longitude → offset conversion |

---

## Chunk 1: Server Infrastructure

### Task 1: PlayerSocket — WebSocket Endpoint with Token Auth

**Files:**
- Create: `server/lib/camp_fire_web/channels/player_socket.ex`
- Modify: `server/lib/camp_fire_web/endpoint.ex` (line 16, after LiveView socket)

**Context:** The existing auth plug at `server/lib/camp_fire_web/plugs/authenticate.ex` hashes tokens via SHA256 and calls `Accounts.get_player_by_token(token)` which returns a `Player` struct with a `.uid` field. We reuse this same function for socket auth. The endpoint currently only has a LiveView socket at `/live`.

- [ ] **Step 1: Create PlayerSocket module**

```elixir
# server/lib/camp_fire_web/channels/player_socket.ex
defmodule CampFireWeb.PlayerSocket do
  use Phoenix.Socket

  channel "player:*", CampFireWeb.PlayerChannel

  @impl true
  def connect(%{"token" => token}, socket, _connect_info) do
    case CampFire.Accounts.get_player_by_token(token) do
      %{uid: uid} -> {:ok, assign(socket, :uid, uid)}
      nil -> :error
    end
  end

  def connect(_params, _socket, _connect_info), do: :error

  @impl true
  def id(socket), do: "player:#{socket.assigns.uid}"
end
```

- [ ] **Step 2: Add socket route to endpoint.ex**

In `server/lib/camp_fire_web/endpoint.ex`, after the existing LiveView socket (line 16), add:

```elixir
socket "/socket/player", CampFireWeb.PlayerSocket,
  websocket: true
```

- [ ] **Step 3: Verify compilation**

Run: `cd server && mix compile`
Expected: Compiles with warnings about missing `CampFireWeb.PlayerChannel` (which we create next)

- [ ] **Step 4: Commit**

```bash
git add server/lib/camp_fire_web/channels/player_socket.ex server/lib/camp_fire_web/endpoint.ex
git commit -m "feat(channels): add PlayerSocket with token auth"
```

---

### Task 2: PlayerChannel — Join, Sync, and Timer Scheduling

**Files:**
- Create: `server/lib/camp_fire_web/channels/player_channel.ex`

**Context:** On join, the channel must:
1. Verify the player UID matches the topic
2. Load current game state (vases filling, mallums on quest, birds, visitor)
3. Push a `sync` event with current state
4. Schedule `Process.send_after` timers for all pending completions

The server game logic functions needed:
- `CampFire.Game.Vases` — vases have `state: "filling"`, `fill_start_time_utc`, `capacity`. Fill duration = `capacity * 60` seconds.
- `CampFire.Game.Mallums` — mallums have `state: "on_quest"`, `start_time_utc`, `assigned_quest_name`. Quest duration comes from config.
- `CampFire.Game.Birds` — `try_spawn_bird_public(uid, flame_level)` returns `{:ok, bird}` or `:no_tile`/`:no_seed`.
- `CampFire.Visitors` — `get_tonight_visitor(uid)` returns visitor payload or nil.
- `CampFire.Economy` — `get_economy(uid)` returns economy record with `lat`, `lon`, `flame_level`.
- `CampFire.Game.ConfigCache` — `get_quest_config(name)` returns quest config with `duration_minutes`.

Serializer functions exist in `game_controller.ex` but are `defp`. We'll extract shared serializers or inline the needed fields.

- [ ] **Step 1: Create PlayerChannel with join and sync**

```elixir
# server/lib/camp_fire_web/channels/player_channel.ex
defmodule CampFireWeb.PlayerChannel do
  use Phoenix.Channel

  alias CampFire.{Economy, Repo}
  alias CampFire.Game.{Vases, Mallums, Birds, ConfigCache}

  @bird_check_interval_ms :timer.hours(1)

  @impl true
  def join("player:" <> uid, _params, socket) do
    if socket.assigns.uid != uid do
      {:error, %{reason: "unauthorized"}}
    else
      send(self(), :after_join)
      {:ok, socket}
    end
  end

  @impl true
  def handle_info(:after_join, socket) do
    uid = socket.assigns.uid

    economy = Economy.get_economy(uid)
    vases = Vases.list_vases(uid)
    mallums = Mallums.list_mallums(uid)
    birds = Birds.list_birds(uid)

    # Load visitor state
    visitor = CampFire.Visitors.get_current_visitor(uid)

    # Push sync state
    push(socket, "sync", %{
      visitor: visitor,
      birds: Enum.map(birds, &serialize_bird/1),
      quests: build_quest_sync(mallums),
      vases: build_vase_sync(vases)
    })

    # Schedule timers for in-progress activities
    socket = schedule_vase_timers(socket, vases)
    socket = schedule_quest_timers(socket, mallums)
    socket = schedule_bird_check(socket)
    socket = schedule_visitor_check(socket, economy)

    {:noreply, socket}
  end

  # ── Vase fill completion ──

  @impl true
  def handle_info({:vase_filled, vase_id}, socket) do
    uid = socket.assigns.uid
    case Vases.check_fill(uid, vase_id) do
      {:ok, vase} when vase.state == "full" ->
        push(socket, "vase:filled", %{
          vase_id: vase.id,
          current_water: vase.current_water,
          capacity: vase.capacity
        })
      _ ->
        :ok
    end
    {:noreply, socket}
  end

  # ── Quest completion ──

  @impl true
  def handle_info({:quest_complete, mallum_id}, socket) do
    uid = socket.assigns.uid
    case Mallums.check_quest(uid, mallum_id) do
      {:ok, mallum} when mallum.state == "quest_complete" ->
        rewards = Enum.map(mallum.pending_rewards || [], fn r ->
          %{seed_name: r["seed_name"] || r[:seed_name], count: r["count"] || r[:count]}
        end)
        push(socket, "quest:completed", %{
          mallum_id: mallum.id,
          rewards: rewards
        })
      _ ->
        :ok
    end
    {:noreply, socket}
  end

  # ── Bird check (hourly) ──

  @impl true
  def handle_info(:bird_check, socket) do
    uid = socket.assigns.uid
    economy = Economy.get_economy(uid)

    if economy do
      case Birds.try_spawn_bird_public(uid, economy.flame_level) do
        {:ok, bird} ->
          push(socket, "bird:spawned", serialize_bird(bird))
        _ ->
          :ok
      end
    end

    # Schedule next check
    timer_ref = Process.send_after(self(), :bird_check, @bird_check_interval_ms)
    {:noreply, assign(socket, :bird_timer, timer_ref)}
  end

  # ── Visitor arrival (at 10 PM local) ──

  @impl true
  def handle_info(:visitor_arrive, socket) do
    uid = socket.assigns.uid

    case CampFire.Visitors.get_tonight_visitor(uid) do
      nil ->
        :ok
      visitor ->
        push(socket, "visitor:arrived", visitor)
    end

    # Schedule departure at end of visitor window (6 AM next day local)
    economy = Economy.get_economy(uid)
    socket = schedule_visitor_departure(socket, economy)
    {:noreply, socket}
  end

  @impl true
  def handle_info(:visitor_depart, socket) do
    push(socket, "visitor:departed", %{})
    # Schedule next arrival
    economy = Economy.get_economy(socket.assigns.uid)
    socket = schedule_visitor_arrival(socket, economy)
    {:noreply, socket}
  end

  # ── External broadcasts (from REST endpoints) ──

  @impl true
  def handle_info(%Phoenix.Socket.Broadcast{event: "quest:started", payload: payload}, socket) do
    # Schedule the completion timer for the newly started quest
    if payload[:mallum_id] && payload[:duration_seconds] do
      ms = max(payload[:duration_seconds] * 1000, 0)
      Process.send_after(self(), {:quest_complete, payload[:mallum_id]}, ms)
    end
    {:noreply, socket}
  end

  @impl true
  def handle_info(%Phoenix.Socket.Broadcast{event: "vase:started", payload: payload}, socket) do
    # Schedule the fill completion timer
    if payload[:vase_id] && payload[:duration_seconds] do
      ms = max(payload[:duration_seconds] * 1000, 0)
      Process.send_after(self(), {:vase_filled, payload[:vase_id]}, ms)
    end
    {:noreply, socket}
  end

  # ── Timer scheduling helpers ──

  defp schedule_vase_timers(socket, vases) do
    Enum.reduce(vases, socket, fn vase, acc ->
      if vase.state == "filling" && vase.fill_start_time_utc do
        elapsed_s = DateTime.diff(DateTime.utc_now(), vase.fill_start_time_utc, :second)
        total_s = vase.capacity * 60
        remaining_ms = max((total_s - elapsed_s) * 1000, 0)
        Process.send_after(self(), {:vase_filled, vase.id}, remaining_ms)
        acc
      else
        acc
      end
    end)
  end

  defp schedule_quest_timers(socket, mallums) do
    Enum.reduce(mallums, socket, fn mallum, acc ->
      if mallum.state == "on_quest" && mallum.start_time_utc && mallum.assigned_quest_name do
        case ConfigCache.get_quest_config(mallum.assigned_quest_name) do
          nil -> acc
          config ->
            elapsed_s = DateTime.diff(DateTime.utc_now(), mallum.start_time_utc, :second)
            total_s = config.duration_minutes * 60
            remaining_ms = max((total_s - elapsed_s) * 1000, 0)
            Process.send_after(self(), {:quest_complete, mallum.id}, remaining_ms)
            acc
        end
      else
        acc
      end
    end)
  end

  defp schedule_bird_check(socket) do
    # First check in a random interval 0–60 minutes to stagger across players
    initial_ms = :rand.uniform(@bird_check_interval_ms)
    timer_ref = Process.send_after(self(), :bird_check, initial_ms)
    assign(socket, :bird_timer, timer_ref)
  end

  defp schedule_visitor_check(socket, economy) do
    if economy && economy.lat && economy.lon do
      socket = schedule_visitor_arrival(socket, economy)
      socket
    else
      socket
    end
  end

  defp schedule_visitor_arrival(socket, economy) do
    if economy && economy.lon do
      offset_hours = CampFire.Game.PlayerTimezone.offset_hours_from_lon(economy.lon)
      now_utc = DateTime.utc_now()
      # 10 PM local = 22:00 local = 22:00 - offset in UTC
      today_10pm_utc = %{now_utc | hour: 0, minute: 0, second: 0}
                       |> DateTime.add(22 * 3600 - offset_hours * 3600, :second)

      target = if DateTime.compare(today_10pm_utc, now_utc) == :gt do
        today_10pm_utc
      else
        DateTime.add(today_10pm_utc, 86400, :second)
      end

      ms = max(DateTime.diff(target, now_utc, :millisecond), 0)
      timer_ref = Process.send_after(self(), :visitor_arrive, ms)
      assign(socket, :visitor_arrival_timer, timer_ref)
    else
      socket
    end
  end

  defp schedule_visitor_departure(socket, economy) do
    if economy && economy.lon do
      offset_hours = CampFire.Game.PlayerTimezone.offset_hours_from_lon(economy.lon)
      now_utc = DateTime.utc_now()
      # Departure at 6 AM next day local
      tomorrow_6am_utc = %{now_utc | hour: 0, minute: 0, second: 0}
                         |> DateTime.add(86400 + 6 * 3600 - offset_hours * 3600, :second)

      ms = max(DateTime.diff(tomorrow_6am_utc, now_utc, :millisecond), 0)
      timer_ref = Process.send_after(self(), :visitor_depart, ms)
      assign(socket, :visitor_depart_timer, timer_ref)
    else
      socket
    end
  end

  # ── Serialization helpers ──

  defp serialize_bird(bird) do
    %{
      id: bird.id,
      grid_x: bird.grid_x,
      grid_y: bird.grid_y,
      seed_name: bird.seed_name,
      seed_count: bird.seed_count
    }
  end

  defp build_quest_sync(mallums) do
    mallums
    |> Enum.filter(&(&1.state == "on_quest"))
    |> Enum.map(fn m ->
      elapsed_s = if m.start_time_utc,
        do: DateTime.diff(DateTime.utc_now(), m.start_time_utc, :second),
        else: 0

      config = ConfigCache.get_quest_config(m.assigned_quest_name)
      total_s = if config, do: config.duration_minutes * 60, else: 0
      remaining = max(total_s - elapsed_s, 0)

      %{mallum_id: m.id, remaining_seconds: remaining}
    end)
  end

  defp build_vase_sync(vases) do
    vases
    |> Enum.filter(&(&1.state == "filling"))
    |> Enum.map(fn v ->
      elapsed_s = if v.fill_start_time_utc,
        do: DateTime.diff(DateTime.utc_now(), v.fill_start_time_utc, :second),
        else: 0

      total_s = v.capacity * 60
      remaining = max(total_s - elapsed_s, 0)

      %{vase_id: v.id, remaining_seconds: remaining}
    end)
  end
end
```

- [ ] **Step 2: Verify compilation**

Run: `cd server && mix compile`
Expected: Compiles (may warn about missing `PlayerTimezone` and missing `list_vases`/`list_mallums`/`list_birds` — those come next)

- [ ] **Step 3: Commit**

```bash
git add server/lib/camp_fire_web/channels/player_channel.ex
git commit -m "feat(channels): add PlayerChannel with join, sync, and timer scheduling"
```

---

### Task 3: Player Timezone Helper

**Files:**
- Create: `server/lib/camp_fire/game/player_timezone.ex`
- Create: `server/test/camp_fire/game/player_timezone_test.exs`

**Context:** The design spec says to derive UTC offset from stored longitude: `offset_hours = round(lon / 15)`. This is a simple pure function.

- [ ] **Step 1: Write the test**

```elixir
# server/test/camp_fire/game/player_timezone_test.exs
defmodule CampFire.Game.PlayerTimezoneTest do
  use ExUnit.Case, async: true

  alias CampFire.Game.PlayerTimezone

  describe "offset_hours_from_lon/1" do
    test "returns 0 for Greenwich meridian" do
      assert PlayerTimezone.offset_hours_from_lon(0.0) == 0
    end

    test "returns positive offset for east longitude" do
      # Berlin: ~13.4° → round(13.4/15) = round(0.89) = 1
      assert PlayerTimezone.offset_hours_from_lon(13.4) == 1
    end

    test "returns negative offset for west longitude" do
      # New York: ~-74° → round(-74/15) = round(-4.93) = -5
      assert PlayerTimezone.offset_hours_from_lon(-74.0) == -5
    end

    test "handles far east" do
      # Tokyo: ~139.7° → round(139.7/15) = round(9.31) = 9
      assert PlayerTimezone.offset_hours_from_lon(139.7) == 9
    end

    test "handles boundary (exactly between zones)" do
      # 7.5° → round(0.5) = 1 (Elixir rounds half to even → 0)
      # Actually: round(7.5/15) = round(0.5) = 0 in Elixir (banker's rounding)
      assert PlayerTimezone.offset_hours_from_lon(7.5) in [0, 1]
    end
  end
end
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd server && mix test test/camp_fire/game/player_timezone_test.exs`
Expected: Compilation error — module `CampFire.Game.PlayerTimezone` not found

- [ ] **Step 3: Write the implementation**

```elixir
# server/lib/camp_fire/game/player_timezone.ex
defmodule CampFire.Game.PlayerTimezone do
  @doc """
  Derives a rough UTC offset (integer hours) from longitude.
  Each 15° of longitude ≈ 1 hour offset from UTC.
  """
  def offset_hours_from_lon(lon) when is_number(lon) do
    round(lon / 15)
  end
end
```

- [ ] **Step 4: Run tests**

Run: `cd server && mix test test/camp_fire/game/player_timezone_test.exs`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add server/lib/camp_fire/game/player_timezone.ex server/test/camp_fire/game/player_timezone_test.exs
git commit -m "feat(channels): add PlayerTimezone longitude-to-offset helper"
```

---

### Task 4: List Functions for Channel Sync

**Files:**
- Modify: `server/lib/camp_fire/game/vases.ex` (add `list_vases/1`)
- Modify: `server/lib/camp_fire/game/mallums.ex` (add `list_mallums/1`)
- Modify: `server/lib/camp_fire/game/birds.ex` (add `list_birds/1`)

**Context:** The channel needs to query current vases/mallums/birds for a player on join. The game controller's `get_state` action already queries these, but the queries are inline in the controller. We need public context functions. Check if these already exist before writing them — the controller may call them from the contexts already.

- [ ] **Step 1: Check existing context functions**

Read the Vases, Mallums, and Birds modules to see if `list_*` functions already exist. If they do, skip creating them. If the controller does inline queries, extract them.

The game controller's `get_state` (around line 136) queries all entities. Check what functions it calls — if it already calls `Vases.list_vases(uid)` etc., these exist. If it does raw Repo queries, we need to add the functions.

- [ ] **Step 2: Add any missing list functions**

For each missing function, add a simple query:

```elixir
# In the appropriate module (Vases, Mallums, or Birds)
def list_vases(player_uid) do
  Repo.all(from v in Vase, where: v.player_uid == ^player_uid)
end
```

Pattern: `Repo.all(from x in Schema, where: x.player_uid == ^player_uid)`

- [ ] **Step 3: Verify compilation**

Run: `cd server && mix compile`
Expected: Clean compilation

- [ ] **Step 4: Commit**

```bash
git add server/lib/camp_fire/game/
git commit -m "feat(channels): add list functions for vases, mallums, birds"
```

---

### Task 5: Broadcast from REST Endpoints

**Files:**
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex`

**Context:** When the REST API starts a quest or vase fill, it must broadcast to the player's channel so the channel process schedules the completion timer. The channel listens for `%Phoenix.Socket.Broadcast{}` messages with events `"quest:started"` and `"vase:started"`.

Key locations in game_controller.ex:
- `start_quest` action (around line 452): After `Mallums.send_on_quest(uid, quest_name)` succeeds, broadcast.
- `fill_vase` action (around line 317): After `Vases.start_fill(uid, vase_id)` succeeds, broadcast.

- [ ] **Step 1: Add broadcast after quest start**

In `game_controller.ex`, in the `start_quest` action, after the success case:

```elixir
case Mallums.send_on_quest(uid, quest_name) do
  {:ok, mallum} ->
    # Broadcast to channel for timer scheduling
    config = ConfigCache.get_quest_config(quest_name)
    if config do
      CampFireWeb.Endpoint.broadcast("player:#{uid}", "quest:started", %{
        mallum_id: mallum.id,
        duration_seconds: config.duration_minutes * 60
      })
    end
    conn |> put_status(200) |> json(serialize_mallum(mallum))
```

- [ ] **Step 2: Add broadcast after vase fill start**

In `game_controller.ex`, in the `fill_vase` action, after the success case:

```elixir
case Vases.start_fill(uid, vase_id) do
  {:ok, vase} ->
    # Broadcast to channel for timer scheduling
    duration_seconds = vase.capacity * 60
    CampFireWeb.Endpoint.broadcast("player:#{uid}", "vase:started", %{
      vase_id: vase.id,
      duration_seconds: duration_seconds
    })
    conn |> put_status(200) |> json(serialize_vase(vase))
```

- [ ] **Step 3: Add ConfigCache alias if not present**

Make sure `alias CampFire.Game.ConfigCache` is at the top of the controller.

- [ ] **Step 4: Verify compilation**

Run: `cd server && mix compile`
Expected: Clean compilation

- [ ] **Step 5: Commit**

```bash
git add server/lib/camp_fire_web/controllers/game_controller.ex
git commit -m "feat(channels): broadcast quest/vase start events to player channel"
```

---

### Task 6: Server Channel Tests

**Files:**
- Create: `server/test/camp_fire_web/channels/player_channel_test.exs`

**Context:** Test the channel join flow, sync payload, and timer-driven events. Phoenix provides `Phoenix.ChannelTest` helpers. The test needs a player with a token in the database.

- [ ] **Step 1: Write channel tests**

```elixir
# server/test/camp_fire_web/channels/player_channel_test.exs
defmodule CampFireWeb.PlayerChannelTest do
  use CampFireWeb.ChannelCase

  alias CampFireWeb.PlayerSocket

  setup do
    # Create a test player with token
    {:ok, player} = CampFire.Accounts.create_player(%{
      display_name: "TestPlayer",
      device_id: "test-device-#{System.unique_integer()}"
    })
    token = CampFire.Accounts.create_token(player)

    # Create economy record
    CampFire.Economy.get_or_create_economy(player.uid)

    {:ok, player: player, token: token}
  end

  describe "join" do
    test "succeeds with valid token and matching uid", %{player: player, token: token} do
      {:ok, socket} = connect(PlayerSocket, %{"token" => token})
      assert {:ok, _reply, _socket} = subscribe_and_join(socket, "player:#{player.uid}", %{})
    end

    test "fails with mismatched uid", %{token: token} do
      {:ok, socket} = connect(PlayerSocket, %{"token" => token})
      assert {:error, %{reason: "unauthorized"}} =
        subscribe_and_join(socket, "player:wrong-uid", %{})
    end

    test "socket connect fails with invalid token" do
      assert :error = connect(PlayerSocket, %{"token" => "bad-token"})
    end
  end

  describe "sync" do
    test "pushes sync event after join", %{player: player, token: token} do
      {:ok, socket} = connect(PlayerSocket, %{"token" => token})
      {:ok, _reply, _socket} = subscribe_and_join(socket, "player:#{player.uid}", %{})

      assert_push "sync", payload
      assert is_list(payload["birds"] || payload[:birds])
      assert is_list(payload["quests"] || payload[:quests])
      assert is_list(payload["vases"] || payload[:vases])
    end
  end
end
```

- [ ] **Step 2: Create ChannelCase if it doesn't exist**

Check if `server/test/support/channel_case.ex` exists. If not, create it:

```elixir
defmodule CampFireWeb.ChannelCase do
  use ExUnit.CaseTemplate

  using do
    quote do
      import Phoenix.ChannelTest
      @endpoint CampFireWeb.Endpoint
    end
  end

  setup tags do
    pid = Ecto.Adapters.SQL.Sandbox.start_owner!(CampFire.Repo, shared: not tags[:async])
    on_exit(fn -> Ecto.Adapters.SQL.Sandbox.stop_owner(pid) end)
    :ok
  end
end
```

- [ ] **Step 3: Run tests**

Run: `cd server && mix test test/camp_fire_web/channels/player_channel_test.exs`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add server/test/camp_fire_web/channels/ server/test/support/
git commit -m "test(channels): add PlayerChannel join, auth, and sync tests"
```

---

## Chunk 2: Client ChannelService

### Task 7: ChannelService — Phoenix WebSocket Client

**Files:**
- Create: `Assets/Scripts/Services/ChannelService.cs`

**Context:** This is the Unity-side WebSocket client that implements the Phoenix Channel protocol. Uses `System.Net.WebSockets.ClientWebSocket` (built into .NET, no external dependencies). The service must:
1. Wait for `SocialService.IsSignedIn` and `GameService.IsOnline`
2. Connect to `ws(s)://{server}/socket/player/websocket?token={token}`
3. Implement Phoenix message format: `[join_ref, ref, topic, event, payload]`
4. Send heartbeat every 30s
5. Auto-reconnect with exponential backoff (1s, 2s, 4s, 8s, 16s, 30s cap)
6. Parse events and fire C# events
7. Re-join on reconnect

The Phoenix WebSocket transport appends `/websocket` to the socket path, so the full URL is `/socket/player/websocket`.

The server base URL is available via `ServerConfig.BaseUrl` (e.g., `http://localhost:4000`). For WebSocket, replace `http` with `ws` and `https` with `wss`.

- [ ] **Step 1: Create ChannelService**

```csharp
// Assets/Scripts/Services/ChannelService.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class ChannelService : MonoBehaviour
    {
        public static ChannelService Instance { get; private set; }

        public bool IsConnected { get; private set; }

        // Events that managers subscribe to
        public event Action<SyncPayload> OnSyncReceived;
        public event Action<VisitorArrivedPayload> OnVisitorArrived;
        public event Action OnVisitorDeparted;
        public event Action<BirdSpawnedPayload> OnBirdSpawned;
        public event Action<QuestCompletedPayload> OnQuestCompleted;
        public event Action<VaseFilledPayload> OnVaseFilled;
        public event Action<GiftReceivedPayload> OnGiftReceived;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private int _refCounter;
        private string _joinRef;
        private string _topic;
        private Coroutine _heartbeatCoroutine;
        private Coroutine _connectCoroutine;
        private bool _intentionalDisconnect;

        // Reconnect backoff
        private static readonly float[] BackoffSeconds = { 1f, 2f, 4f, 8f, 16f, 30f };
        private int _reconnectAttempt;

        // Message queue for main thread dispatch
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _connectCoroutine = StartCoroutine(WaitAndConnect());
        }

        private void Update()
        {
            // Dispatch queued events on main thread
            lock (_mainThreadQueue)
            {
                while (_mainThreadQueue.Count > 0)
                    _mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        private void OnDestroy()
        {
            _intentionalDisconnect = true;
            _cts?.Cancel();
            if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);
            if (_connectCoroutine != null) StopCoroutine(_connectCoroutine);
            _ = CloseWebSocket();
        }

        private IEnumerator WaitAndConnect()
        {
            // Wait for auth and game service
            yield return new WaitUntil(() =>
                SocialService.Instance != null && SocialService.Instance.IsSignedIn &&
                GameService.Instance != null && GameService.Instance.IsOnline);

            _ = ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            var token = SocialSaveManager.Instance?.Data?.authToken;
            var uid = SocialService.Instance?.Uid;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(uid))
            {
                Debug.LogError("[ChannelService] No auth token or UID available");
                return;
            }

            _topic = $"player:{uid}";

            // Build WebSocket URL
            var baseUrl = ServerConfig.BaseUrl;
            var wsUrl = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var uri = new Uri($"{wsUrl}/socket/player/websocket?token={Uri.EscapeDataString(token)}");

            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(uri, _cts.Token);
                IsConnected = true;
                _reconnectAttempt = 0;
                Debug.Log("[ChannelService] WebSocket connected");

                // Join the channel
                await SendJoin();

                // Start heartbeat
                EnqueueMainThread(() =>
                {
                    if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);
                    _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
                });

                // Start receive loop
                await ReceiveLoop();
            }
            catch (Exception e)
            {
                if (!_intentionalDisconnect)
                    Debug.LogWarning($"[ChannelService] Connection error: {e.Message}");
            }
            finally
            {
                IsConnected = false;
                if (!_intentionalDisconnect)
                    EnqueueMainThread(() => StartCoroutine(ReconnectAfterDelay()));
            }
        }

        private async Task SendJoin()
        {
            _joinRef = NextRef();
            var msg = $"[\"{_joinRef}\",\"{NextRef()}\",\"{_topic}\",\"phx_join\",{{}}]";
            await SendRaw(msg);
        }

        private async Task SendHeartbeat()
        {
            if (_ws?.State != WebSocketState.Open) return;
            var msg = $"[null,\"{NextRef()}\",\"phoenix\",\"heartbeat\",{{}}]";
            await SendRaw(msg);
        }

        private async Task SendRaw(string message)
        {
            if (_ws?.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(message);
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChannelService] Send error: {e.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            var sb = new StringBuilder();

            while (_ws?.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[ChannelService] Server closed connection");
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var raw = sb.ToString();
                HandleMessage(raw);
            }
        }

        private void HandleMessage(string raw)
        {
            // Phoenix messages are JSON arrays: [join_ref, ref, topic, event, payload]
            // We parse manually since JsonUtility can't deserialize arrays
            try
            {
                ParsePhoenixMessage(raw, out string eventName, out string payloadJson);
                if (eventName == null) return;

                switch (eventName)
                {
                    case "phx_reply":
                        // Join reply or heartbeat reply — ignore
                        break;
                    case "phx_error":
                        Debug.LogWarning($"[ChannelService] Channel error: {raw}");
                        break;
                    case "phx_close":
                        Debug.Log("[ChannelService] Channel closed by server");
                        break;
                    case "sync":
                        var sync = JsonUtility.FromJson<SyncPayload>(payloadJson);
                        EnqueueMainThread(() => OnSyncReceived?.Invoke(sync));
                        break;
                    case "visitor:arrived":
                        var visitor = JsonUtility.FromJson<VisitorArrivedPayload>(payloadJson);
                        EnqueueMainThread(() => OnVisitorArrived?.Invoke(visitor));
                        break;
                    case "visitor:departed":
                        EnqueueMainThread(() => OnVisitorDeparted?.Invoke());
                        break;
                    case "bird:spawned":
                        var bird = JsonUtility.FromJson<BirdSpawnedPayload>(payloadJson);
                        EnqueueMainThread(() => OnBirdSpawned?.Invoke(bird));
                        break;
                    case "quest:completed":
                        var quest = JsonUtility.FromJson<QuestCompletedPayload>(payloadJson);
                        EnqueueMainThread(() => OnQuestCompleted?.Invoke(quest));
                        break;
                    case "vase:filled":
                        var vase = JsonUtility.FromJson<VaseFilledPayload>(payloadJson);
                        EnqueueMainThread(() => OnVaseFilled?.Invoke(vase));
                        break;
                    case "gift:received":
                        var gift = JsonUtility.FromJson<GiftReceivedPayload>(payloadJson);
                        EnqueueMainThread(() => OnGiftReceived?.Invoke(gift));
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChannelService] Parse error: {e.Message} — raw: {raw}");
            }
        }

        /// <summary>
        /// Parse Phoenix wire format: [join_ref, ref, topic, event, payload]
        /// Extracts event (element 3) as string and payload (element 4) as raw JSON string.
        /// Handles escaped quotes in strings and nested objects/arrays in payload.
        /// </summary>
        private static void ParsePhoenixMessage(string raw, out string eventName, out string payloadJson)
        {
            eventName = null;
            payloadJson = null;

            raw = raw.Trim();
            if (raw.Length < 2 || raw[0] != '[' || raw[raw.Length - 1] != ']') return;

            int i = 1; // skip opening '['
            int elementIndex = 0;

            while (i < raw.Length - 1 && elementIndex < 5)
            {
                // Skip whitespace and commas
                while (i < raw.Length - 1 && (raw[i] == ',' || raw[i] == ' ' || raw[i] == '\n' || raw[i] == '\r' || raw[i] == '\t')) i++;
                if (i >= raw.Length - 1) break;

                int start = i;
                if (raw[i] == '"')
                {
                    // Quoted string — handle escaped quotes
                    i++;
                    while (i < raw.Length && !(raw[i] == '"' && raw[i - 1] != '\\')) i++;
                    i++; // skip closing quote
                    string value = raw.Substring(start + 1, i - start - 2); // strip quotes
                    if (elementIndex == 3) eventName = value;
                }
                else if (raw[i] == '{' || raw[i] == '[')
                {
                    // Object or array — find matching close, respecting nesting and strings
                    char open = raw[i], close = (open == '{') ? '}' : ']';
                    int depth = 1;
                    i++;
                    while (i < raw.Length && depth > 0)
                    {
                        if (raw[i] == '"') { i++; while (i < raw.Length && !(raw[i] == '"' && raw[i - 1] != '\\')) i++; }
                        else if (raw[i] == open) depth++;
                        else if (raw[i] == close) depth--;
                        i++;
                    }
                    if (elementIndex == 4) payloadJson = raw.Substring(start, i - start);
                }
                else if (raw[i] == 'n' && i + 3 < raw.Length && raw.Substring(i, 4) == "null")
                {
                    i += 4;
                }
                else
                {
                    // Number or boolean
                    while (i < raw.Length && raw[i] != ',' && raw[i] != ']') i++;
                }

                elementIndex++;
            }

            if (payloadJson == null) payloadJson = "{}";
        }

        private IEnumerator HeartbeatLoop()
        {
            while (IsConnected)
            {
                yield return new WaitForSeconds(30f);
                if (IsConnected)
                    _ = SendHeartbeat();
            }
        }

        private IEnumerator ReconnectAfterDelay()
        {
            float delay = BackoffSeconds[Mathf.Min(_reconnectAttempt, BackoffSeconds.Length - 1)];
            _reconnectAttempt++;
            Debug.Log($"[ChannelService] Reconnecting in {delay}s (attempt {_reconnectAttempt})");
            yield return new WaitForSeconds(delay);

            if (!_intentionalDisconnect)
                _ = ConnectAsync();
        }

        private async Task CloseWebSocket()
        {
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch { /* ignore */ }
            }
            _ws?.Dispose();
            _ws = null;
        }

        private void EnqueueMainThread(Action action)
        {
            lock (_mainThreadQueue)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        private string NextRef()
        {
            return (++_refCounter).ToString();
        }
    }

    // ── Payload types ──

    [Serializable]
    public class SyncPayload
    {
        public VisitorArrivedPayload visitor;  // null if no active visitor
        public List<SyncBird> birds;
        public List<SyncQuest> quests;
        public List<SyncVase> vases;
    }

    [Serializable]
    public class SyncBird
    {
        public int id;
        public int grid_x;
        public int grid_y;
        public string seed_name;
        public int seed_count;
    }

    [Serializable]
    public class SyncQuest
    {
        public int mallum_id;
        public int remaining_seconds;
    }

    [Serializable]
    public class SyncVase
    {
        public int vase_id;
        public int remaining_seconds;
    }

    [Serializable]
    public class VisitorArrivedPayload
    {
        public string visitor_type;
        public string visitor_id;
        public string name;
        public string portrait_id;
        public List<string> dialogue;
        // Merchant fields
        public List<VisitorOfferPayload> offers;
        // Gifter fields
        public VisitorGiftPayload gift;
        // Quester fields
        public VisitorQuestPayload quest;
    }

    [Serializable]
    public class VisitorOfferPayload
    {
        public List<TradeCost> costs;
        public string rewardSeedName;
        public int rewardCount;
    }

    [Serializable]
    public class VisitorGiftPayload
    {
        public string type;
        public string name;
        public int amount;
    }

    [Serializable]
    public class VisitorQuestPayload
    {
        public int quest_id;
        public string request_item;
        public int request_count;
        public int return_days;
        public List<string> return_dialogue;
        public bool is_return;
    }

    [Serializable]
    public class BirdSpawnedPayload
    {
        public int id;
        public int grid_x;
        public int grid_y;
        public string seed_name;
        public int seed_count;
    }

    [Serializable]
    public class QuestCompletedPayload
    {
        public int mallum_id;
        public List<ServerReward> rewards;
    }

    [Serializable]
    public class VaseFilledPayload
    {
        public int vase_id;
        public int current_water;
        public int capacity;
    }

    [Serializable]
    public class GiftReceivedPayload
    {
        public string from_name;
        public string gift_type;
        public string gift_name;
        public int amount;
    }
}
```

**Note on JSON parsing:** The `ParsePhoenixMessage` method manually parses the Phoenix wire format array `[join_ref, ref, topic, event, payload]` to extract the event name (element 3) as a string and the payload (element 4) as a raw JSON substring. The payload substring is then passed directly to `JsonUtility.FromJson<SpecificPayload>()` for each event type. This avoids needing a third-party JSON library.

**Note on gift:received:** The `gift:received` event is declared on the client but the server-side push for gifts is not implemented in this plan. Gift sending is not yet implemented in the server. When the gift system is built, the gift REST endpoint should broadcast to the recipient's channel, and the channel should forward it as a `gift:received` push. The client handler is ready.

- [ ] **Step 2: Verify compilation in Unity**

Use Unity MCP `read_console` to check for compilation errors after saving the file.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Services/ChannelService.cs
git commit -m "feat(channels): add ChannelService WebSocket client with Phoenix protocol"
```

---

### Task 8: Wire ChannelService into Scene

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs` (add ChannelService component)

**Context:** All singletons are MonoBehaviours on the "--- UI ---" GameObject. `CampFireUI.Start()` already adds `DebugService` dynamically. We need `ChannelService` to be available as a component. The simplest approach is to add it in `CampFireUI.Start()` before services are used, or ensure it's on the same GameObject.

- [ ] **Step 1: Add ChannelService to the UI GameObject**

In `CampFireUI.Start()`, after the debug service setup (around line 153), add:

```csharp
// Ensure ChannelService exists for server push events
if (GetComponent<ChannelService>() == null)
    gameObject.AddComponent<ChannelService>();
```

- [ ] **Step 2: Verify compilation**

Use Unity MCP `read_console` to check for errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat(channels): wire ChannelService into CampFireUI"
```

---

## Chunk 3: Manager Migrations

### Task 9: Migrate BirdManager to Channel Events

**Files:**
- Modify: `Assets/Scripts/Managers/BirdManager.cs`

**Context:** Currently `BirdManager.Update()` (lines 26-52) polls the server hourly via `CheckBirdsFromServer()`. Replace with a subscription to `ChannelService.OnBirdSpawned`.

Changes:
1. Remove the server polling from `Update()` (keep offline fallback if desired, but since app is online-only, can remove entirely)
2. In `Start()` or `OnEnable()`, subscribe to `ChannelService.Instance.OnBirdSpawned`
3. Callback: add bird to `SaveData.birds`, save, fire `OnBirdPlaced`

- [ ] **Step 1: Modify BirdManager**

Remove `Update()` entirely (both server polling and offline fallback). Add subscription via coroutine to handle timing (ChannelService may not exist yet when `Start()` runs):

```csharp
private void Start()
{
    StartCoroutine(SubscribeToChannel());
}

private System.Collections.IEnumerator SubscribeToChannel()
{
    yield return new WaitUntil(() => ChannelService.Instance != null);
    ChannelService.Instance.OnBirdSpawned += HandleBirdSpawned;
}

private void OnDestroy()
{
    if (ChannelService.Instance != null)
        ChannelService.Instance.OnBirdSpawned -= HandleBirdSpawned;
}

private void HandleBirdSpawned(BirdSpawnedPayload payload)
{
    var data = SaveManager.Instance?.Data;
    if (data == null) return;

    data.birds.Add(new BirdSave
    {
        serverId = payload.id,
        gridX = payload.grid_x,
        gridY = payload.grid_y,
        seedName = payload.seed_name,
        seedCount = payload.seed_count
    });
    SaveManager.Instance.Save();
    OnBirdPlaced?.Invoke();
    AudioManager.Instance?.PlaySFX("bird_arrive");
}
```

Remove fields: `_isChecking`

Remove methods: `Update()`, `CheckBirdsFromServer()`

Keep: `CollectBirdFromServer()`, all static helpers (they're used by other systems and tests)

- [ ] **Step 2: Verify compilation**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Managers/BirdManager.cs
git commit -m "refactor(birds): replace Update() polling with channel push subscription"
```

---

### Task 10: Migrate VaseManager to Channel Events

**Files:**
- Modify: `Assets/Scripts/Managers/VaseManager.cs`

**Context:** Currently `VaseManager.Update()` calls `CheckFillCompletion()` every frame, which iterates all vases checking if fill timers have elapsed. Replace with a subscription to `ChannelService.OnVaseFilled`.

Changes:
1. Remove `Update()` and `CheckFillCompletion()`
2. Subscribe to `ChannelService.Instance.OnVaseFilled`
3. Callback: find vase by server ID, set to Full, update water, free mallum, save, fire event

Note: The vase fill completion also needs to free the assigned Mallum. Currently `MallumManager.Update()` handles this by checking if the vase is no longer filling. With the push model, the vase fill callback should also handle freeing the mallum.

- [ ] **Step 1: Modify VaseManager**

Remove `Update()` and `CheckFillCompletion()`. Add subscription via coroutine:

```csharp
private void Start()
{
    StartCoroutine(SubscribeToChannel());
}

private System.Collections.IEnumerator SubscribeToChannel()
{
    yield return new WaitUntil(() => ChannelService.Instance != null);
    ChannelService.Instance.OnVaseFilled += HandleVaseFilled;
}

private void OnDestroy()
{
    if (ChannelService.Instance != null)
        ChannelService.Instance.OnVaseFilled -= HandleVaseFilled;
}

private void HandleVaseFilled(VaseFilledPayload payload)
{
    var data = SaveManager.Instance?.Data;
    if (data == null) return;

    var vase = data.vases.Find(v => v.serverId == payload.vase_id);
    if (vase == null) return;

    vase.currentWater = payload.current_water;
    vase.state = VaseState.Full;
    vase.fillStartTimeUtc = null;

    // Free the mallum that was fetching water for this vase
    int vaseIndex = data.vases.IndexOf(vase);
    var mallum = data.mallums.Find(m =>
        m.state == MallumState.FetchingWater && m.assignedVaseIndex == vaseIndex);
    if (mallum != null)
    {
        int mallumIndex = data.mallums.IndexOf(mallum);
        MallumManager.FreeMallumFromWater(mallum);
        NotificationService.Instance?.CancelWaterFetchNotification(mallumIndex);
        MallumManager.Instance?.NotifyChanged();
    }

    SaveManager.Instance.Save();
    OnVasesChanged?.Invoke();
    AudioManager.Instance?.PlaySFX("vase_fill_complete");
}
```

- [ ] **Step 2: Verify compilation**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Managers/VaseManager.cs
git commit -m "refactor(vases): replace Update() fill polling with channel push subscription"
```

---

### Task 11: Migrate MallumManager to Channel Events

**Files:**
- Modify: `Assets/Scripts/Managers/MallumManager.cs`

**Context:** Currently `MallumManager.Update()` (lines 32-77) checks every frame for:
1. Mallums in `FetchingWater` state — checks if the vase is no longer filling → frees mallum
2. Mallums in `OnQuest` state — checks if quest timer elapsed → completes quest and calls `GameService.CheckQuest()`

With channel push:
- Quest completion: subscribe to `ChannelService.OnQuestCompleted`
- Water fetch completion: handled by `VaseManager.HandleVaseFilled` (Task 10 above), which already frees the mallum

So we only need the quest completion subscription here.

- [ ] **Step 1: Modify MallumManager**

Remove `Update()` entirely. Add:

```csharp
private void Start()
{
    var houseConfig = ConfigService.Instance?.MallumHouseConfig;
    if (houseConfig == null) return;

    var data = SaveManager.Instance.Data;
    int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
    EnsureMallumCount(data.mallums, max);

    // Subscribe to server push events via coroutine (ChannelService may not exist yet)
    StartCoroutine(SubscribeToChannel());
}

private System.Collections.IEnumerator SubscribeToChannel()
{
    yield return new WaitUntil(() => ChannelService.Instance != null);
    ChannelService.Instance.OnQuestCompleted += HandleQuestCompleted;
}

private void OnDestroy()
{
    if (ChannelService.Instance != null)
        ChannelService.Instance.OnQuestCompleted -= HandleQuestCompleted;
}

private void HandleQuestCompleted(QuestCompletedPayload payload)
{
    var data = SaveManager.Instance?.Data;
    if (data == null) return;

    var mallum = data.mallums.Find(m => m.serverId == payload.mallum_id);
    if (mallum == null || mallum.state != MallumState.OnQuest) return;

    // Apply server rewards
    mallum.pendingRewards.Clear();
    if (payload.rewards != null)
    {
        foreach (var r in payload.rewards)
            mallum.pendingRewards.Add(new RewardEntry { seedName = r.seed_name, count = r.count });
    }
    mallum.state = MallumState.QuestComplete;
    mallum.startTimeUtc = null;

    // Cancel local push notification for this quest
    int mallumIndex = data.mallums.IndexOf(mallum);
    NotificationService.Instance?.CancelQuestNotification(mallumIndex);

    SaveManager.Instance.Save();
    OnMallumsChanged?.Invoke();
    AudioManager.Instance?.PlaySFX("quest_complete");
}
```

Remove: `Update()` method entirely

Keep: everything else (quest timer remaining methods are still used by the UI for progress display)

**Important:** The `IsQuestTimerComplete()` and `CompleteQuest()` private methods are no longer called from `Update()`, but `CompleteQuest()` is still used by `SpeedUpQuest()`. `IsQuestTimerComplete()` can be removed if nothing else references it. The `GetQuestRemainingSeconds()` and `GetQuestProgress()` methods are still needed by the UI for countdown display.

- [ ] **Step 2: Verify compilation**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Managers/MallumManager.cs
git commit -m "refactor(mallums): replace Update() quest polling with channel push subscription"
```

---

### Task 12: Migrate VisitorManager to Channel Events

**Files:**
- Modify: `Assets/Scripts/Managers/VisitorManager.cs`

**Context:** Currently `VisitorManager.Update()` (lines 65-93) checks every frame for:
1. Departure: if visitor exists and not visitor hour (10 PM+) → dismiss
2. Arrival: if visitor hour and no visitor → fetch from server

With channel push, the server handles both scheduling:
- `visitor:arrived` pushed at 10 PM local time
- `visitor:departed` pushed at end of visitor window

Changes:
1. Remove `Update()` entirely
2. Subscribe to `ChannelService.OnVisitorArrived` and `ChannelService.OnVisitorDeparted`
3. Keep `DebugKeepVisitor` flag — on `visitor:departed`, check it before dismissing
4. Keep `FetchTonightVisitor()` — it's still used by `DebugService.ReceiveVisitor()`

- [ ] **Step 1: Modify VisitorManager**

Remove `Update()` and `_fetching` field. Remove `FetchTonightVisitorAsync()` (the private wrapper). Add:

```csharp
private void Start()
{
    var data = SaveManager.Instance?.Data;
    if (data == null) return;

    // Clean stale visitor from a previous day (keep existing logic)
    if (data.currentVisitor != null)
    {
        if (string.IsNullOrEmpty(data.currentVisitor.appearedAtUtc))
        {
            DismissVisitor(data);
            SaveManager.Instance.Save();
        }
        else
        {
            var appearedUtc = DateTime.Parse(data.currentVisitor.appearedAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            if (appearedUtc.Date != GameTime.UtcNow.Date)
            {
                DismissVisitor(data);
                SaveManager.Instance.Save();
            }
        }
    }

    int before = data.activeQuests.Count;
    CleanExpiredQuests(data, GameTime.UtcNow);
    if (data.activeQuests.Count < before)
        SaveManager.Instance.Save();

    // Subscribe to channel events via coroutine
    StartCoroutine(SubscribeToChannel());
}

private System.Collections.IEnumerator SubscribeToChannel()
{
    yield return new WaitUntil(() => ChannelService.Instance != null);
    ChannelService.Instance.OnVisitorArrived += HandleVisitorArrived;
    ChannelService.Instance.OnVisitorDeparted += HandleVisitorDeparted;
}

private void OnDestroy()
{
    if (ChannelService.Instance != null)
    {
        ChannelService.Instance.OnVisitorArrived -= HandleVisitorArrived;
        ChannelService.Instance.OnVisitorDeparted -= HandleVisitorDeparted;
    }
}

private void HandleVisitorArrived(VisitorArrivedPayload payload)
{
    var data = SaveManager.Instance?.Data;
    if (data == null) return;

    // Build visitor save from channel payload
    int gridRadius = FlameManager.Instance != null
        ? FlameManager.Instance.GetGridSize(data.flameLevel)
        : 2;
    var freeTiles = BirdManager.GetFreeTiles(data, gridRadius);
    if (freeTiles.Count == 0) return;

    var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];

    // Convert payload to VisitorResponse for BuildVisitorSave
    var response = new VisitorResponse
    {
        visitor_type = payload.visitor_type,
        visitor_id = payload.visitor_id,
        name = payload.name,
        portrait_id = payload.portrait_id,
        dialogue = payload.dialogue
    };

    // Copy merchant offers
    if (payload.offers != null)
    {
        response.offers = new List<OfferResponse>();
        foreach (var o in payload.offers)
            response.offers.Add(new OfferResponse { costs = o.costs, rewardSeedName = o.rewardSeedName, rewardCount = o.rewardCount });
    }

    // Copy gift
    if (payload.gift != null)
        response.gift = new GiftResponse { type = payload.gift.type, name = payload.gift.name, amount = payload.gift.amount };

    // Copy quest
    if (payload.quest != null)
    {
        response.quest = new QuestResponse
        {
            quest_id = payload.quest.quest_id,
            request_item = payload.quest.request_item,
            request_count = payload.quest.request_count,
            return_days = payload.quest.return_days,
            return_dialogue = payload.quest.return_dialogue,
            is_return = payload.quest.is_return
        };
    }

    string todayUtc = GameTime.UtcNow.Date.ToString("o");
    var visitor = BuildVisitorSave(response, tile.q, tile.r, todayUtc);
    data.currentVisitor = visitor;
    data.lastVisitorFetchDateUtc = todayUtc;
    SaveManager.Instance.Save();
    NotifyVisitorArrived();
}

private void HandleVisitorDeparted()
{
    if (DebugKeepVisitor) return;

    var data = SaveManager.Instance?.Data;
    if (data == null) return;
    if (data.currentVisitor == null) return;

    DismissVisitor(data);
    SaveManager.Instance.Save();
    OnVisitorDeparted?.Invoke();
}
```

Remove: `Update()`, `_fetching`, `FetchTonightVisitorAsync()` (the private one)

Keep: `FetchTonightVisitor()` (public, used by DebugService), `IsVisitorHour()` (used by debug), all other methods

- [ ] **Step 2: Verify compilation**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Managers/VisitorManager.cs
git commit -m "refactor(visitors): replace Update() polling with channel push subscription"
```

---

## Chunk 4: Cleanup and Testing

### Task 13: Remove Deprecated GameService Polling Methods

**Files:**
- Modify: `Assets/Scripts/Services/GameService.cs`

**Context:** With channel push, the following GameService methods are no longer called from managers' `Update()` loops:
- `CheckBirds()` — was called by `BirdManager.CheckBirdsFromServer()`
- `CheckVase(int vaseId)` — was called by `VaseManager.CheckFillCompletion()`
- `CheckQuest(int mallumId)` — was called by `MallumManager.Update()`

These methods and their request DTOs can be removed. The REST endpoints on the server remain (they're still used by the channel process internally).

**Important:** Before removing, verify nothing else calls them. Search for `CheckBirds()`, `CheckVase(`, `CheckQuest(` in the codebase.

- [ ] **Step 1: Search for remaining usages**

Grep for `GameService.*Check(Birds|Vase|Quest)` and `\.CheckBirds\(` / `\.CheckVase\(` / `\.CheckQuest\(` across all `.cs` files. Only remove if the only callers were the `Update()` methods we already removed.

- [ ] **Step 2: Remove unused methods**

If safe, remove `CheckBirds()`, `CheckVase()`, `CheckQuest()` from `GameService.cs`. Also remove `BirdCheckResponse` and `CheckVaseRequest` from `GameStateResponse.cs` if they're no longer referenced.

- [ ] **Step 3: Verify compilation**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Services/GameService.cs Assets/Scripts/Data/GameStateResponse.cs
git commit -m "chore: remove deprecated polling methods from GameService"
```

---

### Task 14: Add ChannelService to Debug Panel

**Files:**
- Modify: `Assets/Scripts/Debug/DebugWeatherPanel.cs`
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`

**Context:** The design spec mentions adding a connection indicator to the debug panel. Add a label showing channel connection status.

- [ ] **Step 1: Add connection status label to UXML**

In `CampFireRoot.uxml`, inside the debug panel section, add:

```xml
<ui:Label name="channel-status-label" text="Channel: --" />
```

- [ ] **Step 2: Update DebugWeatherPanel to show status**

In `DebugWeatherPanel.cs`, in `Initialize()`:

```csharp
var channelStatusLabel = root.Q<Label>("channel-status-label");
```

In `Update()`, add:

```csharp
if (channelStatusLabel != null && panel != null && panel.resolvedStyle.display == DisplayStyle.Flex)
{
    var connected = ChannelService.Instance != null && ChannelService.Instance.IsConnected;
    channelStatusLabel.text = $"Channel: {(connected ? "Connected" : "Disconnected")}";
}
```

- [ ] **Step 3: Verify compilation**

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Debug/DebugWeatherPanel.cs Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(debug): add channel connection status to debug panel"
```

---

### Task 15: Handle Sync on Reconnect

**Files:**
- Modify: `Assets/Scripts/Services/ChannelService.cs`

**Context:** When the client reconnects after a disconnect, the server sends a fresh `sync` event. The client should use this to catch up on missed events (e.g., a vase that filled while disconnected, a bird that spawned, etc.).

- [ ] **Step 1: Add sync handler logic**

The `SyncPayload` handler should trigger a full game state refresh, similar to what `GameService.Initialize()` does. The simplest approach: on sync, call `GameService.Instance.Initialize()` to re-fetch the full state.

In `ChannelService.HandleMessage`, for the `"sync"` case:

```csharp
case "sync":
    // On reconnect sync, refresh full game state to catch up
    EnqueueMainThread(() =>
    {
        GameService.Instance?.Initialize();
    });
    break;
```

This is simpler and more reliable than trying to incrementally apply sync deltas.

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Services/ChannelService.cs
git commit -m "feat(channels): refresh game state on channel sync/reconnect"
```

---

### Task 16: Integration Verification

**Files:** None (manual testing)

- [ ] **Step 1: Start the server**

```bash
cd server && make dev
```

- [ ] **Step 2: Run in Unity Editor**

1. Enter Play mode
2. Open Debug panel
3. Verify "Channel: Connected" shows
4. Use "Receive Visitor" cheat — visitor should still work (via REST)
5. Send a Mallum on a quest — after duration, quest should complete via push (no polling)
6. Send a Mallum to fetch water — after fill time, vase should fill via push

- [ ] **Step 3: Test reconnection**

1. While in Play mode, stop the server (`Ctrl+C`)
2. Verify "Channel: Disconnected" shows in debug panel
3. Restart server
4. Verify channel auto-reconnects and game state refreshes

- [ ] **Step 4: Run server tests**

```bash
cd server && mix test test/camp_fire_web/channels/
```

- [ ] **Step 5: Commit any test fixes**
