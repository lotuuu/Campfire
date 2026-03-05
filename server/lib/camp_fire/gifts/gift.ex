defmodule CampFire.Gifts.Gift do
  use Ecto.Schema
  import Ecto.Changeset

  schema "gifts" do
    field :from_uid, :string
    field :to_uid, :string
    field :items, {:array, :map}, default: []
    field :status, :string, default: "pending"
    field :claimed_at, :utc_datetime
    timestamps()
  end

  def changeset(gift, attrs) do
    gift
    |> cast(attrs, [:from_uid, :to_uid, :items, :status, :claimed_at])
    |> validate_required([:from_uid, :to_uid, :items])
    |> validate_inclusion(:status, ["pending", "claimed"])
  end
end
