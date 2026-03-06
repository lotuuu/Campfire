defmodule CampFire.Repo.Migrations.AddTierToSeedConfigs do
  use Ecto.Migration

  def change do
    alter table(:seed_configs) do
      add :tier, :integer, default: 1, null: false
    end
  end
end
