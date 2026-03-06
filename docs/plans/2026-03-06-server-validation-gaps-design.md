# Server Validation Gaps — Design

**Date**: 2026-03-06
**Status**: Approved
**Context**: Comparison of client (16dcbd08) vs current backend revealed exploitable gaps where the server trusts the client.

## Approach

Keep the existing **optimistic local + fire-and-forget notify** pattern for existing systems. Add server-side validation that rejects bad requests; on rejection, client does a full resync.

New systems (birds, apotheke craft, skins) use **server-authoritative** calls (client waits for response before applying locally).

Economy cheat endpoints (`add-gems`, `add-seeds`, `add-items`) kept as-is for dev convenience. To be addressed later.

---

## 1. Server-Side Validation on Existing Endpoints

### 1a. Entity Cap Enforcement

Add to `craft_plot`, `craft_vase`, `Gardens.plant`, and new `MallumHouses.craft`:

```
total = count(plots) + count(vases) + count(gardens) + count(mallum_houses) + 1
cap = ConfigCache("flame_config").entity_caps[flame_level - 1]
if total >= cap, return {:error, :entity_cap_reached}
```

Entity cap array served from `flame_config` in ConfigCache (already synced to client via `GET /game/configs`).

### 1b. Grid Coordinate Validation

On every entity placement (craft plot, craft vase, plant garden, craft mallum house):

1. **Bounds check**: Verify `(grid_x, grid_y)` is within hex grid radius for current flame level. Radius from `ConfigCache("flame_config").grid_sizes[flame_level - 1]`. Use axial distance: `max(|q|, |r|, |q+r|) <= radius`.
2. **Collision check**: Query all entity tables (plots, vases, gardens, mallum_houses, birds) for the player + check reserved positions (apotheke at grid position from player_states, flame at 0,0). Return `{:error, :hex_occupied}` if any match.
3. **Valid hex check**: Coordinates must be integers.

### 1c. Vase Ownership on Water

`Plots.water/3` must verify `vase.player_uid == player_uid` before calling `Vases.use_water`. Return `{:error, :not_owner}`.

### 1d. Transaction Wrapping

Wrap `Plots.water/3` and `Plots.harvest/2` in `Repo.transaction` to prevent partial state corruption.

### 1e. Seed Name Validation on Plant

`Plots.plant/3` must verify `seed_name` exists in `ConfigCache("seed_configs")` before proceeding. Return `{:error, :unknown_seed}`.

---

## 2. Mallum Houses — New Table + Context

### Table: `player_mallum_houses`

| Column | Type | Notes |
|--------|------|-------|
| `id` | bigserial PK | |
| `player_uid` | string FK | |
| `grid_x` | integer | |
| `grid_y` | integer | |
| `skin_name` | string | nullable |
| `unlocked_skins` | string[] | default [] |
| `inserted_at` / `updated_at` | timestamps | |

### Context: `CampFire.Game.MallumHouses`

- `list_houses/1` — list all houses for player
- `count_houses/1` — count for cost lookup
- `craft_house/3 (player_uid, grid_x, grid_y)` — checks entity cap, grid bounds/collision, spends mana + items from `ConfigCache("mallum_house_config").house_costs[min(count, len-1)]`, inserts house, spawns mallums up to `mallums_per_house * new_house_count`. All in transaction.

### Endpoint

`POST /game/mallum-house/craft` → `{"gridX": int, "gridY": int}` → serialized house + new mallum count

### Init Economy Update

`init_economy/1` creates 1 mallum house at (1, -1) alongside existing starter entities. Mallum count derived from `mallums_per_house * 1`.

### Mallum Count Enforcement

`Mallums.create_mallum/1` should only be called from `craft_house` (which calculates the target count). No standalone "add mallum" endpoint.

### GET /game/state

Include `mallumHouses` in the response payload.

---

## 3. Birds — Fully Server-Side

### Table: `player_birds`

| Column | Type | Notes |
|--------|------|-------|
| `id` | bigserial PK | |
| `player_uid` | string FK | |
| `grid_x` | integer | |
| `grid_y` | integer | |
| `seed_name` | string | |
| `seed_count` | integer | |
| `spawned_at_utc` | utc_datetime | |

### Context: `CampFire.Game.Birds`

- `list_birds/1` — list all birds for player
- `check_spawns/1 (player_uid)` — server-side hourly spawn logic:
  - Read `last_bird_check_hour_utc` from player_states data
  - Walk from last check hour to current hour
  - Each hour: `chance = 0.33 * 0.5^current_bird_count`. Roll random. If hit: pick random free hex tile (excluding all entities + flame + apotheke), pick eligible seed (`tier <= flame_level`), random count `[max(1, base-1), base+2)` where `base = max(1, flame_level - tier + 1)`. Insert bird row.
  - Update `last_bird_check_hour_utc`
  - Return list of new birds (for client animation)
- `collect_bird/2 (player_uid, bird_id)` — verify ownership, delete bird row, grant seeds via `Economy.upsert_seed`. Return `{seed_name, count}`.

### Endpoints

- `POST /game/bird/check` → `{newBirds: [{id, gridX, gridY, seedName, seedCount}]}`
- `POST /game/bird/collect` → `{"birdId": id}` → `{seedName, seedCount}`

### GET /game/state

Include `birds` in the response payload.

### Client Changes

- `BirdManager.ProcessHourlyChecks` → replaced with `POST /game/bird/check` call
- `BirdManager.CollectBird` → replaced with `POST /game/bird/collect` call
- Keep local bird list for rendering, populated from server responses
- Offline fallback: if server unreachable, skip bird processing (no local spawning)

---

## 4. Apotheke Craft — Server Endpoint

### Config: `recipe_configs` in ConfigCache

Store as `game_configs` key `"recipe_configs"`:

```json
{
  "Fertilizer": {"ingredients": [{"item_name": "Basil_harvest", "count": 2}], "result_item": "Fertilizer", "result_quantity": 1, "category": "consumable"},
  ...
}
```

### Context: `CampFire.Game.Apotheke`

- `craft/2 (player_uid, recipe_name)` — look up recipe in ConfigCache. Validate all ingredients available. In transaction: deduct all ingredients via `Economy.spend_item`, grant result via `Economy.upsert_item`. Return `{result_item, result_quantity}`.

### Endpoint

`POST /game/apotheke/craft` → `{"recipeName": "Fertilizer"}` → `{resultItem, resultQuantity}`

### GET /game/configs

Include `recipes` in the configs response so client can display recipe UI.

### Client Changes

- `ApothekeManager.Mix()` → call `POST /game/apotheke/craft` instead of local item manipulation + economy queue
- Server-authoritative: wait for response, then update local inventory from response or resync
- Keep `CanMix()` for UI gating (uses local inventory state)

---

## 5. Skins — Server Validation

### Approach

Extend existing `set-skin` endpoints to handle unlock + cost deduction.

### Server Logic (in each entity's context or shared `CampFire.Game.Skins` module)

`apply_skin(player_uid, entity_type, entity_id, skin_name)`:
1. Load skin definition from ConfigCache (skin configs served via `GET /game/configs`)
2. Load entity, verify ownership
3. If `skin_name` not in entity's `unlocked_skins`:
   - Verify player has `cost_item_name` x `cost_quantity`
   - Deduct items via `Economy.spend_item`
   - Append to `unlocked_skins`
4. Set `skin_name` on entity
5. All in transaction

### Endpoints

Existing `POST /game/plot/set-skin` and `POST /game/vase/set-skin` updated to include unlock logic. Add `POST /game/mallum-house/set-skin`.

### GET /game/configs

Include `skins` in configs response: `{skinName, buildingType, costItemName, costQuantity}`.

### Client Changes

- `SkinManager.ApplySkin()` → call server endpoint, apply locally on success
- Server-authoritative: wait for response before updating local state

---

## 6. Garden Mana Cost on Client

Server already has `mana_cost` on `GardenConfig` and `Gardens.plant` spends it. Client's `GardenManager.Plant()` currently doesn't charge mana.

### Fix

`GardenManager.Plant()` reads `manaCost` from `GardenPlantData` (already populated by `ConfigService` overlay from server's `garden_configs.mana_cost`) and calls `CurrencyManager.SpendMana()` before planting.

---

## 7. Init Economy Alignment

Update server `init_economy/1` to create:
- 1 mallum house at (1, -1) — **new**
- Mallum count = `mallums_per_house * 1` (from config) — **changed from hardcoded 1**
- Everything else stays the same (50 mana, 5 gems, 5 Sprouts, 3 Cress, 3 Speed_Potions, 1 plot at (-1,0), 1 full vase at (0,-1))

Client `GameManager` new-player setup should align or defer to server response via `GameService.Initialize()`.

---

## Not In Scope

- Economy cheat endpoint lockdown (deferred)
- Weather snapshot format alignment (server uses arrays, client uses sums — both work independently)
- Duplicate weather polling (server WeatherPoller handles server-side needs, client WeatherService handles UI display)
- API versioning
- Rate limiting per action
- Background job system
