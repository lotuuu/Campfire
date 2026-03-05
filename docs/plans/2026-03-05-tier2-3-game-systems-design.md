# Tiers 2 & 3: Server-Authoritative Game Systems — Design

**Goal:** Move all game entity state, timers, weather, and quality scoring to the server. The server becomes the full source of truth for gameplay. Client becomes a display layer that syncs from server on startup.

**Context:** Tier 1 (economy ledger) is complete — server owns mana, gems, flame level, seeds, items. This design covers the remaining 9 migration items: quest rewards, harvest scoring, gift validation, garden yields, recipe mixing, plant timers, vase timers, server-side weather, and save data migration.

---

## Server Schema

### Entity Tables

**player_plots**
| Column | Type | Notes |
|--------|------|-------|
| id | serial PK | |
| player_uid | FK → players | |
| seed_name | text, nullable | null when empty |
| state | text | "empty", "growing", "mature" |
| plant_time_utc | utc_datetime, nullable | |
| water_count | integer, default 0 | |
| last_watered_utc | utc_datetime, nullable | |
| snapshots | jsonb, default {} | GrowthSnapshots data |
| grid_x | integer | |
| grid_y | integer | |
| skin_name | text, nullable | |
| unlocked_skins | jsonb, default [] | |

**player_vases**
| Column | Type | Notes |
|--------|------|-------|
| id | serial PK | |
| player_uid | FK → players | |
| capacity | integer | |
| current_water | integer, default 0 | |
| state | text | "empty", "filling", "full" |
| fill_start_time_utc | utc_datetime, nullable | |
| grid_x | integer | |
| grid_y | integer | |
| skin_name | text, nullable | |
| unlocked_skins | jsonb, default [] | |

**player_gardens**
| Column | Type | Notes |
|--------|------|-------|
| id | serial PK | |
| player_uid | FK → players | |
| plant_name | text | |
| plant_time_utc | utc_datetime | |
| last_yield_time_utc | utc_datetime, nullable | |
| mature | boolean, default false | |
| grid_x | integer | |
| grid_y | integer | |

**player_mallums**
| Column | Type | Notes |
|--------|------|-------|
| id | serial PK | |
| player_uid | FK → players | |
| state | text | "idle", "fetching_water", "on_quest", "quest_complete" |
| assigned_quest_name | text, nullable | |
| start_time_utc | utc_datetime, nullable | |
| assigned_vase_id | integer, nullable | FK → player_vases |
| pending_rewards | jsonb, default [] | [{seed_name, count}] |

### Config & Cache Tables

**seed_configs**
| Column | Type | Notes |
|--------|------|-------|
| id | serial PK | |
| seed_name | text, unique | e.g. "Basil" |
| growth_duration_hours | float | |
| base_drops | integer | |
| recipe | jsonb | Full GrowthRecipe params: per-axis {enabled, ideal_min, ideal_max, tolerance, weight} |

**weather_cache**
| Column | Type | Notes |
|--------|------|-------|
| id | serial PK | |
| lat | float | Rounded to 2 decimals |
| lon | float | Rounded to 2 decimals |
| weather_data | jsonb | Full weather: temp, wind, humidity, clouds, condition, moon_phase |
| rain_start_utc | utc_datetime, nullable | Tracks continuous rain duration |
| last_rain_effect_utc | utc_datetime, nullable | Prevents duplicate rain effects |
| fetched_at | utc_datetime | |
| unique index | (lat, lon) | |

### Catch-All State

**player_states**
| Column | Type | Notes |
|--------|------|-------|
| player_uid | FK → players, PK | |
| data | jsonb, default {} | mallum_houses, rain tracking, birds, apotheke position, etc. |

### Modifications to Existing Tables

**player_economies** — add columns:
- `lat` (float, nullable) — player's last known latitude
- `lon` (float, nullable) — player's last known longitude

---

## Server-Side Weather System

**Player location:**
- `POST /weather/location` receives `{lat, lon}` from client after GPS resolution
- Stored on `player_economies` (lat/lon columns)
- Rounded to 2 decimal places (~1.1km) for cache grouping

**Proactive weather polling:**
- GenServer runs every 15 minutes
- Queries all distinct (lat, lon) that have at least one player with a growing plot
- For each location: fetch from OWM if cache is stale (>15 min), otherwise use cache
- For each growing plot at that location: record weather snapshot into plot's `snapshots` JSONB
- Enriches with server-computed moon phase

**Rain detection:**
- During polling, if weather condition is rain/storm:
  - Set `rain_start_utc` on weather_cache if not already set
  - If rain persists 15+ minutes and `last_rain_effect_utc` is before `rain_start_utc`:
    - Auto-fill all vases for players at that location
    - Auto-water all growing plots (6-hour cooldown)
    - Free mallums from water-fetching state
    - Update `last_rain_effect_utc`
- If not raining: clear `rain_start_utc`

**Demand-driven polling:**
- Server only polls locations with active growth (growing plots)
- No active growth at a location → no API calls
- Players with no plants growing and app closed → zero cost

**OWM API budget:**
- One call per unique location per 15 minutes
- Free tier: 1000 calls/day (supports ~10 active locations)
- API key in server config/env

---

## Game Endpoints

### Quest System
| Endpoint | Method | Purpose | Validates | Mutates |
|----------|--------|---------|-----------|---------|
| `/game/quest/start` | POST | Send mallum on quest | Idle mallum exists, flame level met | Claims mallum, records start time |
| `/game/quest/check` | POST | Check quest status | Mallum is on_quest | Transitions to quest_complete if elapsed >= duration, rolls rewards |
| `/game/quest/collect` | POST | Collect rewards | Mallum is quest_complete | Adds seeds to player_seeds, resets mallum to idle |
| `/game/quest/speed-up` | POST | Use Speed Potion | Has Speed_Potion item, mallum on_quest | Consumes item, immediately completes quest, rolls rewards |

Server rolls rewards using weighted random from quest's reward pool. No client-side RNG.

### Plot System
| Endpoint | Method | Purpose | Validates | Mutates |
|----------|--------|---------|-----------|---------|
| `/game/plot/craft` | POST | Create plot | Entity cap, mana + items | Deducts cost, creates player_plot |
| `/game/plot/plant` | POST | Plant seed | Plot empty, has seed | Deducts seed, sets growing state |
| `/game/plot/water` | POST | Water plot | Growing state, 2hr cooldown, has water | Deducts 1 water from vase, increments water_count |
| `/game/plot/harvest` | POST | Harvest | State=mature | Evaluates GrowthRecipe, calculates drops, adds items, resets plot |
| `/game/plot/set-skin` | POST | Change skin | Skin in unlocked_skins | Updates skin_name |
| `/game/plots` | GET | Get all plots | Auth | Returns all player_plots |

### Vase System
| Endpoint | Method | Purpose | Validates | Mutates |
|----------|--------|---------|-----------|---------|
| `/game/vase/craft` | POST | Create vase | Entity cap, mana + items | Deducts cost, creates player_vase |
| `/game/vase/fill` | POST | Start filling | Idle mallum available | Claims mallum, sets fill_start_time |
| `/game/vase/check` | POST | Check fill | Vase is filling | Transitions to full if done, frees mallum |
| `/game/vase/set-skin` | POST | Change skin | Skin in unlocked_skins | Updates skin_name |
| `/game/vases` | GET | Get all vases | Auth | Returns all player_vases |

### Garden System
| Endpoint | Method | Purpose | Validates | Mutates |
|----------|--------|---------|-----------|---------|
| `/game/garden/plant` | POST | Plant permanent | Entity cap, has cost | Deducts cost, creates player_garden |
| `/game/garden/collect` | POST | Collect yield | Mature, yield interval elapsed | Adds items, updates last_yield_time |
| `/game/gardens` | GET | Get all gardens | Auth | Returns all player_gardens |

### Gift Validation (modify existing)
- `POST /gifts/send` now checks `player_items` to verify sender owns the items before creating the gift

### Recipe Mixing (modify existing economy)
- `POST /economy/spend-items` already handles item consumption
- No new endpoint needed — client sends spend-items for ingredients + add-items for result (already wired from Tier 1)

### Player State & Sync
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `GET /game/state` | GET | Full game state sync (economy + all entities + weather + cosmetic state) |
| `PUT /game/state` | PUT | Save cosmetic/layout JSONB blob |

### Weather
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `POST /weather/location` | POST | Submit GPS coords |
| `GET /weather/current` | GET | Get cached weather for player's location |

---

## GrowthRecipe Server-Side Evaluation

**seed_configs table** stores per-seed recipe parameters, seeded via `seeds.exs`.

**Evaluation algorithm (ported to Elixir):**
1. For each enabled axis, compute `score_range(actual, ideal_min, ideal_max, tolerance)`:
   - actual within [ideal_min, ideal_max] → 1.0
   - actual within tolerance band → linear falloff to 0.0
   - beyond tolerance → 0.0
2. Weather axes use averages from snapshots (sum_temp/snapshot_count, etc.)
3. Rain axis = rain_snapshots / snapshot_count ratio
4. Moon phase = dominant phase from moon_phase_snapshots array
5. Waterings axis = raw water_count
6. Final score = weighted average of all enabled axes
7. No axes enabled → 1.0 (vacuous truth)
8. Drops = max(1, round(base_drops * score))

At harvest, server returns score + per-axis breakdown + drops so client can show the harvest popup.

---

## Startup Sync

1. Client authenticates (existing SocialService flow)
2. Calls `GET /game/state` — returns everything in one response
3. Client populates SaveData from server response
4. Game renders

`save.json` becomes a local display cache — written on flush for fast cold-start rendering, overwritten when server responds. If server unreachable, gameplay blocked (online required).

---

## Timer Strategy — Lazy Evaluation

No background jobs for timer transitions. State changes happen on request:
- `POST /game/quest/check` → checks elapsed time, transitions if done
- `POST /game/vase/check` → checks elapsed time, transitions if done
- `GET /game/state` → evaluates all timers, returns current state
- `POST /game/plot/harvest` → checks growth complete before allowing harvest

Only exception: **weather polling GenServer** runs periodically because it needs to record snapshots regardless of client activity.

---

## Testing

**Server tests (~30-40):**
- Game context: plot lifecycle (craft → plant → water → harvest with quality scoring), vase lifecycle (craft → fill → complete), garden lifecycle (plant → mature → yield), quest lifecycle (start → complete → collect rewards), speed-up
- Weather: polling logic, snapshot recording, rain detection, cache TTL
- GrowthRecipe evaluation: multi-axis scoring, edge cases (no snapshots, no axes enabled)
- Gift validation: reject send when items insufficient
- State sync: GET /game/state returns complete data

**Client tests (~5-10):**
- Serialization of new request/response types
- SaveData population from server state response

---

## Out of Scope

- Config admin app (future)
- Multi-region weather optimization
- Leaderboards or competitive features
- Anti-cheat beyond server authority (no client-side detection)
