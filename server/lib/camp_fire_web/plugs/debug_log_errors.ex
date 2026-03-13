defmodule CampFireWeb.Plugs.DebugLogErrors do
  @behaviour Plug

  import Plug.Conn

  @impl true
  def init(opts), do: opts

  @impl true
  def call(conn, _opts) do
    register_before_send(conn, fn conn ->
      if conn.status >= 400 do
        player_uid =
          case conn.assigns do
            %{current_player: %{uid: uid}} -> uid
            _ -> nil
          end

        level = if conn.status >= 500, do: :error, else: :warning

        CampFire.DebugLog.log(%{
          level: level,
          source: :server,
          category: "api",
          message: "#{conn.method} #{conn.request_path} -> #{conn.status}",
          player_uid: player_uid,
          metadata: %{
            status: conn.status,
            method: conn.method,
            path: conn.request_path
          }
        })
      end

      conn
    end)
  end
end
