defmodule CampFireWeb.Router do
  use CampFireWeb, :router

  pipeline :browser do
    plug :accepts, ["html"]
    plug :fetch_session
    plug :fetch_live_flash
    plug :put_root_layout, html: {CampFireWeb.Layouts, :root}
    plug :protect_from_forgery
    plug :put_secure_browser_headers
  end

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

  # Admin login (no auth required)
  scope "/admin", CampFireWeb do
    pipe_through :browser

    live "/login", AdminLoginLive, :index
    post "/login", AdminSessionController, :create
  end

  # Admin pages (auth required)
  scope "/admin", CampFireWeb do
    pipe_through [:browser, CampFireWeb.Plugs.AdminAuth]

    live "/seeds", SeedsLive, :index
    live "/seeds/:id/edit", SeedsLive, :edit
    live "/economy", EconomyLive, :index
    live "/visitors", VisitorsLive, :index
    live "/visitors/:id/edit", VisitorsLive, :edit
    live "/quests", QuestsLive, :index
    live "/quests/:id/edit", QuestsLive, :edit
    live "/players", PlayersLive, :index
    live "/players/:uid", PlayersLive, :show
    live "/weather", WeatherLive, :index
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

  # Display name (auth required, normal rate limit)
  scope "/auth", CampFireWeb do
    pipe_through [:api, :authenticated]
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

  scope "/economy", CampFireWeb do
    pipe_through [:api, :authenticated]
    get "/state", EconomyController, :state
    post "/init", EconomyController, :init
    post "/collect-mana", EconomyController, :collect_mana
    post "/spend-mana", EconomyController, :spend_mana
    post "/spend-gems", EconomyController, :spend_gems
    post "/add-gems", EconomyController, :add_gems
    post "/upgrade-flame", EconomyController, :upgrade_flame
    post "/add-seeds", EconomyController, :add_seeds
    post "/spend-seeds", EconomyController, :spend_seeds
    post "/add-items", EconomyController, :add_items
    post "/spend-items", EconomyController, :spend_items
  end

  scope "/game", CampFireWeb do
    pipe_through [:api, :authenticated]
    get "/state", GameController, :get_state
    put "/state", GameController, :save_state
    get "/plots", GameController, :list_plots
    post "/plot/craft", GameController, :craft_plot
    post "/plot/plant", GameController, :plant_seed
    post "/plot/water", GameController, :water_plot
    post "/plot/harvest", GameController, :harvest_plot
    post "/plot/set-skin", GameController, :set_plot_skin
    get "/vases", GameController, :list_vases
    post "/vase/craft", GameController, :craft_vase
    post "/vase/fill", GameController, :fill_vase
    post "/vase/check", GameController, :check_vase
    post "/vase/set-skin", GameController, :set_vase_skin
    get "/gardens", GameController, :list_gardens
    post "/garden/plant", GameController, :plant_garden
    post "/garden/collect", GameController, :collect_garden
    post "/quest/start", GameController, :start_quest
    post "/quest/check", GameController, :check_quest
    post "/quest/collect", GameController, :collect_quest
    post "/quest/speed-up", GameController, :speed_up_quest
  end

  scope "/weather", CampFireWeb do
    pipe_through [:api, :authenticated]
    post "/location", GameController, :submit_location
    get "/current", GameController, :current_weather
  end
end
