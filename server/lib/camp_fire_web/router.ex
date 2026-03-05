defmodule CampFireWeb.Router do
  use CampFireWeb, :router

  pipeline :api do
    plug :accepts, ["json"]
  end

  scope "/api", CampFireWeb do
    pipe_through :api
  end
end
