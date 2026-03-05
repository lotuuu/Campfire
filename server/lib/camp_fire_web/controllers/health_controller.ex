defmodule CampFireWeb.HealthController do
  use CampFireWeb, :controller

  def index(conn, _params) do
    json(conn, %{status: "ok"})
  end
end
