defmodule CampFire.TestHelpers do
  alias CampFire.Accounts

  def register_player do
    {:ok, player} = Accounts.register_player()
    player
  end

  def seed_quest_configs do
    quests = [
      {"SwampForage", %{quest_name: "SwampForage", duration_minutes: 5, required_flame_level: 1, reward_rolls: 2,
        reward_pool: [%{"seed_name" => "Basil", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Chamomile", "weight" => 2, "min" => 1, "max" => 2}]}},
      {"MeadowExpedition", %{quest_name: "MeadowExpedition", duration_minutes: 15, required_flame_level: 2, reward_rolls: 3,
        reward_pool: [%{"seed_name" => "Marigold", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Snowdrop", "weight" => 2, "min" => 1, "max" => 2}]}},
      {"DeepWoodsTrek", %{quest_name: "DeepWoodsTrek", duration_minutes: 60, required_flame_level: 3, reward_rolls: 3,
        reward_pool: [%{"seed_name" => "Mint", "weight" => 3, "min" => 1, "max" => 2}, %{"seed_name" => "Pansy", "weight" => 2, "min" => 1, "max" => 1}]}}
    ]

    # Write directly to ETS since ConfigCache GenServer can't see the test sandbox
    quest_map = Map.new(quests, fn {name, q} -> {name, q} end)
    :ets.insert(:config_cache, {"quest_configs", quest_map})
  end

  def auth_header(player) do
    [{"authorization", "Bearer #{player.auth_token}"}]
  end

  def authed_conn(conn, player) do
    Enum.reduce(auth_header(player), conn, fn {key, val}, conn ->
      Plug.Conn.put_req_header(conn, key, val)
    end)
  end
end
