defmodule CampFireWeb.Plugs.DebugLogErrors do
  @behaviour Plug

  import Plug.Conn

  @impl true
  def init(opts), do: opts

  # 404s that are expected during fresh-account initialization and not worth logging
  @expected_init_404s [
    {"GET", "/economy/state"},
    {"GET", "/weather/current"},
    {"GET", "/weather/forecast"}
  ]

  @impl true
  def call(conn, _opts) do
    register_before_send(conn, fn conn ->
      if conn.status >= 400 and not expected_init_404?(conn) do
        player_uid =
          case conn.assigns do
            %{current_player: %{uid: uid}} -> uid
            _ -> nil
          end

        level = if conn.status >= 500, do: :error, else: :warning

        error_reason = extract_error_reason(conn.resp_body)

        message =
          if error_reason,
            do: "#{conn.method} #{conn.request_path} -> #{conn.status}: #{error_reason}",
            else: "#{conn.method} #{conn.request_path} -> #{conn.status}"

        CampFire.DebugLog.log(%{
          level: level,
          source: :server,
          category: "api",
          message: message,
          player_uid: player_uid,
          metadata: %{status: conn.status, method: conn.method, path: conn.request_path}
        })
      end

      conn
    end)
  end

  defp expected_init_404?(conn) do
    conn.status == 404 and {conn.method, conn.request_path} in @expected_init_404s
  end

  defp extract_error_reason(resp_body) do
    bin =
      case resp_body do
        b when is_binary(b) -> b
        [_ | _] = iodata -> IO.iodata_to_binary(iodata)
        _ -> nil
      end

    with bin when is_binary(bin) <- bin,
         {:ok, %{"error" => reason}} <- Jason.decode(bin) do
      reason
    else
      _ -> nil
    end
  end
end
