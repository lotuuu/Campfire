defmodule CampFire.Social.FriendRequest do
  use Ecto.Schema
  import Ecto.Changeset

  schema "friend_requests" do
    field :from_uid, :string
    field :to_uid, :string
    field :status, :string, default: "pending"
    timestamps()
  end

  def changeset(request, attrs) do
    request
    |> cast(attrs, [:from_uid, :to_uid, :status])
    |> validate_required([:from_uid, :to_uid])
    |> validate_inclusion(:status, ["pending", "accepted", "declined"])
  end
end
