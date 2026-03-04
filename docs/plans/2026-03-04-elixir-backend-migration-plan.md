# Elixir Backend Migration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the Node.js/Express backend with an Elixir/Phoenix JSON API, same endpoints and JSON shapes so the Unity client is unchanged.

**Architecture:** Phoenix app (`camp_fire`) with Ecto/Postgres. Five contexts: Accounts, Social, Villages, Gifts, Visitors. Bearer token auth via a custom Plug. ETS-based rate limiting via Hammer. Same Docker Compose for Postgres.

**Tech Stack:** Elixir ~1.17, Phoenix ~1.7 (API-only, no HTML/LiveView), Ecto 3, Hammer (rate limiting), PostgreSQL 16

---

### Task 1: Scaffold Phoenix Project

**Files:**
- Create: `server/` (entire Phoenix project via `mix phx.new`)

**Step 1: Delete old Node.js server**

```bash
cd /Users/lotu/game/Garden
# Preserve docker-compose.yml and Makefile (we'll rewrite them)
cp server/docker-compose.yml /tmp/campfire-docker-compose.yml
rm -rf server/
```

**Step 2: Generate Phoenix project**

```bash
cd /Users/lotu/game/Garden
mix phx.new server --app camp_fire --no-html --no-assets --no-live --no-mailer --no-dashboard --no-gettext
```

When prompted to install dependencies, say Yes.

**Step 3: Restore docker-compose.yml**

```bash
cp /tmp/campfire-docker-compose.yml /Users/lotu/game/Garden/server/docker-compose.yml
```

**Step 4: Add Hammer dependency to mix.exs**

In `server/mix.exs`, add to `deps`:

```elixir
{:hammer, "~> 6.2"}
```

Then run:

```bash
cd /Users/lotu/game/Garden/server
mix deps.get
```

**Step 5: Configure database in dev.exs**

Edit `server/config/dev.exs` — set the Repo config to match our Docker Postgres:

```elixir
config :camp_fire, CampFire.Repo,
  username: "campfire",
  password: "campfire",
  hostname: "localhost",
  database: "campfire_dev",
  stacktrace: true,
  show_sensitive_data_on_connection_error: true,
  pool_size: 10
```

Also set the endpoint port to 4000:

```elixir
config :camp_fire, CampFireWeb.Endpoint,
  http: [ip: {0, 0, 0, 0}, port: 4000],
  check_origin: false,
  debug_errors: true,
  secret_key_base: "dev-only-secret-key-base-that-is-at-least-64-bytes-long-for-phoenix"
```

**Step 6: Configure test.exs**

```elixir
config :camp_fire, CampFire.Repo,
  username: "campfire",
  password: "campfire",
  hostname: "localhost",
  database: "campfire_test#{System.get_env("MIX_TEST_PARTITION")}",
  pool: Ecto.Adapters.SQL.Sandbox,
  pool_size: 10
```

**Step 7: Configure runtime.exs for production**

```elixir
if config_env() == :prod do
  database_url =
    System.get_env("DATABASE_URL") ||
      raise "DATABASE_URL not set"

  config :camp_fire, CampFire.Repo,
    url: database_url,
    pool_size: String.to_integer(System.get_env("POOL_SIZE") || "10")

  port = String.to_integer(System.get_env("PORT") || "4000")

  config :camp_fire, CampFireWeb.Endpoint,
    url: [host: System.get_env("PHX_HOST") || "localhost", port: 443, scheme: "https"],
    http: [ip: {0, 0, 0, 0, 0, 0, 0, 0}, port: port],
    secret_key_base: System.get_env("SECRET_KEY_BASE") || raise("SECRET_KEY_BASE not set")
end
```

**Step 8: Verify it compiles**

```bash
cd /Users/lotu/game/Garden/server
mix compile
```

Expected: compilation succeeds with no errors.

**Step 9: Commit**

```bash
git add server/
git commit -m "feat(server): scaffold Phoenix project for Elixir migration"
```

---

### Task 2: Ecto Migrations

**Files:**
- Create: `server/priv/repo/migrations/*_create_players.exs`
- Create: `server/priv/repo/migrations/*_create_friend_requests.exs`
- Create: `server/priv/repo/migrations/*_create_friends.exs`
- Create: `server/priv/repo/migrations/*_create_villages.exs`
- Create: `server/priv/repo/migrations/*_create_gifts.exs`
- Create: `server/priv/repo/migrations/*_create_visitor_tables.exs`

**Step 1: Generate and write migrations**

Generate each migration, then fill in the content:

```bash
cd /Users/lotu/game/Garden/server
mix ecto.gen.migration create_players
mix ecto.gen.migration create_friend_requests
mix ecto.gen.migration create_friends
mix ecto.gen.migration create_villages
mix ecto.gen.migration create_gifts
mix ecto.gen.migration create_visitor_tables
```

**create_players:**

```elixir
defmodule CampFire.Repo.Migrations.CreatePlayers do
  use Ecto.Migration

  def change do
    create table(:players, primary_key: false) do
      add :id, :serial, primary_key: true
      add :uid, :text, null: false
      add :auth_token, :text, null: false
      add :friend_code, :text, null: false
      add :display_name, :text, null: false, default: "Camper"

      timestamps(type: :utc_datetime)
    end

    create unique_index(:players, [:uid])
    create unique_index(:players, [:auth_token])
    create unique_index(:players, [:friend_code])
  end
end
```

**create_friend_requests:**

```elixir
defmodule CampFire.Repo.Migrations.CreateFriendRequests do
  use Ecto.Migration

  def change do
    create table(:friend_requests) do
      add :from_uid, references(:players, column: :uid, type: :text), null: false
      add :to_uid, references(:players, column: :uid, type: :text), null: false
      add :status, :text, null: false, default: "pending"

      timestamps(type: :utc_datetime)
    end

    create index(:friend_requests, [:to_uid, :status])
  end
end
```

**create_friends:**

```elixir
defmodule CampFire.Repo.Migrations.CreateFriends do
  use Ecto.Migration

  def change do
    create table(:friends, primary_key: false) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :friend_uid, references(:players, column: :uid, type: :text), null: false
      add :added_at, :utc_datetime, default: fragment("NOW()")
    end

    create unique_index(:friends, [:player_uid, :friend_uid])
  end
end
```

**create_villages:**

```elixir
defmodule CampFire.Repo.Migrations.CreateVillages do
  use Ecto.Migration

  def change do
    create table(:villages) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :snapshot, :map, null: false, default: %{}

      timestamps(type: :utc_datetime)
    end

    create unique_index(:villages, [:player_uid])
  end
end
```

**create_gifts:**

```elixir
defmodule CampFire.Repo.Migrations.CreateGifts do
  use Ecto.Migration

  def change do
    create table(:gifts) do
      add :from_uid, references(:players, column: :uid, type: :text), null: false
      add :to_uid, references(:players, column: :uid, type: :text), null: false
      add :items, :map, null: false, default: fragment("'[]'::jsonb")
      add :status, :text, null: false, default: "pending"
      add :claimed_at, :utc_datetime

      timestamps(type: :utc_datetime)
    end

    create index(:gifts, [:to_uid, :status])
  end
end
```

**create_visitor_tables:**

```elixir
defmodule CampFire.Repo.Migrations.CreateVisitorTables do
  use Ecto.Migration

  def change do
    create table(:visitor_templates) do
      add :visitor_id, :text, null: false
      add :name, :text, null: false
      add :portrait_id, :text
      add :type, :text, null: false
      add :flame_level_min, :integer, null: false, default: 1
      add :dialogue_pool, :map, null: false, default: fragment("'[]'::jsonb")
      add :offer_pool, :map, null: false, default: fragment("'[]'::jsonb")
      add :gift_pool, :map, null: false, default: fragment("'[]'::jsonb")
      add :quest_pool, :map, null: false, default: fragment("'[]'::jsonb")
      add :weight, :float, null: false, default: 1.0
    end

    create unique_index(:visitor_templates, [:visitor_id])

    create table(:visitor_schedule) do
      add :visitor_id, references(:visitor_templates, column: :visitor_id, type: :text), null: false
      add :date, :date
      add :visit_number, :integer
      add :weather_condition, :text
      add :priority, :integer, null: false, default: 0
    end

    create index(:visitor_schedule, [:date])
    create index(:visitor_schedule, [:visit_number])

    create table(:visitor_quests) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :visitor_id, :text, null: false
      add :request_item, :text, null: false
      add :request_count, :integer, null: false
      add :return_date_utc, :date, null: false
      add :reward, :map, null: false, default: fragment("'{}'::jsonb")
      add :return_dialogue, :map, null: false, default: fragment("'[]'::jsonb")

      timestamps(type: :utc_datetime)
    end

    create index(:visitor_quests, [:player_uid])
    create index(:visitor_quests, [:return_date_utc])

    create table(:player_visit_counts, primary_key: false) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false, primary_key: true
      add :count, :integer, null: false, default: 0
      add :last_visit_date, :date
    end
  end
end
```

**Step 2: Run migrations**

```bash
cd /Users/lotu/game/Garden/server
mix ecto.create
mix ecto.migrate
```

Expected: all tables created successfully.

**Step 3: Commit**

```bash
git add server/priv/repo/migrations/
git commit -m "feat(server): add Ecto migrations for all tables"
```

---

### Task 3: Ecto Schemas

**Files:**
- Create: `server/lib/camp_fire/accounts/player.ex`
- Create: `server/lib/camp_fire/social/friend_request.ex`
- Create: `server/lib/camp_fire/social/friend.ex`
- Create: `server/lib/camp_fire/villages/village.ex`
- Create: `server/lib/camp_fire/gifts/gift.ex`
- Create: `server/lib/camp_fire/visitors/visitor_template.ex`
- Create: `server/lib/camp_fire/visitors/visitor_schedule.ex`
- Create: `server/lib/camp_fire/visitors/visitor_quest.ex`
- Create: `server/lib/camp_fire/visitors/player_visit_count.ex`

**Step 1: Create schema modules**

`server/lib/camp_fire/accounts/player.ex`:

```elixir
defmodule CampFire.Accounts.Player do
  use Ecto.Schema
  import Ecto.Changeset

  schema "players" do
    field :uid, :string
    field :auth_token, :string
    field :friend_code, :string
    field :display_name, :string, default: "Camper"

    timestamps()
  end

  def registration_changeset(player, attrs) do
    player
    |> cast(attrs, [:uid, :auth_token, :friend_code])
    |> validate_required([:uid, :auth_token, :friend_code])
    |> unique_constraint(:uid)
    |> unique_constraint(:auth_token)
    |> unique_constraint(:friend_code)
  end

  def display_name_changeset(player, attrs) do
    player
    |> cast(attrs, [:display_name])
    |> validate_required([:display_name])
    |> validate_length(:display_name, min: 1, max: 20)
    |> validate_format(:display_name, ~r/^[a-zA-Z0-9 ]+$/, message: "can only contain letters, numbers, and spaces")
  end
end
```

`server/lib/camp_fire/social/friend_request.ex`:

```elixir
defmodule CampFire.Social.FriendRequest do
  use Ecto.Schema
  import Ecto.Changeset

  schema "friend_requests" do
    field :from_uid, :string
    field :to_uid, :string
    field :status, :string, default: "pending"

    timestamps()
  end

  def changeset(request, attrs) do
    request
    |> cast(attrs, [:from_uid, :to_uid, :status])
    |> validate_required([:from_uid, :to_uid])
    |> validate_inclusion(:status, ["pending", "accepted", "declined"])
  end
end
```

`server/lib/camp_fire/social/friend.ex`:

```elixir
defmodule CampFire.Social.Friend do
  use Ecto.Schema

  @primary_key false
  schema "friends" do
    field :player_uid, :string
    field :friend_uid, :string
    field :added_at, :utc_datetime
  end
end
```

`server/lib/camp_fire/villages/village.ex`:

```elixir
defmodule CampFire.Villages.Village do
  use Ecto.Schema
  import Ecto.Changeset

  schema "villages" do
    field :player_uid, :string
    field :snapshot, :map, default: %{}

    timestamps()
  end

  def changeset(village, attrs) do
    village
    |> cast(attrs, [:player_uid, :snapshot])
    |> validate_required([:player_uid, :snapshot])
    |> unique_constraint(:player_uid)
  end
end
```

`server/lib/camp_fire/gifts/gift.ex`:

```elixir
defmodule CampFire.Gifts.Gift do
  use Ecto.Schema
  import Ecto.Changeset

  schema "gifts" do
    field :from_uid, :string
    field :to_uid, :string
    field :items, :map, default: []
    field :status, :string, default: "pending"
    field :claimed_at, :utc_datetime

    timestamps()
  end

  def changeset(gift, attrs) do
    gift
    |> cast(attrs, [:from_uid, :to_uid, :items, :status, :claimed_at])
    |> validate_required([:from_uid, :to_uid, :items])
    |> validate_inclusion(:status, ["pending", "claimed"])
  end
end
```

`server/lib/camp_fire/visitors/visitor_template.ex`:

```elixir
defmodule CampFire.Visitors.VisitorTemplate do
  use Ecto.Schema

  schema "visitor_templates" do
    field :visitor_id, :string
    field :name, :string
    field :portrait_id, :string
    field :type, :string
    field :flame_level_min, :integer, default: 1
    field :dialogue_pool, {:array, :map}, default: []
    field :offer_pool, {:array, :map}, default: []
    field :gift_pool, {:array, :map}, default: []
    field :quest_pool, {:array, :map}, default: []
    field :weight, :float, default: 1.0
  end
end
```

`server/lib/camp_fire/visitors/visitor_schedule.ex`:

```elixir
defmodule CampFire.Visitors.VisitorSchedule do
  use Ecto.Schema

  schema "visitor_schedule" do
    field :visitor_id, :string
    field :date, :date
    field :visit_number, :integer
    field :weather_condition, :string
    field :priority, :integer, default: 0
  end
end
```

`server/lib/camp_fire/visitors/visitor_quest.ex`:

```elixir
defmodule CampFire.Visitors.VisitorQuest do
  use Ecto.Schema
  import Ecto.Changeset

  schema "visitor_quests" do
    field :player_uid, :string
    field :visitor_id, :string
    field :request_item, :string
    field :request_count, :integer
    field :return_date_utc, :date
    field :reward, :map, default: %{}
    field :return_dialogue, {:array, :string}, default: []

    timestamps()
  end

  def changeset(quest, attrs) do
    quest
    |> cast(attrs, [:player_uid, :visitor_id, :request_item, :request_count, :return_date_utc, :reward, :return_dialogue])
    |> validate_required([:player_uid, :visitor_id, :request_item, :request_count, :return_date_utc])
  end
end
```

`server/lib/camp_fire/visitors/player_visit_count.ex`:

```elixir
defmodule CampFire.Visitors.PlayerVisitCount do
  use Ecto.Schema

  @primary_key {:player_uid, :string, []}
  schema "player_visit_counts" do
    field :count, :integer, default: 0
    field :last_visit_date, :date
  end
end
```

**Step 2: Verify compilation**

```bash
cd /Users/lotu/game/Garden/server
mix compile
```

Expected: no errors.

**Step 3: Commit**

```bash
git add server/lib/camp_fire/
git commit -m "feat(server): add Ecto schemas for all tables"
```

---

### Task 4: Accounts Context + Auth Plug

**Files:**
- Create: `server/lib/camp_fire/accounts.ex`
- Create: `server/lib/camp_fire_web/plugs/authenticate.ex`
- Create: `server/lib/camp_fire_web/plugs/rate_limit.ex`

**Step 1: Write the Accounts context**

`server/lib/camp_fire/accounts.ex`:

```elixir
defmodule CampFire.Accounts do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Accounts.Player

  @friend_code_prefixes ~w(SPARK BLAZE EMBER FLAME TORCH FLARE)
  @code_chars String.graphemes("ABCDEFGHJKLMNPQRSTUVWXYZ23456789")
  @max_retries 10

  def get_player_by_token(token) do
    Repo.one(from p in Player, where: p.auth_token == ^token)
  end

  def get_player_by_uid(uid) do
    Repo.one(from p in Player, where: p.uid == ^uid)
  end

  def get_player_by_friend_code(code) do
    Repo.one(from p in Player, where: p.friend_code == ^code)
  end

  def register_player do
    uid = Ecto.UUID.generate()
    auth_token = Base.encode16(:crypto.strong_rand_bytes(32), case: :lower)

    do_register(uid, auth_token, 0)
  end

  defp do_register(_uid, _token, attempt) when attempt >= @max_retries do
    {:error, :friend_code_exhausted}
  end

  defp do_register(uid, auth_token, attempt) do
    friend_code = generate_friend_code()

    %Player{}
    |> Player.registration_changeset(%{uid: uid, auth_token: auth_token, friend_code: friend_code})
    |> Repo.insert()
    |> case do
      {:ok, player} ->
        {:ok, %{uid: player.uid, auth_token: auth_token, friend_code: player.friend_code, display_name: player.display_name}}

      {:error, %{errors: errors}} ->
        if Keyword.has_key?(errors, :friend_code) do
          do_register(uid, auth_token, attempt + 1)
        else
          {:error, :registration_failed}
        end
    end
  end

  def update_display_name(player, display_name) do
    player
    |> Player.display_name_changeset(%{display_name: String.trim(display_name)})
    |> Repo.update()
  end

  def touch_last_online(uid) do
    Task.start(fn ->
      from(p in Player, where: p.uid == ^uid)
      |> Repo.update_all(set: [updated_at: DateTime.utc_now()])
    end)
  end

  defp generate_friend_code do
    prefix = Enum.random(@friend_code_prefixes)
    suffix = Enum.map(1..4, fn _ -> Enum.random(@code_chars) end) |> Enum.join()
    "#{prefix}-#{suffix}"
  end
end
```

**Step 2: Write the Authenticate plug**

`server/lib/camp_fire_web/plugs/authenticate.ex`:

```elixir
defmodule CampFireWeb.Plugs.Authenticate do
  import Plug.Conn
  alias CampFire.Accounts

  def init(opts), do: opts

  def call(conn, _opts) do
    with ["Bearer " <> token] <- get_req_header(conn, "authorization"),
         %{} = player <- Accounts.get_player_by_token(token) do
      Accounts.touch_last_online(player.uid)
      assign(conn, :current_player, player)
    else
      _ ->
        conn
        |> put_status(401)
        |> Phoenix.Controller.json(%{error: "Missing or invalid auth token"})
        |> halt()
    end
  end
end
```

**Step 3: Write the RateLimit plug**

`server/lib/camp_fire_web/plugs/rate_limit.ex`:

```elixir
defmodule CampFireWeb.Plugs.RateLimit do
  import Plug.Conn

  def init(opts), do: opts

  def call(conn, opts) do
    max = Keyword.fetch!(opts, :max)
    window_ms = Keyword.fetch!(opts, :window_ms)
    key = rate_limit_key(conn)

    case Hammer.check_rate(key, window_ms, max) do
      {:allow, _count} ->
        conn

      {:deny, _limit} ->
        conn
        |> put_status(429)
        |> Phoenix.Controller.json(%{error: "Too many requests, please try again later"})
        |> halt()
    end
  end

  defp rate_limit_key(conn) do
    ip =
      conn
      |> get_peer_data()
      |> Map.get(:address)
      |> :inet.ntoa()
      |> to_string()

    forwarded = get_req_header(conn, "x-forwarded-for")

    actual_ip =
      case forwarded do
        [header | _] -> header |> String.split(",") |> hd() |> String.trim()
        _ -> ip
      end

    "rate_limit:#{actual_ip}"
  end
end
```

**Step 4: Verify compilation**

```bash
cd /Users/lotu/game/Garden/server
mix compile
```

**Step 5: Commit**

```bash
git add server/lib/
git commit -m "feat(server): add Accounts context, auth plug, and rate limiting"
```

---

### Task 5: Router + Auth Controller

**Files:**
- Modify: `server/lib/camp_fire_web/router.ex`
- Create: `server/lib/camp_fire_web/controllers/auth_controller.ex`
- Create: `server/lib/camp_fire_web/controllers/auth_json.ex`
- Create: `server/lib/camp_fire_web/controllers/health_controller.ex`

**Step 1: Write the router**

Replace `server/lib/camp_fire_web/router.ex`:

```elixir
defmodule CampFireWeb.Router do
  use CampFireWeb, :router

  pipeline :api do
    plug :accepts, ["json"]
    plug CampFireWeb.Plugs.RateLimit, max: 100, window_ms: 60_000
  end

  pipeline :auth_rate_limit do
    plug CampFireWeb.Plugs.RateLimit, max: 5, window_ms: 60_000
  end

  pipeline :authenticated do
    plug CampFireWeb.Plugs.Authenticate
  end

  # Health check
  scope "/", CampFireWeb do
    pipe_through :api
    get "/health", HealthController, :index
  end

  # Auth routes (extra rate limiting, no auth required for register)
  scope "/auth", CampFireWeb do
    pipe_through [:api, :auth_rate_limit]

    post "/register", AuthController, :register
    put "/display-name", AuthController, :update_display_name
  end

  # Authenticated routes
  scope "/friends", CampFireWeb do
    pipe_through [:api, :authenticated]

    get "/", FriendController, :index
    post "/request", FriendController, :create_request
    get "/requests", FriendController, :pending_requests
    post "/accept/:request_id", FriendController, :accept
    post "/decline/:request_id", FriendController, :decline
    delete "/:friend_uid", FriendController, :remove
  end

  scope "/village", CampFireWeb do
    pipe_through [:api, :authenticated]

    put "/", VillageController, :upsert
    get "/:uid", VillageController, :show
  end

  scope "/gifts", CampFireWeb do
    pipe_through [:api, :authenticated]

    post "/send", GiftController, :send_gift
    get "/", GiftController, :index
    post "/claim/:gift_id", GiftController, :claim
  end

  scope "/visitors", CampFireWeb do
    pipe_through [:api, :authenticated]

    get "/tonight", VisitorController, :tonight
    post "/quest/accept", VisitorController, :accept_quest
    post "/quest/complete", VisitorController, :complete_quest
  end
end
```

Note: The `/auth/display-name` route needs the auth plug too. Add the authenticate plug inline:

```elixir
  scope "/auth", CampFireWeb do
    pipe_through [:api, :auth_rate_limit]

    post "/register", AuthController, :register

    # display-name requires auth
    pipe_through [:authenticated]
    put "/display-name", AuthController, :update_display_name
  end
```

Actually, Phoenix doesn't allow nested pipe_through like that. Use two scopes instead:

```elixir
  scope "/auth", CampFireWeb do
    pipe_through [:api, :auth_rate_limit]
    post "/register", AuthController, :register
  end

  scope "/auth", CampFireWeb do
    pipe_through [:api, :auth_rate_limit, :authenticated]
    put "/display-name", AuthController, :update_display_name
  end
```

**Step 2: Write AuthController**

`server/lib/camp_fire_web/controllers/auth_controller.ex`:

```elixir
defmodule CampFireWeb.AuthController do
  use CampFireWeb, :controller
  alias CampFire.Accounts

  def register(conn, _params) do
    case Accounts.register_player() do
      {:ok, result} ->
        conn
        |> put_status(201)
        |> json(%{
          uid: result.uid,
          authToken: result.auth_token,
          friendCode: result.friend_code,
          displayName: result.display_name
        })

      {:error, _reason} ->
        conn |> put_status(500) |> json(%{error: "Registration failed"})
    end
  end

  def update_display_name(conn, %{"displayName" => display_name}) do
    player = conn.assigns.current_player

    case Accounts.update_display_name(player, display_name) do
      {:ok, updated} ->
        json(conn, %{displayName: updated.display_name})

      {:error, changeset} ->
        message = format_errors(changeset)
        conn |> put_status(400) |> json(%{error: message})
    end
  end

  def update_display_name(conn, _params) do
    conn |> put_status(400) |> json(%{error: "displayName is required"})
  end

  defp format_errors(changeset) do
    Ecto.Changeset.traverse_errors(changeset, fn {msg, opts} ->
      Regex.replace(~r"%{(\w+)}", msg, fn _, key ->
        opts |> Keyword.get(String.to_existing_atom(key), key) |> to_string()
      end)
    end)
    |> Enum.map_join(", ", fn {_field, errors} -> Enum.join(errors, ", ") end)
  end
end
```

**Step 3: Write HealthController**

`server/lib/camp_fire_web/controllers/health_controller.ex`:

```elixir
defmodule CampFireWeb.HealthController do
  use CampFireWeb, :controller

  def index(conn, _params) do
    json(conn, %{status: "ok"})
  end
end
```

**Step 4: Verify compilation**

```bash
mix compile
```

**Step 5: Commit**

```bash
git add server/lib/
git commit -m "feat(server): add router, auth controller, and health endpoint"
```

---

### Task 6: Friends Context + Controller

**Files:**
- Create: `server/lib/camp_fire/social.ex`
- Create: `server/lib/camp_fire_web/controllers/friend_controller.ex`

**Step 1: Write the Social context**

`server/lib/camp_fire/social.ex`:

```elixir
defmodule CampFire.Social do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Social.{Friend, FriendRequest}
  alias CampFire.Accounts.Player

  @max_friends 20

  def list_friends(uid) do
    from(f in Friend,
      join: p in Player, on: p.uid == f.friend_uid,
      where: f.player_uid == ^uid,
      order_by: p.display_name,
      select: %{uid: p.uid, display_name: p.display_name, friend_code: p.friend_code, last_online: p.updated_at}
    )
    |> Repo.all()
  end

  def are_friends?(uid_a, uid_b) do
    Repo.exists?(from f in Friend, where: f.player_uid == ^uid_a and f.friend_uid == ^uid_b)
  end

  def send_request(from_uid, to_uid) do
    cond do
      from_uid == to_uid ->
        {:error, "Cannot send friend request to yourself"}

      are_friends?(from_uid, to_uid) ->
        {:error, "Already friends"}

      has_pending_request?(from_uid, to_uid) ->
        {:error, "Friend request already pending"}

      true ->
        %FriendRequest{}
        |> FriendRequest.changeset(%{from_uid: from_uid, to_uid: to_uid})
        |> Repo.insert()
        |> case do
          {:ok, _} -> :ok
          {:error, _} -> {:error, "Failed to send friend request"}
        end
    end
  end

  def pending_requests(to_uid) do
    from(fr in FriendRequest,
      join: p in Player, on: p.uid == fr.from_uid,
      where: fr.to_uid == ^to_uid and fr.status == "pending",
      order_by: [desc: fr.inserted_at],
      select: %{id: fr.id, from_uid: fr.from_uid, from_name: p.display_name, status: fr.status, created_at: fr.inserted_at}
    )
    |> Repo.all()
  end

  def accept_request(request_id, current_uid) do
    Repo.transaction(fn ->
      request =
        from(fr in FriendRequest,
          where: fr.id == ^request_id and fr.status == "pending" and fr.to_uid == ^current_uid
        )
        |> Repo.one()

      if is_nil(request) do
        Repo.rollback("Friend request not found")
      end

      count_a = friend_count(request.from_uid)
      count_b = friend_count(request.to_uid)

      cond do
        count_a >= @max_friends ->
          Repo.rollback("Sender has reached max friends")

        count_b >= @max_friends ->
          Repo.rollback("You have reached max friends")

        true ->
          request
          |> FriendRequest.changeset(%{status: "accepted"})
          |> Repo.update!()

          now = DateTime.utc_now()

          Repo.insert_all(Friend, [
            %{player_uid: request.from_uid, friend_uid: request.to_uid, added_at: now},
            %{player_uid: request.to_uid, friend_uid: request.from_uid, added_at: now}
          ], on_conflict: :nothing)

          list_friends(current_uid)
      end
    end)
  end

  def decline_request(request_id, current_uid) do
    query =
      from(fr in FriendRequest,
        where: fr.id == ^request_id and fr.to_uid == ^current_uid and fr.status == "pending"
      )

    case Repo.update_all(query, set: [status: "declined"]) do
      {0, _} -> {:error, :not_found}
      {_, _} -> :ok
    end
  end

  def remove_friend(uid, friend_uid) do
    query =
      from(f in Friend,
        where:
          (f.player_uid == ^uid and f.friend_uid == ^friend_uid) or
          (f.player_uid == ^friend_uid and f.friend_uid == ^uid)
      )

    case Repo.delete_all(query) do
      {0, _} -> {:error, :not_found}
      {_, _} -> :ok
    end
  end

  defp has_pending_request?(from_uid, to_uid) do
    Repo.exists?(
      from fr in FriendRequest,
        where: fr.status == "pending" and
          ((fr.from_uid == ^from_uid and fr.to_uid == ^to_uid) or
           (fr.from_uid == ^to_uid and fr.to_uid == ^from_uid))
    )
  end

  defp friend_count(uid) do
    Repo.aggregate(from(f in Friend, where: f.player_uid == ^uid), :count)
  end
end
```

**Step 2: Write FriendController**

`server/lib/camp_fire_web/controllers/friend_controller.ex`:

```elixir
defmodule CampFireWeb.FriendController do
  use CampFireWeb, :controller
  alias CampFire.Social
  alias CampFire.Accounts

  def index(conn, _params) do
    friends = Social.list_friends(conn.assigns.current_player.uid)
    json(conn, %{friends: friends})
  end

  def create_request(conn, %{"friendCode" => friend_code}) do
    uid = conn.assigns.current_player.uid

    case Accounts.get_player_by_friend_code(friend_code) do
      nil ->
        conn |> put_status(404) |> json(%{error: "Player not found"})

      target ->
        case Social.send_request(uid, target.uid) do
          :ok -> conn |> put_status(201) |> json(%{message: "Friend request sent"})
          {:error, msg} -> conn |> put_status(400) |> json(%{error: msg})
        end
    end
  end

  def create_request(conn, _params) do
    conn |> put_status(400) |> json(%{error: "friendCode is required"})
  end

  def pending_requests(conn, _params) do
    requests = Social.pending_requests(conn.assigns.current_player.uid)
    json(conn, %{requests: requests})
  end

  def accept(conn, %{"request_id" => request_id}) do
    case Social.accept_request(request_id, conn.assigns.current_player.uid) do
      {:ok, friends} -> json(conn, %{friends: friends})
      {:error, msg} -> conn |> put_status(400) |> json(%{error: msg})
    end
  end

  def decline(conn, %{"request_id" => request_id}) do
    case Social.decline_request(request_id, conn.assigns.current_player.uid) do
      :ok -> json(conn, %{message: "Friend request declined"})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Friend request not found"})
    end
  end

  def remove(conn, %{"friend_uid" => friend_uid}) do
    case Social.remove_friend(conn.assigns.current_player.uid, friend_uid) do
      :ok -> json(conn, %{message: "Friend removed"})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Friend not found"})
    end
  end
end
```

**Step 3: Verify compilation, commit**

```bash
mix compile
git add server/lib/
git commit -m "feat(server): add Social context and Friends controller"
```

---

### Task 7: Villages Context + Controller

**Files:**
- Create: `server/lib/camp_fire/villages.ex`
- Create: `server/lib/camp_fire_web/controllers/village_controller.ex`

**Step 1: Write Villages context**

`server/lib/camp_fire/villages.ex`:

```elixir
defmodule CampFire.Villages do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Villages.Village

  @max_snapshot_bytes 102_400

  def upsert_snapshot(player_uid, snapshot) do
    encoded = Jason.encode!(snapshot)

    if byte_size(encoded) > @max_snapshot_bytes do
      {:error, :too_large}
    else
      %Village{}
      |> Village.changeset(%{player_uid: player_uid, snapshot: snapshot})
      |> Repo.insert(
        on_conflict: [set: [snapshot: snapshot, updated_at: DateTime.utc_now()]],
        conflict_target: :player_uid
      )
    end
  end

  def get_snapshot(player_uid) do
    case Repo.one(from v in Village, where: v.player_uid == ^player_uid) do
      nil -> %{snapshot: %{}, updated_at: nil}
      village -> %{snapshot: village.snapshot, updated_at: village.updated_at}
    end
  end
end
```

**Step 2: Write VillageController**

`server/lib/camp_fire_web/controllers/village_controller.ex`:

```elixir
defmodule CampFireWeb.VillageController do
  use CampFireWeb, :controller
  alias CampFire.Villages
  alias CampFire.Social

  def upsert(conn, %{"snapshot" => snapshot}) when is_map(snapshot) do
    case Villages.upsert_snapshot(conn.assigns.current_player.uid, snapshot) do
      {:ok, _} -> json(conn, %{message: "Village updated"})
      {:error, :too_large} -> conn |> put_status(413) |> json(%{error: "Village snapshot too large (max 100KB)"})
      {:error, _} -> conn |> put_status(500) |> json(%{error: "Failed to update village"})
    end
  end

  def upsert(conn, _params) do
    conn |> put_status(400) |> json(%{error: "snapshot must be a JSON object"})
  end

  def show(conn, %{"uid" => uid}) do
    current_uid = conn.assigns.current_player.uid

    if uid != current_uid and not Social.are_friends?(current_uid, uid) do
      conn |> put_status(403) |> json(%{error: "Not friends with this player"})
    else
      result = Villages.get_snapshot(uid)
      json(conn, %{snapshot: result.snapshot, updatedAt: result.updated_at})
    end
  end
end
```

**Step 3: Commit**

```bash
mix compile
git add server/lib/
git commit -m "feat(server): add Villages context and controller"
```

---

### Task 8: Gifts Context + Controller

**Files:**
- Create: `server/lib/camp_fire/gifts.ex`
- Create: `server/lib/camp_fire_web/controllers/gift_controller.ex`

**Step 1: Write Gifts context**

`server/lib/camp_fire/gifts.ex`:

```elixir
defmodule CampFire.Gifts do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Gifts.Gift
  alias CampFire.Accounts.Player

  @max_items_per_gift 3
  @max_gifts_per_day 5
  @gift_expiry_days 7

  def send_gift(from_uid, to_uid, items) do
    cond do
      to_uid == from_uid ->
        {:error, "Cannot send gift to yourself"}

      not is_list(items) or length(items) == 0 ->
        {:error, "items must be a non-empty array"}

      length(items) > @max_items_per_gift ->
        {:error, "Max #{@max_items_per_gift} items per gift"}

      gifts_today(from_uid, to_uid) >= @max_gifts_per_day ->
        {:error, "Max #{@max_gifts_per_day} gifts per day to same player"}

      true ->
        %Gift{}
        |> Gift.changeset(%{from_uid: from_uid, to_uid: to_uid, items: items})
        |> Repo.insert()
    end
  end

  def pending_gifts(to_uid) do
    cutoff = DateTime.add(DateTime.utc_now(), -@gift_expiry_days * 86400, :second)

    from(g in Gift,
      join: p in Player, on: p.uid == g.from_uid,
      where: g.to_uid == ^to_uid and g.status == "pending" and g.inserted_at >= ^cutoff,
      order_by: [desc: g.inserted_at],
      select: %{id: g.id, from_uid: g.from_uid, from_name: p.display_name, items: g.items, created_at: g.inserted_at}
    )
    |> Repo.all()
  end

  def claim_gift(gift_id, to_uid) do
    query =
      from(g in Gift,
        where: g.id == ^gift_id and g.to_uid == ^to_uid and g.status == "pending"
      )

    case Repo.one(query) do
      nil ->
        {:error, :not_found}

      gift ->
        gift
        |> Gift.changeset(%{status: "claimed", claimed_at: DateTime.utc_now()})
        |> Repo.update()
        |> case do
          {:ok, claimed} -> {:ok, claimed.items}
          {:error, _} -> {:error, :update_failed}
        end
    end
  end

  defp gifts_today(from_uid, to_uid) do
    cutoff = DateTime.add(DateTime.utc_now(), -86400, :second)

    from(g in Gift,
      where: g.from_uid == ^from_uid and g.to_uid == ^to_uid and g.inserted_at >= ^cutoff
    )
    |> Repo.aggregate(:count)
  end
end
```

**Step 2: Write GiftController**

`server/lib/camp_fire_web/controllers/gift_controller.ex`:

```elixir
defmodule CampFireWeb.GiftController do
  use CampFireWeb, :controller
  alias CampFire.Gifts
  alias CampFire.Social

  def send_gift(conn, %{"toUid" => to_uid, "items" => items}) do
    from_uid = conn.assigns.current_player.uid

    if not Social.are_friends?(from_uid, to_uid) do
      conn |> put_status(403) |> json(%{error: "Not friends with this player"})
    else
      case Gifts.send_gift(from_uid, to_uid, items) do
        {:ok, gift} ->
          conn |> put_status(201) |> json(%{giftId: gift.id, createdAt: gift.inserted_at})

        {:error, msg} when is_binary(msg) ->
          conn |> put_status(400) |> json(%{error: msg})

        {:error, _} ->
          conn |> put_status(500) |> json(%{error: "Failed to send gift"})
      end
    end
  end

  def send_gift(conn, _params) do
    conn |> put_status(400) |> json(%{error: "toUid and items are required"})
  end

  def index(conn, _params) do
    gifts = Gifts.pending_gifts(conn.assigns.current_player.uid)
    json(conn, %{gifts: gifts})
  end

  def claim(conn, %{"gift_id" => gift_id}) do
    case Gifts.claim_gift(gift_id, conn.assigns.current_player.uid) do
      {:ok, items} -> json(conn, %{items: items})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Gift not found or already claimed"})
      {:error, _} -> conn |> put_status(500) |> json(%{error: "Failed to claim gift"})
    end
  end
end
```

**Step 3: Commit**

```bash
mix compile
git add server/lib/
git commit -m "feat(server): add Gifts context and controller"
```

---

### Task 9: Visitors Context + Controller

**Files:**
- Create: `server/lib/camp_fire/visitors.ex`
- Create: `server/lib/camp_fire_web/controllers/visitor_controller.ex`

**Step 1: Write Visitors context**

`server/lib/camp_fire/visitors.ex`:

```elixir
defmodule CampFire.Visitors do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule, VisitorQuest, PlayerVisitCount}
  alias CampFire.Villages

  def get_tonight_visitor(uid) do
    today = Date.utc_today()

    visit_number = increment_visit_count(uid, today)
    flame_level = get_flame_level(uid)

    # Priority 1: Scheduled date visitors
    with nil <- get_date_visitor(today),
         # Priority 2: Visit number milestones
         nil <- get_milestone_visitor(visit_number),
         # Priority 3: Weather-triggered (no-op placeholder)
         nil <- get_weather_visitor(),
         # Priority 4: Quest returns
         nil <- get_quest_return(uid, today),
         # Priority 5: Random weighted pool
         nil <- get_random_visitor(flame_level) do
      %{visitor_type: nil, message: "No visitors available tonight"}
    else
      visitor -> visitor
    end
  end

  def accept_quest(uid, params) do
    return_date =
      Date.utc_today()
      |> Date.add(params["return_days"])

    %VisitorQuest{}
    |> VisitorQuest.changeset(%{
      player_uid: uid,
      visitor_id: params["visitor_id"],
      request_item: params["request_item"],
      request_count: params["request_count"],
      return_date_utc: return_date,
      reward: params["reward"] || %{},
      return_dialogue: params["return_dialogue"] || []
    })
    |> Repo.insert()
    |> case do
      {:ok, quest} ->
        {:ok, %{quest_id: quest.id, return_date: Date.to_iso8601(quest.return_date_utc)}}

      {:error, _changeset} ->
        {:error, "Failed to accept quest"}
    end
  end

  def complete_quest(uid, quest_id) do
    today = Date.utc_today()

    query =
      from(q in VisitorQuest,
        where: q.id == ^quest_id and q.player_uid == ^uid and q.return_date_utc <= ^today
      )

    case Repo.one(query) do
      nil ->
        {:error, :not_found}

      quest ->
        Repo.delete!(quest)
        {:ok, quest.reward}
    end
  end

  # --- Private helpers ---

  defp increment_visit_count(uid, today) do
    Repo.query!(
      """
      INSERT INTO player_visit_counts (player_uid, count, last_visit_date)
      VALUES ($1, 1, $2)
      ON CONFLICT (player_uid) DO UPDATE
        SET count = CASE
          WHEN player_visit_counts.last_visit_date < $2
            THEN player_visit_counts.count + 1
          ELSE player_visit_counts.count
        END,
        last_visit_date = $2
      RETURNING count
      """,
      [uid, today]
    ).rows
    |> hd()
    |> hd()
  end

  defp get_flame_level(uid) do
    case Villages.get_snapshot(uid) do
      %{snapshot: %{"flameLevel" => level}} -> level
      _ -> 1
    end
  end

  defp get_date_visitor(today) do
    query =
      from(vs in VisitorSchedule,
        join: vt in VisitorTemplate, on: vt.visitor_id == vs.visitor_id,
        where: vs.date == ^today,
        order_by: [desc: vs.priority],
        limit: 1,
        select: vt
      )

    case Repo.one(query) do
      nil -> nil
      template -> build_visitor_payload(template)
    end
  end

  defp get_milestone_visitor(visit_number) do
    query =
      from(vs in VisitorSchedule,
        join: vt in VisitorTemplate, on: vt.visitor_id == vs.visitor_id,
        where: vs.visit_number == ^visit_number,
        order_by: [desc: vs.priority],
        limit: 1,
        select: vt
      )

    case Repo.one(query) do
      nil -> nil
      template -> build_visitor_payload(template)
    end
  end

  defp get_weather_visitor do
    # TODO: Check actual weather conditions when weather data is available on the server.
    nil
  end

  defp get_quest_return(uid, today) do
    query =
      from(q in VisitorQuest,
        where: q.player_uid == ^uid and q.return_date_utc <= ^today,
        order_by: [asc: q.return_date_utc],
        limit: 1
      )

    case Repo.one(query) do
      nil ->
        nil

      quest ->
        template =
          Repo.one(from vt in VisitorTemplate, where: vt.visitor_id == ^quest.visitor_id)

        base =
          if template do
            build_visitor_payload(template)
          else
            %{
              visitor_type: "quester",
              visitor_id: quest.visitor_id,
              name: quest.visitor_id,
              portrait_id: nil,
              dialogue: []
            }
          end

        Map.merge(base, %{
          visitor_type: "quester",
          dialogue: quest.return_dialogue || [],
          quest: %{
            quest_id: quest.id,
            is_return: true,
            reward: quest.reward
          }
        })
    end
  end

  defp get_random_visitor(flame_level) do
    templates =
      from(vt in VisitorTemplate, where: vt.flame_level_min <= ^flame_level)
      |> Repo.all()

    case templates do
      [] -> nil
      list -> list |> weighted_random() |> build_visitor_payload()
    end
  end

  defp weighted_random(templates) do
    total = Enum.reduce(templates, 0.0, fn t, acc -> acc + t.weight end)
    roll = :rand.uniform() * total

    Enum.reduce_while(templates, roll, fn t, remaining ->
      remaining = remaining - t.weight
      if remaining <= 0, do: {:halt, t}, else: {:cont, remaining}
    end)
    |> case do
      %VisitorTemplate{} = t -> t
      _ -> List.last(templates)
    end
  end

  defp build_visitor_payload(template) do
    base = %{
      visitor_type: template.type,
      visitor_id: template.visitor_id,
      name: template.name,
      portrait_id: template.portrait_id,
      dialogue: roll_dialogue(template.dialogue_pool)
    }

    case template.type do
      "merchant" -> Map.put(base, :offers, template.offer_pool)
      "gifter" -> Map.put(base, :gift, roll_gift(template.gift_pool))
      "quester" -> Map.put(base, :quest, roll_quest(template.quest_pool))
      _ -> base
    end
  end

  defp roll_dialogue(pool) when is_list(pool) and length(pool) > 0 do
    pick = Enum.random(pool)
    if is_list(pick), do: pick, else: [pick]
  end
  defp roll_dialogue(_), do: []

  defp roll_gift(pool) when is_list(pool) and length(pool) > 0, do: Enum.random(pool)
  defp roll_gift(_), do: nil

  defp roll_quest(pool) when is_list(pool) and length(pool) > 0, do: Enum.random(pool)
  defp roll_quest(_), do: nil
end
```

**Step 2: Write VisitorController**

`server/lib/camp_fire_web/controllers/visitor_controller.ex`:

```elixir
defmodule CampFireWeb.VisitorController do
  use CampFireWeb, :controller
  alias CampFire.Visitors

  def tonight(conn, _params) do
    visitor = Visitors.get_tonight_visitor(conn.assigns.current_player.uid)
    json(conn, visitor)
  end

  def accept_quest(conn, params) do
    uid = conn.assigns.current_player.uid

    required = ["visitor_id", "request_item", "request_count", "return_days"]
    missing = Enum.filter(required, &(not Map.has_key?(params, &1)))

    if missing != [] do
      conn |> put_status(400) |> json(%{error: "#{Enum.join(missing, ", ")} required"})
    else
      case Visitors.accept_quest(uid, params) do
        {:ok, result} -> conn |> put_status(201) |> json(result)
        {:error, msg} -> conn |> put_status(500) |> json(%{error: msg})
      end
    end
  end

  def complete_quest(conn, %{"quest_id" => quest_id}) do
    uid = conn.assigns.current_player.uid

    case Visitors.complete_quest(uid, quest_id) do
      {:ok, reward} -> json(conn, %{reward: reward})
      {:error, :not_found} -> conn |> put_status(404) |> json(%{error: "Quest not found"})
      {:error, msg} -> conn |> put_status(500) |> json(%{error: msg})
    end
  end

  def complete_quest(conn, _params) do
    conn |> put_status(400) |> json(%{error: "quest_id is required"})
  end
end
```

**Step 3: Commit**

```bash
mix compile
git add server/lib/
git commit -m "feat(server): add Visitors context and controller"
```

---

### Task 10: Seed Data

**Files:**
- Create: `server/priv/repo/seeds.exs`

**Step 1: Write seeds file**

`server/priv/repo/seeds.exs`:

```elixir
alias CampFire.Repo
alias CampFire.Visitors.{VisitorTemplate, VisitorSchedule}

templates = [
  %{
    visitor_id: "thorn_merchant",
    name: "Thorn",
    portrait_id: "thorn",
    type: "merchant",
    flame_level_min: 1,
    dialogue_pool: [
      ["The road was long, but your flame drew me in.", "I've got rare seeds from distant lands.", "Care to trade?"],
      ["Ah, another campsite with good soil.", "Let's do business, shall we?"],
      ["I picked these up on the coast, past the marshes.", "They don't grow just anywhere.", "What have you got to offer in return?"]
    ],
    offer_pool: [
      %{costs: [%{itemName: "Basil Leaf", count: 2}], rewardSeedName: "Lavender", rewardCount: 1},
      %{costs: [%{itemName: "Chamomile Petal", count: 3}], rewardSeedName: "Mint", rewardCount: 1},
      %{costs: [%{itemName: "Mint Leaf", count: 2}], rewardSeedName: "Rosemary", rewardCount: 1},
      %{costs: [%{itemName: "Lavender Petal", count: 2}], rewardSeedName: "Dahlia", rewardCount: 1}
    ],
    gift_pool: [],
    quest_pool: [],
    weight: 1.0
  },
  %{
    visitor_id: "willow_gifter",
    name: "Willow",
    portrait_id: "willow",
    type: "gifter",
    flame_level_min: 1,
    dialogue_pool: [
      ["Hello, dear! Your flame is so warm.", "I brought a little something for you."],
      ["What a lovely campsite you have here!", "Please, take this gift. It's the least I can do.", "May your garden flourish!"],
      ["I was just passing through and felt your flame's warmth.", "Here, I hope this helps your garden grow."]
    ],
    offer_pool: [],
    gift_pool: [
      %{type: "seed", name: "Chamomile", amount: 2},
      %{type: "water", amount: 3},
      %{type: "seed", name: "Basil", amount: 3},
      %{type: "item", name: "Basil Leaf", amount: 2}
    ],
    quest_pool: [],
    weight: 1.5
  },
  %{
    visitor_id: "ember_quester",
    name: "Ember",
    portrait_id: "ember",
    type: "quester",
    flame_level_min: 2,
    dialogue_pool: [
      ["The stars told me to seek you out.", "I need something... rare. Can you help?"],
      ["I've been wandering for a long time, looking for the right campsite.", "Yours has the right energy. I have a request."]
    ],
    offer_pool: [],
    gift_pool: [],
    quest_pool: [
      %{
        request_item: "Lavender Petal", request_count: 3, return_days: 7,
        reward: %{type: "seed", name: "Moonflower", count: 2},
        return_dialogue: ["You found them!", "Here, take these rare seeds as thanks."]
      },
      %{
        request_item: "Chamomile Petal", request_count: 5, return_days: 5,
        reward: %{type: "seed", name: "Jasmine", count: 1},
        return_dialogue: ["Perfect!", "I knew I could count on you."]
      }
    ],
    weight: 0.8
  }
]

for t <- templates do
  Repo.insert!(%VisitorTemplate{
    visitor_id: t.visitor_id,
    name: t.name,
    portrait_id: t.portrait_id,
    type: t.type,
    flame_level_min: t.flame_level_min,
    dialogue_pool: t.dialogue_pool,
    offer_pool: t.offer_pool,
    gift_pool: t.gift_pool,
    quest_pool: t.quest_pool,
    weight: t.weight
  }, on_conflict: :nothing, conflict_target: :visitor_id)
end

schedule = [
  %{visitor_id: "willow_gifter", visit_number: 1, priority: 10},
  %{visitor_id: "ember_quester", visit_number: 7, priority: 10}
]

for s <- schedule do
  exists =
    CampFire.Repo.exists?(
      from vs in VisitorSchedule,
        where: vs.visitor_id == ^s.visitor_id and vs.visit_number == ^s.visit_number
    )

  unless exists do
    Repo.insert!(%VisitorSchedule{
      visitor_id: s.visitor_id,
      visit_number: s.visit_number,
      priority: s.priority
    })
  end
end

IO.puts("Seeds complete.")
```

Add `import Ecto.Query` at the top of the seeds file for the `from` macro.

**Step 2: Run seeds**

```bash
cd /Users/lotu/game/Garden/server
mix run priv/repo/seeds.exs
```

Expected: "Seeds complete."

**Step 3: Commit**

```bash
git add server/priv/repo/seeds.exs
git commit -m "feat(server): add visitor seed data"
```

---

### Task 11: Makefile + DevServerConfig Port Update

**Files:**
- Create: `server/Makefile`
- Modify: `Assets/Scripts/Services/DevServerConfig.cs` (port change from 3000 to 4000)

**Step 1: Write new Makefile**

`server/Makefile`:

```makefile
.PHONY: setup dev start test psql reset up down logs tunnel tunnel-stop deps

DEV_CONFIG := ../Assets/Scripts/Services/DevServerConfig.cs
PORT := 4000
NGROK_PID_FILE := .ngrok.pid

# Install Elixir dependencies
deps:
	mix deps.get

# Start PostgreSQL
up:
	docker compose up -d

# Stop PostgreSQL
down:
	docker compose down

# Stop PostgreSQL and delete data
reset:
	docker compose down -v
	mix ecto.reset

# Full setup: start db, wait for ready, create + migrate + seed
setup: up deps
	@echo "Waiting for PostgreSQL..."
	@until docker exec $$(docker compose ps -q db) pg_isready -U campfire > /dev/null 2>&1; do sleep 0.5; done
	@echo "PostgreSQL ready."
	mix ecto.setup
	@echo "Setup complete. Run 'make dev' to start the server."

# Start server in dev mode with ngrok tunnel
dev: tunnel
	mix phx.server; $(MAKE) tunnel-stop

# Start server without tunnel
start:
	mix phx.server

# Run tests
test:
	mix test

# Tail PostgreSQL logs
logs:
	docker compose logs -f db

# Open psql shell
psql:
	docker exec -it $$(docker compose ps -q db) psql -U campfire campfire_dev

# Start ngrok tunnel and write URL into DevServerConfig.cs
tunnel:
	@if [ -f $(NGROK_PID_FILE) ] && kill -0 $$(cat $(NGROK_PID_FILE)) 2>/dev/null; then \
		echo "ngrok already running (pid $$(cat $(NGROK_PID_FILE)))"; \
	else \
		ngrok http $(PORT) --log=stdout > /dev/null & echo $$! > $(NGROK_PID_FILE); \
		echo "Starting ngrok..."; \
		sleep 2; \
	fi
	@URL=$$(curl -s http://localhost:4040/api/tunnels | python3 -c "import sys,json; print(json.load(sys.stdin)['tunnels'][0]['public_url'])" 2>/dev/null) && \
	if [ -z "$$URL" ]; then echo "ERROR: Could not get ngrok URL. Is ngrok running?"; exit 1; fi && \
	printf 'namespace Garden\n{\n    public static class DevServerConfig\n    {\n        public const string BaseUrl = "%s";\n    }\n}\n' "$$URL" > $(DEV_CONFIG) && \
	echo "DevServerConfig.cs updated: $$URL"

# Stop ngrok tunnel and reset DevServerConfig to localhost
tunnel-stop:
	@if [ -f $(NGROK_PID_FILE) ]; then \
		kill $$(cat $(NGROK_PID_FILE)) 2>/dev/null; \
		rm $(NGROK_PID_FILE); \
		echo "ngrok stopped."; \
	else \
		echo "ngrok not running."; \
	fi
	@printf 'namespace Garden\n{\n    public static class DevServerConfig\n    {\n        public const string BaseUrl = "http://localhost:4000";\n    }\n}\n' > $(DEV_CONFIG)
	@echo "DevServerConfig.cs reset to localhost."
```

**Step 2: Update DevServerConfig default port**

The tunnel-stop target resets to `http://localhost:4000`. The current `DevServerConfig.cs` points to an ngrok URL so it'll be overwritten next time `make dev` or `make tunnel-stop` runs. No code change needed — the Makefile handles it.

**Step 3: Commit**

```bash
git add server/Makefile
git commit -m "feat(server): add Elixir Makefile with same targets"
```

---

### Task 12: Hammer Configuration

**Files:**
- Modify: `server/config/config.exs`

**Step 1: Add Hammer config**

Add to `server/config/config.exs`:

```elixir
config :hammer,
  backend: {Hammer.Backend.ETS,
            [expiry_ms: 60_000 * 10, cleanup_interval_ms: 60_000]}
```

**Step 2: Verify compilation**

```bash
mix compile
```

**Step 3: Commit**

```bash
git add server/config/
git commit -m "feat(server): configure Hammer ETS rate limiting"
```

---

### Task 13: Controller Tests

**Files:**
- Create: `server/test/camp_fire_web/controllers/auth_controller_test.exs`
- Create: `server/test/camp_fire_web/controllers/friend_controller_test.exs`
- Create: `server/test/camp_fire_web/controllers/village_controller_test.exs`
- Create: `server/test/camp_fire_web/controllers/gift_controller_test.exs`
- Create: `server/test/camp_fire_web/controllers/visitor_controller_test.exs`
- Create: `server/test/support/test_helpers.ex`

**Step 1: Create test helper for registering players**

`server/test/support/test_helpers.ex`:

```elixir
defmodule CampFire.TestHelpers do
  alias CampFire.Accounts

  def register_player do
    {:ok, player} = Accounts.register_player()
    player
  end

  def auth_header(player) do
    [{"authorization", "Bearer #{player.auth_token}"}]
  end

  def authed_conn(conn, player) do
    Enum.reduce(auth_header(player), conn, fn {key, val}, conn ->
      Plug.Conn.put_req_header(conn, key, val)
    end)
  end
end
```

**Step 2: Write auth controller tests**

`server/test/camp_fire_web/controllers/auth_controller_test.exs`:

```elixir
defmodule CampFireWeb.AuthControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  describe "POST /auth/register" do
    test "creates a new player", %{conn: conn} do
      conn = post(conn, "/auth/register")
      body = json_response(conn, 201)

      assert body["uid"]
      assert body["authToken"]
      assert body["friendCode"]
      assert body["displayName"] == "Camper"
    end
  end

  describe "PUT /auth/display-name" do
    test "updates display name with valid auth", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> put("/auth/display-name", %{displayName: "NewName"})

      assert json_response(conn, 200)["displayName"] == "NewName"
    end

    test "rejects invalid characters", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> put("/auth/display-name", %{displayName: "bad!name"})

      assert json_response(conn, 400)["error"]
    end

    test "rejects without auth", %{conn: conn} do
      conn = put(conn, "/auth/display-name", %{displayName: "Test"})
      assert json_response(conn, 401)
    end
  end
end
```

**Step 3: Write friend controller tests**

`server/test/camp_fire_web/controllers/friend_controller_test.exs`:

```elixir
defmodule CampFireWeb.FriendControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  setup %{conn: conn} do
    player = register_player()
    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player}
  end

  test "GET /friends returns empty list initially", %{conn: conn} do
    conn = get(conn, "/friends")
    assert json_response(conn, 200)["friends"] == []
  end

  test "full friend request flow", %{conn: conn, player: player} do
    other = register_player()

    # Send request
    conn1 = post(conn, "/friends/request", %{friendCode: other.friend_code})
    assert json_response(conn1, 201)["message"] == "Friend request sent"

    # Other player sees pending request
    other_conn = build_conn() |> authed_conn(other)
    conn2 = get(other_conn, "/friends/requests")
    [request] = json_response(conn2, 200)["requests"]
    assert request["from_uid"] == player.uid

    # Accept
    conn3 = post(other_conn, "/friends/accept/#{request["id"]}")
    friends = json_response(conn3, 200)["friends"]
    assert length(friends) == 1

    # Both see each other
    conn4 = get(conn, "/friends")
    assert length(json_response(conn4, 200)["friends"]) == 1
  end

  test "cannot friend yourself", %{conn: conn, player: player} do
    conn = post(conn, "/friends/request", %{friendCode: player.friend_code})
    assert json_response(conn, 400)["error"] =~ "yourself"
  end
end
```

**Step 4: Write village controller tests**

`server/test/camp_fire_web/controllers/village_controller_test.exs`:

```elixir
defmodule CampFireWeb.VillageControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  setup %{conn: conn} do
    player = register_player()
    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player}
  end

  test "upsert and read own village", %{conn: conn, player: player} do
    snapshot = %{flameLevel: 3, plots: []}

    conn1 = put(conn, "/village", %{snapshot: snapshot})
    assert json_response(conn1, 200)["message"] == "Village updated"

    conn2 = get(conn, "/village/#{player.uid}")
    body = json_response(conn2, 200)
    assert body["snapshot"]["flameLevel"] == 3
  end

  test "cannot read non-friend village", %{conn: conn} do
    other = register_player()
    conn = get(conn, "/village/#{other.uid}")
    assert json_response(conn, 403)
  end
end
```

**Step 5: Write gift controller tests**

`server/test/camp_fire_web/controllers/gift_controller_test.exs`:

```elixir
defmodule CampFireWeb.GiftControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers
  alias CampFire.Social

  setup %{conn: conn} do
    player = register_player()
    friend = register_player()

    # Make them friends directly
    Social.send_request(player.uid, friend.uid)
    # Need to find the request ID
    [req] = Social.pending_requests(friend.uid)
    Social.accept_request(req.id, friend.uid)

    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player, friend: friend}
  end

  test "send and claim gift flow", %{conn: conn, friend: friend} do
    # Send
    conn1 = post(conn, "/gifts/send", %{toUid: friend.uid, items: [%{type: "seed", name: "Basil"}]})
    body = json_response(conn1, 201)
    assert body["giftId"]

    # Friend sees pending gift
    friend_conn = build_conn() |> authed_conn(friend)
    conn2 = get(friend_conn, "/gifts")
    [gift] = json_response(conn2, 200)["gifts"]

    # Claim
    conn3 = post(friend_conn, "/gifts/claim/#{gift["id"]}")
    assert json_response(conn3, 200)["items"]
  end

  test "cannot send to non-friend", %{conn: conn} do
    stranger = register_player()
    conn = post(conn, "/gifts/send", %{toUid: stranger.uid, items: [%{type: "seed"}]})
    assert json_response(conn, 403)
  end
end
```

**Step 6: Write visitor controller tests**

`server/test/camp_fire_web/controllers/visitor_controller_test.exs`:

```elixir
defmodule CampFireWeb.VisitorControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers

  setup %{conn: conn} do
    # Seed visitor templates
    Mix.Tasks.Run.run(["priv/repo/seeds.exs"])

    player = register_player()
    conn = authed_conn(conn, player)
    {:ok, conn: conn, player: player}
  end

  test "GET /visitors/tonight returns a visitor", %{conn: conn} do
    conn = get(conn, "/visitors/tonight")
    body = json_response(conn, 200)
    # First visit should get Willow (milestone visit #1)
    assert body["visitor_id"] == "willow_gifter"
    assert body["name"] == "Willow"
  end

  test "quest accept and complete flow", %{conn: conn} do
    conn1 = post(conn, "/visitors/quest/accept", %{
      visitor_id: "ember_quester",
      request_item: "Lavender Petal",
      request_count: 3,
      return_days: 0
    })
    body = json_response(conn1, 201)
    quest_id = body["quest_id"]
    assert quest_id

    # Complete immediately (return_days: 0)
    conn2 = post(conn, "/visitors/quest/complete", %{quest_id: quest_id})
    assert json_response(conn2, 200)["reward"]
  end
end
```

**Step 7: Run all tests**

```bash
cd /Users/lotu/game/Garden/server
mix test
```

Expected: all tests pass.

**Step 8: Commit**

```bash
git add server/test/
git commit -m "test(server): add controller tests for all endpoints"
```

---

### Task 14: Smoke Test End-to-End

**Step 1: Start the server**

```bash
cd /Users/lotu/game/Garden/server
make setup  # if not already done
mix phx.server
```

**Step 2: Test endpoints with curl**

```bash
# Health check
curl http://localhost:4000/health

# Register
curl -X POST http://localhost:4000/auth/register

# Use the returned authToken for subsequent requests
TOKEN="<paste authToken here>"

# Update display name
curl -X PUT http://localhost:4000/auth/display-name \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"displayName": "TestPlayer"}'

# Get tonight's visitor
curl http://localhost:4000/visitors/tonight \
  -H "Authorization: Bearer $TOKEN"
```

**Step 3: Verify all responses match the Node.js server's JSON format**

Check that:
- `/auth/register` returns `{uid, authToken, friendCode, displayName}` (camelCase)
- `/health` returns `{status: "ok"}`
- `/visitors/tonight` returns visitor payload with `visitor_type`, `visitor_id`, `name`, etc. (snake_case, matching existing)

**Step 4: Commit any fixes, then final commit**

```bash
git add -A
git commit -m "feat(server): complete Elixir backend migration"
```

---

### Task 15: Clean Up

**Step 1: Remove old Node.js references from .gitignore if any**

Check if there's a `.gitignore` entry for `node_modules/` that should be updated.

**Step 2: Update CLAUDE.md**

Update the Social / Backend section to reference Elixir/Phoenix instead of Node.js/Express. Mention `mix phx.server`, port 4000, `mix test`.

**Step 3: Update backend-migration-todo.md**

Add a note at the top that the Elixir migration is complete. The existing "Already Server-Driven" items remain the same.

**Step 4: Commit**

```bash
git add CLAUDE.md docs/
git commit -m "docs: update project docs for Elixir backend"
```
