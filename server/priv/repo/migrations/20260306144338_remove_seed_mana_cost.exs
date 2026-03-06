defmodule CampFire.Repo.Migrations.RemoveSeedManaCost do
  use Ecto.Migration

  def change do
    alter table(:seed_configs) do
      remove :mana_cost, :float, default: 0.0
    end
  end
end
