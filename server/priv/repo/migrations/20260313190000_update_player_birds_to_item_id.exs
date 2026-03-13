defmodule CampFire.Repo.Migrations.UpdatePlayerBirdsToItemId do
  use Ecto.Migration

  def change do
    alter table(:player_birds) do
      remove :seed_name
      add :seed_item_id, references(:items, on_delete: :restrict), null: false
    end
  end
end
