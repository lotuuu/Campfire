defmodule CampFire.Repo.Migrations.CreateFriends do
  use Ecto.Migration

  def change do
    create table(:friends, primary_key: false) do
      add :player_uid, references(:players, column: :uid, type: :text), null: false
      add :friend_uid, references(:players, column: :uid, type: :text), null: false
      add :added_at, :utc_datetime, default: fragment("NOW()")
    end

    create unique_index(:friends, [:player_uid, :friend_uid])
  end
end
