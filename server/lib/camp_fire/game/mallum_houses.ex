defmodule CampFire.Game.MallumHouses do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerMallumHouse, GridValidation, Mallums}
  alias CampFire.Economy

  def list_houses(player_uid) do
    from(h in PlayerMallumHouse, where: h.player_uid == ^player_uid)
    |> Repo.all()
  end

  def count_houses(player_uid) do
    from(h in PlayerMallumHouse, where: h.player_uid == ^player_uid, select: count(h.id))
    |> Repo.one()
  end

  def craft_house(player_uid, grid_x, grid_y) do
    with :ok <- GridValidation.check_entity_cap(player_uid),
         :ok <- GridValidation.validate_grid_placement(player_uid, grid_x, grid_y) do
      house_count = count_houses(player_uid)

      case get_house_cost(house_count) do
        nil -> {:error, :config_not_loaded}
        cost ->
      Repo.transaction(fn ->
        case Economy.spend_mana(player_uid, cost["manaCost"]) do
          {:ok, _economy} -> :ok
          {:error, reason} -> Repo.rollback(reason)
        end

        harvest_costs = cost["harvestCosts"] || []

        Enum.each(harvest_costs, fn %{"itemName" => name, "count" => count} ->
          case Economy.spend_item(player_uid, name, count) do
            {:ok, _} -> :ok
            {:error, reason} -> Repo.rollback(reason)
          end
        end)

        house =
          %PlayerMallumHouse{}
          |> PlayerMallumHouse.changeset(%{
            player_uid: player_uid,
            grid_x: grid_x,
            grid_y: grid_y
          })
          |> Repo.insert!()

        # Spawn mallums up to mallums_per_house * new house count
        mallums_per_house = get_mallums_per_house()
        new_house_count = house_count + 1
        target_mallums = mallums_per_house * new_house_count

        current_mallums = length(Mallums.list_mallums(player_uid))
        mallums_to_spawn = max(target_mallums - current_mallums, 0)

        for _ <- 1..mallums_to_spawn, mallums_to_spawn > 0 do
          Mallums.create_mallum(player_uid)
        end

        house
      end)
      end
    end
  end

  # --- Private Helpers ---

  defp get_house_cost(house_count) do
    case CampFire.ConfigCache.get("mallum_house_config") do
      nil -> nil
      config ->
        costs = config["house_costs"]
        idx = min(house_count, length(costs) - 1)
        Enum.at(costs, idx)
    end
  end

  defp get_mallums_per_house do
    config = CampFire.ConfigCache.get("mallum_house_config")
    config["mallums_per_house"] || 2
  end
end
