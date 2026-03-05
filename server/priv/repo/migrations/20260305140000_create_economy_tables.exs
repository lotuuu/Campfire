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
