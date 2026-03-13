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

  def reset do
    # Truncate all tables (Gigalixir shared Postgres can't DROP SCHEMA)
    {:ok, %{rows: rows}} =
      Ecto.Adapters.SQL.query(CampFire.Repo, """
      SELECT tablename FROM pg_tables WHERE schemaname = 'public'
      """)

    table_names = Enum.map(rows, fn [name] -> name end)

    if table_names != [] do
      tables = Enum.join(table_names, ", ")
      Ecto.Adapters.SQL.query(CampFire.Repo, "TRUNCATE #{tables} CASCADE")
    end

    # Drop migration tracking so all migrations re-run
    Ecto.Adapters.SQL.query(CampFire.Repo, "DROP TABLE IF EXISTS schema_migrations")

    # Re-run all migrations and seeds
    migrate()
    seed()
  end

  defp migrations_path do
    Application.app_dir(@app, "priv/repo/migrations")
  end
end
