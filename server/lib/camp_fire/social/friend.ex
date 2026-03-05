defmodule CampFire.Social.Friend do
  use Ecto.Schema

  @primary_key false
  schema "friends" do
    field :player_uid, :string
    field :friend_uid, :string
    field :added_at, :utc_datetime
  end
end
