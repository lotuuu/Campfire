defmodule CampFireWeb.Plugs.RateLimit do
  import Plug.Conn

  def init(opts), do: opts

  def call(conn, opts) do
    if Application.get_env(:camp_fire, :disable_rate_limit, false) do
      conn
    else
      do_rate_limit(conn, opts)
    end
  end

  defp do_rate_limit(conn, opts) do
    max = Keyword.fetch!(opts, :max)
    window_ms = Keyword.fetch!(opts, :window_ms)
    key = rate_limit_key(conn)

    case Hammer.check_rate(key, window_ms, max) do
      {:allow, _count} ->
        conn

      {:deny, _limit} ->
        accepts = get_req_header(conn, "accept")
        is_html = Enum.any?(accepts, &String.contains?(&1, "text/html"))

        conn
        |> put_status(429)
        |> then(fn c ->
          if is_html do
            c
            |> Phoenix.Controller.put_view(CampFireWeb.ErrorHTML)
            |> Phoenix.Controller.text("Too many requests, please try again later")
          else
            Phoenix.Controller.json(c, %{error: "Too many requests, please try again later"})
          end
        end)
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
