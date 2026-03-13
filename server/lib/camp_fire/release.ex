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
    # Drop all tables (Gigalixir shared Postgres can't DROP SCHEMA)
    {:ok, %{rows: rows}} =
      Ecto.Adapters.SQL.query(CampFire.Repo, """
      SELECT tablename FROM pg_tables WHERE schemaname = 'public'
      """)

    table_names = Enum.map(rows, fn [name] -> name end)

    for table <- table_names do
      Ecto.Adapters.SQL.query(CampFire.Repo, "DROP TABLE IF EXISTS \"#{table}\" CASCADE")
    end

    # Re-run all migrations and seeds
    migrate()
    seed()
  end

  defp migrations_path do
    Application.app_dir(@app, "priv/repo/migrations")
  end
end
