defmodule CampFire.Social do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Social.{Friend, FriendRequest}
  alias CampFire.Accounts.Player

  @max_friends 20

  def list_friends(uid) do
    from(f in Friend,
      join: p in Player, on: p.uid == f.friend_uid,
      where: f.player_uid == ^uid,
      order_by: p.display_name,
      select: %{uid: p.uid, display_name: p.display_name, friend_code: p.friend_code, last_online: p.updated_at}
    )
    |> Repo.all()
  end

  def are_friends?(uid_a, uid_b) do
    Repo.exists?(from f in Friend, where: f.player_uid == ^uid_a and f.friend_uid == ^uid_b)
  end

  def send_request(from_uid, to_uid) do
    cond do
      from_uid == to_uid ->
        {:error, "Cannot send friend request to yourself"}

      are_friends?(from_uid, to_uid) ->
        {:error, "Already friends"}

      has_pending_request?(from_uid, to_uid) ->
        {:error, "Friend request already pending"}

      true ->
        %FriendRequest{}
        |> FriendRequest.changeset(%{from_uid: from_uid, to_uid: to_uid})
        |> Repo.insert()
        |> case do
          {:ok, _} -> :ok
          {:error, _} -> {:error, "Failed to send friend request"}
        end
    end
  end

  def pending_requests(to_uid) do
    from(fr in FriendRequest,
      join: p in Player, on: p.uid == fr.from_uid,
      where: fr.to_uid == ^to_uid and fr.status == "pending",
      order_by: [desc: fr.inserted_at],
      select: %{id: fr.id, from_uid: fr.from_uid, from_name: p.display_name, status: fr.status, created_at: fr.inserted_at}
    )
    |> Repo.all()
  end

  def accept_request(request_id, current_uid) do
    Repo.transaction(fn ->
      request =
        from(fr in FriendRequest,
          where: fr.id == ^request_id and fr.status == "pending" and fr.to_uid == ^current_uid
        )
        |> Repo.one()

      if is_nil(request) do
        Repo.rollback("Friend request not found")
      end

      count_a = friend_count(request.from_uid)
      count_b = friend_count(request.to_uid)

      cond do
        count_a >= @max_friends ->
          Repo.rollback("Sender has reached max friends")

        count_b >= @max_friends ->
          Repo.rollback("You have reached max friends")

        true ->
          request
          |> FriendRequest.changeset(%{status: "accepted"})
          |> Repo.update!()

          now = DateTime.utc_now() |> DateTime.truncate(:second)

          Repo.insert_all(Friend, [
            %{player_uid: request.from_uid, friend_uid: request.to_uid, added_at: now},
            %{player_uid: request.to_uid, friend_uid: request.from_uid, added_at: now}
          ], on_conflict: :nothing)

          list_friends(current_uid)
      end
    end)
  end

  def decline_request(request_id, current_uid) do
    query =
      from(fr in FriendRequest,
        where: fr.id == ^request_id and fr.to_uid == ^current_uid and fr.status == "pending"
      )

    case Repo.update_all(query, set: [status: "declined"]) do
      {0, _} -> {:error, :not_found}
      {_, _} -> :ok
    end
  end

  def remove_friend(uid, friend_uid) do
    query =
      from(f in Friend,
        where:
          (f.player_uid == ^uid and f.friend_uid == ^friend_uid) or
          (f.player_uid == ^friend_uid and f.friend_uid == ^uid)
      )

    case Repo.delete_all(query) do
      {0, _} -> {:error, :not_found}
      {_, _} -> :ok
    end
  end

  defp has_pending_request?(from_uid, to_uid) do
    Repo.exists?(
      from fr in FriendRequest,
        where: fr.status == "pending" and
          ((fr.from_uid == ^from_uid and fr.to_uid == ^to_uid) or
           (fr.from_uid == ^to_uid and fr.to_uid == ^from_uid))
    )
  end

  defp friend_count(uid) do
    Repo.aggregate(from(f in Friend, where: f.player_uid == ^uid), :count)
  end
end
