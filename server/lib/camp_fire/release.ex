defmodule CampFire.Release do
  @app :camp_fire

  def migrate do
    ensure_started()
    Ecto.Migrator.run(CampFire.Repo, migrations_path(), :up, all: true)
  end

  def seed do
    ensure_started()
    Code.eval_file("priv/repo/seeds.exs")
  end

  defp ensure_started do
    Application.ensure_all_started(@app)
  end

  defp migrations_path do
    Application.app_dir(@app, "priv/repo/migrations")
  end
end
