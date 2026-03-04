# Elixir Backend Migration Design

**Date**: 2026-03-04
**Status**: Approved
**Scope**: 1:1 port of Node.js/Express backend to Elixir/Phoenix

## Motivation

The backend is growing from a simple social API into a server-authoritative game server (see `backend-migration-todo.md` Tiers 1-3). Elixir/OTP is a better fit for per-player state management, timer-driven game systems, and concurrent validation. The developer is also more experienced in Elixir and it aligns with company stack.

## Scope

Port all existing endpoints 1:1. Same behavior, same JSON shapes, Unity client unchanged. No Tier 1-3 features in this pass.

### Out of Scope

- Server-authoritative economy, inventory, timers (Tiers 1-3)
- WebSocket/Channel support
- Production deployment (Fly.io, etc.)

## Project Structure

```
server/
  mix.exs
  config/
    config.exs            # base config
    dev.exs               # local dev (localhost:5432)
    test.exs              # test DB
    runtime.exs           # prod env vars (DATABASE_URL, PORT)
  lib/
    camp_fire/            # business logic (contexts)
      accounts.ex         # player CRUD, auth token, friend codes
      accounts/
        player.ex         # Ecto schema
      social.ex           # friends, friend requests
      social/
        friend.ex
        friend_request.ex
      villages.ex         # village snapshots
      villages/
        village.ex
      gifts.ex            # gift send/claim
      gifts/
        gift.ex
      visitors.ex         # visitor selection, quest tracking
      visitors/
        visitor_template.ex
        visitor_schedule.ex
        visitor_quest.ex
        player_visit_count.ex
      repo.ex
    camp_fire_web/        # HTTP layer
      router.ex
      endpoint.ex
      controllers/
        auth_controller.ex
        auth_json.ex
        friend_controller.ex
        friend_json.ex
        village_controller.ex
        village_json.ex
        gift_controller.ex
        gift_json.ex
        visitor_controller.ex
        visitor_json.ex
        health_controller.ex
      plugs/
        authenticate.ex   # Bearer token plug
        rate_limit.ex     # rate limiting plug
  priv/
    repo/
      migrations/         # Ecto migrations
      seeds.exs           # visitor templates + schedule
  test/
    camp_fire/
      accounts_test.exs
      social_test.exs
      gifts_test.exs
      visitors_test.exs
    camp_fire_web/
      controllers/
        auth_controller_test.exs
        friend_controller_test.exs
        village_controller_test.exs
        gift_controller_test.exs
        visitor_controller_test.exs
  docker-compose.yml      # Postgres 16 only
  Makefile
```

## Database Schema

Fresh Ecto migrations. Same tables and columns, Ecto-idiomatic.

| Ecto Schema | Table | Key Fields |
|---|---|---|
| `Player` | `players` | uid (UUID PK), auth_token, friend_code, display_name, timestamps |
| `FriendRequest` | `friend_requests` | from_uid, to_uid, status (pending/accepted/declined) |
| `Friend` | `friends` | player_uid, friend_uid (symmetric pair) |
| `Village` | `villages` | player_uid (unique), snapshot (:map JSONB) |
| `Gift` | `gifts` | from_uid, to_uid, items (:map), status, claimed_at |
| `VisitorTemplate` | `visitor_templates` | visitor_id, name, type, flame_level_min, weight, dialogue/offer/gift/quest pools |
| `VisitorSchedule` | `visitor_schedule` | visitor_id FK, date, visit_number, weather_condition, priority |
| `VisitorQuest` | `visitor_quests` | player_uid, visitor_id, request_item/count, return_date_utc, reward |
| `PlayerVisitCount` | `player_visit_counts` | player_uid, count, last_visit_date |

Same indexes as current schema.

## Endpoint Mapping

All endpoints identical. Same paths, same request/response JSON (camelCase keys).

### Auth (`/auth`, rate limited 5/min/IP)
- `POST /auth/register` — create player, return {uid, authToken, friendCode, displayName}
- `PUT /auth/display-name` — validate 1-20 chars alphanumeric+spaces

### Friends (`/friends`, authenticated)
- `GET /friends` — list friends
- `POST /friends/request` — send by friend code
- `GET /friends/requests` — incoming pending
- `POST /friends/accept/:requestId` — transactional, MAX_FRIENDS=20
- `POST /friends/decline/:requestId`
- `DELETE /friends/:friendUid` — remove symmetric rows

### Village (`/village`, authenticated)
- `PUT /village` — upsert snapshot (100KB max)
- `GET /village/:uid` — friends-only

### Gifts (`/gifts`, authenticated)
- `POST /gifts/send` — 1-3 items, friends-only, 5/day limit
- `GET /gifts` — pending from last 7 days
- `POST /gifts/claim/:giftId`

### Visitors (`/visitors`, authenticated)
- `GET /visitors/tonight` — 5-priority selection cascade
- `POST /visitors/quest/accept`
- `POST /visitors/quest/complete`

### Health
- `GET /health` — {"status": "ok"}

## Auth & Middleware

- **Authentication Plug**: Bearer token lookup, assigns `current_player`, async `last_online` update
- **Rate Limiting**: ETS-based via Hammer library. Global 100 req/min/IP, auth 5/min/IP
- **Trust proxy**: Respect `X-Forwarded-For` first hop (for ngrok)
- **Error handling**: Phoenix ErrorJSON, Ecto changeset formatting

## Dev Workflow

| Make target | What it does |
|---|---|
| `make setup` | docker up (Postgres), mix deps.get, mix ecto.setup |
| `make dev` | ngrok tunnel + update DevServerConfig.cs + mix phx.server |
| `make tunnel-stop` | kill ngrok, reset DevServerConfig.cs |
| `make psql` | docker exec into Postgres |
| `make reset` | docker down -v, mix ecto.reset |
| `make test` | mix test |

**Local requirements**: Elixir ~1.17+ / OTP 27 via mise or asdf. Postgres 16 in Docker.

**Port**: 4000 (Phoenix default). DevServerConfig updated accordingly.

## Testing

Basic controller tests for every endpoint using Phoenix ConnTest. The Node server had zero tests — this is a quality improvement.

## Future Readiness

Phoenix context boundaries map to future server-authoritative systems:
- `Economy` context for Tier 1 (mana, gems, water ledger)
- `Garden` context for Tier 2 (plots, harvests, growth timers)
- `Quests` context for Tier 2 (mallum quest management)
- GenServer-per-player for in-memory state + timer management
- Supervision trees for fault tolerance
