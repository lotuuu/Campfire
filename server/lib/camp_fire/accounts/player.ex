defmodule CampFire.Accounts.Player do
  use Ecto.Schema
  import Ecto.Changeset

  schema "players" do
    field :uid, :string
    field :auth_token, :string
    field :friend_code, :string
    field :display_name, :string, default: "Camper"
    timestamps()
  end

  def registration_changeset(player, attrs) do
    player
    |> cast(attrs, [:uid, :auth_token, :friend_code])
    |> validate_required([:uid, :auth_token, :friend_code])
    |> unique_constraint(:uid)
    |> unique_constraint(:auth_token)
    |> unique_constraint(:friend_code)
  end

  def display_name_changeset(player, attrs) do
    player
    |> cast(attrs, [:display_name])
    |> validate_required([:display_name])
    |> validate_length(:display_name, min: 1, max: 20)
    |> validate_format(:display_name, ~r/^[a-zA-Z0-9 ]+$/, message: "can only contain letters, numbers, and spaces")
  end
end
