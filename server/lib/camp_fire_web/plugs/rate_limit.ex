defmodule CampFireWeb.Plugs.RateLimit do
  import Plug.Conn

  def init(opts), do: opts

  def call(conn, opts) do
    max = Keyword.fetch!(opts, :max)
    window_ms = Keyword.fetch!(opts, :window_ms)
    key = rate_limit_key(conn)

    case Hammer.check_rate(key, window_ms, max) do
      {:allow, _count} ->
        conn

      {:deny, _limit} ->
        conn
        |> put_status(429)
        |> Phoenix.Controller.json(%{error: "Too many requests, please try again later"})
        |> halt()
    end
  end

  defp rate_limit_key(conn) do
    forwarded = get_req_header(conn, "x-forwarded-for")

    ip =
      case forwarded do
        [header | _] -> header |> String.split(",") |> hd() |> String.trim()
        _ ->
          conn.remote_ip
          |> :inet.ntoa()
          |> to_string()
      end

    "rate_limit:#{ip}"
  end
end
