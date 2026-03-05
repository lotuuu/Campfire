defmodule CampFireWeb.Router do
  use CampFireWeb, :router

  pipeline :api do
    plug :accepts, ["json"]
    plug CampFireWeb.Plugs.RateLimit, max: 100, window_ms: 60_000
  end

  pipeline :auth_rate_limit do
    plug CampFireWeb.Plugs.RateLimit, max: 5, window_ms: 60_000
  end

  pipeline :authenticated do
    plug CampFireWeb.Plugs.Authenticate
  end

  scope "/", CampFireWeb do
    pipe_through :api
    get "/health", HealthController, :index
  end

  # Register (no auth, extra rate limit)
  scope "/auth", CampFireWeb do
    pipe_through [:api, :auth_rate_limit]
    post "/register", AuthController, :register
  end

  # Display name (auth + extra rate limit)
  scope "/auth", CampFireWeb do
    pipe_through [:api, :auth_rate_limit, :authenticated]
    put "/display-name", AuthController, :update_display_name
  end

  scope "/friends", CampFireWeb do
    pipe_through [:api, :authenticated]
    get "/", FriendController, :index
    post "/request", FriendController, :create_request
    get "/requests", FriendController, :pending_requests
    post "/accept/:request_id", FriendController, :accept
    post "/decline/:request_id", FriendController, :decline
    delete "/:friend_uid", FriendController, :remove
  end

  scope "/village", CampFireWeb do
    pipe_through [:api, :authenticated]
    put "/", VillageController, :upsert
    get "/:uid", VillageController, :show
  end

  scope "/gifts", CampFireWeb do
    pipe_through [:api, :authenticated]
    post "/send", GiftController, :send_gift
    get "/", GiftController, :index
    post "/claim/:gift_id", GiftController, :claim
  end

  scope "/visitors", CampFireWeb do
    pipe_through [:api, :authenticated]
    get "/tonight", VisitorController, :tonight
    post "/quest/accept", VisitorController, :accept_quest
    post "/quest/complete", VisitorController, :complete_quest
  end
end
