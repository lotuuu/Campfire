defmodule CampFire.Repo.Migrations.HashAuthTokens do
  use Ecto.Migration

  def up do
    alter table(:players) do
      add :token_hash, :text
    end

    flush()

    # Populate token_hash from existing plaintext tokens
    execute """
    UPDATE players SET token_hash = encode(sha256(auth_token::bytea), 'hex')
    """

    alter table(:players) do
      modify :token_hash, :text, null: false
    end

    create unique_index(:players, [:token_hash])
    drop index(:players, [:auth_token])

    alter table(:players) do
      remove :auth_token
    end
  end

  def down do
    alter table(:players) do
      add :auth_token, :text
    end

    # Cannot recover original tokens from hashes — set placeholder
    execute """
    UPDATE players SET auth_token = 'lost-' || token_hash
    """

    alter table(:players) do
      modify :auth_token, :text, null: false
    end

    create unique_index(:players, [:auth_token])
    drop index(:players, [:token_hash])

    alter table(:players) do
      remove :token_hash
    end
  end
end
