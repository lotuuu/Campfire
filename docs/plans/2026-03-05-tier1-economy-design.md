# Tier 1: Server-Authoritative Economy — Design

**Goal:** Move mana, gems, flame level, seeds, and items from client-only to server-validated, using an optimistic client model with offline support.

**Context:** The Elixir/Phoenix backend (March 2026) currently handles auth, friends, villages, gifts, and visitors. All economy data lives in client-side `save.json`. This design makes the server the source of truth for player economy.

---

## Server Schema

Three new tables in the `Economy` Phoenix context:

### player_economies
| Column | Type | Default | Notes |
|--------|------|---------|-------|
| player_uid | string (FK → players) | — | Unique, one per player |
| mana | float | 50.0 | Current mana balance |
| gems | integer | 5 | Premium currency |
| flame_level | integer | 1 | Determines mana rate + entity cap |
| last_mana_collect_utc | utc_datetime | now | For passive mana calculation |
| timestamps | — | — | inserted_at, updated_at |

### player_seeds
| Column | Type | Notes |
|--------|------|-------|
| player_uid | string (FK → players) | — |
| seed_name | string | e.g. "Basil", "Sprouts" |
| count | integer | Must be > 0; delete row when 0 |
| Unique index | (player_uid, seed_name) | |

### player_items
| Column | Type | Notes |
|--------|------|-------|
| player_uid | string (FK → players) | — |
| item_name | string | e.g. "Basil_harvest", "Speed_Potion" |
| count | integer | Must be > 0; delete row when 0 |
| Unique index | (player_uid, item_name) | |

Water is not tracked server-side — it derives from vase state, which is a Tier 2+ concern.

---

## Action Endpoints

All under `/economy`, all require Bearer auth. Each returns updated state after mutation.

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/economy/state` | GET | Fetch current balances, seeds, items |
| `/economy/init` | POST | Initialize economy for new player (new-player defaults) |
| `/economy/collect-mana` | POST | Collect passive mana (server calculates from flame level + elapsed time) |
| `/economy/spend-mana` | POST | Deduct mana (body: `{amount}`) |
| `/economy/spend-gems` | POST | Deduct gems (body: `{amount}`) |
| `/economy/add-gems` | POST | Add gems (body: `{amount}`) |
| `/economy/upgrade-flame` | POST | Consume items + increment flame level |
| `/economy/add-seeds` | POST | Add seeds (body: `{seed_name, count}`) |
| `/economy/spend-seeds` | POST | Consume seeds (body: `{seed_name, count}`) |
| `/economy/add-items` | POST | Add items (body: `{item_name, count}`) |
| `/economy/spend-items` | POST | Consume items (body: `{items: [{item_name, count}]}`) |

### Mana Collection

The server owns passive mana calculation:
- Formula: `(baseManaPerSecond + (flameLevel - 1) * manaPerLevel) * elapsedSeconds`
- Uses `last_mana_collect_utc` to compute elapsed time
- Returns the new mana total; client overwrites its local value
- Client sends `client_timestamp` for sanity check (server rejects if wildly off)

### Flame Upgrade

- Client sends `POST /economy/upgrade-flame`
- Server looks up the recipe for the current flame level (from config)
- Validates player has all required items
- Consumes items, increments flame_level
- Returns updated economy state

---

## Client Integration

### EconomyService (new singleton)

Sits between managers and SaveManager. Manages the action queue and server sync.

**Optimistic flow:**
1. Manager applies change locally (as today via CurrencyManager/ApothekeManager)
2. Manager also enqueues an `EconomyAction` with type, params, and rollback snapshot
3. `EconomyService` drains queue in background, sends to server
4. On success: no-op (already applied locally)
5. On rejection (409/422): apply rollback snapshot, fetch full state via `GET /economy/state`, show toast

### Offline Queue

- `List<EconomyAction>` persisted to `economy_queue.json` alongside `save.json`
- On reconnect, replays queued actions sequentially
- If any action is rejected during replay, discard remaining queue and full-sync from server

### Startup Sync

1. Call `GET /economy/state`
2. If server has record → overwrite local mana/gems/flameLevel/seeds/items
3. If no record (HTTP 404) → call `POST /economy/init` → apply returned starting state
4. Clear any pending offline queue (fresh start means queue is stale)

### Mana Display

- `FlameManager` still accumulates mana locally each frame for smooth UI counter
- The "real" balance commits via `POST /economy/collect-mana` when:
  - Player spends mana (collect first, then spend)
  - App goes to background
  - Periodic interval (e.g. every 60 seconds)

---

## Error Handling

- **Server rejection**: Rollback local change, sync full state, show toast
- **Offline queue conflict**: Discard remaining queue on first failure, sync from server
- **Multi-device**: Last-write-wins; next `GET /economy/state` overwrites other device
- **Clock skew**: Server uses its own clock for mana calculation; client timestamp is advisory only

---

## Out of Scope (Tier 2+)

- Server-side vase/plot/garden entity tracking
- Server-side weather snapshots or growth validation
- Server-side quest timers
- Water spending validation (depends on vase state)
- Transaction audit log

---

## Testing

**Server (~15-20 tests):**
- Economy context: init, collect-mana time calc, spend/add mana/gems/seeds/items, upgrade-flame item consumption
- Controller: auth required, validation errors (insufficient funds, negative amounts, max level), success responses
- Edge cases: double-init, spend-more-than-balance, upgrade at max flame level

**Client (~5-8 tests):**
- EconomyService: queue persistence, action enqueue, replay on reconnect
- Rollback: rejection reverts local state
- Startup sync: server state overwrites local

**Manual integration:**
- Play in editor → collect mana → craft → verify server state → restart Play mode → verify loads from server
