defmodule CampFire.Repo.Migrations.CreateAdminConfigTables do
  use Ecto.Migration

  def change do
    create table(:quest_configs) do
      add :quest_name, :text, null: false
      add :duration_minutes, :integer, null: false
      add :required_flame_level, :integer, default: 1
      add :reward_rolls, :integer, default: 1
      add :reward_pool, :jsonb, default: "[]"

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
      add :value, :jsonb, default: "{}"

      timestamps(type: :utc_datetime)
    end

    create unique_index(:game_configs, [:key])
  end
end
