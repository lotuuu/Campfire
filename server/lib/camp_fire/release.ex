defmodule CampFire.Release do
  @app :camp_fire

  def migrate do
    Ecto.Migrator.run(CampFire.Repo, migrations_path(), :up, all: true)
  end

  def seed do
    seed_path = Application.app_dir(@app, "priv/repo/seeds.exs")
    Code.eval_file(seed_path)
  end

  def migrate_and_seed do
    migrate()
    seed()
  end

  def rollback_all do
    Ecto.Migrator.run(CampFire.Repo, migrations_path(), :down, all: true)
  end

  def reset do
    rollback_all()
    migrate()
    seed()
  end

  defp migrations_path do
    Application.app_dir(@app, "priv/repo/migrations")
  end
end
