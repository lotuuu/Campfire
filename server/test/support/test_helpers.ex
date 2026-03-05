defmodule CampFire.TestHelpers do
  alias CampFire.Accounts

  def register_player do
    {:ok, player} = Accounts.register_player()
    player
  end

  def auth_header(player) do
    [{"authorization", "Bearer #{player.auth_token}"}]
  end

  def authed_conn(conn, player) do
    Enum.reduce(auth_header(player), conn, fn {key, val}, conn ->
      Plug.Conn.put_req_header(conn, key, val)
    end)
  end
end
