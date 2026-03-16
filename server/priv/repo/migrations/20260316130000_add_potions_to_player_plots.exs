defmodule CampFire.Repo.Migrations.AddPotionsToPlayerPlots do
  use Ecto.Migration

  def change do
    alter table(:player_plots) do
      add :potions, :map, default: fragment("'[]'::jsonb"), null: false
    end
  end
end
