defmodule CampFire.Repo.Migrations.CreateMallumHouses do
  use Ecto.Migration

  def change do
    create table(:player_mallum_houses) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :grid_x, :integer, null: false
      add :grid_y, :integer, null: false
      add :skin_name, :text
      add :unlocked_skins, {:array, :text}, default: []
      timestamps(type: :utc_datetime)
    end

    create index(:player_mallum_houses, [:player_uid])
    create unique_index(:player_mallum_houses, [:player_uid, :grid_x, :grid_y])
  end
end
