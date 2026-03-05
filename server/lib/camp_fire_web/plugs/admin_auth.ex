defmodule CampFireWeb.Plugs.AdminAuth do
  import Plug.Conn
  import Phoenix.Controller

  def init(opts), do: opts

  def call(conn, _opts) do
    if get_session(conn, :admin_authenticated) == true do
      conn
    else
      conn
      |> redirect(to: "/admin/login")
      |> halt()
    end
  end
end
