defmodule CampFire.Repo.Migrations.ReplaceBaseDropsWithMinMax do
  use Ecto.Migration

  def change do
    alter table(:seed_configs) do
      add :min_drops, :integer
      add :max_drops, :integer
    end

    execute """
    UPDATE seed_configs SET min_drops = base_drops, max_drops = base_drops * 2
    """, """
    UPDATE seed_configs SET base_drops = min_drops
    """

    alter table(:seed_configs) do
      remove :base_drops, :integer, null: false
    end

    alter table(:seed_configs) do
      modify :min_drops, :integer, null: false
      modify :max_drops, :integer, null: false
    end
  end
end
