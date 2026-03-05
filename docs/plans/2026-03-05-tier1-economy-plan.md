# Tier 1: Server-Authoritative Economy — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the server the source of truth for mana, gems, flame level, seeds, and items — with an optimistic client model and offline queue.

**Architecture:** Action-based API under `/economy`. Server stores current balances in 3 new tables. Client applies changes optimistically, sends to server in background, rolls back on rejection. Offline actions queue and replay on reconnect.

**Tech Stack:** Elixir/Phoenix (server), Unity C# (client), Ecto/Postgres (DB), NUnit (client tests), ExUnit (server tests)

**Design doc:** `docs/plans/2026-03-05-tier1-economy-design.md`

---

### Task 1: Database Migrations

**Files:**
- Create: `server/priv/repo/migrations/TIMESTAMP_create_economy_tables.exs`

**Step 1: Write the migration**

```elixir
defmodule CampFire.Repo.Migrations.CreateEconomyTables do
  use Ecto.Migration

  def change do
    create table(:player_economies, primary_key: false) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false, primary_key: true
      add :mana, :float, null: false, default: 50.0
      add :gems, :integer, null: false, default: 5
      add :flame_level, :integer, null: false, default: 1
      add :last_mana_collect_utc, :utc_datetime, null: false, default: fragment("now()")
      timestamps(type: :utc_datetime)
    end

    create table(:player_seeds) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :seed_name, :text, null: false
      add :count, :integer, null: false
    end

    create unique_index(:player_seeds, [:player_uid, :seed_name])

    create table(:player_items) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :item_name, :text, null: false
      add :count, :integer, null: false
    end

    create unique_index(:player_items, [:player_uid, :item_name])
  end
end
```

**Step 2: Run the migration**

Run: `cd server && mix ecto.migrate`
Expected: Migration runs successfully, 3 tables created.

**Step 3: Commit**

```bash
git add server/priv/repo/migrations/*_create_economy_tables.exs
git commit -m "feat(server): add economy tables migration"
```

---

### Task 2: Ecto Schemas

**Files:**
- Create: `server/lib/camp_fire/economy/player_economy.ex`
- Create: `server/lib/camp_fire/economy/player_seed.ex`
- Create: `server/lib/camp_fire/economy/player_item.ex`

**Step 1: Create PlayerEconomy schema**

```elixir
defmodule CampFire.Economy.PlayerEconomy do
  use Ecto.Schema
  import Ecto.Changeset

  @primary_key false
  schema "player_economies" do
    field :player_uid, :string, primary_key: true
    field :mana, :float, default: 50.0
    field :gems, :integer, default: 5
    field :flame_level, :integer, default: 1
    field :last_mana_collect_utc, :utc_datetime
    timestamps(type: :utc_datetime)
  end

  def changeset(economy, attrs) do
    economy
    |> cast(attrs, [:player_uid, :mana, :gems, :flame_level, :last_mana_collect_utc])
    |> validate_required([:player_uid])
    |> validate_number(:mana, greater_than_or_equal_to: 0)
    |> validate_number(:gems, greater_than_or_equal_to: 0)
    |> validate_number(:flame_level, greater_than: 0)
    |> unique_constraint(:player_uid, name: :player_economies_pkey)
  end
end
```

**Step 2: Create PlayerSeed schema**

```elixir
defmodule CampFire.Economy.PlayerSeed do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_seeds" do
    field :player_uid, :string
    field :seed_name, :string
    field :count, :integer
  end

  def changeset(seed, attrs) do
    seed
    |> cast(attrs, [:player_uid, :seed_name, :count])
    |> validate_required([:player_uid, :seed_name, :count])
    |> validate_number(:count, greater_than: 0)
    |> unique_constraint([:player_uid, :seed_name])
  end
end
```

**Step 3: Create PlayerItem schema**

```elixir
defmodule CampFire.Economy.PlayerItem do
  use Ecto.Schema
  import Ecto.Changeset

  schema "player_items" do
    field :player_uid, :string
    field :item_name, :string
    field :count, :integer
  end

  def changeset(item, attrs) do
    item
    |> cast(attrs, [:player_uid, :item_name, :count])
    |> validate_required([:player_uid, :item_name, :count])
    |> validate_number(:count, greater_than: 0)
    |> unique_constraint([:player_uid, :item_name])
  end
end
```

**Step 4: Verify compilation**

Run: `cd server && mix compile`
Expected: Compiles without errors.

**Step 5: Commit**

```bash
git add server/lib/camp_fire/economy/
git commit -m "feat(server): add Economy schemas"
```

---

### Task 3: Economy Context — Core Operations

**Files:**
- Create: `server/lib/camp_fire/economy.ex`

This is the Phoenix context module with all economy business logic.

**Step 1: Write the Economy context**

```elixir
defmodule CampFire.Economy do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Economy.{PlayerEconomy, PlayerSeed, PlayerItem}

  # --- Mana config (matches FlameConfig.asset) ---
  @base_mana_per_second 0.5
  @mana_per_level 0.3
  @max_flame_level 12

  # --- Init ---

  def get_economy(player_uid) do
    Repo.get(PlayerEconomy, player_uid)
  end

  def init_economy(player_uid) do
    now = DateTime.utc_now() |> DateTime.truncate(:second)

    %PlayerEconomy{}
    |> PlayerEconomy.changeset(%{
      player_uid: player_uid,
      mana: 50.0,
      gems: 5,
      flame_level: 1,
      last_mana_collect_utc: now
    })
    |> Repo.insert()
    |> case do
      {:ok, economy} ->
        # Add starting seeds
        upsert_seed(player_uid, "Sprouts", 5)
        upsert_seed(player_uid, "Cress", 3)
        # Add starting items
        upsert_item(player_uid, "Speed_Potion", 3)
        {:ok, economy}

      {:error, changeset} ->
        {:error, changeset}
    end
  end

  # --- State ---

  def get_full_state(player_uid) do
    economy = Repo.get(PlayerEconomy, player_uid)
    seeds = list_seeds(player_uid)
    items = list_items(player_uid)
    {economy, seeds, items}
  end

  # --- Mana ---

  def collect_mana(player_uid) do
    economy = Repo.get!(PlayerEconomy, player_uid)
    now = DateTime.utc_now() |> DateTime.truncate(:second)
    elapsed = DateTime.diff(now, economy.last_mana_collect_utc, :second)
    elapsed = max(elapsed, 0)

    mana_rate = @base_mana_per_second + (economy.flame_level - 1) * @mana_per_level
    earned = mana_rate * elapsed

    economy
    |> PlayerEconomy.changeset(%{
      mana: economy.mana + earned,
      last_mana_collect_utc: now
    })
    |> Repo.update()
  end

  def spend_mana(player_uid, amount) when is_number(amount) and amount > 0 do
    economy = Repo.get!(PlayerEconomy, player_uid)

    if economy.mana < amount do
      {:error, :insufficient_mana}
    else
      economy
      |> PlayerEconomy.changeset(%{mana: economy.mana - amount})
      |> Repo.update()
    end
  end

  # --- Gems ---

  def add_gems(player_uid, amount) when is_integer(amount) and amount > 0 do
    economy = Repo.get!(PlayerEconomy, player_uid)

    economy
    |> PlayerEconomy.changeset(%{gems: economy.gems + amount})
    |> Repo.update()
  end

  def spend_gems(player_uid, amount) when is_integer(amount) and amount > 0 do
    economy = Repo.get!(PlayerEconomy, player_uid)

    if economy.gems < amount do
      {:error, :insufficient_gems}
    else
      economy
      |> PlayerEconomy.changeset(%{gems: economy.gems - amount})
      |> Repo.update()
    end
  end

  # --- Flame ---

  def upgrade_flame(player_uid, required_items) when is_list(required_items) do
    Repo.transaction(fn ->
      economy = Repo.get!(PlayerEconomy, player_uid)

      if economy.flame_level >= @max_flame_level do
        Repo.rollback(:max_level)
      end

      # Verify and consume all required items
      Enum.each(required_items, fn %{"item_name" => name, "count" => count} ->
        case spend_items_in_tx(player_uid, name, count) do
          :ok -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end
      end)

      # Increment flame level, reset mana collect time
      now = DateTime.utc_now() |> DateTime.truncate(:second)

      economy
      |> PlayerEconomy.changeset(%{
        flame_level: economy.flame_level + 1,
        last_mana_collect_utc: now
      })
      |> Repo.update!()
    end)
  end

  # --- Seeds ---

  def list_seeds(player_uid) do
    from(s in PlayerSeed, where: s.player_uid == ^player_uid)
    |> Repo.all()
  end

  def upsert_seed(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from s in PlayerSeed, where: s.player_uid == ^player_uid and s.seed_name == ^seed_name) do
      nil ->
        %PlayerSeed{}
        |> PlayerSeed.changeset(%{player_uid: player_uid, seed_name: seed_name, count: count})
        |> Repo.insert()

      existing ->
        existing
        |> PlayerSeed.changeset(%{count: existing.count + count})
        |> Repo.update()
    end
  end

  def spend_seed(player_uid, seed_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from s in PlayerSeed, where: s.player_uid == ^player_uid and s.seed_name == ^seed_name) do
      nil ->
        {:error, :insufficient_seeds}

      existing when existing.count < count ->
        {:error, :insufficient_seeds}

      existing when existing.count == count ->
        Repo.delete(existing)
        {:ok, :deleted}

      existing ->
        existing
        |> PlayerSeed.changeset(%{count: existing.count - count})
        |> Repo.update()
    end
  end

  # --- Items ---

  def list_items(player_uid) do
    from(i in PlayerItem, where: i.player_uid == ^player_uid)
    |> Repo.all()
  end

  def upsert_item(player_uid, item_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from i in PlayerItem, where: i.player_uid == ^player_uid and i.item_name == ^item_name) do
      nil ->
        %PlayerItem{}
        |> PlayerItem.changeset(%{player_uid: player_uid, item_name: item_name, count: count})
        |> Repo.insert()

      existing ->
        existing
        |> PlayerItem.changeset(%{count: existing.count + count})
        |> Repo.update()
    end
  end

  def spend_item(player_uid, item_name, count) when is_integer(count) and count > 0 do
    case Repo.one(from i in PlayerItem, where: i.player_uid == ^player_uid and i.item_name == ^item_name) do
      nil ->
        {:error, :insufficient_items}

      existing when existing.count < count ->
        {:error, :insufficient_items}

      existing when existing.count == count ->
        Repo.delete(existing)
        {:ok, :deleted}

      existing ->
        existing
        |> PlayerItem.changeset(%{count: existing.count - count})
        |> Repo.update()
    end
  end

  def spend_items(player_uid, items) when is_list(items) do
    Repo.transaction(fn ->
      Enum.each(items, fn %{"item_name" => name, "count" => count} ->
        case spend_items_in_tx(player_uid, name, count) do
          :ok -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end
      end)
    end)
  end

  # --- Private ---

  defp spend_items_in_tx(player_uid, item_name, count) do
    case Repo.one(from i in PlayerItem, where: i.player_uid == ^player_uid and i.item_name == ^item_name) do
      nil ->
        {:error, {:insufficient_items, item_name}}

      existing when existing.count < count ->
        {:error, {:insufficient_items, item_name}}

      existing when existing.count == count ->
        Repo.delete!(existing)
        :ok

      existing ->
        existing
        |> PlayerItem.changeset(%{count: existing.count - count})
        |> Repo.update!()
        :ok
    end
  end
end
```

**Step 2: Verify compilation**

Run: `cd server && mix compile`
Expected: Compiles without errors.

**Step 3: Commit**

```bash
git add server/lib/camp_fire/economy.ex
git commit -m "feat(server): add Economy context with all operations"
```

---

### Task 4: Economy Controller & Router

**Files:**
- Create: `server/lib/camp_fire_web/controllers/economy_controller.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

**Step 1: Create the EconomyController**

```elixir
defmodule CampFireWeb.EconomyController do
  use CampFireWeb, :controller
  alias CampFire.Economy

  # GET /economy/state
  def state(conn, _params) do
    player_uid = conn.assigns.current_player.uid

    case Economy.get_economy(player_uid) do
      nil ->
        conn |> put_status(404) |> json(%{error: "No economy record. Call POST /economy/init first."})

      economy ->
        {_economy, seeds, items} = Economy.get_full_state(player_uid)
        conn |> put_status(200) |> json(format_state(economy, seeds, items))
    end
  end

  # POST /economy/init
  def init(conn, _params) do
    player_uid = conn.assigns.current_player.uid

    if Economy.get_economy(player_uid) do
      conn |> put_status(409) |> json(%{error: "Economy already initialized"})
    else
      case Economy.init_economy(player_uid) do
        {:ok, economy} ->
          {_economy, seeds, items} = Economy.get_full_state(player_uid)
          conn |> put_status(201) |> json(format_state(economy, seeds, items))

        {:error, _changeset} ->
          conn |> put_status(422) |> json(%{error: "Failed to initialize economy"})
      end
    end
  end

  # POST /economy/collect-mana
  def collect_mana(conn, _params) do
    player_uid = conn.assigns.current_player.uid

    case Economy.collect_mana(player_uid) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{mana: economy.mana})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to collect mana"})
    end
  end

  # POST /economy/spend-mana
  def spend_mana(conn, %{"amount" => amount}) when is_number(amount) do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_mana(player_uid, amount) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{mana: economy.mana})

      {:error, :insufficient_mana} ->
        conn |> put_status(422) |> json(%{error: "Insufficient mana"})
    end
  end

  def spend_mana(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'amount'"})
  end

  # POST /economy/spend-gems
  def spend_gems(conn, %{"amount" => amount}) when is_integer(amount) do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_gems(player_uid, amount) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{gems: economy.gems})

      {:error, :insufficient_gems} ->
        conn |> put_status(422) |> json(%{error: "Insufficient gems"})
    end
  end

  def spend_gems(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'amount'"})
  end

  # POST /economy/add-gems
  def add_gems(conn, %{"amount" => amount}) when is_integer(amount) and amount > 0 do
    player_uid = conn.assigns.current_player.uid

    case Economy.add_gems(player_uid, amount) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{gems: economy.gems})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to add gems"})
    end
  end

  def add_gems(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing or invalid 'amount'"})
  end

  # POST /economy/upgrade-flame
  def upgrade_flame(conn, %{"items" => items}) when is_list(items) do
    player_uid = conn.assigns.current_player.uid

    case Economy.upgrade_flame(player_uid, items) do
      {:ok, economy} ->
        conn |> put_status(200) |> json(%{flameLevel: economy.flame_level})

      {:error, :max_level} ->
        conn |> put_status(422) |> json(%{error: "Already at max flame level"})

      {:error, {:insufficient_items, name}} ->
        conn |> put_status(422) |> json(%{error: "Insufficient items: #{name}"})
    end
  end

  def upgrade_flame(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'items' array"})
  end

  # POST /economy/add-seeds
  def add_seeds(conn, %{"seed_name" => name, "count" => count})
      when is_binary(name) and is_integer(count) and count > 0 do
    player_uid = conn.assigns.current_player.uid

    case Economy.upsert_seed(player_uid, name, count) do
      {:ok, _} ->
        seeds = Economy.list_seeds(player_uid)
        conn |> put_status(200) |> json(%{seeds: format_seeds(seeds)})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to add seeds"})
    end
  end

  def add_seeds(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'seed_name' (string) and 'count' (positive integer)"})
  end

  # POST /economy/spend-seeds
  def spend_seeds(conn, %{"seed_name" => name, "count" => count})
      when is_binary(name) and is_integer(count) and count > 0 do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_seed(player_uid, name, count) do
      {:ok, _} ->
        seeds = Economy.list_seeds(player_uid)
        conn |> put_status(200) |> json(%{seeds: format_seeds(seeds)})

      {:error, :insufficient_seeds} ->
        conn |> put_status(422) |> json(%{error: "Insufficient seeds: #{name}"})
    end
  end

  def spend_seeds(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'seed_name' (string) and 'count' (positive integer)"})
  end

  # POST /economy/add-items
  def add_items(conn, %{"item_name" => name, "count" => count})
      when is_binary(name) and is_integer(count) and count > 0 do
    player_uid = conn.assigns.current_player.uid

    case Economy.upsert_item(player_uid, name, count) do
      {:ok, _} ->
        items = Economy.list_items(player_uid)
        conn |> put_status(200) |> json(%{items: format_items(items)})

      {:error, _} ->
        conn |> put_status(422) |> json(%{error: "Failed to add items"})
    end
  end

  def add_items(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'item_name' (string) and 'count' (positive integer)"})
  end

  # POST /economy/spend-items
  def spend_items(conn, %{"items" => items}) when is_list(items) do
    player_uid = conn.assigns.current_player.uid

    case Economy.spend_items(player_uid, items) do
      {:ok, _} ->
        all_items = Economy.list_items(player_uid)
        conn |> put_status(200) |> json(%{items: format_items(all_items)})

      {:error, {:insufficient_items, name}} ->
        conn |> put_status(422) |> json(%{error: "Insufficient items: #{name}"})
    end
  end

  def spend_items(conn, _params) do
    conn |> put_status(400) |> json(%{error: "Missing 'items' array"})
  end

  # --- Private ---

  defp format_state(economy, seeds, items) do
    %{
      mana: economy.mana,
      gems: economy.gems,
      flameLevel: economy.flame_level,
      lastManaCollectUtc: DateTime.to_iso8601(economy.last_mana_collect_utc),
      seeds: format_seeds(seeds),
      items: format_items(items)
    }
  end

  defp format_seeds(seeds) do
    Enum.map(seeds, fn s -> %{seedName: s.seed_name, count: s.count} end)
  end

  defp format_items(items) do
    Enum.map(items, fn i -> %{itemName: i.item_name, count: i.count} end)
  end
end
```

**Step 2: Add routes to router**

Add this scope block to `server/lib/camp_fire_web/router.ex` after the `/visitors` scope:

```elixir
  scope "/economy", CampFireWeb do
    pipe_through [:api, :authenticated]
    get "/state", EconomyController, :state
    post "/init", EconomyController, :init
    post "/collect-mana", EconomyController, :collect_mana
    post "/spend-mana", EconomyController, :spend_mana
    post "/spend-gems", EconomyController, :spend_gems
    post "/add-gems", EconomyController, :add_gems
    post "/upgrade-flame", EconomyController, :upgrade_flame
    post "/add-seeds", EconomyController, :add_seeds
    post "/spend-seeds", EconomyController, :spend_seeds
    post "/add-items", EconomyController, :add_items
    post "/spend-items", EconomyController, :spend_items
  end
```

**Step 3: Verify compilation**

Run: `cd server && mix compile`
Expected: Compiles without errors.

**Step 4: Commit**

```bash
git add server/lib/camp_fire_web/controllers/economy_controller.ex server/lib/camp_fire_web/router.ex
git commit -m "feat(server): add Economy controller and routes"
```

---

### Task 5: Server Tests — Economy Context

**Files:**
- Create: `server/test/camp_fire/economy_test.exs`

**Step 1: Write Economy context tests**

```elixir
defmodule CampFire.EconomyTest do
  use CampFire.DataCase
  import CampFire.TestHelpers
  alias CampFire.Economy

  describe "init_economy/1" do
    test "creates economy with default values" do
      player = register_player()
      {:ok, economy} = Economy.init_economy(player.uid)

      assert economy.mana == 50.0
      assert economy.gems == 5
      assert economy.flame_level == 1
      assert economy.last_mana_collect_utc
    end

    test "creates starting seeds and items" do
      player = register_player()
      {:ok, _economy} = Economy.init_economy(player.uid)

      seeds = Economy.list_seeds(player.uid)
      items = Economy.list_items(player.uid)

      sprouts = Enum.find(seeds, &(&1.seed_name == "Sprouts"))
      cress = Enum.find(seeds, &(&1.seed_name == "Cress"))
      potion = Enum.find(items, &(&1.item_name == "Speed_Potion"))

      assert sprouts.count == 5
      assert cress.count == 3
      assert potion.count == 3
    end

    test "rejects duplicate init" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:error, _} = Economy.init_economy(player.uid)
    end
  end

  describe "collect_mana/1" do
    test "accumulates mana based on flame level and time" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      # Manually set last_mana_collect_utc to 10 seconds ago
      economy = Economy.get_economy(player.uid)
      ten_seconds_ago = DateTime.add(DateTime.utc_now(), -10, :second) |> DateTime.truncate(:second)

      economy
      |> Ecto.Changeset.change(last_mana_collect_utc: ten_seconds_ago)
      |> CampFire.Repo.update!()

      {:ok, updated} = Economy.collect_mana(player.uid)

      # Level 1: 0.5 mana/sec * 10 sec = ~5.0 mana earned
      # Starting mana is 50.0, so should be ~55.0
      assert updated.mana >= 54.0 and updated.mana <= 56.0
    end
  end

  describe "spend_mana/2" do
    test "deducts mana when sufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      {:ok, economy} = Economy.spend_mana(player.uid, 20.0)
      assert economy.mana == 30.0
    end

    test "rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_mana} = Economy.spend_mana(player.uid, 999.0)
    end
  end

  describe "gems" do
    test "add and spend gems" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      {:ok, economy} = Economy.add_gems(player.uid, 10)
      assert economy.gems == 15

      {:ok, economy} = Economy.spend_gems(player.uid, 5)
      assert economy.gems == 10
    end

    test "rejects overspend" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_gems} = Economy.spend_gems(player.uid, 100)
    end
  end

  describe "seeds" do
    test "upsert adds to existing" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      # Already has 5 Sprouts from init
      {:ok, seed} = Economy.upsert_seed(player.uid, "Sprouts", 3)
      assert seed.count == 8
    end

    test "spend reduces count" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      {:ok, _} = Economy.spend_seed(player.uid, "Sprouts", 2)
      seeds = Economy.list_seeds(player.uid)
      sprouts = Enum.find(seeds, &(&1.seed_name == "Sprouts"))
      assert sprouts.count == 3
    end

    test "spend deletes row when count reaches zero" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      {:ok, :deleted} = Economy.spend_seed(player.uid, "Sprouts", 5)
      seeds = Economy.list_seeds(player.uid)
      assert Enum.find(seeds, &(&1.seed_name == "Sprouts")) == nil
    end

    test "spend rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_seeds} = Economy.spend_seed(player.uid, "Sprouts", 99)
    end
  end

  describe "items" do
    test "upsert adds to existing" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      {:ok, item} = Economy.upsert_item(player.uid, "Speed_Potion", 2)
      assert item.count == 5
    end

    test "spend reduces count" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      {:ok, _} = Economy.spend_item(player.uid, "Speed_Potion", 1)
      items = Economy.list_items(player.uid)
      potion = Enum.find(items, &(&1.item_name == "Speed_Potion"))
      assert potion.count == 2
    end

    test "spend rejects when insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)
      assert {:error, :insufficient_items} = Economy.spend_item(player.uid, "Speed_Potion", 99)
    end
  end

  describe "upgrade_flame/2" do
    test "consumes items and increments level" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      # Add required items for level 1→2 upgrade: 1 Sprouts_harvest
      {:ok, _} = Economy.upsert_item(player.uid, "Sprouts_harvest", 5)

      {:ok, economy} = Economy.upgrade_flame(player.uid, [
        %{"item_name" => "Sprouts_harvest", "count" => 1}
      ])

      assert economy.flame_level == 2

      # Verify items consumed
      items = Economy.list_items(player.uid)
      sprouts = Enum.find(items, &(&1.item_name == "Sprouts_harvest"))
      assert sprouts.count == 4
    end

    test "rejects when items insufficient" do
      player = register_player()
      {:ok, _} = Economy.init_economy(player.uid)

      assert {:error, {:insufficient_items, "Sprouts_harvest"}} =
               Economy.upgrade_flame(player.uid, [
                 %{"item_name" => "Sprouts_harvest", "count" => 1}
               ])
    end
  end
end
```

**Step 2: Run tests**

Run: `cd server && mix test test/camp_fire/economy_test.exs`
Expected: All tests pass.

**Step 3: Commit**

```bash
git add server/test/camp_fire/economy_test.exs
git commit -m "test(server): add Economy context tests"
```

---

### Task 6: Server Tests — Economy Controller

**Files:**
- Create: `server/test/camp_fire_web/controllers/economy_controller_test.exs`

**Step 1: Write controller tests**

```elixir
defmodule CampFireWeb.EconomyControllerTest do
  use CampFireWeb.ConnCase
  import CampFire.TestHelpers
  alias CampFire.Economy

  describe "GET /economy/state" do
    test "returns 404 when not initialized", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> get("/economy/state")

      assert json_response(conn, 404)["error"]
    end

    test "returns full state when initialized", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> get("/economy/state")

      body = json_response(conn, 200)
      assert body["mana"] == 50.0
      assert body["gems"] == 5
      assert body["flameLevel"] == 1
      assert is_list(body["seeds"])
      assert is_list(body["items"])
    end
  end

  describe "POST /economy/init" do
    test "initializes economy with defaults", %{conn: conn} do
      player = register_player()

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/init")

      body = json_response(conn, 201)
      assert body["mana"] == 50.0
      assert body["gems"] == 5
      assert body["flameLevel"] == 1
      assert length(body["seeds"]) == 2
      assert length(body["items"]) == 1
    end

    test "rejects double init", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/init")

      assert json_response(conn, 409)["error"]
    end
  end

  describe "POST /economy/spend-mana" do
    test "deducts mana", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/spend-mana", %{amount: 20.0})

      body = json_response(conn, 200)
      assert body["mana"] == 30.0
    end

    test "rejects when insufficient", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/spend-mana", %{amount: 999.0})

      assert json_response(conn, 422)["error"] =~ "Insufficient"
    end
  end

  describe "POST /economy/add-seeds and spend-seeds" do
    test "adds and spends seeds", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)

      conn1 =
        conn
        |> authed_conn(player)
        |> post("/economy/add-seeds", %{seed_name: "Basil", count: 5})

      body = json_response(conn1, 200)
      basil = Enum.find(body["seeds"], &(&1["seedName"] == "Basil"))
      assert basil["count"] == 5

      conn2 =
        build_conn()
        |> authed_conn(player)
        |> post("/economy/spend-seeds", %{seed_name: "Basil", count: 2})

      body = json_response(conn2, 200)
      basil = Enum.find(body["seeds"], &(&1["seedName"] == "Basil"))
      assert basil["count"] == 3
    end
  end

  describe "POST /economy/upgrade-flame" do
    test "upgrades flame level", %{conn: conn} do
      player = register_player()
      Economy.init_economy(player.uid)
      Economy.upsert_item(player.uid, "Sprouts_harvest", 5)

      conn =
        conn
        |> authed_conn(player)
        |> post("/economy/upgrade-flame", %{items: [%{item_name: "Sprouts_harvest", count: 1}]})

      body = json_response(conn, 200)
      assert body["flameLevel"] == 2
    end
  end

  describe "auth required" do
    test "rejects unauthenticated requests", %{conn: conn} do
      conn = get(conn, "/economy/state")
      assert json_response(conn, 401)
    end
  end
end
```

**Step 2: Run all server tests**

Run: `cd server && mix test`
Expected: All tests pass (existing + new economy tests).

**Step 3: Commit**

```bash
git add server/test/camp_fire_web/controllers/economy_controller_test.exs
git commit -m "test(server): add Economy controller tests"
```

---

### Task 7: Unity Client — EconomyService

**Files:**
- Create: `Assets/Scripts/Services/EconomyService.cs`

This singleton manages the action queue, server sync, and startup state loading.

**Step 1: Create EconomyService**

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    [Serializable]
    public class EconomyAction
    {
        public string type;       // e.g. "spend-mana", "add-seeds"
        public string jsonBody;   // serialized request body
    }

    [Serializable]
    public class EconomyQueue
    {
        public List<EconomyAction> actions = new();
    }

    [Serializable]
    public class EconomyState
    {
        public float mana;
        public int gems;
        public int flameLevel;
        public string lastManaCollectUtc;
        public List<SeedInventoryEntry> seeds;
        public List<InventoryItem> items;
    }

    public class EconomyService : MonoBehaviour
    {
        public static EconomyService Instance { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsOnline { get; private set; }

        public event Action OnStateSynced;

        private EconomyQueue _queue = new();
        private string _queuePath;
        private bool _isSyncing;

        private static string ServerBaseUrl =>
#if UNITY_EDITOR
            "http://localhost:4000";
#else
            DevServerConfig.BaseUrl;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _queuePath = System.IO.Path.Combine(Application.persistentDataPath, "economy_queue.json");
            LoadQueue();
        }

        /// <summary>
        /// Called after SocialService signs in. Syncs economy state from server.
        /// </summary>
        public async void Initialize()
        {
            if (!SocialService.Instance.IsSignedIn) return;

            try
            {
                // Try to get existing state
                using var getReq = GetAuth("/economy/state");
                await SendAsync(getReq);

                if (getReq.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<EconomyState>(getReq.downloadHandler.text);
                    ApplyServerState(state);
                    IsInitialized = true;
                    IsOnline = true;
                    OnStateSynced?.Invoke();
                    // Replay any queued offline actions
                    await DrainQueue();
                    return;
                }

                if (getReq.responseCode == 404)
                {
                    // New player — init on server
                    using var initReq = PostJson("/economy/init", "{}");
                    await SendAsync(initReq);

                    if (initReq.responseCode == 201)
                    {
                        var state = JsonUtility.FromJson<EconomyState>(initReq.downloadHandler.text);
                        ApplyServerState(state);
                        IsInitialized = true;
                        IsOnline = true;
                        OnStateSynced?.Invoke();
                        ClearQueue();
                        return;
                    }
                }

                Debug.LogWarning("EconomyService: Could not sync with server, running offline.");
                IsInitialized = true;
                IsOnline = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: Init failed ({e.Message}), running offline.");
                IsInitialized = true;
                IsOnline = false;
            }
        }

        /// <summary>
        /// Enqueue an action for server sync. Called by managers after local application.
        /// </summary>
        public void Enqueue(string actionType, string jsonBody)
        {
            _queue.actions.Add(new EconomyAction { type = actionType, jsonBody = jsonBody });
            SaveQueue();

            if (IsOnline && !_isSyncing)
                _ = DrainQueue();
        }

        /// <summary>
        /// Send collect-mana to server and update local mana from response.
        /// </summary>
        public async Task CollectMana()
        {
            if (!IsOnline) return;

            try
            {
                using var req = PostJson("/economy/collect-mana", "{}");
                await SendAsync(req);

                if (req.responseCode == 200)
                {
                    var resp = JsonUtility.FromJson<ManaResponse>(req.downloadHandler.text);
                    SaveManager.Instance.Data.mana = resp.mana;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: CollectMana failed: {e.Message}");
            }
        }

        /// <summary>
        /// Full state sync from server. Call when a server action is rejected.
        /// </summary>
        public async Task SyncFromServer()
        {
            try
            {
                using var req = GetAuth("/economy/state");
                await SendAsync(req);

                if (req.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<EconomyState>(req.downloadHandler.text);
                    ApplyServerState(state);
                    OnStateSynced?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: SyncFromServer failed: {e.Message}");
            }
        }

        // --- Private ---

        private async Task DrainQueue()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                while (_queue.actions.Count > 0)
                {
                    var action = _queue.actions[0];
                    bool success = await SendAction(action);

                    if (success)
                    {
                        _queue.actions.RemoveAt(0);
                        SaveQueue();
                    }
                    else
                    {
                        // Action rejected — clear queue and full sync
                        Debug.LogWarning($"EconomyService: Action {action.type} rejected, syncing full state.");
                        ClearQueue();
                        await SyncFromServer();
                        break;
                    }
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task<bool> SendAction(EconomyAction action)
        {
            try
            {
                using var req = PostJson($"/economy/{action.type}", action.jsonBody);
                await SendAsync(req);
                return req.responseCode >= 200 && req.responseCode < 300;
            }
            catch
            {
                IsOnline = false;
                return false;
            }
        }

        private void ApplyServerState(EconomyState state)
        {
            var data = SaveManager.Instance.Data;
            data.mana = state.mana;
            data.gems = state.gems;
            data.flameLevel = state.flameLevel;

            // Replace seed inventory
            data.seedInventory.Clear();
            if (state.seeds != null)
            {
                foreach (var s in state.seeds)
                    data.seedInventory.Add(new SeedInventoryEntry { seedName = s.seedName, count = s.count });
            }

            // Replace item inventory
            data.items.Clear();
            if (state.items != null)
            {
                foreach (var i in state.items)
                    data.items.Add(new InventoryItem { itemName = i.itemName, count = i.count });
            }

            SaveManager.Instance.Save();
        }

        private void LoadQueue()
        {
            try
            {
                if (System.IO.File.Exists(_queuePath))
                {
                    var json = System.IO.File.ReadAllText(_queuePath);
                    _queue = JsonUtility.FromJson<EconomyQueue>(json) ?? new EconomyQueue();
                }
            }
            catch { _queue = new EconomyQueue(); }
        }

        private void SaveQueue()
        {
            try
            {
                System.IO.File.WriteAllText(_queuePath, JsonUtility.ToJson(_queue));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: Failed to save queue: {e.Message}");
            }
        }

        private void ClearQueue()
        {
            _queue.actions.Clear();
            SaveQueue();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && IsOnline)
                _ = CollectMana();
        }

        // --- HTTP helpers (same pattern as SocialService) ---

        private UnityWebRequest GetAuth(string path)
        {
            var request = UnityWebRequest.Get(ServerBaseUrl + path);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PostJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            return request;
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            var token = SocialSaveManager.Instance?.Data?.authToken;
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        private static Task<UnityWebRequest> SendAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(request);
            return tcs.Task;
        }

        [Serializable]
        private class ManaResponse { public float mana; }
    }
}
```

**Step 2: Verify compilation in Unity**

Run Unity tests or check console for compilation errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/EconomyService.cs Assets/Scripts/Services/EconomyService.cs.meta
git commit -m "feat: add EconomyService for server economy sync"
```

---

### Task 8: Wire EconomyService into Managers

**Files:**
- Modify: `Assets/Scripts/Services/CurrencyManager.cs`
- Modify: `Assets/Scripts/Managers/FlameManager.cs`
- Modify: `Assets/Scripts/Managers/ApothekeManager.cs`
- Modify: `Assets/Scripts/Managers/GameManager.cs`

This task wires the existing managers to enqueue economy actions when they mutate state.

**Step 1: Wire CurrencyManager to enqueue spend/add actions**

After each successful local mutation, call `EconomyService.Instance?.Enqueue(...)`.

In `CurrencyManager.AddMana()` — after the save, enqueue if the amount is negative (a spend):
```csharp
// At the end of AddMana, after SaveManager.Instance.Save():
if (amount < 0 && EconomyService.Instance != null)
{
    EconomyService.Instance.Enqueue("spend-mana",
        JsonUtility.ToJson(new SpendManaRequest { amount = -amount }));
}
```

Add request classes at the bottom of `CurrencyManager.cs`:
```csharp
[Serializable] public class SpendManaRequest { public float amount; }
[Serializable] public class SpendGemsRequest { public int amount; }
[Serializable] public class AddGemsRequest { public int amount; }
```

In `SpendGems()` — after `AddGems(-amount)`:
```csharp
if (EconomyService.Instance != null)
    EconomyService.Instance.Enqueue("spend-gems",
        JsonUtility.ToJson(new SpendGemsRequest { amount = amount }));
```

In `AddGems()` — when amount is positive:
```csharp
if (amount > 0 && EconomyService.Instance != null)
    EconomyService.Instance.Enqueue("add-gems",
        JsonUtility.ToJson(new AddGemsRequest { amount = amount }));
```

**Step 2: Wire FlameManager to collect mana and enqueue upgrades**

In `FlameManager.UpgradeFlame()` (or wherever the upgrade is triggered), enqueue the upgrade action with the items list.

**Step 3: Wire ApothekeManager to enqueue seed operations**

In `ApothekeManager.AddSeed()`:
```csharp
if (EconomyService.Instance != null)
    EconomyService.Instance.Enqueue("add-seeds",
        JsonUtility.ToJson(new AddSeedRequest { seed_name = seedName, count = count }));
```

Add request classes:
```csharp
[Serializable] public class AddSeedRequest { public string seed_name; public int count; }
[Serializable] public class SpendSeedRequest { public string seed_name; public int count; }
```

When seeds are consumed (e.g., in `PlotManager.Plant()`), enqueue `spend-seeds`.

**Step 4: Wire GameManager to call EconomyService.Initialize**

In `GameManager.Start()` or after `SocialService.OnSignedIn`:
```csharp
SocialService.Instance.OnSignedIn += () => EconomyService.Instance?.Initialize();
```

**Step 5: Verify compilation**

Run: Check Unity console for compilation errors.

**Step 6: Commit**

```bash
git add Assets/Scripts/Services/CurrencyManager.cs Assets/Scripts/Managers/FlameManager.cs Assets/Scripts/Managers/ApothekeManager.cs Assets/Scripts/Managers/GameManager.cs
git commit -m "feat: wire managers to enqueue economy actions"
```

---

### Task 9: Periodic Mana Collection

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs`

**Step 1: Add periodic mana collection**

FlameManager already accumulates mana locally every frame. Add a periodic server sync:

```csharp
private float _manaCollectTimer;
private const float ManaCollectIntervalSeconds = 60f;

private void Update()
{
    // Existing local accumulation
    SaveManager.Instance.Data.mana = AccumulateMana(
        SaveManager.Instance.Data.mana, ManaPerSecond, Time.deltaTime);

    // Periodic server collection
    _manaCollectTimer += Time.deltaTime;
    if (_manaCollectTimer >= ManaCollectIntervalSeconds)
    {
        _manaCollectTimer = 0f;
        _ = EconomyService.Instance?.CollectMana();
    }
}
```

Also collect mana before any spend operation. In `CurrencyManager.SpendMana()`, before the local check:
```csharp
// Collect server mana before spending
_ = EconomyService.Instance?.CollectMana();
```

**Step 2: Verify compilation**

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/FlameManager.cs Assets/Scripts/Services/CurrencyManager.cs
git commit -m "feat: add periodic mana collection from server"
```

---

### Task 10: Unity EditMode Tests

**Files:**
- Create: `Assets/Tests/EditMode/TestEconomyService.cs`

**Step 1: Write tests for queue persistence and state application**

```csharp
using NUnit.Framework;
using Garden;
using System.Collections.Generic;

[TestFixture]
public class TestEconomyService
{
    [Test]
    public void EconomyQueue_SerializesAndDeserializes()
    {
        var queue = new EconomyQueue();
        queue.actions.Add(new EconomyAction { type = "spend-mana", jsonBody = "{\"amount\":20}" });
        queue.actions.Add(new EconomyAction { type = "add-seeds", jsonBody = "{\"seed_name\":\"Basil\",\"count\":3}" });

        string json = UnityEngine.JsonUtility.ToJson(queue);
        var restored = UnityEngine.JsonUtility.FromJson<EconomyQueue>(json);

        Assert.AreEqual(2, restored.actions.Count);
        Assert.AreEqual("spend-mana", restored.actions[0].type);
        Assert.AreEqual("add-seeds", restored.actions[1].type);
    }

    [Test]
    public void EconomyState_DeserializesFromServerJson()
    {
        string json = @"{""mana"":42.5,""gems"":10,""flameLevel"":3,""lastManaCollectUtc"":""2026-03-05T12:00:00Z"",""seeds"":[{""seedName"":""Basil"",""count"":5}],""items"":[{""itemName"":""Speed_Potion"",""count"":2}]}";
        var state = UnityEngine.JsonUtility.FromJson<EconomyState>(json);

        Assert.AreEqual(42.5f, state.mana, 0.01f);
        Assert.AreEqual(10, state.gems);
        Assert.AreEqual(3, state.flameLevel);
        Assert.AreEqual(1, state.seeds.Count);
        Assert.AreEqual("Basil", state.seeds[0].seedName);
        Assert.AreEqual(5, state.seeds[0].count);
        Assert.AreEqual(1, state.items.Count);
    }

    [Test]
    public void SpendManaRequest_SerializesCorrectly()
    {
        var req = new SpendManaRequest { amount = 25.5f };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("25.5"));
    }

    [Test]
    public void AddSeedRequest_SerializesCorrectly()
    {
        var req = new AddSeedRequest { seed_name = "Basil", count = 3 };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("Basil"));
        Assert.IsTrue(json.Contains("3"));
    }
}
```

**Step 2: Run tests**

Run via Unity MCP: `run_tests` with mode "EditMode".
Expected: All tests pass.

**Step 3: Commit**

```bash
git add Assets/Tests/EditMode/TestEconomyService.cs Assets/Tests/EditMode/TestEconomyService.cs.meta
git commit -m "test: add EconomyService serialization tests"
```

---

### Task 11: Add EconomyService to Scene

**Files:**
- Modify: Unity scene to add EconomyService MonoBehaviour

**Step 1: Add EconomyService to the services GameObject**

The EconomyService MonoBehaviour needs to be added to the same GameObject that has other services (or a new one). In the Garden scene, add it to the `"--- Services ---"` or similar root GameObject that holds `SaveManager`, `SocialService`, etc.

Use Unity MCP `manage_components` to add the component, or add it manually.

**Step 2: Verify in Play mode**

Enter Play mode in Unity editor. Check console:
- No compilation errors
- EconomyService initializes after SocialService signs in
- `GET /economy/state` or `POST /economy/init` fires in server logs

**Step 3: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat: add EconomyService to scene"
```

---

### Task 12: Update backend-migration-todo.md

**Files:**
- Modify: `docs/plans/backend-migration-todo.md`

**Step 1: Mark Tier 1 items as complete**

Update the todo to reflect that the 4 Tier 1 items are now implemented:
- ~~Server-side economy ledger~~ ✅
- ~~Resource spending validation~~ ✅
- ~~Flame level management~~ ✅
- ~~Seed & item inventory~~ ✅

**Step 2: Commit**

```bash
git add docs/plans/backend-migration-todo.md
git commit -m "docs: mark Tier 1 economy items complete"
```

---

### Task 13: Run All Tests & Manual Verification

**Step 1: Run all server tests**

Run: `cd server && mix test`
Expected: All tests pass (existing + economy tests).

**Step 2: Run all Unity EditMode tests**

Run via Unity MCP: `run_tests` with mode "EditMode".
Expected: All tests pass.

**Step 3: Manual integration test**

1. Start server: `cd server && make dev`
2. Enter Unity Play mode
3. Verify server logs show `/economy/init` call (fresh player)
4. Craft a plot or plant a seed → verify server logs show economy action
5. Stop and restart Play mode → verify mana/gems/seeds load from server
6. Verify existing features still work (friends, gifts, visitors)
