defmodule CampFireWeb.SeedsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin
  alias CampFire.Game.SeedConfig

  def mount(_params, _session, socket) do
    {:ok, assign(socket, active_tab: :seeds, seeds: Admin.list_seeds())}
  end

  def handle_params(%{"id" => id}, _uri, socket) do
    seed = Admin.get_seed!(id)

    form =
      seed
      |> SeedConfig.changeset(%{})
      |> to_form()

    recipe_json = Jason.encode!(seed.recipe || %{}, pretty: true)

    {:noreply,
     assign(socket,
       editing: seed,
       form: form,
       recipe_json: recipe_json
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, assign(socket, editing: nil, form: nil, recipe_json: nil)}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/seeds/#{id}/edit")}
  end

  def handle_event("cancel", _params, socket) do
    {:noreply, push_patch(socket, to: "/admin/seeds")}
  end

  def handle_event("new", _params, socket) do
    case Admin.create_seed(%{
           seed_name: "NewSeed_#{System.unique_integer([:positive])}",
           growth_duration_hours: 1.0,
           min_drops: 1,
           max_drops: 3,
           recipe: %{}
         }) do
      {:ok, seed} ->
        {:noreply,
         socket
         |> assign(seeds: Admin.list_seeds())
         |> push_patch(to: "/admin/seeds/#{seed.id}/edit")}

      {:error, _changeset} ->
        {:noreply, put_flash(socket, :error, "Failed to create seed")}
    end
  end

  def handle_event("save", %{"seed_config" => params}, socket) do
    seed = socket.assigns.editing
    recipe_json = Map.get(params, "recipe_json", "{}")

    case Jason.decode(recipe_json) do
      {:ok, recipe} ->
        attrs = Map.put(params, "recipe", recipe) |> Map.delete("recipe_json")

        case Admin.update_seed(seed, attrs) do
          {:ok, _seed} ->
            {:noreply,
             socket
             |> put_flash(:info, "Seed updated")
             |> assign(seeds: Admin.list_seeds())
             |> push_patch(to: "/admin/seeds")}

          {:error, changeset} ->
            {:noreply, assign(socket, form: to_form(changeset))}
        end

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid recipe JSON")}
    end
  end

  def handle_event("delete", %{"id" => id}, socket) do
    seed = Admin.get_seed!(id)

    case Admin.delete_seed(seed) do
      {:ok, _} ->
        {:noreply,
         socket
         |> put_flash(:info, "Seed deleted")
         |> assign(seeds: Admin.list_seeds())
         |> push_patch(to: "/admin/seeds")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete seed")}
    end
  end

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Seeds</h2>
        <button phx-click="new" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New Seed
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white border rounded-lg p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.seed_name}</h3>
          <.form for={@form} phx-submit="save" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Seed Name</label>
                <input
                  type="text"
                  name="seed_config[seed_name]"
                  value={@form[:seed_name].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Growth Duration (hours)</label>
                <input
                  type="number"
                  step="0.1"
                  name="seed_config[growth_duration_hours]"
                  value={@form[:growth_duration_hours].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Min Drops</label>
                <input
                  type="number"
                  name="seed_config[min_drops]"
                  value={@form[:min_drops].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Max Drops</label>
                <input
                  type="number"
                  name="seed_config[max_drops]"
                  value={@form[:max_drops].value}
                  class="mt-1 block w-full border rounded px-3 py-2"
                />
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Recipe (JSON)</label>
              <div id="recipe-editor" phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
                <div class="json-toolbar">
                  <button type="button" data-action="format">Format</button>
                  <button type="button" data-action="minify">Minify</button>
                </div>
                <textarea
                  name="seed_config[recipe_json]"
                  rows="10"
                  class="mt-1 block w-full border rounded px-3 py-2"
                >{@recipe_json}</textarea>
                <div class="json-error-msg"></div>
              </div>
            </div>
            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
                Save
              </button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">
                Cancel
              </button>
              <button
                type="button"
                phx-click="delete"
                phx-value-id={@editing.id}
                data-confirm="Delete this seed?"
                class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto"
              >
                Delete
              </button>
            </div>
          </.form>
        </div>
      <% end %>

      <table class="w-full bg-white border rounded-lg">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Seed Name</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Growth (hrs)</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Drops</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Recipe</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for seed <- @seeds do %>
            <tr class="hover:bg-gray-50">
              <td class="px-4 py-3 font-medium">{seed.seed_name}</td>
              <td class="px-4 py-3">{seed.growth_duration_hours}</td>
              <td class="px-4 py-3">{seed.min_drops}-{seed.max_drops}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{recipe_summary(seed.recipe)}</td>
              <td class="px-4 py-3">
                <button phx-click="edit" phx-value-id={seed.id} class="text-blue-600 hover:underline">
                  Edit
                </button>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp recipe_summary(nil), do: "none"
  defp recipe_summary(recipe) when recipe == %{}, do: "none"

  defp recipe_summary(recipe) do
    axes =
      recipe
      |> Enum.filter(fn {_k, v} -> is_map(v) and v["enabled"] == true end)
      |> Enum.map(fn {k, _} -> k end)

    case axes do
      [] -> "no axes"
      list -> Enum.join(list, ", ")
    end
  end
end
