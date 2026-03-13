defmodule CampFire.Application do
  # See https://hexdocs.pm/elixir/Application.html
  # for more information on OTP Applications
  @moduledoc false

  use Application

  @impl true
  def start(_type, _args) do
    children = [
      CampFireWeb.Telemetry,
      CampFire.Repo,
      CampFire.DebugLog,
      CampFire.ConfigCache,
      {DNSCluster, query: Application.get_env(:camp_fire, :dns_cluster_query) || :ignore},
      {Phoenix.PubSub, name: CampFire.PubSub},
      # Start a worker by calling: CampFire.Worker.start_link(arg)
      # {CampFire.Worker, arg},
      # Weather poller — polls OWM for active locations
      CampFire.Game.WeatherPoller,
      # Start to serve requests, typically the last entry
      CampFireWeb.Endpoint
    ]

    # See https://hexdocs.pm/elixir/Supervisor.html
    # for other strategies and supported options
    opts = [strategy: :one_for_one, name: CampFire.Supervisor]
    Supervisor.start_link(children, opts)
  end

  # Tell Phoenix to update the endpoint configuration
  # whenever the application is updated.
  @impl true
  def config_change(changed, _new, removed) do
    CampFireWeb.Endpoint.config_change(changed, removed)
    :ok
  end
end
