defmodule CampFire.Gifts do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Gifts.Gift
  alias CampFire.Accounts.Player
  alias CampFire.Economy

  @max_items_per_gift 3
  @max_gifts_per_day 5
  @gift_expiry_days 7

  def send_gift(from_uid, to_uid, items) do
    cond do
      to_uid == from_uid ->
        {:error, "Cannot send gift to yourself"}

      not is_list(items) or length(items) == 0 ->
        {:error, "items must be a non-empty array"}

      length(items) > @max_items_per_gift ->
        {:error, "Max #{@max_items_per_gift} items per gift"}

      gifts_today(from_uid, to_uid) >= @max_gifts_per_day ->
        {:error, "Max #{@max_gifts_per_day} gifts per day to same player"}

      true ->
        Repo.transaction(fn ->
          # Deduct items from sender's inventory
          Enum.each(items, fn item ->
            item_name = item["item_name"] || item["itemName"]
            count = item["count"] || 1

            case Economy.spend_item(from_uid, item_name, count) do
              {:ok, _} -> :ok
              {:error, _} -> Repo.rollback({:insufficient_items, item_name})
            end
          end)

          # Insert the gift record
          case %Gift{}
               |> Gift.changeset(%{from_uid: from_uid, to_uid: to_uid, items: items})
               |> Repo.insert() do
            {:ok, gift} -> gift
            {:error, changeset} -> Repo.rollback(changeset)
          end
        end)
    end
  end

  def pending_gifts(to_uid) do
    cutoff = DateTime.add(DateTime.utc_now(), -@gift_expiry_days * 86400, :second)

    from(g in Gift,
      join: p in Player, on: p.uid == g.from_uid,
      where: g.to_uid == ^to_uid and g.status == "pending" and g.inserted_at >= ^cutoff,
      order_by: [desc: g.inserted_at],
      select: %{id: g.id, from_uid: g.from_uid, from_name: p.display_name, items: g.items, created_at: g.inserted_at}
    )
    |> Repo.all()
  end

  def claim_gift(gift_id, to_uid) do
    query =
      from(g in Gift,
        where: g.id == ^gift_id and g.to_uid == ^to_uid and g.status == "pending"
      )

    case Repo.one(query) do
      nil ->
        {:error, :not_found}

      gift ->
        gift
        |> Gift.changeset(%{status: "claimed", claimed_at: DateTime.utc_now() |> DateTime.truncate(:second)})
        |> Repo.update()
        |> case do
          {:ok, claimed} ->
            # Add items to receiver's inventory
            Enum.each(claimed.items, fn item ->
              item_name = item["item_name"] || item["itemName"]
              count = item["count"] || 1
              Economy.upsert_item(to_uid, item_name, count)
            end)

            {:ok, claimed.items}

          {:error, _} ->
            {:error, :update_failed}
        end
    end
  end

  defp gifts_today(from_uid, to_uid) do
    cutoff = DateTime.add(DateTime.utc_now(), -86400, :second)

    from(g in Gift,
      where: g.from_uid == ^from_uid and g.to_uid == ^to_uid and g.inserted_at >= ^cutoff
    )
    |> Repo.aggregate(:count)
  end
end
