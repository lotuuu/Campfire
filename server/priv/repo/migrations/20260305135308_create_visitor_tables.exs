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
