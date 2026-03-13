defmodule CampFire.Repo.Migrations.CreateItemsTable do
  use Ecto.Migration

  def change do
    # 1. Create items table
    create table(:items, primary_key: false) do
      add :item_key, :string, primary_key: true
      add :display_name, :string, null: false
      add :category, :string, null: false
      add :sprite_key, :string

      timestamps()
    end

    create index(:items, [:category])

    # 2. Add item_key and harvest_item_key to seed_configs
    alter table(:seed_configs) do
      add :item_key, references(:items, column: :item_key, type: :string, on_update: :update_all)
      add :harvest_item_key, references(:items, column: :item_key, type: :string, on_update: :update_all)
    end

    create unique_index(:seed_configs, [:item_key])
    create unique_index(:seed_configs, [:harvest_item_key])

    # 3. Rename item_name -> item_key in player_inventory
    #    Must drop and recreate unique constraint since column name changes
    drop unique_index(:player_inventory, [:player_uid, :item_name])
    rename table(:player_inventory), :item_name, to: :item_key

    create unique_index(:player_inventory, [:player_uid, :item_key])

    # 4. Add FK from player_inventory.item_key -> items.item_key
    #    Done as a separate flush/step to avoid issues with rename + modify in same migration
    flush()

    alter table(:player_inventory) do
      modify :item_key, references(:items, column: :item_key, type: :string, on_update: :update_all),
        from: :string
    end
  end
end
