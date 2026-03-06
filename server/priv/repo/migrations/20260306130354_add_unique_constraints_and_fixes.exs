defmodule CampFire.Repo.Migrations.AddUniqueConstraintsAndFixes do
  use Ecto.Migration

  def change do
    # Prevent grid collisions per player
    create unique_index(:player_plots, [:player_uid, :grid_x, :grid_y])
    create unique_index(:player_vases, [:player_uid, :grid_x, :grid_y])
    create unique_index(:player_gardens, [:player_uid, :grid_x, :grid_y])
    create unique_index(:player_mallum_houses, [:player_uid, :grid_x, :grid_y])
    create unique_index(:player_birds, [:player_uid, :grid_x, :grid_y])
  end
end
