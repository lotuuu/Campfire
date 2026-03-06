defmodule CampFire.Repo.Migrations.CreatePlayerBirds do
  use Ecto.Migration

  def change do
    create table(:player_birds) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :seed_name, :text, null: false
      add :seed_count, :integer, null: false
      add :spawned_at_utc, :utc_datetime, null: false
      timestamps(type: :utc_datetime)
    end

    create index(:player_birds, [:player_uid])
  end
end
