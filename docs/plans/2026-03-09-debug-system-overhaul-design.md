# Debug System Overhaul Design

## Problem

The current debug system was built for offline/local play. Now that the game is server-authoritative:
- **Time skip** doesn't work — server uses real time, ignores `GameTime` overrides
- **Max Currency** desyncs client from server — local mana/gems get overwritten on next server sync, causing UI flashes
- **Free Mode** works (server accepts `free_mode` param) but other cheats don't
- **Missing features** — no way to grant seeds/items, spawn birds, complete quests, or set flame level from debug panel
- **Fake timestamps** — time-skipped `GameTime.UtcNow` gets sent to server as `plant_time_utc` etc., potentially corrupting server state

## Approach

Replace local debug cheats with **server-side debug endpoints**. Each debug action mutates server state directly (no per-player time offset). Client debug panel calls these endpoints, then re-syncs game state.

## Server Side

### Routes

New `/debug/*` scope using existing `:api` + `:authenticated` pipeline. All endpoints use `player_uid` from auth token.

```
POST /debug/skip-time         {hours}
POST /debug/set-currency      {mana?, gems?}
POST /debug/grant-seeds       {seed_name, count}
POST /debug/grant-items       {item_name, count}
POST /debug/spawn-bird        {}
POST /debug/complete-quests   {}
POST /debug/fill-vases        {}
POST /debug/mature-plots      {}
POST /debug/set-flame-level   {level}
POST /debug/clear-save        {}
```

### New Module: `CampFire.Game.Debug`

Contains all debug mutation logic. Each function takes `player_uid` and params.

### skip-time Implementation

Shift existing timestamps backward by N hours so the next regular check sees elapsed time:
- **Plots**: `plant_time_utc -= N hours`, `last_watered_utc -= N hours`
- **Vases**: `fill_start_time_utc -= N hours`
- **Mallums on quest**: `start_time_utc -= N hours`
- **Gardens**: `last_yield_utc -= N hours`
- **Birds**: shift `last_bird_check_hour_utc` back by N hours, then call `Birds.check_spawns` so the walk-forward logic runs for those hours

### Other Endpoints

- **set-currency**: Direct DB update on `PlayerEconomy.mana` / `PlayerEconomy.gems`
- **grant-seeds**: `Economy.upsert_seed(uid, seed_name, count)`
- **grant-items**: `Economy.upsert_item(uid, item_name, count)`
- **spawn-bird**: Call `Birds.try_spawn_bird` directly for this player
- **complete-quests**: Find all mallums with `state = "on_quest"`, set `state = "quest_complete"`, roll rewards via existing quest reward logic
- **fill-vases**: Set all `state = "filling"` vases to `state = "full"`, `current_water = capacity`
- **mature-plots**: Set all `state = "growing"` plots to `state = "mature"`
- **set-flame-level**: Direct DB update on `PlayerEconomy.flame_level`
- **clear-save**: Reset player economy to initial state (reuse existing init logic)

### Auth

No extra gating beyond existing player Bearer token auth. Can add admin-only guard later if needed.

## Client Side

### Remove

- `GameTime` override system (`SetOverride`, `ClearOverride`) — no longer needed when online; keep `GameTime.UtcNow` as pass-through to `DateTime.UtcNow`
- Local Max Currency button (replaced by server set-currency)
- `Shift+G` shortcut in `GameManager`
- Local-only currency manipulation in debug panel

### Replace Debug Panel

New server-backed controls:

| Control | UI Element | Server Call |
|---|---|---|
| Time Skip | IntegerField + Button | `/debug/skip-time` |
| Set Mana | FloatField + Button | `/debug/set-currency` |
| Set Gems | IntegerField + Button | `/debug/set-currency` |
| Grant Seeds | TextField + IntegerField + Button | `/debug/grant-seeds` |
| Grant Items | TextField + IntegerField + Button | `/debug/grant-items` |
| Spawn Bird | Button | `/debug/spawn-bird` |
| Complete Quests | Button | `/debug/complete-quests` |
| Fill Vases | Button | `/debug/fill-vases` |
| Mature Plots | Button | `/debug/mature-plots` |
| Set Flame Level | IntegerField + Button | `/debug/set-flame-level` |
| Clear Save | Button (danger) | `/debug/clear-save` |
| Free Mode | Toggle | Sets `CurrencyManager.FreeMode` (still sent in request bodies) |

### Keep As-Is

- **Weather override** — purely local/display, doesn't affect server state
- **Server selector** — switches between local/gigalixir
- **Debug button guard** — `isEditor || isDebugBuild`

### Post-Action Sync

After every debug endpoint call, re-sync full game state via `GameService.Instance.FetchGameState()` so UI reflects changes immediately.

### DebugService (New)

New client service to encapsulate debug API calls. Thin wrapper: POST to `/debug/{action}`, await response, trigger game state re-sync.

## Files to Create/Modify

### Server (create)
- `server/lib/camp_fire/game/debug.ex` — debug mutation logic
- `server/lib/camp_fire_web/controllers/debug_controller.ex` — endpoint handlers

### Server (modify)
- `server/lib/camp_fire_web/router.ex` — add `/debug` scope

### Client (create)
- `Assets/Scripts/Services/DebugService.cs` — server debug API calls

### Client (modify)
- `Assets/Scripts/Debug/DebugWeatherPanel.cs` — replace local cheats with server-backed buttons
- `Assets/Scripts/Utils/GameTime.cs` — remove override system (keep as thin DateTime wrapper)
- `Assets/Scripts/Managers/GameManager.cs` — remove Shift+G shortcut
- `Assets/Scripts/Managers/BirdManager.cs` — remove `GameTime.IsOverridden` check (no longer needed)
- `Assets/UI/Documents/CampFireRoot.uxml` — update debug panel markup
- `Assets/UI/Styles/Debug.uss` — update debug panel styles
