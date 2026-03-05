# Dev Dashboard Design

**Date**: 2026-03-05
**Status**: Approved

## Overview

A Phoenix LiveView admin dashboard at `/dev/*` for looking up players, managing visitors, and configuring game events. Runs inside the existing `camp_fire` Phoenix app.

## Tech Stack

- **Phoenix LiveView** for server-rendered real-time UI
- **Tailwind CSS** for styling
- **No auth** — dev-only access (localhost)

## Dependencies to Add

- `phoenix_live_view` — LiveView framework
- `phoenix_html` — HTML helpers
- `phoenix_live_dashboard` — free server stats/metrics page
- `esbuild` — JS bundling (LiveView client JS)
- `tailwind` — CSS utility framework

## Routes

All under `/dev` with a `:browser` pipeline (session + CSRF, no bearer token auth).

| Route | LiveView | Purpose |
|---|---|---|
| `/dev` | `DashboardLive` | System overview: player count, recent registrations, active players |
| `/dev/players` | `PlayerListLive` | Search players by UID/friend code/display name |
| `/dev/players/:uid` | `PlayerDetailLive` | Player detail: profile, friends, village snapshot, gifts |
| `/dev/visitors` | `VisitorListLive` | Visitor templates, schedule, tonight preview |
| `/dev/tools` | `ToolsLive` | Send gifts, modify resources, force visitors |

## Pages

### 1. System Overview (`/dev`)

- Total player count
- Registrations in last 24h / 7d
- Active players (by `updated_at` recency)
- Link to Phoenix LiveDashboard for server metrics

### 2. Player Lookup (`/dev/players`)

- Search box: query by UID, friend code, or display name (fuzzy ILIKE)
- Results table with columns: display name, friend code, UID, last online
- Click row to navigate to player detail

### 3. Player Detail (`/dev/players/:uid`)

- Profile: display name, friend code, UID, registered at, last online
- Friends list (names + friend codes)
- Village snapshot: rendered as formatted JSON or key fields (flame level, mana, gems, etc.)
- Recent gifts sent/received

### 4. Visitor Management (`/dev/visitors`)

- **Templates tab**: list all visitor templates (name, type, weight, flame_level_min). Inline create/edit forms.
- **Schedule tab**: calendar-style view of scheduled visitors by date. Add manual schedule entries.
- **Tonight preview**: select a player, show what tonight's visitor selection would return.

### 5. Tools (`/dev/tools`)

- **Send gift**: pick player (by friend code or UID), choose item type + count, send as admin gift
- **Modify resources**: look up player, set mana/gems/water in their village snapshot
- **Force visitor**: pick player + visitor template, override tonight's visitor

## Data Layer

Existing Ecto contexts (`Accounts`, `Villages`, `Gifts`, `Visitors`, `Social`) handle most reads. Add a new `CampFire.Admin` context for:

- Player search (fuzzy ILIKE on display_name, exact on UID/friend_code)
- Stats aggregations (player count, recent registrations)
- Direct village snapshot modification
- Admin gift insertion

## File Structure

```
lib/camp_fire_web/
  live/
    dev/
      dashboard_live.ex
      dashboard_live.html.heex
      player_list_live.ex
      player_list_live.html.heex
      player_detail_live.ex
      player_detail_live.html.heex
      visitor_list_live.ex
      visitor_list_live.html.heex
      tools_live.ex
      tools_live.html.heex
  components/
    layouts.ex          # root + app HTML layouts
    core_components.ex  # shared components (tables, forms, badges, nav)
lib/camp_fire/
  admin.ex              # Admin context for dev-specific queries
```
