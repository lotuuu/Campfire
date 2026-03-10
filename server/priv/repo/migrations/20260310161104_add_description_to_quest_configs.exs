defmodule CampFire.Repo.Migrations.AddDescriptionToQuestConfigs do
  use Ecto.Migration

  def change do
    alter table(:quest_configs) do
      add :description, :text, default: ""
    end
  end
end
