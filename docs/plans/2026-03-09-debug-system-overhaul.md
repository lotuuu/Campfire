# Debug System Overhaul Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace local debug cheats with server-side debug endpoints so debug tools work correctly in the server-authoritative architecture.

**Architecture:** New `DebugController` + `CampFire.Game.Debug` module on server. New `DebugService.cs` on client. Debug panel rewired to call server endpoints, then re-sync game state. Weather overrides and server selector remain local-only.

**Tech Stack:** Elixir/Phoenix (server), C#/Unity UI Toolkit (client)

---

### Task 1: Server — Debug Module (`CampFire.Game.Debug`)

**Files:**
- Create: `server/lib/camp_fire/game/debug.ex`

**Step 1: Create the debug module with all mutation functions**

```elixir
defmodule CampFire.Game.Debug do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Economy.PlayerEconomy
  alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerGarden, PlayerMallum, PlayerState, Birds, Mallums}

  @doc "Shift all player timestamps back by N hours so timers appear elapsed."
  def skip_time(player_uid, hours) when is_integer(hours) and hours > 0 do
    shift = -hours * 3600

    Repo.transaction(fn ->
      # Plots: shift plant_time_utc and last_watered_utc
      from(p in PlayerPlot,
        where: p.player_uid == ^player_uid and p.state == "growing"
      )
      |> Repo.all()
      |> Enum.each(fn plot ->
        changes = %{
          plant_time_utc: DateTime.add(plot.plant_time_utc, shift, :second)
        }
        changes = if plot.last_watered_utc do
          Map.put(changes, :last_watered_utc, DateTime.add(plot.last_watered_utc, shift, :second))
        else
          changes
        end
        plot |> PlayerPlot.changeset(changes) |> Repo.update!()
      end)

      # Vases: shift fill_start_time_utc
      from(v in PlayerVase,
        where: v.player_uid == ^player_uid and v.state == "filling"
      )
      |> Repo.all()
      |> Enum.each(fn vase ->
        vase
        |> PlayerVase.changeset(%{
          fill_start_time_utc: DateTime.add(vase.fill_start_time_utc, shift, :second)
        })
        |> Repo.update!()
      end)

      # Mallums on quest: shift start_time_utc
      from(m in PlayerMallum,
        where: m.player_uid == ^player_uid and m.state == "on_quest"
      )
      |> Repo.all()
      |> Enum.each(fn mallum ->
        mallum
        |> PlayerMallum.changeset(%{
          start_time_utc: DateTime.add(mallum.start_time_utc, shift, :second)
        })
        |> Repo.update!()
      end)

      # Mallums fetching water: shift start_time_utc
      from(m in PlayerMallum,
        where: m.player_uid == ^player_uid and m.state == "fetching_water"
      )
      |> Repo.all()
      |> Enum.each(fn mallum ->
        mallum
        |> PlayerMallum.changeset(%{
          start_time_utc: DateTime.add(mallum.start_time_utc, shift, :second)
        })
        |> Repo.update!()
      end)

      # Gardens: shift last_yield_time_utc (and plant_time_utc for immature)
      from(g in PlayerGarden, where: g.player_uid == ^player_uid)
      |> Repo.all()
      |> Enum.each(fn garden ->
        changes = %{
          plant_time_utc: DateTime.add(garden.plant_time_utc, shift, :second)
        }
        changes = if garden.last_yield_time_utc do
          Map.put(changes, :last_yield_time_utc, DateTime.add(garden.last_yield_time_utc, shift, :second))
        else
          changes
        end
        garden |> PlayerGarden.changeset(changes) |> Repo.update!()
      end)

      # Birds: shift last_bird_check_hour back so check_spawns walks the hours
      case Repo.get(PlayerState, player_uid) do
        nil -> :ok
        state ->
          case Map.get(state.data || %{}, "last_bird_check_hour_utc") do
            nil -> :ok
            iso_str ->
              case DateTime.from_iso8601(iso_str) do
                {:ok, dt, _} ->
                  shifted = DateTime.add(dt, shift, :second)
                  new_data = Map.put(state.data || %{}, "last_bird_check_hour_utc", DateTime.to_iso8601(shifted))
                  state |> PlayerState.changeset(%{data: new_data}) |> Repo.update!()
                _ -> :ok
              end
          end
      end

      # Mana: shift last_mana_collect_utc so mana accumulates
      case Repo.get(PlayerEconomy, player_uid) do
        nil -> :ok
        economy ->
          economy
          |> PlayerEconomy.changeset(%{
            last_mana_collect_utc: DateTime.add(economy.last_mana_collect_utc, shift, :second)
          })
          |> Repo.update!()
      end

      :ok
    end)
  end

  @doc "Set mana and/or gems directly."
  def set_currency(player_uid, opts) do
    case Repo.get(PlayerEconomy, player_uid) do
      nil -> {:error, :not_found}
      economy ->
        changes = %{}
        changes = if opts[:mana], do: Map.put(changes, :mana, opts[:mana] / 1), else: changes
        changes = if opts[:gems], do: Map.put(changes, :gems, opts[:gems]), else: changes
        economy |> PlayerEconomy.changeset(changes) |> Repo.update()
    end
  end

  @doc "Grant seeds to player."
  def grant_seeds(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    Economy.upsert_seed(player_uid, seed_name, count)
  end

  @doc "Grant items to player."
  def grant_items(player_uid, item_name, count) when is_integer(count) and count > 0 do
    Economy.upsert_item(player_uid, item_name, count)
  end

  @doc "Force-spawn a bird on a free tile."
  def spawn_bird(player_uid) do
    economy = Repo.get(PlayerEconomy, player_uid)
    if economy == nil do
      {:error, :not_found}
    else
      Birds.try_spawn_bird_public(player_uid, economy.flame_level)
    end
  end

  @doc "Instantly complete all active quests and roll rewards."
  def complete_quests(player_uid) do
    mallums =
      from(m in PlayerMallum,
        where: m.player_uid == ^player_uid and m.state == "on_quest"
      )
      |> Repo.all()

    Enum.each(mallums, fn mallum ->
      config = Mallums.get_quest_config_public(mallum.assigned_quest_name)
      rewards = if config, do: Mallums.roll_rewards(config), else: []

      mallum
      |> PlayerMallum.changeset(%{state: "quest_complete", pending_rewards: rewards})
      |> Repo.update!()
    end)

    {:ok, length(mallums)}
  end

  @doc "Instantly fill all filling vases."
  def fill_vases(player_uid) do
    vases =
      from(v in PlayerVase,
        where: v.player_uid == ^player_uid and v.state == "filling"
      )
      |> Repo.all()

    Enum.each(vases, fn vase ->
      # Free the mallum assigned to this vase
      case Repo.one(
        from(m in PlayerMallum,
          where: m.player_uid == ^player_uid and m.state == "fetching_water" and m.assigned_vase_id == ^vase.id,
          limit: 1
        )
      ) do
        nil -> :ok
        mallum ->
          mallum
          |> PlayerMallum.changeset(%{state: "idle", assigned_vase_id: nil, start_time_utc: nil})
          |> Repo.update!()
      end

      vase
      |> PlayerVase.changeset(%{state: "full", current_water: vase.capacity, fill_start_time_utc: nil})
      |> Repo.update!()
    end)

    {:ok, length(vases)}
  end

  @doc "Instantly mature all growing plots."
  def mature_plots(player_uid) do
    {count, _} =
      from(p in PlayerPlot,
        where: p.player_uid == ^player_uid and p.state == "growing"
      )
      |> Repo.update_all(set: [state: "mature"])

    {:ok, count}
  end

  @doc "Set flame level directly."
  def set_flame_level(player_uid, level) when is_integer(level) and level >= 1 do
    case Repo.get(PlayerEconomy, player_uid) do
      nil -> {:error, :not_found}
      economy ->
        economy |> PlayerEconomy.changeset(%{flame_level: level}) |> Repo.update()
    end
  end

  @doc "Reset player to initial state."
  def clear_save(player_uid) do
    Repo.transaction(fn ->
      # Delete all player entities
      from(p in PlayerPlot, where: p.player_uid == ^player_uid) |> Repo.delete_all()
      from(v in PlayerVase, where: v.player_uid == ^player_uid) |> Repo.delete_all()
      from(g in PlayerGarden, where: g.player_uid == ^player_uid) |> Repo.delete_all()
      from(m in PlayerMallum, where: m.player_uid == ^player_uid) |> Repo.delete_all()
      from(b in CampFire.Game.PlayerBird, where: b.player_uid == ^player_uid) |> Repo.delete_all()
      from(h in CampFire.Game.PlayerMallumHouse, where: h.player_uid == ^player_uid) |> Repo.delete_all()
      from(s in CampFire.Economy.PlayerSeed, where: s.player_uid == ^player_uid) |> Repo.delete_all()
      from(i in CampFire.Economy.PlayerItem, where: i.player_uid == ^player_uid) |> Repo.delete_all()

      # Delete economy record
      case Repo.get(PlayerEconomy, player_uid) do
        nil -> :ok
        economy -> Repo.delete!(economy)
      end

      # Delete player state
      case Repo.get(PlayerState, player_uid) do
        nil -> :ok
        state -> Repo.delete!(state)
      end

      # Re-init
      Economy.init_economy(player_uid)
      :ok
    end)
  end
end
```

Note: This requires exposing two currently-private functions from existing modules. See Task 2.

**Step 2: Commit**

```bash
git add server/lib/camp_fire/game/debug.ex
git commit -m "feat(server): add Debug module with server-side cheat functions"
```

---

### Task 2: Server — Expose helpers needed by Debug module

**Files:**
- Modify: `server/lib/camp_fire/game/birds.ex`
- Modify: `server/lib/camp_fire/game/mallums.ex`

**Step 1: Add public `try_spawn_bird_public/2` to Birds**

In `server/lib/camp_fire/game/birds.ex`, add after `insert_bird/5` (after line 115):

```elixir
@doc "Force-spawn a bird (debug use). Returns {:ok, bird} or :no_tile / :no_seed."
def try_spawn_bird_public(player_uid, flame_level) do
  try_spawn_bird(player_uid, flame_level, [])
end
```

**Step 2: Add public `get_quest_config_public/1` to Mallums**

In `server/lib/camp_fire/game/mallums.ex`, add after `get_quest_configs/0` (after line 55):

```elixir
def get_quest_config_public(quest_name), do: get_quest_config(quest_name)
```

**Step 3: Commit**

```bash
git add server/lib/camp_fire/game/birds.ex server/lib/camp_fire/game/mallums.ex
git commit -m "feat(server): expose bird spawn and quest config helpers for debug"
```

---

### Task 3: Server — Debug Controller and Routes

**Files:**
- Create: `server/lib/camp_fire_web/controllers/debug_controller.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

**Step 1: Create DebugController**

```elixir
defmodule CampFireWeb.DebugController do
  use CampFireWeb, :controller

  alias CampFire.Game.Debug

  def skip_time(conn, %{"hours" => hours}) when is_integer(hours) do
    uid = conn.assigns.current_player.uid
    case Debug.skip_time(uid, hours) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true, hours: hours})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end
  def skip_time(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'hours' (integer)"})

  def set_currency(conn, params) do
    uid = conn.assigns.current_player.uid
    opts = []
    opts = if params["mana"], do: Keyword.put(opts, :mana, params["mana"] / 1), else: opts
    opts = if params["gems"], do: Keyword.put(opts, :gems, params["gems"]), else: opts

    case Debug.set_currency(uid, opts) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end

  def grant_seeds(conn, %{"seedName" => name, "count" => count}) when is_integer(count) do
    uid = conn.assigns.current_player.uid
    Debug.grant_seeds(uid, name, count)
    conn |> put_status(200) |> json(%{ok: true})
  end
  def grant_seeds(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'seedName' and 'count'"})

  def grant_items(conn, %{"itemName" => name, "count" => count}) when is_integer(count) do
    uid = conn.assigns.current_player.uid
    Debug.grant_items(uid, name, count)
    conn |> put_status(200) |> json(%{ok: true})
  end
  def grant_items(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'itemName' and 'count'"})

  def spawn_bird(conn, _params) do
    uid = conn.assigns.current_player.uid
    case Debug.spawn_bird(uid) do
      {:ok, bird} -> conn |> put_status(200) |> json(%{ok: true, birdId: bird.id})
      :no_tile -> conn |> put_status(422) |> json(%{error: "no_free_tile"})
      :no_seed -> conn |> put_status(422) |> json(%{error: "no_eligible_seed"})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end

  def complete_quests(conn, _params) do
    uid = conn.assigns.current_player.uid
    {:ok, count} = Debug.complete_quests(uid)
    conn |> put_status(200) |> json(%{ok: true, completed: count})
  end

  def fill_vases(conn, _params) do
    uid = conn.assigns.current_player.uid
    {:ok, count} = Debug.fill_vases(uid)
    conn |> put_status(200) |> json(%{ok: true, filled: count})
  end

  def mature_plots(conn, _params) do
    uid = conn.assigns.current_player.uid
    {:ok, count} = Debug.mature_plots(uid)
    conn |> put_status(200) |> json(%{ok: true, matured: count})
  end

  def set_flame_level(conn, %{"level" => level}) when is_integer(level) do
    uid = conn.assigns.current_player.uid
    case Debug.set_flame_level(uid, level) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end
  def set_flame_level(conn, _), do: conn |> put_status(400) |> json(%{error: "Missing 'level' (integer)"})

  def clear_save(conn, _params) do
    uid = conn.assigns.current_player.uid
    case Debug.clear_save(uid) do
      {:ok, _} -> conn |> put_status(200) |> json(%{ok: true})
      {:error, reason} -> conn |> put_status(422) |> json(%{error: inspect(reason)})
    end
  end
end
```

**Step 2: Add routes to router.ex**

In `server/lib/camp_fire_web/router.ex`, add after the `/weather` scope (after line 161):

```elixir
  scope "/debug", CampFireWeb do
    pipe_through [:api, :authenticated]
    post "/skip-time", DebugController, :skip_time
    post "/set-currency", DebugController, :set_currency
    post "/grant-seeds", DebugController, :grant_seeds
    post "/grant-items", DebugController, :grant_items
    post "/spawn-bird", DebugController, :spawn_bird
    post "/complete-quests", DebugController, :complete_quests
    post "/fill-vases", DebugController, :fill_vases
    post "/mature-plots", DebugController, :mature_plots
    post "/set-flame-level", DebugController, :set_flame_level
    post "/clear-save", DebugController, :clear_save
  end
```

**Step 3: Compile and verify no errors**

Run: `cd server && mix compile`
Expected: Compilation succeeds with no errors.

**Step 4: Commit**

```bash
git add server/lib/camp_fire_web/controllers/debug_controller.ex server/lib/camp_fire_web/router.ex
git commit -m "feat(server): add debug controller with /debug/* endpoints"
```

---

### Task 4: Client — DebugService

**Files:**
- Create: `Assets/Scripts/Services/DebugService.cs`

**Step 1: Create DebugService**

Thin wrapper that POSTs to `/debug/*` endpoints and triggers game state re-sync. Follow the same HTTP pattern as `GameService.cs` (PostJson, SendAsync, SetAuthHeader).

```csharp
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class DebugService : MonoBehaviour
    {
        public static DebugService Instance { get; private set; }

        private static string ServerBaseUrl => ServerConfig.BaseUrl;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public async Task<bool> SkipTime(int hours)
        {
            return await Post("/debug/skip-time", JsonUtility.ToJson(new SkipTimeReq { hours = hours }));
        }

        public async Task<bool> SetCurrency(float? mana = null, int? gems = null)
        {
            var body = "{";
            if (mana.HasValue) body += $"\"mana\":{mana.Value}";
            if (mana.HasValue && gems.HasValue) body += ",";
            if (gems.HasValue) body += $"\"gems\":{gems.Value}";
            body += "}";
            return await Post("/debug/set-currency", body);
        }

        public async Task<bool> GrantSeeds(string seedName, int count)
        {
            return await Post("/debug/grant-seeds",
                JsonUtility.ToJson(new GrantSeedsReq { seedName = seedName, count = count }));
        }

        public async Task<bool> GrantItems(string itemName, int count)
        {
            return await Post("/debug/grant-items",
                JsonUtility.ToJson(new GrantItemsReq { itemName = itemName, count = count }));
        }

        public async Task<bool> SpawnBird()
        {
            return await Post("/debug/spawn-bird", "{}");
        }

        public async Task<bool> CompleteQuests()
        {
            return await Post("/debug/complete-quests", "{}");
        }

        public async Task<bool> FillVases()
        {
            return await Post("/debug/fill-vases", "{}");
        }

        public async Task<bool> MaturePlots()
        {
            return await Post("/debug/mature-plots", "{}");
        }

        public async Task<bool> SetFlameLevel(int level)
        {
            return await Post("/debug/set-flame-level",
                JsonUtility.ToJson(new SetFlameLevelReq { level = level }));
        }

        public async Task<bool> ClearSave()
        {
            return await Post("/debug/clear-save", "{}");
        }

        private async Task<bool> Post(string path, string json)
        {
            try
            {
                using var req = new UnityWebRequest(ServerBaseUrl + path, "POST");
                req.timeout = 15;
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                var token = SocialSaveManager.Instance?.Data?.authToken;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var tcs = new TaskCompletionSource<bool>();
                var op = req.SendWebRequest();
                op.completed += _ => tcs.SetResult(true);
                await tcs.Task;

                if (req.responseCode >= 200 && req.responseCode < 300)
                {
                    Debug.Log($"[DebugService] {path} OK: {req.downloadHandler.text}");
                    // Re-sync game state
                    GameService.Instance?.Initialize();
                    return true;
                }

                Debug.LogWarning($"[DebugService] {path} failed ({req.responseCode}): {req.downloadHandler.text}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DebugService] {path} error: {e.Message}");
                return false;
            }
        }

        [Serializable] private class SkipTimeReq { public int hours; }
        [Serializable] private class GrantSeedsReq { public string seedName; public int count; }
        [Serializable] private class GrantItemsReq { public string itemName; public int count; }
        [Serializable] private class SetFlameLevelReq { public int level; }
    }
}
```

**Step 2: Add DebugService to the scene**

The DebugService component needs to be added to the `"--- Services ---"` or `"--- UI ---"` GameObject in the Unity scene (same pattern as other singletons). Since we can't use Unity editor from CLI, we add it via code in `CampFireUI.cs` or `GameManager.cs` — wherever other services are bootstrapped. Alternatively, add it to the `"--- UI ---"` GameObject directly since `DebugWeatherPanel` is already there.

Check how `DebugWeatherPanel` is added. If it's manually on the GO, just add `DebugService` the same way via `gameObject.AddComponent<DebugService>()` in `CampFireUI.Initialize()`, gated behind the same `isEditor || isDebugBuild` check.

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/DebugService.cs
git commit -m "feat: add DebugService for server-side debug API calls"
```

---

### Task 5: Client — Remove old local cheats

**Files:**
- Modify: `Assets/Scripts/Utils/GameTime.cs` — remove SetOverride/ClearOverride
- Modify: `Assets/Scripts/Managers/GameManager.cs` — remove Shift+G shortcut
- Modify: `Assets/Scripts/Managers/BirdManager.cs` — remove `GameTime.IsOverridden` check

**Step 1: Simplify GameTime.cs**

Replace the entire file with:

```csharp
using System;

namespace Garden
{
    public static class GameTime
    {
        public static DateTime UtcNow => DateTime.UtcNow;
        public static DateTime Now => DateTime.Now;
    }
}
```

**Step 2: Remove Shift+G from GameManager.cs**

Remove the `#if UNITY_EDITOR` block (lines 40-45) that checks for `Shift+G` and calls `GrantInfiniteGems`.

**Step 3: Remove GameTime.IsOverridden check from BirdManager.cs**

Change line 34 back to the simpler check (undo the fix we applied earlier):

```csharp
if (GameService.Instance != null && GameService.Instance.IsOnline)
```

Time skipping is now server-side, so `BirdManager` should always use the server path when online.

**Step 4: Commit**

```bash
git add Assets/Scripts/Utils/GameTime.cs Assets/Scripts/Managers/GameManager.cs Assets/Scripts/Managers/BirdManager.cs
git commit -m "refactor: remove local time override and old cheat shortcuts"
```

---

### Task 6: Client — Rewire Debug Panel UI

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (lines 220-288)
- Modify: `Assets/Scripts/Debug/DebugWeatherPanel.cs`

**Step 1: Update UXML debug panel**

Replace the "Time" and "Cheats" sections (lines 264-287) with new server-backed controls. Keep the "Server" and "Weather Override" sections unchanged. The new sections:

```xml
<ui:Label text="Time" class="debug-section-header" />
<ui:VisualElement class="debug-row">
    <ui:Label text="Hours" class="debug-label" />
    <ui:IntegerField name="time-skip-field" value="1" />
    <ui:Button name="time-skip-button" text="Skip Time" class="preset-btn" />
</ui:VisualElement>
<ui:Label name="current-time-label" />

<ui:Label text="Economy" class="debug-section-header" />
<ui:VisualElement class="debug-row">
    <ui:Label text="Mana" class="debug-label" />
    <ui:FloatField name="debug-mana-field" value="999" />
    <ui:Button name="set-mana-button" text="Set" class="preset-btn" />
</ui:VisualElement>
<ui:VisualElement class="debug-row">
    <ui:Label text="Gems" class="debug-label" />
    <ui:IntegerField name="debug-gems-field" value="999" />
    <ui:Button name="set-gems-button" text="Set" class="preset-btn" />
</ui:VisualElement>
<ui:VisualElement class="debug-row">
    <ui:Label text="Flame Lvl" class="debug-label" />
    <ui:IntegerField name="debug-flame-field" value="1" />
    <ui:Button name="set-flame-button" text="Set" class="preset-btn" />
</ui:VisualElement>

<ui:Label text="Inventory" class="debug-section-header" />
<ui:VisualElement class="debug-row">
    <ui:TextField name="debug-seed-name" value="Sprouts" />
    <ui:IntegerField name="debug-seed-count" value="10" />
    <ui:Button name="grant-seeds-button" text="Grant" class="preset-btn" />
</ui:VisualElement>
<ui:VisualElement class="debug-row">
    <ui:TextField name="debug-item-name" value="Speed_Potion" />
    <ui:IntegerField name="debug-item-count" value="5" />
    <ui:Button name="grant-items-button" text="Grant" class="preset-btn" />
</ui:VisualElement>

<ui:Label text="Quick Actions" class="debug-section-header" />
<ui:Button name="spawn-bird-button" text="Spawn Bird" />
<ui:Button name="complete-quests-button" text="Complete Quests" />
<ui:Button name="fill-vases-button" text="Fill Vases" />
<ui:Button name="mature-plots-button" text="Mature Plots" />

<ui:Label text="Danger Zone" class="debug-section-header" />
<ui:Toggle name="free-mode-toggle" label="Free Mode" />
<ui:Button name="clear-save-button" text="Clear Save Data" class="btn-danger" />
```

Remove the old time override fields (`time-override-field`, `set-time-button`, `reset-time-button`) and `max-currency-button`.

**Step 2: Rewire DebugWeatherPanel.cs**

Replace the time override and cheat wiring. Remove fields: `timeOverrideField`. Remove methods: `SetTimeOverride()`, `ResetTimeOverride()`, `MaxCurrency()`. Replace `SkipTime()` and `ClearSaveData()` with calls to `DebugService`. Add wiring for new buttons.

The key changes in `Initialize()`:

```csharp
// Time skip — now server-backed
root.Q<Button>("time-skip-button")?.RegisterCallback<ClickEvent>(_ =>
{
    int hours = Mathf.Max(1, timeSkipField != null ? timeSkipField.value : 1);
    _ = DebugService.Instance?.SkipTime(hours);
});

// Economy
root.Q<Button>("set-mana-button")?.RegisterCallback<ClickEvent>(_ =>
{
    var field = root.Q<FloatField>("debug-mana-field");
    if (field != null) _ = DebugService.Instance?.SetCurrency(mana: field.value);
});
root.Q<Button>("set-gems-button")?.RegisterCallback<ClickEvent>(_ =>
{
    var field = root.Q<IntegerField>("debug-gems-field");
    if (field != null) _ = DebugService.Instance?.SetCurrency(gems: field.value);
});
root.Q<Button>("set-flame-button")?.RegisterCallback<ClickEvent>(_ =>
{
    var field = root.Q<IntegerField>("debug-flame-field");
    if (field != null) _ = DebugService.Instance?.SetFlameLevel(field.value);
});

// Inventory
root.Q<Button>("grant-seeds-button")?.RegisterCallback<ClickEvent>(_ =>
{
    var name = root.Q<TextField>("debug-seed-name")?.value;
    var count = root.Q<IntegerField>("debug-seed-count")?.value ?? 1;
    if (!string.IsNullOrEmpty(name)) _ = DebugService.Instance?.GrantSeeds(name, count);
});
root.Q<Button>("grant-items-button")?.RegisterCallback<ClickEvent>(_ =>
{
    var name = root.Q<TextField>("debug-item-name")?.value;
    var count = root.Q<IntegerField>("debug-item-count")?.value ?? 1;
    if (!string.IsNullOrEmpty(name)) _ = DebugService.Instance?.GrantItems(name, count);
});

// Quick Actions
root.Q<Button>("spawn-bird-button")?.RegisterCallback<ClickEvent>(_ =>
    _ = DebugService.Instance?.SpawnBird());
root.Q<Button>("complete-quests-button")?.RegisterCallback<ClickEvent>(_ =>
    _ = DebugService.Instance?.CompleteQuests());
root.Q<Button>("fill-vases-button")?.RegisterCallback<ClickEvent>(_ =>
    _ = DebugService.Instance?.FillVases());
root.Q<Button>("mature-plots-button")?.RegisterCallback<ClickEvent>(_ =>
    _ = DebugService.Instance?.MaturePlots());

// Free mode toggle — stays local
// ... (keep existing code)

// Clear save — now server-backed
root.Q<Button>("clear-save-button")?.RegisterCallback<ClickEvent>(_ =>
{
    _ = DebugService.Instance?.ClearSave();
});
```

Remove the `Update()` time override display logic (simplify to just show current server time).

**Step 3: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml Assets/Scripts/Debug/DebugWeatherPanel.cs
git commit -m "feat: rewire debug panel to use server-side debug endpoints"
```

---

### Task 7: Client — Ensure DebugService is instantiated

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

**Step 1: Add DebugService component alongside DebugWeatherPanel**

In `CampFireUI.Initialize()`, where the debug button is wired up (around line 132), ensure `DebugService` exists:

```csharp
if (Application.isEditor || UnityEngine.Debug.isDebugBuild)
{
    if (GetComponent<DebugService>() == null)
        gameObject.AddComponent<DebugService>();
    // ... existing debug button wiring
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat: bootstrap DebugService on debug-enabled builds"
```

---

### Task 8: Server — Test compilation and manual smoke test

**Step 1: Compile server**

Run: `cd server && mix compile`
Expected: No errors.

**Step 2: Run server tests**

Run: `cd server && mix test`
Expected: All existing tests pass (no regressions).

**Step 3: Commit any fixes if needed**

---

### Task 9: Cleanup — Remove stale GameTime references

**Files:**
- Search all .cs files for `GameTime.IsOverridden`, `GameTime.SetOverride`, `GameTime.ClearOverride`
- Remove or update any remaining references

**Step 1: Search for stale references**

Grep for `GameTime\.IsOverridden|GameTime\.SetOverride|GameTime\.ClearOverride` across all .cs files. Fix any remaining callers.

**Step 2: Commit**

```bash
git commit -m "refactor: remove remaining GameTime override references"
```
