defmodule CampFire.Game.Skins do
  @moduledoc """
  Shared skin unlock + apply logic for all entity types (plots, vases, mallum houses).
  Loads skin definitions from ConfigCache, verifies ownership, deducts item costs on first
  unlock, and sets the active skin — all within a transaction.
  """

  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerMallumHouse}

  def apply_skin(player_uid, entity_type, entity_id, skin_name) do
    skin_configs = CampFire.ConfigCache.get("skin_configs") || %{}

    case Map.get(skin_configs, skin_name) do
      nil ->
        {:error, :unknown_skin}

      skin ->
        schema = schema_for(entity_type)
        entity = Repo.get(schema, entity_id)

        cond do
          entity == nil ->
            {:error, :not_found}

          entity.player_uid != player_uid ->
            {:error, :not_owned}

          true ->
            already_unlocked = skin_name in (entity.unlocked_skins || [])

            Repo.transaction(fn ->
              unless already_unlocked do
                cost_item = skin["cost_item_key"]
                cost_qty = skin["cost_quantity"] || 1

                case Economy.spend_item(player_uid, cost_item, cost_qty) do
                  {:ok, _} -> :ok
                  {:error, reason} -> Repo.rollback(reason)
                end
              end

              new_unlocked =
                if already_unlocked,
                  do: entity.unlocked_skins || [],
                  else: (entity.unlocked_skins || []) ++ [skin_name]

              entity
              |> Ecto.Changeset.change(%{skin_name: skin_name, unlocked_skins: new_unlocked})
              |> Repo.update!()
            end)
        end
    end
  end

  defp schema_for(:plot), do: PlayerPlot
  defp schema_for(:vase), do: PlayerVase
  defp schema_for(:mallum_house), do: PlayerMallumHouse
end
