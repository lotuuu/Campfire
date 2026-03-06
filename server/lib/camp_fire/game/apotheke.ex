defmodule CampFire.Game.Apotheke do
  alias CampFire.Repo
  alias CampFire.Economy
  alias CampFire.ConfigCache

  def craft(player_uid, recipe_name, opts \\ []) do
    recipes = ConfigCache.get("recipe_configs") || %{}

    case Map.get(recipes, recipe_name) do
      nil ->
        {:error, :unknown_recipe}

      recipe ->
        Repo.transaction(fn ->
          Enum.each(recipe["ingredients"], fn %{"item_name" => name, "count" => count} ->
            case Economy.spend_item(player_uid, name, count, opts) do
              {:ok, _} -> :ok
              {:error, reason} -> Repo.rollback(reason)
            end
          end)

          Economy.upsert_item(player_uid, recipe["result_item"], recipe["result_quantity"])

          %{result_item: recipe["result_item"], result_quantity: recipe["result_quantity"]}
        end)
    end
  end
end
