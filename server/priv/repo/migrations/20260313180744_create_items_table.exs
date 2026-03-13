defmodule CampFire.Repo.Migrations.CreateItemsTable do
  use Ecto.Migration

  def change do
    # 1. Create items table with integer PK
    create table(:items) do
      add :item_key, :string, null: false
      add :display_name, :string, null: false
      add :category, :string, null: false
      add :sprite_key, :string

      timestamps()
    end

    create unique_index(:items, [:item_key])
    create index(:items, [:category])

    # 2. Rework seed_configs: drop seed_name, add integer FKs
    alter table(:seed_configs) do
      remove :seed_name
      add :item_id, references(:items, on_delete: :restrict), null: false
      add :harvest_item_id, references(:items, on_delete: :restrict), null: false
    end

    create unique_index(:seed_configs, [:item_id])
    create unique_index(:seed_configs, [:harvest_item_id])

    # 3. Rework player_inventory: drop item_name, add integer FK
    drop unique_index(:player_inventory, [:player_uid, :item_name])
    alter table(:player_inventory) do
      remove :item_name
      add :item_id, references(:items, on_delete: :restrict), null: false
    end

    create unique_index(:player_inventory, [:player_uid, :item_id])

    # 4. Rework player_plots: drop seed_name, add nullable integer FK
    alter table(:player_plots) do
      remove :seed_name
      add :seed_item_id, references(:items, on_delete: :restrict)
    end
  end
end
