defmodule CampFire.Repo.Migrations.AddGrowthStagesToSeedConfigs do
  use Ecto.Migration

  def change do
    alter table(:seed_configs) do
      add :growth_stages, {:array, :float}, default: []
    end
  end
end
