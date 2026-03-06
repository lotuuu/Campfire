defmodule CampFire.Repo.Migrations.RemoveGrowthStagesFromSeedConfigs do
  use Ecto.Migration

  def change do
    alter table(:seed_configs) do
      remove :growth_stages, {:array, :float}, default: []
    end
  end
end
