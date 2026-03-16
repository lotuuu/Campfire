defmodule CampFire.Repo.Migrations.AddFertilizedToPlotsAndGardens do
  use Ecto.Migration

  def change do
    alter table(:player_plots) do
      add :fertilized, :boolean, default: false, null: false
    end

    alter table(:player_gardens) do
      add :fertilized, :boolean, default: false, null: false
    end
  end
end
