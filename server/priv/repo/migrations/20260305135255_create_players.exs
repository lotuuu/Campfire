defmodule CampFire.Repo.Migrations.CreatePlayers do
  use Ecto.Migration

  def change do
    create table(:players, primary_key: false) do
      add :id, :serial, primary_key: true
      add :uid, :text, null: false
      add :auth_token, :text, null: false
      add :friend_code, :text, null: false
      add :display_name, :text, null: false, default: "Camper"
      timestamps(type: :utc_datetime)
    end

    create unique_index(:players, [:uid])
    create unique_index(:players, [:auth_token])
    create unique_index(:players, [:friend_code])
  end
end
