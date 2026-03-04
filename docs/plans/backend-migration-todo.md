# Backend Migration Todo (Elixir)

Features to migrate to the server when rebuilding the backend in Elixir. Currently all client-side only.

## Tier 1 — Economy & Core State

1. **Server-side economy ledger** — Track mana, gems, water as server-authoritative values. All spend/earn operations go through server. Client displays but doesn't own the numbers.

2. **Resource spending validation** — All crafting (plots, vases, mallum houses), planting (seed costs), upgrades (flame ingredients) must be validated server-side before applying.

3. **Flame level management** — Server owns flame level and entity cap. Upgrade requests validated against ingredient inventory server-side.

4. **Seed & item inventory** — Server tracks all inventory (seeds, harvest items). Client syncs on load and after each action. Prevents duplication.

## Tier 2 — Game Systems

5. **Mallum quest completion & rewards** — Server manages quest timers, rolls rewards on completion, prevents instant-complete and re-rolling.

6. **Harvest quality scoring** — Server stores weather snapshots during growth and re-evaluates GrowthRecipe at harvest. Client can't fake perfect weather.

7. **Gift item ownership validation** — Server verifies sender actually owns the items before allowing gift send. Currently trusts the client.

8. **Garden yield timers** — Server manages yield intervals and collection. Prevents manipulating lastYieldTimeUtc.

9. **Recipe mixing validation** — Server checks ingredient ownership and consumes them for Apotheke recipes.

## Tier 3 — Timers & Weather

10. **Plant growth timer validation** — Server tracks plantTimeUtc and validates maturity before allowing harvest.

11. **Vase fill timer validation** — Server authorizes fill requests and tracks fill completion time.

12. **Weather as server-side source of truth** — Server fetches weather data (or caches OpenWeatherMap responses) so all players at same location get identical weather. Eliminates client-side debug weather abuse.

13. **Save data migration** — Move from local save.json to server-authoritative save. Client becomes a cache that syncs on load. Server is source of truth.

## Already Server-Driven

- **Visitor system** — GET /visitors/tonight, quest accept/complete (implemented March 2026)
- **Friend system** — requests, acceptance, friend list
- **Gift delivery** — friendship-gated with daily limits (but no item ownership validation yet)
- **Village snapshots** — display-only, pushed on save
- **Auth** — auto-register, bearer token
