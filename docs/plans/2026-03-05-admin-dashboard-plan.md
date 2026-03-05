# Admin Dashboard Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Phoenix LiveView admin dashboard for managing all Camp Fire game config and browsing player data.

**Architecture:** LiveView pages at `/admin` in the existing Phoenix server, with new DB tables for configs currently hardcoded. Config values served to game logic via an ETS-backed ConfigCache GenServer.

**Tech Stack:** Phoenix LiveView, Ecto, Postgres, Tailwind CSS (CDN), ETS cache

---

### Task 1: Add LiveView Dependencies

**Files:**
- Modify: `server/mix.exs`

**Step 1: Add phoenix_live_view and phoenix_html deps**

In `mix.exs`, add to the `deps` function:

```elixir
{:phoenix_html, "~> 4.1"},
{:phoenix_live_view, "~> 1.0"},
```

**Step 2: Install deps**

Run: `cd server && mix deps.get`
Expected: Dependencies fetched successfully

**Step 3: Commit**

```bash
cd server && git add mix.exs mix.lock
git commit -m "deps: add phoenix_live_view and phoenix_html"
```

---

### Task 2: Enable LiveView Socket and Root Layout

**Files:**
- Modify: `server/lib/camp_fire_web/endpoint.ex`
- Modify: `server/lib/camp_fire_web/router.ex`
- Create: `server/lib/camp_fire_web/components/layouts.ex`
- Create: `server/lib/camp_fire_web/components/layouts/root.html.heex`
- Create: `server/lib/camp_fire_web/components/layouts/admin.html.heex`

**Step 1: Uncomment LiveView socket in endpoint.ex**

In `endpoint.ex`, uncomment the LiveView socket lines:

```elixir
socket "/live", Phoenix.LiveView.Socket,
  websocket: [connect_info: [session: @session_options]],
  longpoll: [connect_info: [session: @session_options]]
```

**Step 2: Create layouts module**

Create `server/lib/camp_fire_web/components/layouts.ex`:

```elixir
defmodule CampFireWeb.Layouts do
  use CampFireWeb, :html

  embed_templates "layouts/*"
end
```

This requires adding an `:html` helper to `CampFireWeb`. Modify `server/lib/camp_fire_web.ex` — add a `html` function:

```elixir
def html do
  quote do
    use Phoenix.Component

    import Phoenix.HTML
    import CampFireWeb.Layouts

    unquote(verified_routes())
  end
end
```

**Step 3: Create root layout**

Create `server/lib/camp_fire_web/components/layouts/root.html.heex`:

```heex
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="csrf-token" content={get_csrf_token()} />
    <title>Camp Fire Admin</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script defer phx-track-static src="https://cdn.jsdelivr.net/npm/phoenix@1.7.21/priv/static/phoenix.min.js"></script>
    <script defer phx-track-static src="https://cdn.jsdelivr.net/npm/phoenix_live_view@1.0.0/priv/static/phoenix_live_view.min.js"></script>
    <script>
      window.addEventListener("DOMContentLoaded", () => {
        let csrfToken = document.querySelector("meta[name='csrf-token']").getAttribute("content");
        let liveSocket = new window.LiveView.LiveSocket("/live", window.Phoenix.Socket, {
          params: { _csrf_token: csrfToken },
          longPollFallbackMs: 2500
        });
        liveSocket.connect();
        window.liveSocket = liveSocket;
      });
    </script>
  </head>
  <body class="bg-gray-50 text-gray-900">
    {@inner_content}
  </body>
</html>
```

**Step 4: Create admin layout**

Create `server/lib/camp_fire_web/components/layouts/admin.html.heex`:

```heex
<div class="flex h-screen">
  <nav class="w-56 bg-gray-900 text-gray-300 flex flex-col py-4">
    <div class="px-4 mb-6">
      <h1 class="text-lg font-bold text-white">Camp Fire Admin</h1>
    </div>
    <a href="/admin/seeds" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :seeds, do: "bg-gray-800 text-white", else: ""}"}>Seeds</a>
    <a href="/admin/economy" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :economy, do: "bg-gray-800 text-white", else: ""}"}>Economy</a>
    <a href="/admin/visitors" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :visitors, do: "bg-gray-800 text-white", else: ""}"}>Visitors</a>
    <a href="/admin/quests" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :quests, do: "bg-gray-800 text-white", else: ""}"}>Quests</a>
    <a href="/admin/players" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :players, do: "bg-gray-800 text-white", else: ""}"}>Players</a>
  </nav>
  <main class="flex-1 overflow-y-auto p-6">
    <.flash_group flash={@flash} />
    {@inner_content}
  </main>
</div>
```

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(admin): enable LiveView socket and root/admin layouts"
```

---

### Task 3: Admin Auth Plug and Router

**Files:**
- Create: `server/lib/camp_fire_web/plugs/admin_auth.ex`
- Create: `server/lib/camp_fire_web/live/admin_login_live.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

**Step 1: Create AdminAuth plug**

Create `server/lib/camp_fire_web/plugs/admin_auth.ex`:

```elixir
defmodule CampFireWeb.Plugs.AdminAuth do
  import Plug.Conn
  import Phoenix.Controller

  def init(opts), do: opts

  def call(conn, _opts) do
    admin_secret = System.get_env("ADMIN_SECRET") || "dev-admin-secret"

    if get_session(conn, :admin_authenticated) == true do
      conn
    else
      conn
      |> redirect(to: "/admin/login")
      |> halt()
    end
  end

  def login(conn, secret) do
    admin_secret = System.get_env("ADMIN_SECRET") || "dev-admin-secret"

    if secret == admin_secret do
      conn
      |> put_session(:admin_authenticated, true)
      |> redirect(to: "/admin/seeds")
    else
      conn
      |> put_flash(:error, "Invalid secret")
      |> redirect(to: "/admin/login")
    end
  end
end
```

**Step 2: Create login LiveView**

Create `server/lib/camp_fire_web/live/admin_login_live.ex`:

```elixir
defmodule CampFireWeb.AdminLoginLive do
  use CampFireWeb, :live_view

  def mount(_params, _session, socket) do
    {:ok, socket}
  end

  def render(assigns) do
    ~H"""
    <div class="min-h-screen flex items-center justify-center bg-gray-50">
      <div class="bg-white p-8 rounded-lg shadow-md w-96">
        <h1 class="text-xl font-bold mb-4">Camp Fire Admin</h1>
        <form phx-submit="login">
          <input
            type="password"
            name="secret"
            placeholder="Admin secret"
            class="w-full border rounded px-3 py-2 mb-4"
            autofocus
          />
          <button type="submit" class="w-full bg-gray-900 text-white py-2 rounded hover:bg-gray-800">
            Login
          </button>
        </form>
      </div>
    </div>
    """
  end

  def handle_event("login", %{"secret" => secret}, socket) do
    admin_secret = System.get_env("ADMIN_SECRET") || "dev-admin-secret"

    if secret == admin_secret do
      {:noreply, redirect(socket, to: "/admin/seeds")}
    else
      {:noreply, put_flash(socket, :error, "Invalid secret")}
    end
  end
end
```

Note: LiveView-based login is simpler but doesn't set a session cookie for auth. We'll use a simpler approach — in dev, skip auth entirely. The `AdminAuth` plug is for production. For now, we route all `/admin` through a browser pipeline and skip the plug in dev.

**Step 3: Add admin routes to router.ex**

Add to `router.ex`:

```elixir
pipeline :browser do
  plug :accepts, ["html"]
  plug :fetch_session
  plug :fetch_live_flash
  plug :put_root_layout, html: {CampFireWeb.Layouts, :root}
  plug :protect_from_forgery
  plug :put_secure_browser_headers
end

scope "/admin", CampFireWeb do
  pipe_through :browser

  live "/login", AdminLoginLive, :index
  live "/seeds", SeedsLive, :index
  live "/seeds/:id/edit", SeedsLive, :edit
  live "/economy", EconomyLive, :index
  live "/visitors", VisitorsLive, :index
  live "/visitors/:id/edit", VisitorsLive, :edit
  live "/quests", QuestsLive, :index
  live "/quests/:id/edit", QuestsLive, :edit
  live "/players", PlayersLive, :index
  live "/players/:uid", PlayersLive, :show
end
```

**Step 4: Add :live_view helper to CampFireWeb**

In `server/lib/camp_fire_web.ex`, add:

```elixir
def live_view do
  quote do
    use Phoenix.LiveView,
      layout: {CampFireWeb.Layouts, :admin}

    import Phoenix.HTML
    unquote(verified_routes())
  end
end
```

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(admin): add auth plug, login page, and admin routes"
```

---

### Task 4: DB Migrations for Config Tables

**Files:**
- Create: `server/priv/repo/migrations/TIMESTAMP_create_admin_config_tables.exs`
- Create: `server/lib/camp_fire/admin/quest_config.ex`
- Create: `server/lib/camp_fire/admin/garden_config.ex`
- Create: `server/lib/camp_fire/admin/game_config.ex`

**Step 1: Create migration**

Run: `cd server && mix ecto.gen.migration create_admin_config_tables`

Fill the generated file:

```elixir
defmodule CampFire.Repo.Migrations.CreateAdminConfigTables do
  use Ecto.Migration

  def change do
    create table(:quest_configs) do
      add :quest_name, :text, null: false
      add :duration_minutes, :integer, null: false
      add :required_flame_level, :integer, default: 1
      add :reward_rolls, :integer, default: 1
      add :reward_pool, :map, default: "[]"

      timestamps(type: :utc_datetime)
    end

    create unique_index(:quest_configs, [:quest_name])

    create table(:garden_configs) do
      add :plant_name, :text, null: false
      add :growth_duration_hours, :float, null: false
      add :yield_item, :text, null: false
      add :yield_amount, :integer, default: 1
      add :yield_interval_hours, :float, null: false
      add :water_required, :integer, default: 1
      add :mana_cost, :float, default: 0.0

      timestamps(type: :utc_datetime)
    end

    create unique_index(:garden_configs, [:plant_name])

    create table(:game_configs) do
      add :key, :text, null: false
      add :value, :map, default: "{}"

      timestamps(type: :utc_datetime)
    end

    create unique_index(:game_configs, [:key])
  end
end
```

**Step 2: Create Ecto schemas**

Create `server/lib/camp_fire/admin/quest_config.ex`:

```elixir
defmodule CampFire.Admin.QuestConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "quest_configs" do
    field :quest_name, :string
    field :duration_minutes, :integer
    field :required_flame_level, :integer, default: 1
    field :reward_rolls, :integer, default: 1
    field :reward_pool, {:array, :map}, default: []

    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:quest_name, :duration_minutes, :required_flame_level, :reward_rolls, :reward_pool])
    |> validate_required([:quest_name, :duration_minutes])
    |> unique_constraint(:quest_name)
  end
end
```

Create `server/lib/camp_fire/admin/garden_config.ex`:

```elixir
defmodule CampFire.Admin.GardenConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "garden_configs" do
    field :plant_name, :string
    field :growth_duration_hours, :float
    field :yield_item, :string
    field :yield_amount, :integer, default: 1
    field :yield_interval_hours, :float
    field :water_required, :integer, default: 1
    field :mana_cost, :float, default: 0.0

    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:plant_name, :growth_duration_hours, :yield_item, :yield_amount, :yield_interval_hours, :water_required, :mana_cost])
    |> validate_required([:plant_name, :growth_duration_hours, :yield_item, :yield_interval_hours])
    |> unique_constraint(:plant_name)
  end
end
```

Create `server/lib/camp_fire/admin/game_config.ex`:

```elixir
defmodule CampFire.Admin.GameConfig do
  use Ecto.Schema
  import Ecto.Changeset

  schema "game_configs" do
    field :key, :string
    field :value, :map, default: %{}

    timestamps(type: :utc_datetime)
  end

  def changeset(config, attrs) do
    config
    |> cast(attrs, [:key, :value])
    |> validate_required([:key, :value])
    |> unique_constraint(:key)
  end
end
```

**Step 3: Run migration**

Run: `cd server && mix ecto.migrate`
Expected: Migration runs successfully

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(admin): add quest_configs, garden_configs, game_configs tables"
```

---

### Task 5: Seed Config Tables with Hardcoded Values

**Files:**
- Modify: `server/priv/repo/seeds.exs`

**Step 1: Add seed data**

Append to `server/priv/repo/seeds.exs`:

```elixir
alias CampFire.Repo
alias CampFire.Admin.{QuestConfig, GardenConfig, GameConfig}

# Quest configs (from Game.Mallums @quest_configs)
quests = [
  %{quest_name: "SwampForage", duration_minutes: 30, required_flame_level: 1, reward_rolls: 1,
    reward_pool: [%{seed_name: "Sprouts", weight: 40, min: 1, max: 2}, %{seed_name: "Cress", weight: 30, min: 1, max: 1}, %{seed_name: "Basil", weight: 20, min: 1, max: 1}, %{seed_name: "Mint", weight: 10, min: 1, max: 1}]},
  %{quest_name: "MeadowExpedition", duration_minutes: 60, required_flame_level: 2, reward_rolls: 1,
    reward_pool: [%{seed_name: "Basil", weight: 30, min: 1, max: 2}, %{seed_name: "Chamomile", weight: 25, min: 1, max: 1}, %{seed_name: "Mint", weight: 25, min: 1, max: 1}, %{seed_name: "Marigold", weight: 15, min: 1, max: 1}, %{seed_name: "Sprouts", weight: 5, min: 1, max: 2}]},
  %{quest_name: "DeepWoodsTrek", duration_minutes: 120, required_flame_level: 3, reward_rolls: 2,
    reward_pool: [%{seed_name: "Lavender", weight: 25, min: 1, max: 2}, %{seed_name: "Rosemary", weight: 25, min: 1, max: 1}, %{seed_name: "Chamomile", weight: 20, min: 1, max: 2}, %{seed_name: "Poppy", weight: 20, min: 1, max: 1}, %{seed_name: "Basil", weight: 10, min: 1, max: 2}]},
  %{quest_name: "MountainPass", duration_minutes: 180, required_flame_level: 4, reward_rolls: 2,
    reward_pool: [%{seed_name: "Dahlia", weight: 25, min: 1, max: 1}, %{seed_name: "Poppy", weight: 25, min: 1, max: 2}, %{seed_name: "Lavender", weight: 20, min: 1, max: 2}, %{seed_name: "Rosemary", weight: 15, min: 1, max: 1}, %{seed_name: "Marigold", weight: 15, min: 1, max: 2}]},
  %{quest_name: "CrystalCavern", duration_minutes: 240, required_flame_level: 5, reward_rolls: 2,
    reward_pool: [%{seed_name: "Jasmine", weight: 25, min: 1, max: 1}, %{seed_name: "Dahlia", weight: 25, min: 1, max: 2}, %{seed_name: "Moonflower", weight: 15, min: 1, max: 1}, %{seed_name: "Lavender", weight: 20, min: 1, max: 2}, %{seed_name: "Poppy", weight: 15, min: 1, max: 1}]},
  %{quest_name: "StarlitMarsh", duration_minutes: 300, required_flame_level: 6, reward_rolls: 3,
    reward_pool: [%{seed_name: "Moonflower", weight: 25, min: 1, max: 1}, %{seed_name: "Jasmine", weight: 25, min: 1, max: 2}, %{seed_name: "Snowdrop", weight: 15, min: 1, max: 1}, %{seed_name: "Dahlia", weight: 20, min: 1, max: 1}, %{seed_name: "Rosemary", weight: 15, min: 1, max: 2}]},
  %{quest_name: "FrostpeakSummit", duration_minutes: 360, required_flame_level: 7, reward_rolls: 3,
    reward_pool: [%{seed_name: "Snowdrop", weight: 30, min: 1, max: 2}, %{seed_name: "Moonflower", weight: 25, min: 1, max: 1}, %{seed_name: "Jasmine", weight: 20, min: 1, max: 2}, %{seed_name: "Dahlia", weight: 15, min: 1, max: 1}, %{seed_name: "Lavender", weight: 10, min: 1, max: 2}]},
  %{quest_name: "AncientGrove", duration_minutes: 480, required_flame_level: 8, reward_rolls: 3,
    reward_pool: [%{seed_name: "Moonflower", weight: 25, min: 1, max: 2}, %{seed_name: "Snowdrop", weight: 25, min: 1, max: 2}, %{seed_name: "Jasmine", weight: 20, min: 1, max: 2}, %{seed_name: "Dahlia", weight: 15, min: 1, max: 2}, %{seed_name: "Rosemary", weight: 15, min: 1, max: 2}]}
]

for q <- quests do
  Repo.insert!(%QuestConfig{} |> QuestConfig.changeset(q) |> Ecto.Changeset.apply_changes() |> then(&struct(QuestConfig, Map.from_struct(&1))),
    on_conflict: :nothing, conflict_target: :quest_name)
end

# Garden configs (from Game.Gardens @plant_configs)
gardens = [
  %{plant_name: "BerryBush", growth_duration_hours: 24.0, yield_item: "Berry", yield_amount: 2, yield_interval_hours: 12.0, water_required: 1, mana_cost: 30.0},
  %{plant_name: "Oak", growth_duration_hours: 48.0, yield_item: "Acorn", yield_amount: 1, yield_interval_hours: 24.0, water_required: 1, mana_cost: 50.0}
]

for g <- gardens do
  %GardenConfig{}
  |> GardenConfig.changeset(g)
  |> Repo.insert!(on_conflict: :nothing, conflict_target: :plant_name)
end

# Game configs (economy constants)
configs = [
  %{key: "flame_config", value: %{
    base_mana_per_second: 0.5,
    mana_per_level: 0.3,
    max_flame_level: 12,
    entity_caps: [6, 6, 8, 8, 12, 15, 18, 22, 26, 30, 35, 40],
    grid_sizes: [2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5]
  }},
  %{key: "vase_config", value: %{
    craft_cost: 15,
    default_capacity: 5,
    fill_seconds_per_unit: 60,
    capacity_tiers: [5, 8, 12, 20],
    upgrade_costs: [75, 200, 500]
  }},
  %{key: "mallum_house_config", value: %{
    mallums_per_house: 1,
    house_costs: [
      %{mana: 15, harvests: []},
      %{mana: 30, harvests: [%{item: "Basil_harvest", count: 2}]},
      %{mana: 60, harvests: [%{item: "Lavender_harvest", count: 3}]},
      %{mana: 100, harvests: [%{item: "Chamomile_harvest", count: 2}, %{item: "Mint_harvest", count: 2}]}
    ]
  }}
]

for c <- configs do
  %GameConfig{}
  |> GameConfig.changeset(c)
  |> Repo.insert!(on_conflict: :nothing, conflict_target: :key)
end
```

**Step 2: Run seeds**

Run: `cd server && mix run priv/repo/seeds.exs`
Expected: No errors

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(admin): seed quest, garden, and game config tables"
```

---

### Task 6: Admin Context Module

**Files:**
- Create: `server/lib/camp_fire/admin.ex`

**Step 1: Create the Admin context**

```elixir
defmodule CampFire.Admin do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Admin.{QuestConfig, GardenConfig, GameConfig}
  alias CampFire.Game.SeedConfig
  alias CampFire.Visitors.VisitorTemplate
  alias CampFire.Visitors.VisitorSchedule

  # ── Seeds ──

  def list_seeds, do: Repo.all(from s in SeedConfig, order_by: s.seed_name)
  def get_seed!(id), do: Repo.get!(SeedConfig, id)

  def update_seed(%SeedConfig{} = seed, attrs) do
    seed |> SeedConfig.changeset(attrs) |> Repo.update()
  end

  def create_seed(attrs) do
    %SeedConfig{} |> SeedConfig.changeset(attrs) |> Repo.insert()
  end

  def delete_seed(%SeedConfig{} = seed), do: Repo.delete(seed)

  # ── Quests ──

  def list_quests, do: Repo.all(from q in QuestConfig, order_by: q.required_flame_level)
  def get_quest!(id), do: Repo.get!(QuestConfig, id)

  def update_quest(%QuestConfig{} = quest, attrs) do
    quest |> QuestConfig.changeset(attrs) |> Repo.update()
  end

  def create_quest(attrs) do
    %QuestConfig{} |> QuestConfig.changeset(attrs) |> Repo.insert()
  end

  def delete_quest(%QuestConfig{} = quest), do: Repo.delete(quest)

  # ── Gardens ──

  def list_gardens, do: Repo.all(from g in GardenConfig, order_by: g.plant_name)
  def get_garden!(id), do: Repo.get!(GardenConfig, id)

  def update_garden(%GardenConfig{} = garden, attrs) do
    garden |> GardenConfig.changeset(attrs) |> Repo.update()
  end

  def create_garden(attrs) do
    %GardenConfig{} |> GardenConfig.changeset(attrs) |> Repo.insert()
  end

  def delete_garden(%GardenConfig{} = garden), do: Repo.delete(garden)

  # ── Game Config ──

  def list_game_configs, do: Repo.all(from c in GameConfig, order_by: c.key)
  def get_game_config!(id), do: Repo.get!(GameConfig, id)
  def get_game_config_by_key(key), do: Repo.get_by(GameConfig, key: key)

  def upsert_game_config(key, value) do
    case get_game_config_by_key(key) do
      nil -> %GameConfig{} |> GameConfig.changeset(%{key: key, value: value}) |> Repo.insert()
      config -> config |> GameConfig.changeset(%{value: value}) |> Repo.update()
    end
  end

  # ── Visitors ──

  def list_visitors, do: Repo.all(from v in VisitorTemplate, order_by: v.visitor_id)
  def get_visitor!(id), do: Repo.get!(VisitorTemplate, id)

  def update_visitor(%VisitorTemplate{} = visitor, attrs) do
    visitor |> VisitorTemplate.changeset(attrs) |> Repo.update()
  end

  def create_visitor(attrs) do
    %VisitorTemplate{} |> VisitorTemplate.changeset(attrs) |> Repo.insert()
  end

  def delete_visitor(%VisitorTemplate{} = visitor), do: Repo.delete(visitor)

  def list_visitor_schedule do
    Repo.all(from s in VisitorSchedule, order_by: [desc: s.date, desc: s.priority])
  end

  # ── Players ──

  def search_players(query) do
    like = "%#{query}%"
    Repo.all(
      from p in CampFire.Accounts.Player,
        where: ilike(p.uid, ^like) or ilike(p.display_name, ^like) or ilike(p.friend_code, ^like),
        limit: 50,
        order_by: [desc: p.updated_at]
    )
  end

  def get_player_detail(uid) do
    player = Repo.get_by!(CampFire.Accounts.Player, uid: uid)
    economy = Repo.get_by(CampFire.Economy.PlayerEconomy, player_uid: uid)
    seeds = Repo.all(from s in CampFire.Economy.PlayerSeed, where: s.player_uid == ^uid)
    items = Repo.all(from i in CampFire.Economy.PlayerItem, where: i.player_uid == ^uid)
    plots = Repo.all(from p in CampFire.Game.PlayerPlot, where: p.player_uid == ^uid)
    vases = Repo.all(from v in CampFire.Game.PlayerVase, where: v.player_uid == ^uid)
    gardens = Repo.all(from g in CampFire.Game.PlayerGarden, where: g.player_uid == ^uid)
    mallums = Repo.all(from m in CampFire.Game.PlayerMallum, where: m.player_uid == ^uid)

    %{
      player: player,
      economy: economy,
      seeds: seeds,
      items: items,
      plots: plots,
      vases: vases,
      gardens: gardens,
      mallums: mallums
    }
  end

  def update_economy(uid, attrs) do
    case Repo.get_by(CampFire.Economy.PlayerEconomy, player_uid: uid) do
      nil -> {:error, :not_found}
      economy -> economy |> Ecto.Changeset.change(attrs) |> Repo.update()
    end
  end
end
```

**Step 2: Verify compilation**

Run: `cd server && mix compile`
Expected: Compiles with no errors (may have warnings about missing schemas — that's fine, they exist)

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(admin): add Admin context with CRUD for all config types"
```

---

### Task 7: Seeds LiveView

**Files:**
- Create: `server/lib/camp_fire_web/live/seeds_live.ex`

**Step 1: Create the Seeds LiveView**

Create `server/lib/camp_fire_web/live/seeds_live.ex`:

```elixir
defmodule CampFireWeb.SeedsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok, assign(socket, seeds: Admin.list_seeds(), active_tab: :seeds, editing: nil, form: nil)}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    seed = Admin.get_seed!(id)
    form = seed |> CampFire.Game.SeedConfig.changeset(%{}) |> to_form()
    {:noreply, assign(socket, editing: seed, form: form)}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil)}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/seeds/#{id}/edit")}
  end

  def handle_event("cancel", _, socket) do
    {:noreply, push_patch(socket, to: "/admin/seeds")}
  end

  def handle_event("save", %{"seed_config" => params}, socket) do
    # Parse recipe JSON if present
    params = parse_recipe_param(params)

    case Admin.update_seed(socket.assigns.editing, params) do
      {:ok, _seed} ->
        {:noreply,
         socket
         |> put_flash(:info, "Seed updated")
         |> assign(seeds: Admin.list_seeds())
         |> push_patch(to: "/admin/seeds")}

      {:error, changeset} ->
        {:noreply, assign(socket, form: to_form(changeset))}
    end
  end

  def handle_event("delete", %{"id" => id}, socket) do
    seed = Admin.get_seed!(id)
    {:ok, _} = Admin.delete_seed(seed)
    {:noreply, assign(socket, seeds: Admin.list_seeds()) |> put_flash(:info, "Seed deleted")}
  end

  def handle_event("new", _, socket) do
    case Admin.create_seed(%{seed_name: "NewSeed", growth_duration_hours: 1.0, base_drops: 1}) do
      {:ok, seed} ->
        {:noreply, push_patch(socket, to: "/admin/seeds/#{seed.id}/edit")}
      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to create seed")}
    end
  end

  defp parse_recipe_param(%{"recipe_json" => json} = params) do
    case Jason.decode(json) do
      {:ok, recipe} -> Map.put(params, "recipe", recipe) |> Map.delete("recipe_json")
      _ -> params
    end
  end
  defp parse_recipe_param(params), do: params

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Seed Configs</h2>
        <button phx-click="new" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New Seed
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white rounded-lg shadow p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.seed_name}</h3>
          <.form for={@form} phx-submit="save" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Seed Name</label>
                <input type="text" name="seed_config[seed_name]" value={@form[:seed_name].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Growth Duration (hours)</label>
                <input type="number" step="0.1" name="seed_config[growth_duration_hours]" value={@form[:growth_duration_hours].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Base Drops</label>
                <input type="number" name="seed_config[base_drops]" value={@form[:base_drops].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Mana Cost</label>
                <input type="number" step="0.1" name="seed_config[mana_cost]" value={@form[:mana_cost].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700">Recipe (JSON)</label>
              <textarea name="seed_config[recipe_json]" rows="8" class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"><%= Jason.encode!(@editing.recipe || %{}, pretty: true) %></textarea>
            </div>
            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
            </div>
          </.form>
        </div>
      <% end %>

      <div class="bg-white rounded-lg shadow overflow-hidden">
        <table class="min-w-full divide-y divide-gray-200">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Growth (hrs)</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Drops</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mana Cost</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Recipe Axes</th>
              <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200">
            <%= for seed <- @seeds do %>
              <tr class="hover:bg-gray-50">
                <td class="px-4 py-3 font-medium"><%= seed.seed_name %></td>
                <td class="px-4 py-3"><%= seed.growth_duration_hours %></td>
                <td class="px-4 py-3"><%= seed.base_drops %></td>
                <td class="px-4 py-3"><%= seed.mana_cost %></td>
                <td class="px-4 py-3 text-sm text-gray-500"><%= recipe_summary(seed.recipe) %></td>
                <td class="px-4 py-3 text-right">
                  <button phx-click="edit" phx-value-id={seed.id} class="text-blue-600 hover:text-blue-800 mr-2">Edit</button>
                  <button phx-click="delete" phx-value-id={seed.id} data-confirm="Delete this seed?" class="text-red-600 hover:text-red-800">Delete</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      </div>
    </div>
    """
  end

  defp recipe_summary(nil), do: "none"
  defp recipe_summary(recipe) when recipe == %{}, do: "none"
  defp recipe_summary(recipe) do
    axes = []
    axes = if recipe["use_heat"], do: axes ++ ["heat"], else: axes
    axes = if recipe["use_wind"], do: axes ++ ["wind"], else: axes
    axes = if recipe["use_humidity"], do: axes ++ ["humidity"], else: axes
    axes = if recipe["use_sunlight"], do: axes ++ ["sunlight"], else: axes
    axes = if recipe["use_rain"], do: axes ++ ["rain"], else: axes
    axes = if recipe["use_moon"], do: axes ++ ["moon"], else: axes
    axes = if recipe["use_waterings"], do: axes ++ ["waterings"], else: axes
    if axes == [], do: "none", else: Enum.join(axes, ", ")
  end
end
```

**Step 2: Verify it compiles**

Run: `cd server && mix compile`

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(admin): add Seeds LiveView with table and edit form"
```

---

### Task 8: Economy LiveView

**Files:**
- Create: `server/lib/camp_fire_web/live/economy_live.ex`

**Step 1: Create Economy LiveView**

This displays `game_config` entries as structured cards. Each config key gets a card with its JSONB value rendered as a JSON editor (textarea for now — structured fields are a follow-up).

```elixir
defmodule CampFireWeb.EconomyLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    configs = Admin.list_game_configs()
    {:ok, assign(socket, configs: configs, active_tab: :economy, editing_key: nil, json_text: "")}
  end

  def handle_params(_params, _uri, socket), do: {:noreply, socket}

  def handle_event("edit", %{"key" => key}, socket) do
    config = Enum.find(socket.assigns.configs, &(&1.key == key))
    json = Jason.encode!(config.value, pretty: true)
    {:noreply, assign(socket, editing_key: key, json_text: json)}
  end

  def handle_event("cancel", _, socket) do
    {:noreply, assign(socket, editing_key: nil)}
  end

  def handle_event("save", %{"json" => json}, socket) do
    case Jason.decode(json) do
      {:ok, value} ->
        case Admin.upsert_game_config(socket.assigns.editing_key, value) do
          {:ok, _} ->
            {:noreply,
             socket
             |> put_flash(:info, "Config saved")
             |> assign(configs: Admin.list_game_configs(), editing_key: nil)}
          {:error, _} ->
            {:noreply, put_flash(socket, :error, "Failed to save")}
        end
      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid JSON")}
    end
  end

  def render(assigns) do
    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-6">Economy Config</h2>

      <div class="space-y-4">
        <%= for config <- @configs do %>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="flex justify-between items-center mb-2">
              <h3 class="text-lg font-semibold"><%= config.key %></h3>
              <button phx-click="edit" phx-value-key={config.key} class="text-blue-600 hover:text-blue-800">Edit</button>
            </div>

            <%= if @editing_key == config.key do %>
              <form phx-submit="save" class="mt-2">
                <textarea name="json" rows="16" class="w-full border rounded px-3 py-2 font-mono text-sm"><%= @json_text %></textarea>
                <div class="flex gap-2 mt-2">
                  <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
                  <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
                </div>
              </form>
            <% else %>
              <pre class="bg-gray-50 rounded p-3 text-sm overflow-x-auto"><%= Jason.encode!(config.value, pretty: true) %></pre>
            <% end %>
          </div>
        <% end %>
      </div>
    </div>
    """
  end
end
```

**Step 2: Commit**

```bash
git add -A && git commit -m "feat(admin): add Economy LiveView with JSON config editor"
```

---

### Task 9: Visitors LiveView

**Files:**
- Create: `server/lib/camp_fire_web/live/visitors_live.ex`

**Step 1: Create Visitors LiveView**

Table of visitor templates with inline editing of all fields. Dialogue/offer/gift/quest pools edited as JSON textareas.

```elixir
defmodule CampFireWeb.VisitorsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok, assign(socket,
      visitors: Admin.list_visitors(),
      schedule: Admin.list_visitor_schedule(),
      active_tab: :visitors,
      editing: nil,
      form: nil,
      tab: "templates"
    )}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    visitor = Admin.get_visitor!(id)
    form = visitor |> CampFire.Visitors.VisitorTemplate.changeset(%{}) |> to_form()
    {:noreply, assign(socket, editing: visitor, form: form)}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil)}
  end

  def handle_event("switch_tab", %{"tab" => tab}, socket) do
    {:noreply, assign(socket, tab: tab)}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/visitors/#{id}/edit")}
  end

  def handle_event("cancel", _, socket) do
    {:noreply, push_patch(socket, to: "/admin/visitors")}
  end

  def handle_event("save", %{"visitor" => params}, socket) do
    params = parse_json_fields(params, ~w(dialogue_pool offer_pool gift_pool quest_pool))

    case Admin.update_visitor(socket.assigns.editing, params) do
      {:ok, _} ->
        {:noreply,
         socket
         |> put_flash(:info, "Visitor updated")
         |> assign(visitors: Admin.list_visitors())
         |> push_patch(to: "/admin/visitors")}
      {:error, changeset} ->
        {:noreply, assign(socket, form: to_form(changeset))}
    end
  end

  def handle_event("delete", %{"id" => id}, socket) do
    visitor = Admin.get_visitor!(id)
    {:ok, _} = Admin.delete_visitor(visitor)
    {:noreply, assign(socket, visitors: Admin.list_visitors()) |> put_flash(:info, "Visitor deleted")}
  end

  defp parse_json_fields(params, fields) do
    Enum.reduce(fields, params, fn field, acc ->
      json_key = field <> "_json"
      case Map.get(acc, json_key) do
        nil -> acc
        json ->
          case Jason.decode(json) do
            {:ok, val} -> acc |> Map.put(field, val) |> Map.delete(json_key)
            _ -> acc
          end
      end
    end)
  end

  def render(assigns) do
    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-4">Visitors</h2>

      <div class="flex gap-2 mb-6">
        <button phx-click="switch_tab" phx-value-tab="templates"
          class={"px-4 py-2 rounded #{if @tab == "templates", do: "bg-gray-900 text-white", else: "bg-gray-200"}"}>
          Templates
        </button>
        <button phx-click="switch_tab" phx-value-tab="schedule"
          class={"px-4 py-2 rounded #{if @tab == "schedule", do: "bg-gray-900 text-white", else: "bg-gray-200"}"}>
          Schedule
        </button>
      </div>

      <%= if @tab == "templates" do %>
        <%= if @editing do %>
          <div class="bg-white rounded-lg shadow p-6 mb-6">
            <h3 class="text-lg font-semibold mb-4">Edit: {@editing.name}</h3>
            <.form for={@form} phx-submit="save" class="space-y-4">
              <div class="grid grid-cols-3 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700">Visitor ID</label>
                  <input type="text" name="visitor[visitor_id]" value={@form[:visitor_id].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Name</label>
                  <input type="text" name="visitor[name]" value={@form[:name].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Type</label>
                  <select name="visitor[type]" class="mt-1 block w-full border rounded px-3 py-2">
                    <option value="gifter" selected={@form[:type].value == "gifter"}>Gifter</option>
                    <option value="merchant" selected={@form[:type].value == "merchant"}>Merchant</option>
                    <option value="quester" selected={@form[:type].value == "quester"}>Quester</option>
                  </select>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Weight</label>
                  <input type="number" step="0.1" name="visitor[weight]" value={@form[:weight].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Min Flame Level</label>
                  <input type="number" name="visitor[flame_level_min]" value={@form[:flame_level_min].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700">Portrait ID</label>
                  <input type="text" name="visitor[portrait_id]" value={@form[:portrait_id].value} class="mt-1 block w-full border rounded px-3 py-2" />
                </div>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Dialogue Pool (JSON array)</label>
                <textarea name="visitor[dialogue_pool_json]" rows="4" class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"><%= Jason.encode!(@editing.dialogue_pool || [], pretty: true) %></textarea>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Offer Pool (JSON array)</label>
                <textarea name="visitor[offer_pool_json]" rows="4" class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"><%= Jason.encode!(@editing.offer_pool || [], pretty: true) %></textarea>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Gift Pool (JSON array)</label>
                <textarea name="visitor[gift_pool_json]" rows="4" class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"><%= Jason.encode!(@editing.gift_pool || [], pretty: true) %></textarea>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Quest Pool (JSON array)</label>
                <textarea name="visitor[quest_pool_json]" rows="4" class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"><%= Jason.encode!(@editing.quest_pool || [], pretty: true) %></textarea>
              </div>
              <div class="flex gap-2">
                <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
                <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
              </div>
            </.form>
          </div>
        <% end %>

        <div class="bg-white rounded-lg shadow overflow-hidden">
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">ID</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Type</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Weight</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Min Level</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200">
              <%= for v <- @visitors do %>
                <tr class="hover:bg-gray-50">
                  <td class="px-4 py-3 font-mono text-sm"><%= v.visitor_id %></td>
                  <td class="px-4 py-3"><%= v.name %></td>
                  <td class="px-4 py-3"><%= v.type %></td>
                  <td class="px-4 py-3"><%= v.weight %></td>
                  <td class="px-4 py-3"><%= v.flame_level_min %></td>
                  <td class="px-4 py-3 text-right">
                    <button phx-click="edit" phx-value-id={v.id} class="text-blue-600 hover:text-blue-800 mr-2">Edit</button>
                    <button phx-click="delete" phx-value-id={v.id} data-confirm="Delete?" class="text-red-600 hover:text-red-800">Delete</button>
                  </td>
                </tr>
              <% end %>
            </tbody>
          </table>
        </div>
      <% end %>

      <%= if @tab == "schedule" do %>
        <div class="bg-white rounded-lg shadow overflow-hidden">
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Date</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Visitor</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Visit #</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Weather</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Priority</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200">
              <%= for s <- @schedule do %>
                <tr class="hover:bg-gray-50">
                  <td class="px-4 py-3"><%= s.date %></td>
                  <td class="px-4 py-3"><%= s.visitor_id %></td>
                  <td class="px-4 py-3"><%= s.visit_number %></td>
                  <td class="px-4 py-3"><%= s.weather_condition || "—" %></td>
                  <td class="px-4 py-3"><%= s.priority %></td>
                </tr>
              <% end %>
            </tbody>
          </table>
        </div>
      <% end %>
    </div>
    """
  end
end
```

**Step 2: Commit**

```bash
git add -A && git commit -m "feat(admin): add Visitors LiveView with templates and schedule tabs"
```

---

### Task 10: Quests LiveView

**Files:**
- Create: `server/lib/camp_fire_web/live/quests_live.ex`

**Step 1: Create Quests LiveView**

Same pattern as Seeds — table + edit form. Reward pool edited as JSON.

```elixir
defmodule CampFireWeb.QuestsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok, assign(socket, quests: Admin.list_quests(), active_tab: :quests, editing: nil, form: nil)}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    quest = Admin.get_quest!(id)
    form = quest |> CampFire.Admin.QuestConfig.changeset(%{}) |> to_form()
    {:noreply, assign(socket, editing: quest, form: form)}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil)}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/quests/#{id}/edit")}
  end

  def handle_event("cancel", _, socket) do
    {:noreply, push_patch(socket, to: "/admin/quests")}
  end

  def handle_event("save", %{"quest_config" => params}, socket) do
    params = case Map.get(params, "reward_pool_json") do
      nil -> params
      json ->
        case Jason.decode(json) do
          {:ok, pool} -> params |> Map.put("reward_pool", pool) |> Map.delete("reward_pool_json")
          _ -> params
        end
    end

    case Admin.update_quest(socket.assigns.editing, params) do
      {:ok, _} ->
        {:noreply,
         socket
         |> put_flash(:info, "Quest updated")
         |> assign(quests: Admin.list_quests())
         |> push_patch(to: "/admin/quests")}
      {:error, changeset} ->
        {:noreply, assign(socket, form: to_form(changeset))}
    end
  end

  def handle_event("delete", %{"id" => id}, socket) do
    quest = Admin.get_quest!(id)
    {:ok, _} = Admin.delete_quest(quest)
    {:noreply, assign(socket, quests: Admin.list_quests()) |> put_flash(:info, "Quest deleted")}
  end

  def handle_event("new", _, socket) do
    case Admin.create_quest(%{quest_name: "NewQuest", duration_minutes: 30, required_flame_level: 1, reward_rolls: 1, reward_pool: []}) do
      {:ok, quest} -> {:noreply, push_patch(socket, to: "/admin/quests/#{quest.id}/edit")}
      {:error, _} -> {:noreply, put_flash(socket, :error, "Failed to create quest")}
    end
  end

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Quest Configs</h2>
        <button phx-click="new" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">+ New Quest</button>
      </div>

      <%= if @editing do %>
        <div class="bg-white rounded-lg shadow p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.quest_name}</h3>
          <.form for={@form} phx-submit="save" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Quest Name</label>
                <input type="text" name="quest_config[quest_name]" value={@form[:quest_name].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Duration (minutes)</label>
                <input type="number" name="quest_config[duration_minutes]" value={@form[:duration_minutes].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Required Flame Level</label>
                <input type="number" name="quest_config[required_flame_level]" value={@form[:required_flame_level].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Reward Rolls</label>
                <input type="number" name="quest_config[reward_rolls]" value={@form[:reward_rolls].value} class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700">Reward Pool (JSON)</label>
              <textarea name="quest_config[reward_pool_json]" rows="8" class="mt-1 block w-full border rounded px-3 py-2 font-mono text-sm"><%= Jason.encode!(@editing.reward_pool || [], pretty: true) %></textarea>
            </div>
            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
            </div>
          </.form>
        </div>
      <% end %>

      <div class="bg-white rounded-lg shadow overflow-hidden">
        <table class="min-w-full divide-y divide-gray-200">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Quest</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Duration</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Flame Lvl</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Rolls</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Rewards</th>
              <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200">
            <%= for q <- @quests do %>
              <tr class="hover:bg-gray-50">
                <td class="px-4 py-3 font-medium"><%= q.quest_name %></td>
                <td class="px-4 py-3"><%= q.duration_minutes %> min</td>
                <td class="px-4 py-3"><%= q.required_flame_level %></td>
                <td class="px-4 py-3"><%= q.reward_rolls %></td>
                <td class="px-4 py-3 text-sm text-gray-500"><%= length(q.reward_pool || []) %> seeds</td>
                <td class="px-4 py-3 text-right">
                  <button phx-click="edit" phx-value-id={q.id} class="text-blue-600 hover:text-blue-800 mr-2">Edit</button>
                  <button phx-click="delete" phx-value-id={q.id} data-confirm="Delete?" class="text-red-600 hover:text-red-800">Delete</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      </div>
    </div>
    """
  end
end
```

**Step 2: Commit**

```bash
git add -A && git commit -m "feat(admin): add Quests LiveView with table and edit form"
```

---

### Task 11: Players LiveView

**Files:**
- Create: `server/lib/camp_fire_web/live/players_live.ex`

**Step 1: Create Players LiveView**

Search bar + player detail view with economy, inventory, and entity tables.

```elixir
defmodule CampFireWeb.PlayersLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok, assign(socket, active_tab: :players, query: "", results: [], detail: nil, editing_economy: false)}
  end

  def handle_params(%{"uid" => uid}, _uri, socket) do
    detail = Admin.get_player_detail(uid)
    {:noreply, assign(socket, detail: detail, editing_economy: false)}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, detail: nil)}
  end

  def handle_event("search", %{"query" => q}, socket) do
    results = if String.length(q) >= 2, do: Admin.search_players(q), else: []
    {:noreply, assign(socket, query: q, results: results)}
  end

  def handle_event("view", %{"uid" => uid}, socket) do
    {:noreply, push_patch(socket, to: "/admin/players/#{uid}")}
  end

  def handle_event("back", _, socket) do
    {:noreply, push_patch(socket, to: "/admin/players")}
  end

  def handle_event("edit_economy", _, socket) do
    {:noreply, assign(socket, editing_economy: true)}
  end

  def handle_event("save_economy", params, socket) do
    uid = socket.assigns.detail.player.uid
    attrs = %{}
    attrs = if p = params["mana"], do: Map.put(attrs, :mana, String.to_float(p)), else: attrs
    attrs = if p = params["gems"], do: Map.put(attrs, :gems, String.to_integer(p)), else: attrs
    attrs = if p = params["flame_level"], do: Map.put(attrs, :flame_level, String.to_integer(p)), else: attrs

    case Admin.update_economy(uid, attrs) do
      {:ok, _} ->
        detail = Admin.get_player_detail(uid)
        {:noreply, socket |> put_flash(:info, "Economy updated") |> assign(detail: detail, editing_economy: false)}
      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to update")}
    end
  end

  def render(assigns) do
    ~H"""
    <div>
      <%= if @detail do %>
        <button phx-click="back" class="text-blue-600 hover:text-blue-800 mb-4">&larr; Back to search</button>
        <.player_detail detail={@detail} editing_economy={@editing_economy} />
      <% else %>
        <h2 class="text-2xl font-bold mb-6">Players</h2>
        <form phx-change="search" class="mb-6">
          <input type="text" name="query" value={@query} placeholder="Search by UID, name, or friend code..."
            class="w-full border rounded px-4 py-2" phx-debounce="300" autofocus />
        </form>

        <%= if @results != [] do %>
          <div class="bg-white rounded-lg shadow overflow-hidden">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Friend Code</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">UID</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Last Online</th>
                  <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200">
                <%= for p <- @results do %>
                  <tr class="hover:bg-gray-50 cursor-pointer" phx-click="view" phx-value-uid={p.uid}>
                    <td class="px-4 py-3"><%= p.display_name %></td>
                    <td class="px-4 py-3 font-mono text-sm"><%= p.friend_code %></td>
                    <td class="px-4 py-3 font-mono text-xs text-gray-500"><%= String.slice(p.uid, 0..7) %>...</td>
                    <td class="px-4 py-3 text-sm"><%= p.updated_at %></td>
                    <td class="px-4 py-3 text-right">
                      <button class="text-blue-600 hover:text-blue-800">View</button>
                    </td>
                  </tr>
                <% end %>
              </tbody>
            </table>
          </div>
        <% end %>
      <% end %>
    </div>
    """
  end

  defp player_detail(assigns) do
    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-2">{@detail.player.display_name}</h2>
      <p class="text-gray-500 mb-6 font-mono text-sm">{@detail.player.uid} | {@detail.player.friend_code}</p>

      <%= if @detail.economy do %>
        <div class="bg-white rounded-lg shadow p-4 mb-4">
          <div class="flex justify-between items-center mb-2">
            <h3 class="text-lg font-semibold">Economy</h3>
            <button phx-click="edit_economy" class="text-blue-600 hover:text-blue-800 text-sm">Edit</button>
          </div>
          <%= if @editing_economy do %>
            <form phx-submit="save_economy" class="grid grid-cols-3 gap-4">
              <div>
                <label class="text-sm text-gray-500">Mana</label>
                <input type="number" step="0.1" name="mana" value={@detail.economy.mana} class="block w-full border rounded px-2 py-1" />
              </div>
              <div>
                <label class="text-sm text-gray-500">Gems</label>
                <input type="number" name="gems" value={@detail.economy.gems} class="block w-full border rounded px-2 py-1" />
              </div>
              <div>
                <label class="text-sm text-gray-500">Flame Level</label>
                <input type="number" name="flame_level" value={@detail.economy.flame_level} class="block w-full border rounded px-2 py-1" />
              </div>
              <button type="submit" class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Save</button>
            </form>
          <% else %>
            <div class="grid grid-cols-3 gap-4 text-sm">
              <div><span class="text-gray-500">Mana:</span> <%= Float.round(@detail.economy.mana, 1) %></div>
              <div><span class="text-gray-500">Gems:</span> <%= @detail.economy.gems %></div>
              <div><span class="text-gray-500">Flame:</span> Lv.<%= @detail.economy.flame_level %></div>
            </div>
          <% end %>
        </div>
      <% end %>

      <div class="grid grid-cols-2 gap-4 mb-4">
        <div class="bg-white rounded-lg shadow p-4">
          <h3 class="font-semibold mb-2">Seeds (<%= length(@detail.seeds) %>)</h3>
          <div class="text-sm space-y-1">
            <%= for s <- @detail.seeds do %>
              <div class="flex justify-between"><span><%= s.seed_name %></span><span class="text-gray-500">x<%= s.count %></span></div>
            <% end %>
          </div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <h3 class="font-semibold mb-2">Items (<%= length(@detail.items) %>)</h3>
          <div class="text-sm space-y-1">
            <%= for i <- @detail.items do %>
              <div class="flex justify-between"><span><%= i.item_name %></span><span class="text-gray-500">x<%= i.count %></span></div>
            <% end %>
          </div>
        </div>
      </div>

      <div class="grid grid-cols-2 gap-4">
        <div class="bg-white rounded-lg shadow p-4">
          <h3 class="font-semibold mb-2">Plots (<%= length(@detail.plots) %>)</h3>
          <div class="text-sm space-y-1">
            <%= for p <- @detail.plots do %>
              <div><%= p.state %> <%= if p.seed_name, do: "- #{p.seed_name}", else: "" %> (<%= p.grid_x %>,<%= p.grid_y %>)</div>
            <% end %>
          </div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <h3 class="font-semibold mb-2">Vases (<%= length(@detail.vases) %>)</h3>
          <div class="text-sm space-y-1">
            <%= for v <- @detail.vases do %>
              <div><%= v.state %> <%= v.current_water %>/<%= v.capacity %> (<%= v.grid_x %>,<%= v.grid_y %>)</div>
            <% end %>
          </div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <h3 class="font-semibold mb-2">Gardens (<%= length(@detail.gardens) %>)</h3>
          <div class="text-sm space-y-1">
            <%= for g <- @detail.gardens do %>
              <div><%= g.plant_name %> <%= if g.mature, do: "(mature)", else: "(growing)" %></div>
            <% end %>
          </div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <h3 class="font-semibold mb-2">Mallums (<%= length(@detail.mallums) %>)</h3>
          <div class="text-sm space-y-1">
            <%= for m <- @detail.mallums do %>
              <div><%= m.state %> <%= if m.assigned_quest_name, do: "- #{m.assigned_quest_name}", else: "" %></div>
            <% end %>
          </div>
        </div>
      </div>
    </div>
    """
  end
end
```

**Step 2: Commit**

```bash
git add -A && git commit -m "feat(admin): add Players LiveView with search and detail view"
```

---

### Task 12: ConfigCache GenServer

**Files:**
- Create: `server/lib/camp_fire/config_cache.ex`
- Modify: `server/lib/camp_fire/application.ex`

**Step 1: Create ConfigCache**

```elixir
defmodule CampFire.ConfigCache do
  use GenServer

  @table :config_cache

  def start_link(_opts) do
    GenServer.start_link(__MODULE__, [], name: __MODULE__)
  end

  def get(key) do
    case :ets.lookup(@table, key) do
      [{^key, value}] -> value
      [] -> nil
    end
  end

  def refresh do
    GenServer.cast(__MODULE__, :refresh)
  end

  # GenServer callbacks

  def init(_) do
    @table = :ets.new(@table, [:named_table, :set, :public, read_concurrency: true])
    load_all()
    {:ok, %{}}
  end

  def handle_cast(:refresh, state) do
    load_all()
    {:noreply, state}
  end

  defp load_all do
    configs = CampFire.Repo.all(CampFire.Admin.GameConfig)
    for config <- configs do
      :ets.insert(@table, {config.key, config.value})
    end

    quests = CampFire.Repo.all(CampFire.Admin.QuestConfig)
    quest_map = Map.new(quests, fn q -> {q.quest_name, q} end)
    :ets.insert(@table, {"quest_configs", quest_map})

    gardens = CampFire.Repo.all(CampFire.Admin.GardenConfig)
    garden_map = Map.new(gardens, fn g -> {g.plant_name, g} end)
    :ets.insert(@table, {"garden_configs", garden_map})
  end
end
```

**Step 2: Add to supervision tree in application.ex**

Add `CampFire.ConfigCache` to the children list, after `CampFire.Repo`:

```elixir
CampFire.ConfigCache,
```

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(admin): add ConfigCache GenServer with ETS backing"
```

---

### Task 13: Wire Game Contexts to ConfigCache

**Files:**
- Modify: `server/lib/camp_fire/game/mallums.ex`
- Modify: `server/lib/camp_fire/game/gardens.ex`

**Step 1: Update Mallums to read from ConfigCache**

In `mallums.ex`, replace the `@quest_configs` module attribute usage with a function that reads from cache, falling back to the hardcoded map:

```elixir
defp get_quest_config(quest_name) do
  case CampFire.ConfigCache.get("quest_configs") do
    nil -> Map.get(@quest_configs, quest_name)
    quest_map -> Map.get(quest_map, quest_name)
  end
end
```

Replace all `Map.get(@quest_configs, quest_name)` calls with `get_quest_config(quest_name)`. Keep `@quest_configs` as fallback.

**Step 2: Update Gardens similarly**

In `gardens.ex`, replace `Map.get(@plant_configs, plant_name)` with a function that checks ConfigCache first.

**Step 3: Trigger cache refresh from admin saves**

In each admin LiveView's save handler, add `CampFire.ConfigCache.refresh()` after successful DB writes.

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(admin): wire game contexts to ConfigCache for live config updates"
```

---

### Task 14: Verify and Test

**Step 1: Run the server**

Run: `cd server && mix ecto.reset && mix phx.server`

**Step 2: Open the admin dashboard**

Visit: `http://localhost:4000/admin/seeds`
Verify: sidebar navigation works, seed table shows data, edit form opens

**Step 3: Test each tab**

- Seeds: edit a seed's growth duration, save, verify it persists
- Economy: edit flame_config, save, verify JSON parses correctly
- Visitors: view templates list, edit dialogue pool
- Quests: edit a quest's reward pool, save
- Players: search by "Camper", view player detail, edit economy

**Step 4: Verify ConfigCache**

- Edit a quest in admin
- Call `GET /game/quest/start` from client
- Verify the updated quest config is used

**Step 5: Commit any fixes**

```bash
git add -A && git commit -m "fix(admin): polish and fix issues found during testing"
```
