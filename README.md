# Camp Fire

A campsite management game built around a magical flame (Spark of Ara). Players grow plants, dispatch Mallum helpers on quests, manage resources, and expand their hex-grid campsite using real-world weather data.

Built with Unity 6 (6000.3.6f1), 2D URP. Online-only with a server-authoritative economy powered by an Elixir/Phoenix backend.

## Prerequisites

- Unity 6 (6000.3.6f1)
- Docker (for local Postgres)
- Elixir/Erlang (for the Phoenix server)
- ngrok (for device testing against local server)

## Setup

### Unity Client

Open the project in Unity 6. The single scene is `Assets/Scenes/Garden.unity`.

### Weather API Key

The server uses OpenWeatherMap for real-world weather. For local dev, create:

```
server/secrets.json
```

```json
{
  "openWeatherMapApiKey": "YOUR_API_KEY_HERE"
}
```

This file is gitignored. The local Phoenix server reads it automatically via `dev.exs`. On Gigalixir, set the `OWM_API_KEY` env var instead.

### Server (Local Development)

From `server/`:

```bash
make setup    # start Postgres (Docker), install deps, create + migrate + seed DB
make dev      # start Phoenix server with ngrok tunnel (port 4000)
make start    # start Phoenix server without tunnel
```

The server provides all game config, economy, social features, and sprites. The client connects to `localhost:4000` by default (or the ngrok URL written to `DevServerConfig.cs`).

### Server (Deployment)

Hosted on Gigalixir at `https://campfire.gigalixirapp.com`. From `server/`:

```bash
make deploy              # push code, wait for new version, migrate forward
make redeploy            # push code, wait for new version, reset DB + re-migrate + re-seed
make deploy COMMIT=abc   # deploy a specific commit (no working tree changes)
make redeploy COMMIT=abc # redeploy a specific commit
```

`deploy` is for normal releases. `redeploy` nukes the DB and rebuilds from scratch — use for schema changes that require a fresh start.

## Architecture

```
Assets/Scripts/     Unity client (C#, namespace Garden)
  ├── Services/     Singletons: ConfigService, EconomyService, SaveManager, etc.
  ├── Managers/     Singletons: FlameManager, PlotManager, MallumManager, etc.
  ├── UI/           UI Toolkit controllers (CampFireUI, CampsiteViewUI, etc.)
  ├── Data/         Shared types (ConfigTypes, SaveData)
  └── Utils/        Helpers (HexGridUtil, MiniJson, GameTime)

server/             Phoenix backend (Elixir)
  ├── lib/camp_fire/          Contexts: Accounts, Economy, Game, Admin, etc.
  ├── lib/camp_fire_web/      Controllers, LiveViews, Router
  └── priv/                   Migrations, seeds, static assets (sprites)
```

All game config comes from the server via `ConfigService` — no local config ScriptableObjects. The server is the single source of truth for tuning values, seeds, quests, building costs, etc.

## Running Tests

**Unity**: Window > General > Test Runner > EditMode tab, or via Unity MCP `run_tests` tool.

**Server**: `cd server && mix test`
