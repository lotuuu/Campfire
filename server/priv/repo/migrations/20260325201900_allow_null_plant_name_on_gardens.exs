defmodule CampFire.Repo.Migrations.AllowNullPlantNameOnGardens do
  use Ecto.Migration

  def change do
    alter table(:player_gardens) do
      modify :plant_name, :string, null: true
      modify :plant_time_utc, :utc_datetime, null: true
    end
  end
end
