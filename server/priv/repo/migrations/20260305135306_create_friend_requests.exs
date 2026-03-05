defmodule CampFire.Repo.Migrations.CreateFriendRequests do
  use Ecto.Migration

  def change do
    create table(:friend_requests) do
      add :from_uid, references(:players, column: :uid, type: :text), null: false
      add :to_uid, references(:players, column: :uid, type: :text), null: false
      add :status, :text, null: false, default: "pending"
      timestamps(type: :utc_datetime)
    end

    create index(:friend_requests, [:to_uid, :status])
  end
end
