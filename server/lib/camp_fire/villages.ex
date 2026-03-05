defmodule CampFire.Villages do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Villages.Village

  @max_snapshot_bytes 102_400

  def upsert_snapshot(player_uid, snapshot) do
    encoded = Jason.encode!(snapshot)

    if byte_size(encoded) > @max_snapshot_bytes do
      {:error, :too_large}
    else
      %Village{}
      |> Village.changeset(%{player_uid: player_uid, snapshot: snapshot})
      |> Repo.insert(
        on_conflict: [set: [snapshot: snapshot, updated_at: DateTime.utc_now() |> DateTime.truncate(:second)]],
        conflict_target: :player_uid
      )
    end
  end

  def get_snapshot(player_uid) do
    case Repo.one(from v in Village, where: v.player_uid == ^player_uid) do
      nil -> %{snapshot: %{}, updated_at: nil}
      village -> %{snapshot: village.snapshot, updated_at: village.updated_at}
    end
  end
end
