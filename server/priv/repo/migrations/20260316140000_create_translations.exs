defmodule CampFire.Repo.Migrations.CreateTranslations do
  use Ecto.Migration

  def change do
    create table(:translations) do
      add :locale, :text, null: false
      add :key, :text, null: false
      add :value, :text, null: false
      timestamps(type: :utc_datetime)
    end

    create unique_index(:translations, [:locale, :key])
    create index(:translations, [:locale])

    create table(:config_translations) do
      add :locale, :text, null: false
      add :translatable_type, :text, null: false
      add :translatable_key, :text, null: false
      add :field, :text, null: false
      add :value, :text, null: false
      timestamps(type: :utc_datetime)
    end

    create unique_index(:config_translations, [:locale, :translatable_type, :translatable_key, :field])
    create index(:config_translations, [:locale])
  end
end
