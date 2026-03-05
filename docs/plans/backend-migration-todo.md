# Backend Migration Todo (Elixir)

Elixir/Phoenix backend migration complete (March 2026). All existing endpoints ported 1:1. Features below are next — to be built on the new Elixir foundation.

## Tier 1 — Economy & Core State ✅

1. ~~**Server-side economy ledger**~~ ✅ — `player_economies` table tracks mana, gems, flame level. Client syncs on launch, enqueues mutations.

2. ~~**Resource spending validation**~~ ✅ — All spend operations (mana, gems, seeds, items) validated server-side. Client optimistically applies, server rejects insufficient funds.

3. ~~**Flame level management**~~ ✅ — `POST /economy/upgrade-flame` validates item ingredients and increments level. Capped at level 12.

4. ~~**Seed & item inventory**~~ ✅ — `player_seeds` and `player_items` tables with upsert/spend operations. Client syncs full state on launch.

## Tier 2 — Game Systems ✅

5. ~~**Mallum quest completion & rewards**~~ ✅ — Server manages quest timers, rolls rewards on completion. 8 quests with weighted reward pools. Speed-up consumes Speed_Potion server-side.

6. ~~**Harvest quality scoring**~~ ✅ — Server stores weather snapshots during growth via WeatherPoller GenServer. GrowthRecipe evaluation ported to Elixir. seed_configs table stores per-seed recipe parameters.

7. ~~**Gift item ownership validation**~~ ✅ — `send_gift` deducts items from sender in a transaction before creating the gift. `claim_gift` adds items to receiver.

8. ~~**Garden yield timers**~~ ✅ — Server manages garden growth and yield intervals. BerryBush and Oak configs. Lazy evaluation on collect.

9. ~~**Recipe mixing validation**~~ ✅ — Handled via existing `POST /economy/spend-items` (Tier 1) + `POST /economy/add-items`. Server validates ingredient ownership.

## Tier 3 — Timers & Weather ✅

10. ~~**Plant growth timer validation**~~ ✅ — Server tracks plant_time_utc, validates maturity via seed_configs growth_duration_hours before allowing harvest. Lazy evaluation on harvest and GET /game/state.

11. ~~**Vase fill timer validation**~~ ✅ — Server authorizes fill (requires idle mallum), tracks fill_start_time_utc, validates completion (60s per unit). Lazy evaluation on check.

12. ~~**Weather as server-side source of truth**~~ ✅ — WeatherPoller GenServer proactively polls OWM every 15min for locations with active growth. weather_cache table with 15min TTL. Rain detection auto-fills vases after 15min sustained rain.

13. ~~**Save data migration**~~ ✅ — `GET /game/state` returns full game state (economy + entities + weather + cosmetic state). `PUT /game/state` saves cosmetic JSONB. Client is display cache; server is source of truth.

## Already Server-Driven

- **Visitor system** — GET /visitors/tonight, quest accept/complete (implemented March 2026)
- **Friend system** — requests, acceptance, friend list
- **Gift delivery** — friendship-gated with daily limits, now with item ownership validation
- **Village snapshots** — display-only, pushed on save
- **Auth** — auto-register, bearer token
