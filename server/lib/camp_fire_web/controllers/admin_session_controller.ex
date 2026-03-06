defmodule CampFireWeb.AdminSessionController do
  use CampFireWeb, :controller

  def create(conn, %{"secret" => secret}) do
    admin_secret = System.get_env("ADMIN_SECRET") || "dev-admin-secret"

    if Plug.Crypto.secure_compare(secret, admin_secret) do
      conn
      |> put_session(:admin_authenticated, true)
      |> redirect(to: "/admin/seeds")
    else
      conn
      |> put_flash(:error, "Invalid secret")
      |> redirect(to: "/admin/login")
    end
  end
end
