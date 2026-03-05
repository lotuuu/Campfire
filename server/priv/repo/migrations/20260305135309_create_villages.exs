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
