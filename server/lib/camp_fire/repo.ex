defmodule CampFire.Repo do
  use Ecto.Repo,
    otp_app: :camp_fire,
    adapter: Ecto.Adapters.Postgres
end
