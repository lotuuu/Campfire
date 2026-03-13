# This file is responsible for configuring your application
# and its dependencies with the aid of the Config module.
#
# This configuration file is loaded before any dependency and
# is restricted to this project.

# General application configuration
import Config

config :camp_fire,
  ecto_repos: [CampFire.Repo],
  generators: [timestamp_type: :utc_datetime]

# Configures the endpoint
config :camp_fire, CampFireWeb.Endpoint,
  url: [host: "localhost"],
  adapter: Bandit.PhoenixAdapter,
  render_errors: [
    formats: [json: CampFireWeb.ErrorJSON],
    layout: false
  ],
  pubsub_server: CampFire.PubSub,
  live_view: [signing_salt: "NO1LK2+H"]

# Configures Elixir's Logger
config :logger, :console,
  format: "$time $metadata[$level] $message\n",
  metadata: [:request_id]

config :logger, backends: [:console, CampFire.DebugLog.LoggerBackend]

# Use Jason for JSON parsing in Phoenix
config :phoenix, :json_library, Jason

config :hammer,
  backend: {Hammer.Backend.ETS,
            [expiry_ms: 60_000 * 10, cleanup_interval_ms: 60_000]}

config :camp_fire, :owm_api_key, System.get_env("OWM_API_KEY") || ""

# Import environment specific config. This must remain at the bottom
# of this file so it overrides the configuration defined above.
import_config "#{config_env()}.exs"
