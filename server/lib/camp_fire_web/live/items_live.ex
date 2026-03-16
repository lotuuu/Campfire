defmodule CampFireWeb.ItemsLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin
  alias CampFire.Game.SeedConfig

  @sub_tabs ~w(seeds pigments consumables skins)a
  @recipe_categories %{pigments: "Pigment"}
  @consumable_categories ~w(potion consumable)

  def mount(_params, _session, socket) do
    {:ok,
     socket
     |> assign(
       active_tab: :items,
       sub_tab: :seeds,
       seeds: Admin.list_seeds(),
       seed_names: Admin.list_seeds() |> Enum.map(& &1.item.item_key) |> Enum.sort(),
       recipes: Admin.list_recipes(),
       skins: Admin.list_skins(),
       consumable_items: load_consumable_items(),
       editing: nil,
       form: nil,
       recipe_json: nil,
       ingredients: [],
       skin_form: %{}
     )
     |> allow_upload(:icon,
       accept: ~w(.png),
       max_file_size: 512_000,
       max_entries: 1
     )}
  end

  def handle_params(%{"sub" => sub} = params, _uri, socket) do
    sub_tab = String.to_existing_atom(sub)

    socket =
      if sub_tab in @sub_tabs do
        assign(socket, sub_tab: sub_tab)
      else
        assign(socket, sub_tab: :seeds)
      end

    socket = handle_edit_params(params, socket)
    {:noreply, socket}
  end

  def handle_params(params, _uri, socket) do
    socket = handle_edit_params(params, socket)
    {:noreply, socket}
  end

  defp handle_edit_params(%{"id" => id}, %{assigns: %{sub_tab: :seeds}} = socket) do
    seed = Admin.get_seed!(id)
    form = seed |> SeedConfig.changeset(%{}) |> to_form()
    recipe_json = Jason.encode!(seed.recipe || %{}, pretty: true)
    assign(socket, editing: seed, form: form, recipe_json: recipe_json)
  end

  defp handle_edit_params(%{"id" => name}, %{assigns: %{sub_tab: :pigments}} = socket) do
    case Admin.get_recipe(name) do
      nil ->
        assign(socket, editing: nil, form: nil, ingredients: [])

      recipe ->
        ingredients =
          Enum.map(recipe["ingredients"] || [], fn ing ->
            %{item_key: ing["itemKey"] || "", count: to_string(ing["count"] || 1)}
          end)

        assign(socket,
          editing: %{name: name, recipe: recipe},
          ingredients: ingredients
        )
    end
  end

  defp handle_edit_params(%{"id" => name}, %{assigns: %{sub_tab: :skins}} = socket) do
    case Admin.get_skin(name) do
      nil ->
        assign(socket, editing: nil, skin_form: %{})

      skin ->
        assign(socket,
          editing: %{name: name, skin: skin},
          skin_form: %{
            name: name,
            building_type: skin["building_type"] || "plot",
            cost_item_key: skin["cost_item_key"] || "",
            cost_quantity: to_string(skin["cost_quantity"] || 1)
          }
        )
    end
  end

  defp handle_edit_params(%{"id" => id_str}, %{assigns: %{sub_tab: :consumables}} = socket) do
    case Integer.parse(id_str) do
      {id, _} ->
        case CampFire.Game.get_item(id) do
          nil -> assign(socket, editing: nil)
          item -> assign(socket, editing: item)
        end
      :error -> assign(socket, editing: nil)
    end
  end

  defp handle_edit_params(_params, socket) do
    assign(socket, editing: nil, form: nil, recipe_json: nil, ingredients: [], skin_form: %{})
  end

  # ---------------------------------------------------------------------------
  # Sub-tab navigation
  # ---------------------------------------------------------------------------

  def handle_event("switch_tab", %{"tab" => tab}, socket) do
    {:noreply,
     socket
     |> assign(editing: nil, form: nil, recipe_json: nil, ingredients: [], skin_form: %{})
     |> push_patch(to: "/admin/items/#{tab}")}
  end

  # ---------------------------------------------------------------------------
  # Seed events (ported from SeedsLive)
  # ---------------------------------------------------------------------------

  def handle_event("edit_seed", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/items/seeds/#{id}/edit")}
  end

  def handle_event("cancel", _params, socket) do
    {:noreply, push_patch(socket, to: "/admin/items/#{socket.assigns.sub_tab}")}
  end

  def handle_event("new_seed", _params, socket) do
    alias CampFire.Game.Item
    alias CampFire.Repo

    suffix = System.unique_integer([:positive])
    seed_key = "new_seed_#{suffix}"
    harvest_key = "new_harvest_#{suffix}"

    Repo.transaction(fn ->
      {:ok, seed_item} =
        %Item{}
        |> Item.changeset(%{item_key: seed_key, display_name: "New Seed #{suffix}", category: "seed"})
        |> Repo.insert()

      {:ok, harvest_item} =
        %Item{}
        |> Item.changeset(%{item_key: harvest_key, display_name: "New Harvest #{suffix}", category: "harvest"})
        |> Repo.insert()

      Admin.create_seed(%{
        item_id: seed_item.id,
        harvest_item_id: harvest_item.id,
        growth_duration_hours: 1.0,
        min_drops: 1,
        max_drops: 3,
        recipe: %{}
      })
    end)
    |> case do
      {:ok, {:ok, seed}} ->
        {:noreply,
         socket
         |> refresh_seeds()
         |> push_patch(to: "/admin/items/seeds/#{seed.id}/edit")}

      _ ->
        {:noreply, put_flash(socket, :error, "Failed to create seed")}
    end
  end

  def handle_event("save_seed", %{"seed_config" => params}, socket) do
    # Upload icon if provided
    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      seed = socket.assigns.editing
      plant_slug = seed.harvest_item.item_key
      key = "items/#{String.downcase(plant_slug)}/seed"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)

    seed = socket.assigns.editing
    recipe_json = Map.get(params, "recipe_json", "{}")

    case Jason.decode(recipe_json) do
      {:ok, recipe} ->
        attrs =
          params
          |> Map.put("recipe", recipe)
          |> Map.delete("recipe_json")

        case Admin.update_seed(seed, attrs) do
          {:ok, _seed} ->
            CampFire.ConfigCache.refresh()

            {:noreply,
             socket
             |> put_flash(:info, "Seed updated")
             |> refresh_seeds()
             |> push_patch(to: "/admin/items/seeds")}

          {:error, changeset} ->
            {:noreply, assign(socket, form: to_form(changeset))}
        end

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid recipe JSON")}
    end
  end

  def handle_event("delete_seed", %{"id" => id}, socket) do
    seed = Admin.get_seed!(id)

    case Admin.delete_seed(seed) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Seed deleted")
         |> refresh_seeds()
         |> push_patch(to: "/admin/items/seeds")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete seed")}
    end
  end

  # ---------------------------------------------------------------------------
  # Recipe events (pigments, potions, fertilizer)
  # ---------------------------------------------------------------------------

  def handle_event("edit_recipe", %{"name" => name}, socket) do
    {:noreply, push_patch(socket, to: "/admin/items/#{socket.assigns.sub_tab}/#{URI.encode(name)}/edit")}
  end

  def handle_event("new_recipe", _params, socket) do
    sub_tab = socket.assigns.sub_tab
    category = Map.fetch!(@recipe_categories, sub_tab)
    name = "New_#{category}_#{System.unique_integer([:positive])}"

    recipe = %{
      "ingredients" => [],
      "result_item" => name,
      "result_quantity" => 1,
      "category" => category
    }

    case Admin.upsert_recipe(name, recipe) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> assign(recipes: Admin.list_recipes())
         |> push_patch(to: "/admin/items/#{sub_tab}/#{URI.encode(name)}/edit")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to create recipe")}
    end
  end

  def handle_event("save_recipe", params, socket) do
    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      result_item = socket.assigns.editing.recipe["result_item"] || socket.assigns.editing.name
      key = "items/#{String.downcase(result_item)}"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)

    old_name = socket.assigns.editing.name
    new_name = String.trim(params["recipe_name"] || old_name)
    result_item = String.trim(params["result_item"] || new_name)
    result_qty = parse_int(params["result_quantity"] || "1")
    sub_tab = socket.assigns.sub_tab
    category = Map.fetch!(@recipe_categories, sub_tab)

    ingredients =
      Enum.map(socket.assigns.ingredients, fn ing ->
        %{"itemKey" => ing.item_key, "count" => parse_int(ing.count)}
      end)
      |> Enum.reject(fn ing -> ing["itemKey"] == "" end)

    recipe = %{
      "ingredients" => ingredients,
      "result_item" => result_item,
      "result_quantity" => result_qty,
      "category" => category
    }

    result =
      if new_name != old_name do
        Admin.rename_recipe(old_name, new_name, recipe)
      else
        Admin.upsert_recipe(new_name, recipe)
      end

    case result do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Recipe saved")
         |> assign(recipes: Admin.list_recipes())
         |> push_patch(to: "/admin/items/#{sub_tab}")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save recipe")}
    end
  end

  def handle_event("delete_recipe", %{"name" => name}, socket) do
    case Admin.delete_recipe(name) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Recipe deleted")
         |> assign(recipes: Admin.list_recipes())
         |> push_patch(to: "/admin/items/#{socket.assigns.sub_tab}")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete recipe")}
    end
  end

  def handle_event("add_ingredient", _params, socket) do
    ingredients = socket.assigns.ingredients ++ [%{item_key: "", count: "1"}]
    {:noreply, assign(socket, ingredients: ingredients)}
  end

  def handle_event("remove_ingredient", %{"index" => index}, socket) do
    idx = String.to_integer(index)
    ingredients = List.delete_at(socket.assigns.ingredients, idx)
    {:noreply, assign(socket, ingredients: ingredients)}
  end

  def handle_event("update_ingredient", %{"index" => index, "field" => field, "value" => value}, socket) do
    idx = String.to_integer(index)
    field_atom = String.to_existing_atom(field)
    ingredients = List.update_at(socket.assigns.ingredients, idx, &Map.put(&1, field_atom, value))
    {:noreply, assign(socket, ingredients: ingredients)}
  end

  # ---------------------------------------------------------------------------
  # Consumable events
  # ---------------------------------------------------------------------------

  def handle_event("edit_consumable", %{"id" => id}, socket) do
    {:noreply, push_patch(socket, to: "/admin/items/consumables/#{id}/edit")}
  end

  def handle_event("new_consumable", _params, socket) do
    alias CampFire.Game.Item
    alias CampFire.Repo

    suffix = System.unique_integer([:positive])
    key = "new_consumable_#{suffix}"

    case %Item{}
         |> Item.changeset(%{item_key: key, display_name: "New Consumable #{suffix}", category: "consumable"})
         |> Repo.insert() do
      {:ok, item} ->
        CampFire.ConfigCache.refresh()
        {:noreply,
         socket
         |> assign(consumable_items: load_consumable_items())
         |> push_patch(to: "/admin/items/consumables/#{item.id}/edit")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to create consumable")}
    end
  end

  def handle_event("save_consumable", params, socket) do
    alias CampFire.Game.Item

    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      item = socket.assigns.editing
      key = "items/#{String.downcase(item.item_key)}"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)

    item = socket.assigns.editing
    attrs = %{
      item_key: String.trim(params["item_key"] || item.item_key),
      display_name: String.trim(params["display_name"] || item.display_name),
      category: params["category"] || item.category
    }

    case item |> Item.changeset(attrs) |> CampFire.Repo.update() do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()
        {:noreply,
         socket
         |> put_flash(:info, "Consumable saved")
         |> assign(consumable_items: load_consumable_items())
         |> push_patch(to: "/admin/items/consumables")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save consumable")}
    end
  end

  def handle_event("delete_consumable", %{"id" => id}, socket) do
    alias CampFire.Game.Item

    case CampFire.Repo.get(Item, id) do
      nil ->
        {:noreply, put_flash(socket, :error, "Item not found")}

      item ->
        case CampFire.Repo.delete(item) do
          {:ok, _} ->
            CampFire.ConfigCache.refresh()
            {:noreply,
             socket
             |> put_flash(:info, "Consumable deleted")
             |> assign(consumable_items: load_consumable_items())
             |> push_patch(to: "/admin/items/consumables")}

          {:error, _} ->
            {:noreply, put_flash(socket, :error, "Failed to delete consumable")}
        end
    end
  end

  # ---------------------------------------------------------------------------
  # Skin events
  # ---------------------------------------------------------------------------

  def handle_event("edit_skin", %{"name" => name}, socket) do
    {:noreply, push_patch(socket, to: "/admin/items/skins/#{URI.encode(name)}/edit")}
  end

  def handle_event("new_skin", _params, socket) do
    name = "NewSkin_#{System.unique_integer([:positive])}"

    skin = %{
      "building_type" => "plot",
      "cost_item_key" => "",
      "cost_quantity" => 1
    }

    case Admin.upsert_skin(name, skin) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> assign(skins: Admin.list_skins())
         |> push_patch(to: "/admin/items/skins/#{URI.encode(name)}/edit")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to create skin")}
    end
  end

  def handle_event("save_skin", params, socket) do
    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      old_name = socket.assigns.editing.name
      key = "skins/#{String.downcase(old_name)}"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)

    old_name = socket.assigns.editing.name
    new_name = String.trim(params["skin_name"] || old_name)

    skin = %{
      "building_type" => params["building_type"] || "plot",
      "cost_item_key" => String.trim(params["cost_item_key"] || ""),
      "cost_quantity" => parse_int(params["cost_quantity"] || "1")
    }

    result =
      if new_name != old_name do
        Admin.rename_skin(old_name, new_name, skin)
      else
        Admin.upsert_skin(new_name, skin)
      end

    case result do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Skin saved")
         |> assign(skins: Admin.list_skins())
         |> push_patch(to: "/admin/items/skins")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save skin")}
    end
  end

  def handle_event("delete_skin", %{"name" => name}, socket) do
    case Admin.delete_skin(name) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Skin deleted")
         |> assign(skins: Admin.list_skins())
         |> push_patch(to: "/admin/items/skins")}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete skin")}
    end
  end

  def handle_event("update_skin_field", %{"field" => field, "value" => value}, socket) do
    skin_form = Map.put(socket.assigns.skin_form, String.to_existing_atom(field), value)
    {:noreply, assign(socket, skin_form: skin_form)}
  end

  # ---------------------------------------------------------------------------
  # Helpers
  # ---------------------------------------------------------------------------

  defp load_consumable_items do
    alias CampFire.Game
    Enum.flat_map(@consumable_categories, &Game.list_items_by_category/1)
    |> Enum.sort_by(& &1.item_key)
  end

  defp refresh_seeds(socket) do
    seeds = Admin.list_seeds()
    assign(socket,
      seeds: seeds,
      seed_names: Enum.map(seeds, & &1.item.item_key) |> Enum.sort()
    )
  end

  defp parse_int(val) when is_binary(val) do
    case Integer.parse(val) do
      {i, _} -> i
      :error -> 0
    end
  end

  defp parse_int(val) when is_integer(val), do: val
  defp parse_int(_), do: 0

  defp format_duration(hours) when is_number(hours) do
    total_seconds = round(hours * 3600)
    h = div(total_seconds, 3600)
    m = div(rem(total_seconds, 3600), 60)
    s = rem(total_seconds, 60)

    parts =
      [{h, "h"}, {m, "m"}, {s, "s"}]
      |> Enum.reject(fn {v, _} -> v == 0 end)
      |> Enum.map(fn {v, u} -> "#{v}#{u}" end)

    case parts do
      [] -> "0s"
      _ -> Enum.join(parts, " ")
    end
  end

  defp format_duration(_), do: "—"

  defp recipes_for_category(recipes, category) do
    recipes
    |> Enum.filter(fn {_name, r} -> r["category"] == category end)
    |> Enum.sort_by(fn {name, _} -> name end)
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

  # ---------------------------------------------------------------------------
  # Render
  # ---------------------------------------------------------------------------

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-2xl font-bold">Items</h2>
      </div>

      <%!-- Sub-tab bar --%>
      <div class="flex border-b mb-6">
        <%= for tab <- ~w(seeds pigments consumables fertilizer skins)a do %>
          <button
            phx-click="switch_tab"
            phx-value-tab={tab}
            class={"px-4 py-2 text-sm font-medium border-b-2 -mb-px #{if @sub_tab == tab, do: "border-blue-600 text-blue-600", else: "border-transparent text-gray-500 hover:text-gray-700"}"}
          >
            {tab |> Atom.to_string() |> String.capitalize()}
          </button>
        <% end %>
      </div>

      <%!-- Content --%>
      <%= case @sub_tab do %>
        <% :seeds -> %>
          {render_seeds(assigns)}
        <% tab when tab in [:pigments, :fertilizer] -> %>
          {render_recipes(assigns)}
        <% :consumables -> %>
          {render_consumables(assigns)}
        <% :skins -> %>
          {render_skins(assigns)}
      <% end %>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Seeds sub-tab
  # ---------------------------------------------------------------------------

  defp render_seeds(assigns) do
    ~H"""
    <div>
      <div class="flex justify-end mb-4">
        <button phx-click="new_seed" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New Seed
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white border rounded-lg p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.item.item_key}</h3>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center overflow-hidden">
              <img
                src={CampFire.Sprites.sprite_url("items/#{String.downcase(@editing.harvest_item.item_key)}/seed")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
          <.form for={@form} phx-submit="save_seed" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Seed Item Key</label>
                <input type="text" value={@editing.item.item_key} disabled
                  class="mt-1 block w-full border rounded px-3 py-2 bg-gray-100 text-gray-500" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Harvest Item Key</label>
                <input type="text" value={@editing.harvest_item.item_key} disabled
                  class="mt-1 block w-full border rounded px-3 py-2 bg-gray-100 text-gray-500" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Growth Duration (hours)</label>
                <input type="number" step="0.1" name="seed_config[growth_duration_hours]" value={@form[:growth_duration_hours].value}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Min Drops</label>
                <input type="number" name="seed_config[min_drops]" value={@form[:min_drops].value}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Max Drops</label>
                <input type="number" name="seed_config[max_drops]" value={@form[:max_drops].value}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Recipe (JSON)</label>
              <div id="recipe-editor" phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
                <div class="json-toolbar">
                  <button type="button" data-action="format">Format</button>
                  <button type="button" data-action="minify">Minify</button>
                </div>
                <textarea name="seed_config[recipe_json]" rows="10"
                  class="mt-1 block w-full border rounded px-3 py-2">{@recipe_json}</textarea>
                <div class="json-error-msg"></div>
              </div>
            </div>
            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
              <button type="button" phx-click="delete_seed" phx-value-id={@editing.id} data-confirm="Delete this seed?"
                class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto">Delete</button>
            </div>
          </.form>
        </div>
      <% end %>

      <table class="w-full bg-white border rounded-lg">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-4 py-3 w-12"></th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Seed Item</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Harvest Item</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Growth Time</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Drops</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Recipe</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for seed <- Enum.sort_by(@seeds, & &1.growth_duration_hours) do %>
            <tr class="hover:bg-gray-50">
              <td class="px-4 py-3">
                <img src={CampFire.Sprites.sprite_url("items/#{String.downcase(seed.harvest_item.item_key)}/seed")}
                  class="w-8 h-8 object-contain" onerror="this.style.display='none'" />
              </td>
              <td class="px-4 py-3 font-medium">{seed.item.item_key}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{seed.harvest_item.item_key}</td>
              <td class="px-4 py-3">{format_duration(seed.growth_duration_hours)}</td>
              <td class="px-4 py-3">{seed.min_drops}-{seed.max_drops}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{recipe_summary(seed.recipe)}</td>
              <td class="px-4 py-3">
                <button phx-click="edit_seed" phx-value-id={seed.id} class="text-blue-600 hover:underline">Edit</button>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Recipes sub-tab (pigments, potions, fertilizer)
  # ---------------------------------------------------------------------------

  defp render_recipes(assigns) do
    category = Map.fetch!(@recipe_categories, assigns.sub_tab)
    items = recipes_for_category(assigns.recipes, category)
    assigns = assign(assigns, items: items, category: category)

    ~H"""
    <div>
      <div class="flex justify-end mb-4">
        <button phx-click="new_recipe" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New {@category}
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white border rounded-lg p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.name}</h3>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center overflow-hidden">
              <img
                src={CampFire.Sprites.sprite_url("items/#{String.downcase(@editing.recipe["result_item"] || @editing.name)}")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
          <form phx-submit="save_recipe" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Recipe Name</label>
                <input type="text" name="recipe_name" value={@editing.name}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Result Item</label>
                <input type="text" name="result_item" value={@editing.recipe["result_item"]}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Result Quantity</label>
                <input type="number" name="result_quantity" value={@editing.recipe["result_quantity"] || 1} min="1"
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
            </div>

            <div>
              <div class="flex justify-between items-center mb-2">
                <label class="block text-sm font-medium text-gray-700">Ingredients</label>
                <button type="button" phx-click="add_ingredient"
                  class="text-sm bg-green-100 text-green-700 px-3 py-1 rounded hover:bg-green-200">
                  + Add Ingredient
                </button>
              </div>

              <%= if @ingredients == [] do %>
                <p class="text-sm text-gray-400 italic py-4 text-center">No ingredients yet.</p>
              <% else %>
                <div class="border rounded-lg overflow-hidden">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-50">
                      <tr>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Item Name</th>
                        <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 w-24">Count</th>
                        <th class="px-3 py-2 w-10"></th>
                      </tr>
                    </thead>
                    <tbody class="divide-y">
                      <%= for {ing, idx} <- Enum.with_index(@ingredients) do %>
                        <tr class="hover:bg-gray-50">
                          <td class="px-3 py-2">
                            <input
                              id={"ing-#{idx}-item_key"}
                              type="text"
                              value={ing.item_key}
                              phx-hook="RewardInput"
                              data-index={idx}
                              data-field="item_key"
                              data-event="update_ingredient"
                              class="w-full border rounded px-2 py-1 text-sm"
                              placeholder="e.g. Basil"
                            />
                          </td>
                          <td class="px-3 py-2">
                            <input
                              id={"ing-#{idx}-count"}
                              type="number"
                              value={ing.count}
                              phx-hook="RewardInput"
                              data-index={idx}
                              data-field="count"
                              data-event="update_ingredient"
                              min="1"
                              class="w-full border rounded px-2 py-1 text-sm"
                            />
                          </td>
                          <td class="px-3 py-2">
                            <button type="button" phx-click="remove_ingredient" phx-value-index={idx}
                              class="text-red-400 hover:text-red-600 text-lg leading-none" title="Remove">&times;</button>
                          </td>
                        </tr>
                      <% end %>
                    </tbody>
                  </table>
                </div>
              <% end %>
            </div>

            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
              <button type="button" phx-click="delete_recipe" phx-value-name={@editing.name} data-confirm="Delete this recipe?"
                class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto">Delete</button>
            </div>
          </form>
        </div>
      <% end %>

      <table class="w-full bg-white border rounded-lg">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-4 py-3 w-12"></th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Name</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Ingredients</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Result</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for {name, recipe} <- @items do %>
            <tr class="hover:bg-gray-50">
              <td class="px-4 py-3">
                <img src={CampFire.Sprites.sprite_url("items/#{String.downcase(recipe["result_item"] || name)}")}
                  class="w-8 h-8 object-contain" onerror="this.style.display='none'" />
              </td>
              <td class="px-4 py-3 font-medium">{name}</td>
              <td class="px-4 py-3 text-sm text-gray-500">
                <%= for ing <- recipe["ingredients"] || [] do %>
                  <span class="inline-block bg-gray-100 rounded px-2 py-0.5 mr-1 mb-1">
                    {ing["itemKey"]} x{ing["count"]}
                  </span>
                <% end %>
              </td>
              <td class="px-4 py-3 text-sm">{recipe["result_item"]} x{recipe["result_quantity"]}</td>
              <td class="px-4 py-3">
                <button phx-click="edit_recipe" phx-value-name={name} class="text-blue-600 hover:underline">Edit</button>
              </td>
            </tr>
          <% end %>
          <%= if @items == [] do %>
            <tr><td colspan="5" class="px-4 py-6 text-center text-gray-400 italic">No items configured yet.</td></tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Consumables sub-tab
  # ---------------------------------------------------------------------------

  defp render_consumables(assigns) do
    ~H"""
    <div>
      <div class="flex justify-end mb-4">
        <button phx-click="new_consumable" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New Consumable
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white border rounded-lg p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.item_key}</h3>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center overflow-hidden">
              <img
                src={CampFire.Sprites.sprite_url("items/#{String.downcase(@editing.item_key)}")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
          <form phx-submit="save_consumable" class="space-y-4">
            <div class="grid grid-cols-3 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Item Key</label>
                <input type="text" name="item_key" value={@editing.item_key}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Display Name</label>
                <input type="text" name="display_name" value={@editing.display_name}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Category</label>
                <select name="category" class="mt-1 block w-full border rounded px-3 py-2">
                  <option value="consumable" selected={@editing.category == "consumable"}>consumable</option>
                  <option value="potion" selected={@editing.category == "potion"}>potion</option>
                </select>
              </div>
            </div>

            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
              <button type="button" phx-click="delete_consumable" phx-value-id={@editing.id} data-confirm="Delete this item?"
                class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto">Delete</button>
            </div>
          </form>
        </div>
      <% end %>

      <table class="w-full bg-white border rounded-lg">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-4 py-3 w-12"></th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Item Key</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Display Name</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Category</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for item <- @consumable_items do %>
            <tr class="hover:bg-gray-50">
              <td class="px-4 py-3">
                <img src={CampFire.Sprites.sprite_url("items/#{String.downcase(item.item_key)}")}
                  class="w-8 h-8 object-contain" onerror="this.style.display='none'" />
              </td>
              <td class="px-4 py-3 font-medium">{item.item_key}</td>
              <td class="px-4 py-3">{item.display_name}</td>
              <td class="px-4 py-3 text-sm">
                <span class={"inline-block rounded px-2 py-0.5 text-xs font-medium #{if item.category == "potion", do: "bg-purple-100 text-purple-700", else: "bg-blue-100 text-blue-700"}"}>
                  {item.category}
                </span>
              </td>
              <td class="px-4 py-3">
                <button phx-click="edit_consumable" phx-value-id={item.id} class="text-blue-600 hover:underline">Edit</button>
              </td>
            </tr>
          <% end %>
          <%= if @consumable_items == [] do %>
            <tr><td colspan="5" class="px-4 py-6 text-center text-gray-400 italic">No consumable items yet.</td></tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  # ---------------------------------------------------------------------------
  # Skins sub-tab
  # ---------------------------------------------------------------------------

  defp render_skins(assigns) do
    skin_list = assigns.skins |> Enum.sort_by(fn {name, _} -> name end)
    assigns = assign(assigns, skin_list: skin_list)

    ~H"""
    <div>
      <div class="flex justify-end mb-4">
        <button phx-click="new_skin" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
          + New Skin
        </button>
      </div>

      <%= if @editing do %>
        <div class="bg-white border rounded-lg p-6 mb-6">
          <h3 class="text-lg font-semibold mb-4">Edit: {@editing.name}</h3>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center overflow-hidden">
              <img
                src={CampFire.Sprites.sprite_url("skins/#{String.downcase(@editing.name)}")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
          <form phx-submit="save_skin" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700">Skin Name</label>
                <input type="text" name="skin_name" value={@skin_form[:name]}
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Building Type</label>
                <select name="building_type" class="mt-1 block w-full border rounded px-3 py-2">
                  <%= for bt <- ~w(plot vase mallum_house) do %>
                    <option value={bt} selected={bt == @skin_form[:building_type]}>{bt}</option>
                  <% end %>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Cost Item</label>
                <input type="text" name="cost_item_key" value={@skin_form[:cost_item_key]}
                  class="mt-1 block w-full border rounded px-3 py-2" placeholder="e.g. Basil_Pigment" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700">Cost Quantity</label>
                <input type="number" name="cost_quantity" value={@skin_form[:cost_quantity]} min="1"
                  class="mt-1 block w-full border rounded px-3 py-2" />
              </div>
            </div>

            <div class="flex gap-2">
              <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
              <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
              <button type="button" phx-click="delete_skin" phx-value-name={@editing.name} data-confirm="Delete this skin?"
                class="bg-red-600 text-white px-4 py-2 rounded hover:bg-red-700 ml-auto">Delete</button>
            </div>
          </form>
        </div>
      <% end %>

      <table class="w-full bg-white border rounded-lg">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-4 py-3 w-12"></th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Skin Name</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Building Type</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500">Cost</th>
            <th class="px-4 py-3 text-left text-sm font-medium text-gray-500"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for {name, skin} <- @skin_list do %>
            <tr class="hover:bg-gray-50">
              <td class="px-4 py-3">
                <img src={CampFire.Sprites.sprite_url("skins/#{String.downcase(name)}")}
                  class="w-8 h-8 object-contain" onerror="this.style.display='none'" />
              </td>
              <td class="px-4 py-3 font-medium">{name}</td>
              <td class="px-4 py-3 text-sm">{skin["building_type"]}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{skin["cost_item_key"]} x{skin["cost_quantity"]}</td>
              <td class="px-4 py-3">
                <button phx-click="edit_skin" phx-value-name={name} class="text-blue-600 hover:underline">Edit</button>
              </td>
            </tr>
          <% end %>
          <%= if @skin_list == [] do %>
            <tr><td colspan="4" class="px-4 py-6 text-center text-gray-400 italic">No skins configured yet.</td></tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end
end
