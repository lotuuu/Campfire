defmodule CampFire.Repo.Migrations.CreateGameTables do
  use Ecto.Migration

  def change do
    # Modify player_economies to add location
    alter table(:player_economies) do
      add :lat, :float
      add :lon, :float
    end

    # Plot entities
    create table(:player_plots) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :seed_name, :text
      add :state, :text, null: false, default: "empty"
      add :plant_time_utc, :utc_datetime
      add :water_count, :integer, default: 0
      add :last_watered_utc, :utc_datetime
      add :snapshots, :map, default: %{}
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :skin_name, :text
      add :unlocked_skins, {:array, :text}, default: []
      timestamps(type: :utc_datetime)
    end

    create index(:player_plots, [:player_uid])

    # Vase entities
    create table(:player_vases) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :capacity, :integer, null: false, default: 5
      add :current_water, :integer, default: 0
      add :state, :text, null: false, default: "empty"
      add :fill_start_time_utc, :utc_datetime
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :skin_name, :text
      add :unlocked_skins, {:array, :text}, default: []
      timestamps(type: :utc_datetime)
    end

    create index(:player_vases, [:player_uid])

    # Garden entities (permanent plants)
    create table(:player_gardens) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :plant_name, :text, null: false
      add :plant_time_utc, :utc_datetime, null: false
      add :last_yield_time_utc, :utc_datetime
      add :mature, :boolean, default: false
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      timestamps(type: :utc_datetime)
    end

    create index(:player_gardens, [:player_uid])

    # Mallum entities
    create table(:player_mallums) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :state, :text, null: false, default: "idle"
      add :assigned_quest_name, :text
      add :start_time_utc, :utc_datetime
      add :assigned_vase_id, references(:player_vases)
      add :pending_rewards, :map, default: fragment("'[]'::jsonb")
      timestamps(type: :utc_datetime)
    end

    create index(:player_mallums, [:player_uid])

    # Seed config (server-side recipe data)
    create table(:seed_configs) do
      add :seed_name, :text, null: false
      add :growth_duration_hours, :float, null: false
      add :base_drops, :integer, null: false
      add :mana_cost, :float, null: false, default: 0
      add :recipe, :map, null: false, default: %{}
      timestamps(type: :utc_datetime)
    end

    create unique_index(:seed_configs, [:seed_name])

    # Weather cache
    create table(:weather_cache) do
      add :lat, :float, null: false
      add :lon, :float, null: false
      add :weather_data, :map, null: false, default: %{}
      add :rain_start_utc, :utc_datetime
      add :last_rain_effect_utc, :utc_datetime
      add :fetched_at, :utc_datetime, null: false
      timestamps(type: :utc_datetime)
    end

    create unique_index(:weather_cache, [:lat, :lon])

    # Catch-all JSONB state
    create table(:player_states, primary_key: false) do
      add :player_uid, references(:players, column: :uid, type: :text), primary_key: true
      add :data, :map, default: %{}
      timestamps(type: :utc_datetime)
    end
  end
end
