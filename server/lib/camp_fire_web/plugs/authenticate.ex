defmodule CampFireWeb.Plugs.Authenticate do
  import Plug.Conn
  alias CampFire.Accounts

  def init(opts), do: opts

  def call(conn, _opts) do
    with ["Bearer " <> token] <- get_req_header(conn, "authorization"),
         %{} = player <- Accounts.get_player_by_token(token) do
      Accounts.touch_last_online(player.uid)
      assign(conn, :current_player, player)
    else
      _ ->
        conn
        |> put_status(401)
        |> Phoenix.Controller.json(%{error: "Missing or invalid auth token"})
        |> halt()
    end
  end
end
