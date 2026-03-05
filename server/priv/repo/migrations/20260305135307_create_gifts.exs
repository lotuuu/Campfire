defmodule CampFire.Repo.Migrations.CreateGifts do
  use Ecto.Migration

  def change do
    create table(:gifts) do
      add :from_uid, references(:players, column: :uid, type: :text), null: false
      add :to_uid, references(:players, column: :uid, type: :text), null: false
      add :items, :map, null: false, default: fragment("'[]'::jsonb")
      add :status, :text, null: false, default: "pending"
      add :claimed_at, :utc_datetime
      timestamps(type: :utc_datetime)
    end

    create index(:gifts, [:to_uid, :status])
  end
end
