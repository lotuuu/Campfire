defmodule CampFire.Repo.Migrations.CreatePlayerApothekes do
  use Ecto.Migration

  def change do
    create table(:player_apothekes) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :grid_x, :integer, null: false, default: 1
      add :grid_y, :integer, null: false, default: 0
      timestamps(type: :utc_datetime)
    end

    create unique_index(:player_apothekes, [:player_uid])
  end
end
