defmodule CampFire.Accounts do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Accounts.Player

  @friend_code_prefixes ~w(SPARK BLAZE EMBER FLAME TORCH FLARE)
  @code_chars String.graphemes("ABCDEFGHJKLMNPQRSTUVWXYZ23456789")
  @max_retries 10

  def hash_token(token) do
    :crypto.hash(:sha256, token) |> Base.encode16(case: :lower)
  end

  def get_player_by_token(token) do
    hash = hash_token(token)
    Repo.one(from p in Player, where: p.token_hash == ^hash)
  end

  def get_player_by_uid(uid) do
    Repo.one(from p in Player, where: p.uid == ^uid)
  end

  def get_player_by_friend_code(code) do
    upper = String.upcase(code)
    Repo.one(from p in Player, where: p.friend_code == ^upper)
  end

  def register_player do
    uid = Ecto.UUID.generate()
    auth_token = Base.encode16(:crypto.strong_rand_bytes(32), case: :lower)
    do_register(uid, auth_token, 0)
  end

  defp do_register(_uid, _token, attempt) when attempt >= @max_retries do
    {:error, :friend_code_exhausted}
  end

  defp do_register(uid, auth_token, attempt) do
    friend_code = generate_friend_code()
    token_hash = hash_token(auth_token)

    %Player{}
    |> Player.registration_changeset(%{uid: uid, token_hash: token_hash, friend_code: friend_code})
    |> Repo.insert()
    |> case do
      {:ok, player} ->
        {:ok, %{uid: player.uid, auth_token: auth_token, friend_code: player.friend_code, display_name: player.display_name}}

      {:error, %{errors: errors}} ->
        if Keyword.has_key?(errors, :friend_code) do
          do_register(uid, auth_token, attempt + 1)
        else
          {:error, :registration_failed}
        end
    end
  end

  def update_display_name(player, display_name) do
    player
    |> Player.display_name_changeset(%{display_name: String.trim(display_name)})
    |> Repo.update()
  end

  def count_players do
    Repo.aggregate(Player, :count)
  end

  def touch_last_online(uid) do
    from(p in Player, where: p.uid == ^uid)
    |> Repo.update_all(set: [updated_at: DateTime.utc_now()])
  end

  defp generate_friend_code do
    prefix = Enum.random(@friend_code_prefixes)
    suffix = Enum.map(1..4, fn _ -> Enum.random(@code_chars) end) |> Enum.join()
    "#{prefix}-#{suffix}"
  end
end
