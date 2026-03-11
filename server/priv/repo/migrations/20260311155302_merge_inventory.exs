defmodule CampFire.Repo.Migrations.MergeInventory do
  use Ecto.Migration

  def change do
    create table(:player_inventory) do
      add :player_uid, :string, null: false
      add :item_name, :string, null: false
      add :count, :integer, default: 0
    end

    create unique_index(:player_inventory, [:player_uid, :item_name])
    create index(:player_inventory, [:player_uid])

    # Migrate seeds: append _Seed suffix to seed_name
    execute(
      """
      INSERT INTO player_inventory (player_uid, item_name, count)
      SELECT player_uid, seed_name || '_Seed', count
      FROM player_seeds
      ON CONFLICT (player_uid, item_name) DO UPDATE SET count = player_inventory.count + EXCLUDED.count
      """,
      ""
    )

    # Migrate items: strip _harvest suffix, fix _pigment -> _Pigment casing, rest unchanged
    execute(
      """
      INSERT INTO player_inventory (player_uid, item_name, count)
      SELECT
        player_uid,
        CASE
          WHEN item_name LIKE '%_harvest' THEN regexp_replace(item_name, '_harvest$', '')
          WHEN item_name LIKE '%_pigment' THEN regexp_replace(item_name, '_pigment$', '_Pigment')
          ELSE item_name
        END AS item_name,
        count
      FROM player_items
      ON CONFLICT (player_uid, item_name) DO UPDATE SET count = player_inventory.count + EXCLUDED.count
      """,
      ""
    )

    drop table(:player_seeds)
    drop table(:player_items)
  end
end
