# Admin Dashboard Design

**Date**: 2026-03-05
**Status**: Approved

## Overview

A Phoenix LiveView admin dashboard at `/admin` in the existing `server/` app for visualizing and configuring all game data. Full CRUD for seeds, economy, visitors, quests, and player data. All config migrates to Postgres as source of truth.

## Architecture

- Phoenix LiveView pages with sidebar nav + content area
- New `Admin` context module for read/write queries
- Auth: `ADMIN_SECRET` env var checked against session cookie
- 5 tabs: Seeds, Economy, Visitors, Quests, Players
- Tailwind CSS via CDN for styling (no build pipeline)

## New DB Tables

### `quest_configs`
| Column | Type | Notes |
|--------|------|-------|
| quest_name | text, unique | PK-like identifier |
| duration_minutes | integer | |
| required_flame_level | integer | |
| reward_rolls | integer | Number of rolls from pool |
| reward_pool | jsonb | `[{seed_name, weight, min_count, max_count}]` |
| timestamps | utc_datetime | |

### `garden_configs`
| Column | Type | Notes |
|--------|------|-------|
| plant_name | text, unique | |
| growth_duration_hours | float | |
| yield_item | text | |
| yield_amount | integer | |
| yield_interval_hours | float | |
| water_required | integer | |
| mana_cost | float | |
| timestamps | utc_datetime | |

### `game_config`
| Column | Type | Notes |
|--------|------|-------|
| key | text, unique | e.g. "flame_config", "vase_config" |
| value | jsonb | Structured config data |
| timestamps | utc_datetime | |

Keys: `flame_config`, `vase_config`, `building_costs`, `mallum_house_config`

## Tab Designs

### Seeds Tab
- Table: all `seed_configs` — seed_name, growth_duration_hours, base_drops, mana_cost
- Edit form: all fields + expandable GrowthRecipe editor (toggle axes, set ideal range/tolerance/weight)
- Actions: create, edit, delete

### Economy Tab
- One card per `game_config` key with structured field editors
- `flame_config`: mana rates, entity caps per level array, grid sizes per level array, upgrade recipes
- `vase_config`: base capacity, craft cost, fill duration, capacity tiers, upgrade costs
- `building_costs`: plot and vase cost tiers (mana + harvest costs)
- `mallum_house_config`: mallums per house, house cost tiers

### Visitors Tab
- Table: all `visitor_templates` — visitor_id, name, type, weight, flame_level_min
- Edit form: dialogue pool (add/remove lines), offer/gift/quest pools (structured list editors)
- Schedule sub-tab: `visitor_schedule` CRUD — date, visitor_id, priority, weather_condition

### Quests Tab
- Table: all `quest_configs` — quest_name, duration, flame req, reward count
- Edit form: reward pool as weighted list editor (seed, weight, min/max count)

### Players Tab
- Search by UID, friend code, or display name
- Player detail: economy (mana/gems/flame), inventory (seeds + items), entity counts
- Sub-sections: plots, vases, gardens, mallums as mini tables
- Edit: adjust mana/gems/flame level, grant/remove seeds/items

## Technical Details

### Enabling LiveView
- Add `phoenix_live_view` and `phoenix_html` deps to mix.exs
- Uncomment LiveView socket in endpoint.ex
- Root layout template + minimal app.js
- New `:admin` pipeline in router with browser session + CSRF

### Admin Auth
- `AdminAuth` plug checks `ADMIN_SECRET` env var against session cookie
- Login page: paste secret → set session cookie → redirect to `/admin`
- No player auth reuse

### Config Cache
- `ConfigCache` GenServer loads all `game_config` rows at startup
- Exposes `ConfigCache.get("flame_config")` for runtime reads
- Admin save triggers cache refresh
- Contexts (`Game.Mallums`, `Game.Gardens`, `Game.Vases`) read from cache instead of hardcoded values

### Migration of Hardcoded Values
- Seed new tables with current hardcoded values in DB migration
- Update `Game.Mallums` quest configs to read from `quest_configs` table
- Update `Game.Gardens` plant configs to read from `garden_configs` table
- Economy constants from `game_config` table via ConfigCache

### LiveView File Structure
```
lib/camp_fire_web/live/
  admin_live/
    index.ex          # Sidebar + tab routing
    seeds_live.ex     # Seeds CRUD
    economy_live.ex   # Economy config editor
    visitors_live.ex  # Visitor templates + schedule
    quests_live.ex    # Quest configs CRUD
    players_live.ex   # Player browser + editor
  components/
    admin_components.ex  # Shared table, form, JSON editor components
```

### Client Impact (Follow-up, not v1)
- Unity client should eventually fetch quest/economy configs from server at startup
- For now, local ScriptableObjects remain as client-side data
