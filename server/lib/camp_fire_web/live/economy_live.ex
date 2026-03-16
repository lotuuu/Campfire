defmodule CampFireWeb.EconomyLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  @known_keys ~w(flame_config vase_config mallum_house_config bird_config plot_config new_player_config recipe_configs skin_configs)

  def mount(_params, _session, socket) do
    {:ok,
     assign(socket,
       active_tab: :economy,
       configs: Admin.list_game_configs(),
       editing_key: nil,
       edit_data: nil,
       edit_json: nil
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, socket}
  end

  # --- Edit ---

  def handle_event("edit", %{"id" => id}, socket) do
    config = Admin.get_game_config!(id)

    if config.key in @known_keys do
      edit_data =
        case config.key do
          key when key in ~w(recipe_configs skin_configs) ->
            entries =
              config.value
              |> Enum.sort_by(fn {k, _v} -> k end)
              |> Enum.to_list()

            %{"_entries" => entries}

          _ ->
            config.value
        end

      {:noreply, assign(socket, editing_key: config.key, edit_data: edit_data)}
    else
      json = Jason.encode!(config.value, pretty: true)
      {:noreply, assign(socket, editing_key: config.key, edit_json: json)}
    end
  end

  def handle_event("cancel", _params, socket) do
    {:noreply, assign(socket, editing_key: nil, edit_data: nil, edit_json: nil)}
  end

  # --- Structured saves ---

  def handle_event("save_flame", params, socket) do
    upgrade_recipes =
      (params["recipe"] || %{})
      |> Enum.sort_by(fn {k, _v} -> parse_int(k) end)
      |> Enum.map(fn {_idx, recipe_params} ->
        ingredients =
          (recipe_params["ingredient"] || %{})
          |> Enum.sort_by(fn {k, _} -> parse_int(k) end)
          |> Enum.map(fn {_i, ing} ->
            %{"itemKey" => ing["itemKey"], "count" => parse_int(ing["count"])}
          end)
          |> Enum.reject(fn ing -> ing["itemKey"] == "" or ing["itemKey"] == nil end)

        %{"ingredients" => ingredients}
      end)

    value = %{
      "max_flame_level" => parse_int(params["max_flame_level"]),
      "mana_rates" => parse_float_list(params, "mana_rate"),
      "mana_caps" => parse_int_list(params, "mana_cap"),
      "entity_caps" => parse_int_list(params, "entity_cap"),
      "grid_sizes" => parse_int_list(params, "grid_size"),
      "upgrade_recipes" => upgrade_recipes
    }

    save_config("flame_config", value, socket)
  end

  def handle_event("save_vase", params, socket) do
    value = %{
      "default_capacity" => parse_int(params["default_capacity"]),
      "craft_cost_mana" => parse_int(params["craft_cost_mana"]),
      "fill_duration_minutes" => parse_int(params["fill_duration_minutes"]),
      "capacity_tiers" => parse_int_list(params, "capacity_tier"),
      # First entry is a hidden 0 for the base tier — drop it
      "upgrade_costs" => parse_int_list(params, "upgrade_cost") |> Enum.drop(1)
    }

    save_config("vase_config", value, socket)
  end

  def handle_event("save_mallum_house", params, socket) do
    mallums_per_house = parse_int(params["mallums_per_house"])

    house_costs =
      (params["house_cost"] || %{})
      |> Enum.sort_by(fn {k, _v} -> parse_int(k) end)
      |> Enum.map(fn {_idx, cost_params} ->
        harvestCosts =
          (cost_params["harvest"] || %{})
          |> Enum.sort_by(fn {k, _} -> parse_int(k) end)
          |> Enum.map(fn {_i, h} ->
            %{"itemKey" => h["item"], "count" => parse_int(h["count"])}
          end)
          |> Enum.reject(fn h -> h["itemKey"] == "" or h["itemKey"] == nil end)

        %{"manaCost" => parse_int(cost_params["mana"]), "harvestCosts" => harvestCosts}
      end)

    value = %{
      "mallums_per_house" => mallums_per_house,
      "house_costs" => house_costs
    }

    save_config("mallum_house_config", value, socket)
  end

  def handle_event("save_building_cost", params, socket) do
    plot_costs = parse_cost_list(params, "plot_cost")
    vase_costs = parse_cost_list(params, "vase_cost")
    garden_costs = parse_cost_list(params, "garden_cost")

    value = %{
      "plot_costs" => plot_costs,
      "vase_costs" => vase_costs,
      "garden_costs" => garden_costs
    }

    # Merge building costs into the existing flame_config
    existing = CampFire.ConfigCache.get("flame_config") || %{}
    merged = Map.merge(existing, value)
    save_config("flame_config", merged, socket)
  end

  # Add/remove rows for flame config
  def handle_event("add_flame_level", _params, socket) do
    data = socket.assigns.edit_data
    caps = (data["entity_caps"] || []) ++ [0]
    sizes = (data["grid_sizes"] || []) ++ [2]
    rates = (data["mana_rates"] || []) ++ [0.0]
    mana_caps = (data["mana_caps"] || []) ++ [0]
    recipes = (data["upgrade_recipes"] || []) ++ [%{"ingredients" => []}]
    data = data |> Map.put("entity_caps", caps) |> Map.put("grid_sizes", sizes) |> Map.put("mana_rates", rates) |> Map.put("mana_caps", mana_caps) |> Map.put("upgrade_recipes", recipes)
    {:noreply, assign(socket, edit_data: data)}
  end

  def handle_event("remove_flame_level", %{"index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    caps = List.delete_at(data["entity_caps"] || [], i)
    sizes = List.delete_at(data["grid_sizes"] || [], i)
    rates = List.delete_at(data["mana_rates"] || [], i)
    mana_caps = List.delete_at(data["mana_caps"] || [], i)
    recipes = List.delete_at(data["upgrade_recipes"] || [], i)
    data = data |> Map.put("entity_caps", caps) |> Map.put("grid_sizes", sizes) |> Map.put("mana_rates", rates) |> Map.put("mana_caps", mana_caps) |> Map.put("upgrade_recipes", recipes)
    {:noreply, assign(socket, edit_data: data)}
  end

  # Add/remove flame recipe ingredients
  def handle_event("add_recipe_ingredient", %{"recipe-index" => ri}, socket) do
    i = parse_int(ri)
    data = socket.assigns.edit_data
    recipes = data["upgrade_recipes"] || []
    recipe = Enum.at(recipes, i)
    ingredients = (recipe["ingredients"] || []) ++ [%{"itemKey" => "", "count" => 1}]
    recipe = Map.put(recipe, "ingredients", ingredients)
    recipes = List.replace_at(recipes, i, recipe)
    {:noreply, assign(socket, edit_data: Map.put(data, "upgrade_recipes", recipes))}
  end

  def handle_event("remove_recipe_ingredient", %{"recipe-index" => ri, "ingredient-index" => ii}, socket) do
    i = parse_int(ri)
    j = parse_int(ii)
    data = socket.assigns.edit_data
    recipes = data["upgrade_recipes"] || []
    recipe = Enum.at(recipes, i)
    ingredients = List.delete_at(recipe["ingredients"] || [], j)
    recipe = Map.put(recipe, "ingredients", ingredients)
    recipes = List.replace_at(recipes, i, recipe)
    {:noreply, assign(socket, edit_data: Map.put(data, "upgrade_recipes", recipes))}
  end

  # Add/remove rows for vase tiers
  def handle_event("add_capacity_tier", _params, socket) do
    data = socket.assigns.edit_data
    tiers = (data["capacity_tiers"] || []) ++ [0]
    costs = (data["upgrade_costs"] || []) ++ [0]
    data = data |> Map.put("capacity_tiers", tiers) |> Map.put("upgrade_costs", costs)
    {:noreply, assign(socket, edit_data: data)}
  end

  def handle_event("remove_capacity_tier", %{"index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    tiers = List.delete_at(data["capacity_tiers"] || [], i)
    costs = List.delete_at(data["upgrade_costs"] || [], i)
    data = data |> Map.put("capacity_tiers", tiers) |> Map.put("upgrade_costs", costs)
    {:noreply, assign(socket, edit_data: data)}
  end

  # Add/remove house costs
  def handle_event("add_house_cost", _params, socket) do
    data = socket.assigns.edit_data
    costs = (data["house_costs"] || []) ++ [%{"manaCost" => 0, "harvestCosts" => []}]
    {:noreply, assign(socket, edit_data: Map.put(data, "house_costs", costs))}
  end

  def handle_event("remove_house_cost", %{"index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    costs = List.delete_at(data["house_costs"] || [], i)
    {:noreply, assign(socket, edit_data: Map.put(data, "house_costs", costs))}
  end

  def handle_event("add_harvest", %{"house-index" => hi}, socket) do
    i = parse_int(hi)
    data = socket.assigns.edit_data
    costs = data["house_costs"] || []
    house = Enum.at(costs, i)
    harvests = (house["harvestCosts"] || []) ++ [%{"itemKey" => "", "count" => 1}]
    house = Map.put(house, "harvestCosts", harvests)
    costs = List.replace_at(costs, i, house)
    {:noreply, assign(socket, edit_data: Map.put(data, "house_costs", costs))}
  end

  def handle_event("remove_harvest", %{"house-index" => hi, "harvest-index" => hvi}, socket) do
    i = parse_int(hi)
    j = parse_int(hvi)
    data = socket.assigns.edit_data
    costs = data["house_costs"] || []
    house = Enum.at(costs, i)
    harvests = List.delete_at(house["harvestCosts"] || [], j)
    house = Map.put(house, "harvestCosts", harvests)
    costs = List.replace_at(costs, i, house)
    {:noreply, assign(socket, edit_data: Map.put(data, "house_costs", costs))}
  end

  # --- Bird config save ---
  def handle_event("save_bird", params, socket) do
    value = %{
      "spawn_base_chance" => parse_float(params["spawn_base_chance"]),
      "spawn_decay" => parse_float(params["spawn_decay"])
    }

    save_config("bird_config", value, socket)
  end

  # --- Plot config save ---
  def handle_event("save_plot", params, socket) do
    value = %{
      "water_cooldown_seconds" => parse_int(params["water_cooldown_seconds"]),
      "rain_water_cooldown_seconds" => parse_int(params["rain_water_cooldown_seconds"]),
      "rain_trigger_minutes" => parse_int(params["rain_trigger_minutes"]),
      "drop_spread_factor" => parse_float(params["drop_spread_factor"]),
      "speed_item" => params["speed_item"] || ""
    }

    save_config("plot_config", value, socket)
  end

  # --- New player config save ---
  def handle_event("save_new_player", params, socket) do
    items =
      (params["item"] || %{})
      |> Enum.sort_by(fn {k, _v} -> parse_int(k) end)
      |> Enum.map(fn {_idx, item_params} ->
        %{"itemKey" => item_params["itemKey"], "count" => parse_int(item_params["count"])}
      end)
      |> Enum.reject(fn item -> item["itemKey"] == "" or item["itemKey"] == nil end)

    value = %{
      "mana" => parse_float(params["mana"]),
      "gems" => parse_int(params["gems"]),
      "starting_water" => parse_int(params["starting_water"]),
      "items" => items
    }

    save_config("new_player_config", value, socket)
  end

  def handle_event("add_new_player_item", _params, socket) do
    data = socket.assigns.edit_data
    items = (data["items"] || []) ++ [%{"itemKey" => "", "count" => 1}]
    {:noreply, assign(socket, edit_data: Map.put(data, "items", items))}
  end

  def handle_event("remove_new_player_item", %{"index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    items = List.delete_at(data["items"] || [], i)
    {:noreply, assign(socket, edit_data: Map.put(data, "items", items))}
  end

  # --- Recipe configs save ---
  def handle_event("save_recipe", params, socket) do
    recipes =
      (params["recipe"] || %{})
      |> Enum.sort_by(fn {k, _v} -> k end)
      |> Enum.into(%{}, fn {_idx, recipe_params} ->
        key = recipe_params["key"] || ""

        ingredients =
          (recipe_params["ingredient"] || %{})
          |> Enum.sort_by(fn {k, _} -> parse_int(k) end)
          |> Enum.map(fn {_i, ing} ->
            %{"itemKey" => ing["itemKey"], "count" => parse_int(ing["count"])}
          end)
          |> Enum.reject(fn ing -> ing["itemKey"] == "" or ing["itemKey"] == nil end)

        {key,
         %{
           "ingredients" => ingredients,
           "result_item" => recipe_params["result_item"] || "",
           "result_quantity" => parse_int(recipe_params["result_quantity"]),
           "category" => recipe_params["category"] || ""
         }}
      end)

    save_config("recipe_configs", recipes, socket)
  end

  def handle_event("add_recipe", _params, socket) do
    data = socket.assigns.edit_data
    # edit_data for recipes is a list of {key, value} tuples for ordering
    entries = data["_entries"] || []
    entries = entries ++ [{"", %{"ingredients" => [], "result_item" => "", "result_quantity" => 1, "category" => ""}}]
    {:noreply, assign(socket, edit_data: Map.put(data, "_entries", entries))}
  end

  def handle_event("remove_recipe", %{"index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    entries = List.delete_at(data["_entries"] || [], i)
    {:noreply, assign(socket, edit_data: Map.put(data, "_entries", entries))}
  end

  def handle_event("add_recipe_ing", %{"recipe-index" => ri}, socket) do
    i = parse_int(ri)
    data = socket.assigns.edit_data
    entries = data["_entries"] || []
    {key, recipe} = Enum.at(entries, i)
    ingredients = (recipe["ingredients"] || []) ++ [%{"itemKey" => "", "count" => 1}]
    recipe = Map.put(recipe, "ingredients", ingredients)
    entries = List.replace_at(entries, i, {key, recipe})
    {:noreply, assign(socket, edit_data: Map.put(data, "_entries", entries))}
  end

  def handle_event("remove_recipe_ing", %{"recipe-index" => ri, "ingredient-index" => ii}, socket) do
    i = parse_int(ri)
    j = parse_int(ii)
    data = socket.assigns.edit_data
    entries = data["_entries"] || []
    {key, recipe} = Enum.at(entries, i)
    ingredients = List.delete_at(recipe["ingredients"] || [], j)
    recipe = Map.put(recipe, "ingredients", ingredients)
    entries = List.replace_at(entries, i, {key, recipe})
    {:noreply, assign(socket, edit_data: Map.put(data, "_entries", entries))}
  end

  # --- Skin configs save ---
  def handle_event("save_skin", params, socket) do
    skins =
      (params["skin"] || %{})
      |> Enum.sort_by(fn {k, _v} -> k end)
      |> Enum.into(%{}, fn {_idx, skin_params} ->
        key = skin_params["key"] || ""

        {key,
         %{
           "building_type" => skin_params["building_type"] || "",
           "cost_item_key" => skin_params["cost_item_key"] || "",
           "cost_quantity" => parse_int(skin_params["cost_quantity"])
         }}
      end)

    save_config("skin_configs", skins, socket)
  end

  def handle_event("add_skin", _params, socket) do
    data = socket.assigns.edit_data
    entries = data["_entries"] || []
    entries = entries ++ [{"", %{"building_type" => "", "cost_item_key" => "", "cost_quantity" => 1}}]
    {:noreply, assign(socket, edit_data: Map.put(data, "_entries", entries))}
  end

  def handle_event("remove_skin", %{"index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    entries = List.delete_at(data["_entries"] || [], i)
    {:noreply, assign(socket, edit_data: Map.put(data, "_entries", entries))}
  end

  # Add/remove building cost rows
  def handle_event("add_building_cost", %{"type" => type}, socket) do
    data = socket.assigns.edit_data
    key = type <> "_costs"
    costs = (data[key] || []) ++ [%{"manaCost" => 0, "harvestCosts" => []}]
    {:noreply, assign(socket, edit_data: Map.put(data, key, costs))}
  end

  def handle_event("remove_building_cost", %{"type" => type, "index" => idx}, socket) do
    i = parse_int(idx)
    data = socket.assigns.edit_data
    key = type <> "_costs"
    costs = List.delete_at(data[key] || [], i)
    {:noreply, assign(socket, edit_data: Map.put(data, key, costs))}
  end

  def handle_event("add_building_harvest", %{"type" => type, "cost-index" => ci}, socket) do
    i = parse_int(ci)
    data = socket.assigns.edit_data
    key = type <> "_costs"
    costs = data[key] || []
    entry = Enum.at(costs, i)
    harvests = (entry["harvestCosts"] || []) ++ [%{"itemKey" => "", "count" => 1}]
    entry = Map.put(entry, "harvestCosts", harvests)
    costs = List.replace_at(costs, i, entry)
    {:noreply, assign(socket, edit_data: Map.put(data, key, costs))}
  end

  def handle_event("remove_building_harvest", %{"type" => type, "cost-index" => ci, "harvest-index" => hi}, socket) do
    i = parse_int(ci)
    j = parse_int(hi)
    data = socket.assigns.edit_data
    key = type <> "_costs"
    costs = data[key] || []
    entry = Enum.at(costs, i)
    harvests = List.delete_at(entry["harvestCosts"] || [], j)
    entry = Map.put(entry, "harvestCosts", harvests)
    costs = List.replace_at(costs, i, entry)
    {:noreply, assign(socket, edit_data: Map.put(data, key, costs))}
  end

  # --- Generic JSON save (fallback for unknown keys) ---

  def handle_event("save", %{"json" => json, "key" => key}, socket) do
    case Jason.decode(json) do
      {:ok, value} -> save_config(key, value, socket)
      {:error, _} -> {:noreply, put_flash(socket, :error, "Invalid JSON")}
    end
  end

  def handle_event("new", %{"key" => key, "json" => json}, socket) do
    case Jason.decode(json) do
      {:ok, value} -> save_config(key, value, socket)
      {:error, _} -> {:noreply, put_flash(socket, :error, "Invalid JSON")}
    end
  end

  # --- Helpers ---

  defp save_config(key, value, socket) do
    case Admin.upsert_game_config(key, value) do
      {:ok, _} ->
        CampFire.ConfigCache.refresh()

        {:noreply,
         socket
         |> put_flash(:info, "Config '#{key}' saved")
         |> assign(configs: Admin.list_game_configs(), editing_key: nil, edit_data: nil, edit_json: nil)}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to save config")}
    end
  end

  defp parse_float(val) when is_binary(val) do
    case Float.parse(val) do
      {f, _} -> f
      :error -> 0.0
    end
  end

  defp parse_float(val) when is_number(val), do: val * 1.0
  defp parse_float(_), do: 0.0

  defp parse_int(val) when is_binary(val) do
    case Integer.parse(val) do
      {i, _} -> i
      :error -> 0
    end
  end

  defp parse_int(val) when is_integer(val), do: val
  defp parse_int(val) when is_float(val), do: round(val)
  defp parse_int(_), do: 0

  defp parse_int_list(params, prefix) do
    params
    |> Enum.filter(fn {k, _} -> String.starts_with?(k, prefix <> "_") end)
    |> Enum.sort_by(fn {k, _} -> parse_int(String.replace(k, prefix <> "_", "")) end)
    |> Enum.map(fn {_, v} -> parse_int(v) end)
  end

  defp parse_float_list(params, prefix) do
    params
    |> Enum.filter(fn {k, _} -> String.starts_with?(k, prefix <> "_") end)
    |> Enum.sort_by(fn {k, _} -> parse_int(String.replace(k, prefix <> "_", "")) end)
    |> Enum.map(fn {_, v} -> parse_float(v) end)
  end

  defp parse_cost_list(params, prefix) do
    (params[prefix] || %{})
    |> Enum.sort_by(fn {k, _v} -> parse_int(k) end)
    |> Enum.map(fn {_idx, cost_params} ->
      harvests =
        (cost_params["harvest"] || %{})
        |> Enum.sort_by(fn {k, _} -> parse_int(k) end)
        |> Enum.map(fn {_i, h} ->
          %{"itemKey" => h["itemKey"], "count" => parse_int(h["count"])}
        end)
        |> Enum.reject(fn h -> h["itemKey"] == "" or h["itemKey"] == nil end)

      %{"manaCost" => parse_int(cost_params["manaCost"]), "harvestCosts" => harvests}
    end)
  end

  # --- Render ---

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Economy / Game Config</h2>
      </div>

      <div class="mb-6 bg-white border rounded-lg p-4">
        <h3 class="text-lg font-semibold mb-3">Add New Config</h3>
        <form phx-submit="new" class="space-y-3">
          <div class="flex gap-3 items-end">
            <div class="flex-shrink-0">
              <label class="block text-sm font-medium text-gray-700">Key</label>
              <input type="text" name="key" placeholder="config_key" class="mt-1 border rounded px-3 py-2" required />
            </div>
            <button type="submit" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
              Create
            </button>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Value (JSON)</label>
            <div id="new-config-editor" phx-hook="JsonEditor" class="json-editor-wrap">
              <div class="json-toolbar">
                <button type="button" data-action="format">Format</button>
                <button type="button" data-action="minify">Minify</button>
              </div>
              <textarea name="json" rows="4" class="w-full border rounded px-3 py-2">{}</textarea>
              <div class="json-error-msg"></div>
            </div>
          </div>
        </form>
      </div>

      <div class="space-y-4">
        <%= for config <- @configs do %>
          <div class="bg-white border rounded-lg p-4">
            <div class="flex justify-between items-start">
              <h3 class="font-semibold text-lg">{config.key}</h3>
              <%= if @editing_key != config.key do %>
                <button phx-click="edit" phx-value-id={config.id} class="text-blue-600 hover:underline text-sm">
                  Edit
                </button>
              <% end %>
            </div>

            <%= if @editing_key == config.key do %>
              {render_editor(assigns, config)}
            <% else %>
              {render_display(assigns, config)}
            <% end %>
          </div>
        <% end %>
      </div>
    </div>
    """
  end

  # --- Display (read-only) ---

  defp render_display(assigns, config) do
    assigns = assign(assigns, :config, config)

    case config.key do
      "flame_config" -> render_flame_display(assigns)
      "vase_config" -> render_vase_display(assigns)
      "mallum_house_config" -> render_mallum_display(assigns)
      "bird_config" -> render_bird_display(assigns)
      "plot_config" -> render_plot_display(assigns)
      "new_player_config" -> render_new_player_display(assigns)
      "recipe_configs" -> render_recipe_display(assigns)
      "skin_configs" -> render_skin_display(assigns)
      _ -> render_json_display(assigns)
    end
  end

  defp render_flame_display(assigns) do
    v = assigns.config.value

    assigns =
      assign(assigns,
        max_level: v["max_flame_level"],
        mana_rates: v["mana_rates"] || [],
        mana_caps: v["mana_caps"] || [],
        caps: v["entity_caps"] || [],
        sizes: v["grid_sizes"] || [],
        recipes: v["upgrade_recipes"] || []
      )

    ~H"""
    <div class="mt-3 space-y-3">
      <div class="text-sm">
        <span class="text-gray-500">Max Level:</span> <span class="font-medium">{@max_level}</span>
      </div>
      <table class="w-full text-sm">
        <thead><tr class="bg-gray-50">
          <th class="px-3 py-2 text-left text-gray-500">Level</th>
          <th class="px-3 py-2 text-left text-gray-500">Mana/s</th>
          <th class="px-3 py-2 text-left text-gray-500">Mana Cap</th>
          <th class="px-3 py-2 text-left text-gray-500">Entity Cap</th>
          <th class="px-3 py-2 text-left text-gray-500">Grid Size</th>
          <th class="px-3 py-2 text-left text-gray-500">Upgrade Recipe</th>
        </tr></thead>
        <tbody class="divide-y">
          <%= for {cap, i} <- Enum.with_index(@caps) do %>
            <tr>
              <td class="px-3 py-1.5 font-medium">{i + 1}</td>
              <td class="px-3 py-1.5">{Enum.at(@mana_rates, i, "-")}</td>
              <td class="px-3 py-1.5">{Enum.at(@mana_caps, i, "-")}</td>
              <td class="px-3 py-1.5">{cap}</td>
              <td class="px-3 py-1.5">{Enum.at(@sizes, i, "-")}</td>
              <td class="px-3 py-1.5">
                <%= if i == 0 do %>
                  <span class="text-gray-400">-</span>
                <% else %>
                  <% recipe = Enum.at(@recipes, i - 1) %>
                  <%= if recipe && recipe["ingredients"] != [] do %>
                    <%= for ing <- recipe["ingredients"] || [] do %>
                      <span class="inline-block bg-purple-100 text-purple-800 rounded px-2 py-0.5 text-xs mr-1">
                        {ing["count"]}x {ing["itemKey"]}
                      </span>
                    <% end %>
                  <% else %>
                    <span class="text-gray-400">none</span>
                  <% end %>
                <% end %>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp render_vase_display(assigns) do
    v = assigns.config.value

    assigns =
      assign(assigns,
        default_cap: v["default_capacity"],
        craft_cost_mana: v["craft_cost_mana"],
        fill_duration: v["fill_duration_minutes"],
        tiers: v["capacity_tiers"] || [],
        costs: v["upgrade_costs"] || []
      )

    ~H"""
    <div class="mt-3 space-y-3">
      <div class="grid grid-cols-3 gap-4 text-sm">
        <div><span class="text-gray-500">Default Capacity:</span> <span class="font-medium">{@default_cap}</span></div>
        <div><span class="text-gray-500">Craft Cost:</span> <span class="font-medium">{@craft_cost_mana} mana</span></div>
        <div><span class="text-gray-500">Fill Duration:</span> <span class="font-medium">{@fill_duration} min</span></div>
      </div>
      <table class="w-full text-sm">
        <thead><tr class="bg-gray-50">
          <th class="px-3 py-2 text-left text-gray-500">Tier</th>
          <th class="px-3 py-2 text-left text-gray-500">Capacity</th>
          <th class="px-3 py-2 text-left text-gray-500">Upgrade Cost (mana)</th>
        </tr></thead>
        <tbody class="divide-y">
          <%= for {tier, i} <- Enum.with_index(@tiers) do %>
            <tr>
              <td class="px-3 py-1.5 font-medium">{i + 1}</td>
              <td class="px-3 py-1.5">{tier}</td>
              <td class="px-3 py-1.5">{if i == 0, do: "-", else: Enum.at(@costs, i - 1, "-")}</td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp render_mallum_display(assigns) do
    v = assigns.config.value

    assigns =
      assign(assigns,
        per_house: v["mallums_per_house"],
        house_costs: v["house_costs"] || []
      )

    ~H"""
    <div class="mt-3 space-y-3">
      <div class="text-sm"><span class="text-gray-500">Mallums per House:</span> <span class="font-medium">{@per_house}</span></div>
      <table class="w-full text-sm">
        <thead><tr class="bg-gray-50">
          <th class="px-3 py-2 text-left text-gray-500">House #</th>
          <th class="px-3 py-2 text-left text-gray-500">Mana Cost</th>
          <th class="px-3 py-2 text-left text-gray-500">Harvest Requirements</th>
        </tr></thead>
        <tbody class="divide-y">
          <%= for {cost, i} <- Enum.with_index(@house_costs) do %>
            <tr>
              <td class="px-3 py-1.5 font-medium">{i + 1}</td>
              <td class="px-3 py-1.5">{cost["manaCost"]}</td>
              <td class="px-3 py-1.5">
                <%= if (cost["harvestCosts"] || []) == [] do %>
                  <span class="text-gray-400">none</span>
                <% else %>
                  <%= for h <- cost["harvestCosts"] do %>
                    <span class="inline-block bg-amber-100 text-amber-800 rounded px-2 py-0.5 text-xs mr-1">
                      {h["count"]}x {h["itemKey"]}
                    </span>
                  <% end %>
                <% end %>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp render_bird_display(assigns) do
    v = assigns.config.value
    assigns = assign(assigns, base_chance: v["spawn_base_chance"], decay: v["spawn_decay"])

    ~H"""
    <div class="mt-3 grid grid-cols-2 gap-4 text-sm">
      <div><span class="text-gray-500">Spawn Base Chance:</span> <span class="font-medium">{@base_chance}</span></div>
      <div><span class="text-gray-500">Spawn Decay:</span> <span class="font-medium">{@decay}</span></div>
    </div>
    """
  end

  defp render_plot_display(assigns) do
    v = assigns.config.value

    assigns =
      assign(assigns,
        water_cd: v["water_cooldown_seconds"],
        rain_cd: v["rain_water_cooldown_seconds"],
        rain_trigger: v["rain_trigger_minutes"],
        drop_spread: v["drop_spread_factor"],
        speed_item: v["speed_item"]
      )

    ~H"""
    <div class="mt-3 grid grid-cols-2 gap-4 text-sm">
      <div><span class="text-gray-500">Water Cooldown:</span> <span class="font-medium">{@water_cd}s</span></div>
      <div><span class="text-gray-500">Rain Water Cooldown:</span> <span class="font-medium">{@rain_cd}s</span></div>
      <div><span class="text-gray-500">Rain Trigger:</span> <span class="font-medium">{@rain_trigger} min</span></div>
      <div><span class="text-gray-500">Drop Spread Factor:</span> <span class="font-medium">{@drop_spread}</span></div>
      <div><span class="text-gray-500">Speed Item:</span> <span class="font-medium">{@speed_item}</span></div>
    </div>
    """
  end

  defp render_new_player_display(assigns) do
    v = assigns.config.value

    assigns =
      assign(assigns,
        mana: v["mana"],
        gems: v["gems"],
        starting_water: v["starting_water"],
        items: v["items"] || []
      )

    ~H"""
    <div class="mt-3 space-y-3">
      <div class="grid grid-cols-3 gap-4 text-sm">
        <div><span class="text-gray-500">Mana:</span> <span class="font-medium">{@mana}</span></div>
        <div><span class="text-gray-500">Gems:</span> <span class="font-medium">{@gems}</span></div>
        <div><span class="text-gray-500">Starting Water:</span> <span class="font-medium">{@starting_water}</span></div>
      </div>
      <div class="text-sm">
        <span class="text-gray-500">Starting Items:</span>
        <div class="mt-1 flex flex-wrap gap-1">
          <%= for item <- @items do %>
            <span class="inline-block bg-blue-100 text-blue-800 rounded px-2 py-0.5 text-xs">
              {item["count"]}x {item["itemKey"]}
            </span>
          <% end %>
        </div>
      </div>
    </div>
    """
  end

  defp render_recipe_display(assigns) do
    entries =
      assigns.config.value
      |> Enum.sort_by(fn {k, _v} -> k end)

    assigns = assign(assigns, entries: entries)

    ~H"""
    <div class="mt-3">
      <table class="w-full text-sm">
        <thead>
          <tr class="bg-gray-50">
            <th class="px-3 py-2 text-left text-gray-500">Recipe</th>
            <th class="px-3 py-2 text-left text-gray-500">Category</th>
            <th class="px-3 py-2 text-left text-gray-500">Ingredients</th>
            <th class="px-3 py-2 text-left text-gray-500">Result</th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for {key, recipe} <- @entries do %>
            <tr>
              <td class="px-3 py-1.5 font-medium">{key}</td>
              <td class="px-3 py-1.5">
                <span class="inline-block bg-gray-100 text-gray-700 rounded px-2 py-0.5 text-xs">{recipe["category"]}</span>
              </td>
              <td class="px-3 py-1.5">
                <%= for ing <- recipe["ingredients"] || [] do %>
                  <span class="inline-block bg-amber-100 text-amber-800 rounded px-2 py-0.5 text-xs mr-1">
                    {ing["count"]}x {ing["itemKey"]}
                  </span>
                <% end %>
              </td>
              <td class="px-3 py-1.5">{recipe["result_quantity"]}x {recipe["result_item"]}</td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp render_skin_display(assigns) do
    entries =
      assigns.config.value
      |> Enum.sort_by(fn {k, _v} -> k end)

    assigns = assign(assigns, entries: entries)

    ~H"""
    <div class="mt-3">
      <table class="w-full text-sm">
        <thead>
          <tr class="bg-gray-50">
            <th class="px-3 py-2 text-left text-gray-500">Skin Key</th>
            <th class="px-3 py-2 text-left text-gray-500">Building Type</th>
            <th class="px-3 py-2 text-left text-gray-500">Cost</th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for {key, skin} <- @entries do %>
            <tr>
              <td class="px-3 py-1.5 font-medium">{key}</td>
              <td class="px-3 py-1.5">{skin["building_type"]}</td>
              <td class="px-3 py-1.5">
                <span class="inline-block bg-purple-100 text-purple-800 rounded px-2 py-0.5 text-xs">
                  {skin["cost_quantity"]}x {skin["cost_item_key"]}
                </span>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end

  defp render_json_display(assigns) do
    ~H"""
    <pre class="mt-2 text-sm text-gray-600 bg-gray-50 rounded p-3 max-h-48 overflow-auto font-mono leading-relaxed">{Jason.encode!(@config.value, pretty: true)}</pre>
    """
  end

  # --- Editors ---

  defp render_editor(assigns, config) do
    assigns = assign(assigns, :config, config)

    case config.key do
      "flame_config" -> render_flame_editor(assigns)
      "vase_config" -> render_vase_editor(assigns)
      "mallum_house_config" -> render_mallum_editor(assigns)
      "bird_config" -> render_bird_editor(assigns)
      "plot_config" -> render_plot_editor(assigns)
      "new_player_config" -> render_new_player_editor(assigns)
      "recipe_configs" -> render_recipe_editor(assigns)
      "skin_configs" -> render_skin_editor(assigns)
      _ -> render_json_editor(assigns)
    end
  end

  defp render_flame_editor(assigns) do
    d = assigns.edit_data
    caps = d["entity_caps"] || []
    sizes = d["grid_sizes"] || []
    rates = d["mana_rates"] || []
    mana_caps = d["mana_caps"] || []
    recipes = d["upgrade_recipes"] || []

    assigns =
      assign(assigns,
        max_level: d["max_flame_level"],
        caps: caps,
        sizes: sizes,
        rates: rates,
        mana_caps: mana_caps,
        recipes: recipes
      )

    ~H"""
    <form phx-submit="save_flame" class="mt-3 space-y-4">
      <div class="w-48">
        <label class="block text-sm font-medium text-gray-700">Max Flame Level</label>
        <input type="number" name="max_flame_level" value={@max_level} class="mt-1 w-full border rounded px-3 py-2" />
      </div>

      <div>
        <div class="flex justify-between items-center mb-2">
          <label class="text-sm font-medium text-gray-700">Per-Level Values</label>
          <button type="button" phx-click="add_flame_level" class="text-sm text-green-600 hover:underline">+ Add Level</button>
        </div>
        <table class="w-full text-sm border">
          <thead><tr class="bg-gray-50">
            <th class="px-3 py-2 text-left text-gray-500 w-16">Level</th>
            <th class="px-3 py-2 text-left text-gray-500">Mana/s</th>
            <th class="px-3 py-2 text-left text-gray-500">Mana Cap</th>
            <th class="px-3 py-2 text-left text-gray-500">Entity Cap</th>
            <th class="px-3 py-2 text-left text-gray-500">Grid Size</th>
            <th class="px-3 py-2 text-left text-gray-500">Upgrade Recipe</th>
            <th class="px-3 py-2 w-10"></th>
          </tr></thead>
          <tbody class="divide-y">
            <%= for {cap, i} <- Enum.with_index(@caps) do %>
              <tr class="align-top">
                <td class="px-3 py-1.5 font-medium text-gray-500">{i + 1}</td>
                <td class="px-3 py-1"><input type="number" step="0.01" name={"mana_rate_#{i}"} value={Enum.at(@rates, i, 0.0)} class="w-full border rounded px-2 py-1" /></td>
                <td class="px-3 py-1"><input type="number" name={"mana_cap_#{i}"} value={Enum.at(@mana_caps, i, 0)} class="w-full border rounded px-2 py-1" /></td>
                <td class="px-3 py-1"><input type="number" name={"entity_cap_#{i}"} value={cap} class="w-full border rounded px-2 py-1" /></td>
                <td class="px-3 py-1"><input type="number" name={"grid_size_#{i}"} value={Enum.at(@sizes, i, 2)} class="w-full border rounded px-2 py-1" /></td>
                <td class="px-3 py-1">
                  <%= if i == 0 do %>
                    <span class="text-gray-400 text-xs">base level</span>
                  <% else %>
                    <% recipe = Enum.at(@recipes, i - 1) %>
                    <% ingredients = if recipe, do: recipe["ingredients"] || [], else: [] %>
                    <div class="space-y-1">
                      <%= for {ing, j} <- Enum.with_index(ingredients) do %>
                        <div class="flex gap-1 items-center">
                          <input type="text" name={"recipe[#{i - 1}][ingredient][#{j}][itemKey]"} value={ing["itemKey"]} placeholder="item_key" class="border rounded px-1 py-0.5 text-xs w-24" />
                          <span class="text-gray-400 text-xs">x</span>
                          <input type="number" name={"recipe[#{i - 1}][ingredient][#{j}][count]"} value={ing["count"]} class="border rounded px-1 py-0.5 text-xs w-14" />
                          <button type="button" phx-click="remove_recipe_ingredient" phx-value-recipe-index={i - 1} phx-value-ingredient-index={j} class="text-red-500 hover:text-red-700 text-xs">X</button>
                        </div>
                      <% end %>
                      <button type="button" phx-click="add_recipe_ingredient" phx-value-recipe-index={i - 1} class="text-xs text-green-600 hover:underline">+ ingredient</button>
                    </div>
                  <% end %>
                </td>
                <td class="px-3 py-1">
                  <button type="button" phx-click="remove_flame_level" phx-value-index={i} class="text-red-500 hover:text-red-700 text-xs">X</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      </div>

      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_vase_editor(assigns) do
    d = assigns.edit_data
    tiers = d["capacity_tiers"] || []
    costs = d["upgrade_costs"] || []

    assigns =
      assign(assigns,
        default_cap: d["default_capacity"],
        craft_cost_mana: d["craft_cost_mana"],
        fill_duration: d["fill_duration_minutes"],
        tiers: tiers,
        costs: costs
      )

    ~H"""
    <form phx-submit="save_vase" class="mt-3 space-y-4">
      <div class="grid grid-cols-3 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700">Default Capacity</label>
          <input type="number" name="default_capacity" value={@default_cap} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Craft Cost (mana)</label>
          <input type="number" name="craft_cost_mana" value={@craft_cost_mana} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Fill Duration (minutes)</label>
          <input type="number" name="fill_duration_minutes" value={@fill_duration} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
      </div>

      <div>
        <div class="flex justify-between items-center mb-2">
          <label class="text-sm font-medium text-gray-700">Capacity Tiers & Upgrade Costs</label>
          <button type="button" phx-click="add_capacity_tier" class="text-sm text-green-600 hover:underline">+ Add Tier</button>
        </div>
        <table class="w-full text-sm border">
          <thead><tr class="bg-gray-50">
            <th class="px-3 py-2 text-left text-gray-500 w-16">Tier</th>
            <th class="px-3 py-2 text-left text-gray-500">Capacity</th>
            <th class="px-3 py-2 text-left text-gray-500">Upgrade Cost (mana)</th>
            <th class="px-3 py-2 w-10"></th>
          </tr></thead>
          <tbody class="divide-y">
            <%= for {tier, i} <- Enum.with_index(@tiers) do %>
              <tr>
                <td class="px-3 py-1.5 font-medium text-gray-500">{i + 1}</td>
                <td class="px-3 py-1"><input type="number" name={"capacity_tier_#{i}"} value={tier} class="w-full border rounded px-2 py-1" /></td>
                <td class="px-3 py-1">
                  <%= if i == 0 do %>
                    <span class="text-gray-400 px-2">base tier</span>
                    <input type="hidden" name={"upgrade_cost_#{i}"} value="0" />
                  <% else %>
                    <input type="number" name={"upgrade_cost_#{i}"} value={Enum.at(@costs, i - 1, 0)} class="w-full border rounded px-2 py-1" />
                  <% end %>
                </td>
                <td class="px-3 py-1">
                  <button type="button" phx-click="remove_capacity_tier" phx-value-index={i} class="text-red-500 hover:text-red-700 text-xs">X</button>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      </div>

      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_mallum_editor(assigns) do
    d = assigns.edit_data
    house_costs = d["house_costs"] || []

    assigns =
      assign(assigns,
        per_house: d["mallums_per_house"],
        house_costs: house_costs
      )

    ~H"""
    <form phx-submit="save_mallum_house" class="mt-3 space-y-4">
      <div class="w-48">
        <label class="block text-sm font-medium text-gray-700">Mallums per House</label>
        <input type="number" name="mallums_per_house" value={@per_house} class="mt-1 w-full border rounded px-3 py-2" />
      </div>

      <div>
        <div class="flex justify-between items-center mb-2">
          <label class="text-sm font-medium text-gray-700">House Costs</label>
          <button type="button" phx-click="add_house_cost" class="text-sm text-green-600 hover:underline">+ Add House</button>
        </div>
        <div class="space-y-3">
          <%= for {cost, i} <- Enum.with_index(@house_costs) do %>
            <div class="border rounded p-3 bg-gray-50">
              <div class="flex justify-between items-center mb-2">
                <span class="text-sm font-medium text-gray-700">House #{i + 1}</span>
                <button type="button" phx-click="remove_house_cost" phx-value-index={i} class="text-red-500 hover:text-red-700 text-xs">Remove</button>
              </div>
              <div class="flex gap-3 items-end mb-2">
                <div class="w-32">
                  <label class="block text-xs text-gray-500">Mana Cost</label>
                  <input type="number" name={"house_cost[#{i}][mana]"} value={cost["manaCost"]} class="w-full border rounded px-2 py-1 text-sm" />
                </div>
                <button type="button" phx-click="add_harvest" phx-value-house-index={i} class="text-xs text-green-600 hover:underline">+ Harvest Req</button>
              </div>
              <%= if (cost["harvestCosts"] || []) != [] do %>
                <div class="space-y-1 ml-4">
                  <%= for {h, j} <- Enum.with_index(cost["harvestCosts"] || []) do %>
                    <div class="flex gap-2 items-center">
                      <input type="text" name={"house_cost[#{i}][harvest][#{j}][item]"} value={h["itemKey"]} placeholder="item_key" class="border rounded px-2 py-1 text-sm w-40" />
                      <span class="text-gray-400 text-xs">x</span>
                      <input type="number" name={"house_cost[#{i}][harvest][#{j}][count]"} value={h["count"]} class="border rounded px-2 py-1 text-sm w-16" />
                      <button type="button" phx-click="remove_harvest" phx-value-house-index={i} phx-value-harvest-index={j} class="text-red-500 hover:text-red-700 text-xs">X</button>
                    </div>
                  <% end %>
                </div>
              <% end %>
            </div>
          <% end %>
        </div>
      </div>

      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_bird_editor(assigns) do
    d = assigns.edit_data
    assigns = assign(assigns, base_chance: d["spawn_base_chance"], decay: d["spawn_decay"])

    ~H"""
    <form phx-submit="save_bird" class="mt-3 space-y-4">
      <div class="grid grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700">Spawn Base Chance</label>
          <input type="number" step="0.01" name="spawn_base_chance" value={@base_chance} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Spawn Decay</label>
          <input type="number" step="0.01" name="spawn_decay" value={@decay} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
      </div>
      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_plot_editor(assigns) do
    d = assigns.edit_data

    assigns =
      assign(assigns,
        water_cd: d["water_cooldown_seconds"],
        rain_cd: d["rain_water_cooldown_seconds"],
        rain_trigger: d["rain_trigger_minutes"],
        drop_spread: d["drop_spread_factor"],
        speed_item: d["speed_item"]
      )

    ~H"""
    <form phx-submit="save_plot" class="mt-3 space-y-4">
      <div class="grid grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700">Water Cooldown (seconds)</label>
          <input type="number" name="water_cooldown_seconds" value={@water_cd} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Rain Water Cooldown (seconds)</label>
          <input type="number" name="rain_water_cooldown_seconds" value={@rain_cd} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Rain Trigger (minutes)</label>
          <input type="number" name="rain_trigger_minutes" value={@rain_trigger} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Drop Spread Factor</label>
          <input type="number" step="0.01" name="drop_spread_factor" value={@drop_spread} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Speed Item Key</label>
          <input type="text" name="speed_item" value={@speed_item} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
      </div>
      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_new_player_editor(assigns) do
    d = assigns.edit_data

    assigns =
      assign(assigns,
        mana: d["mana"],
        gems: d["gems"],
        starting_water: d["starting_water"],
        items: d["items"] || []
      )

    ~H"""
    <form phx-submit="save_new_player" class="mt-3 space-y-4">
      <div class="grid grid-cols-3 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700">Starting Mana</label>
          <input type="number" step="0.1" name="mana" value={@mana} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Starting Gems</label>
          <input type="number" name="gems" value={@gems} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Starting Water</label>
          <input type="number" name="starting_water" value={@starting_water} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
      </div>

      <div>
        <div class="flex justify-between items-center mb-2">
          <label class="text-sm font-medium text-gray-700">Starting Items</label>
          <button type="button" phx-click="add_new_player_item" class="text-sm text-green-600 hover:underline">+ Add Item</button>
        </div>
        <div class="space-y-2">
          <%= for {item, i} <- Enum.with_index(@items) do %>
            <div class="flex gap-2 items-center">
              <input type="text" name={"item[#{i}][itemKey]"} value={item["itemKey"]} placeholder="item_key" class="border rounded px-2 py-1 text-sm w-48" />
              <span class="text-gray-400 text-xs">x</span>
              <input type="number" name={"item[#{i}][count]"} value={item["count"]} class="border rounded px-2 py-1 text-sm w-20" />
              <button type="button" phx-click="remove_new_player_item" phx-value-index={i} class="text-red-500 hover:text-red-700 text-xs">X</button>
            </div>
          <% end %>
        </div>
      </div>

      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_recipe_editor(assigns) do
    entries = assigns.edit_data["_entries"] || []
    assigns = assign(assigns, entries: entries)

    ~H"""
    <form phx-submit="save_recipe" class="mt-3 space-y-4">
      <div class="flex justify-between items-center mb-2">
        <label class="text-sm font-medium text-gray-700">Recipes</label>
        <button type="button" phx-click="add_recipe" class="text-sm text-green-600 hover:underline">+ Add Recipe</button>
      </div>
      <div class="space-y-3">
        <%= for {{key, recipe}, i} <- Enum.with_index(@entries) do %>
          <div class="border rounded p-3 bg-gray-50">
            <div class="flex justify-between items-center mb-2">
              <span class="text-sm font-medium text-gray-700">Recipe #{i + 1}</span>
              <button type="button" phx-click="remove_recipe" phx-value-index={i} class="text-red-500 hover:text-red-700 text-xs">Remove</button>
            </div>
            <div class="grid grid-cols-4 gap-3 mb-2">
              <div>
                <label class="block text-xs text-gray-500">Key</label>
                <input type="text" name={"recipe[#{i}][key]"} value={key} class="w-full border rounded px-2 py-1 text-sm" />
              </div>
              <div>
                <label class="block text-xs text-gray-500">Result Item</label>
                <input type="text" name={"recipe[#{i}][result_item]"} value={recipe["result_item"]} class="w-full border rounded px-2 py-1 text-sm" />
              </div>
              <div>
                <label class="block text-xs text-gray-500">Result Qty</label>
                <input type="number" name={"recipe[#{i}][result_quantity]"} value={recipe["result_quantity"]} class="w-full border rounded px-2 py-1 text-sm" />
              </div>
              <div>
                <label class="block text-xs text-gray-500">Category</label>
                <select name={"recipe[#{i}][category]"} class="w-full border rounded px-2 py-1 text-sm">
                  <option value="Pigment" selected={recipe["category"] == "Pigment"}>Pigment</option>
                  <option value="Consumable" selected={recipe["category"] == "Consumable"}>Consumable</option>
                </select>
              </div>
            </div>
            <div class="ml-2">
              <label class="block text-xs text-gray-500 mb-1">Ingredients</label>
              <div class="space-y-1">
                <%= for {ing, j} <- Enum.with_index(recipe["ingredients"] || []) do %>
                  <div class="flex gap-1 items-center">
                    <input type="text" name={"recipe[#{i}][ingredient][#{j}][itemKey]"} value={ing["itemKey"]} placeholder="item_key" class="border rounded px-1 py-0.5 text-xs w-32" />
                    <span class="text-gray-400 text-xs">x</span>
                    <input type="number" name={"recipe[#{i}][ingredient][#{j}][count]"} value={ing["count"]} class="border rounded px-1 py-0.5 text-xs w-14" />
                    <button type="button" phx-click="remove_recipe_ing" phx-value-recipe-index={i} phx-value-ingredient-index={j} class="text-red-500 hover:text-red-700 text-xs">X</button>
                  </div>
                <% end %>
                <button type="button" phx-click="add_recipe_ing" phx-value-recipe-index={i} class="text-xs text-green-600 hover:underline">+ ingredient</button>
              </div>
            </div>
          </div>
        <% end %>
      </div>

      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_skin_editor(assigns) do
    entries = assigns.edit_data["_entries"] || []
    assigns = assign(assigns, entries: entries)

    ~H"""
    <form phx-submit="save_skin" class="mt-3 space-y-4">
      <div class="flex justify-between items-center mb-2">
        <label class="text-sm font-medium text-gray-700">Skins</label>
        <button type="button" phx-click="add_skin" class="text-sm text-green-600 hover:underline">+ Add Skin</button>
      </div>
      <table class="w-full text-sm border">
        <thead>
          <tr class="bg-gray-50">
            <th class="px-3 py-2 text-left text-gray-500">Skin Key</th>
            <th class="px-3 py-2 text-left text-gray-500">Building Type</th>
            <th class="px-3 py-2 text-left text-gray-500">Cost Item</th>
            <th class="px-3 py-2 text-left text-gray-500">Cost Qty</th>
            <th class="px-3 py-2 w-10"></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <%= for {{key, skin}, i} <- Enum.with_index(@entries) do %>
            <tr>
              <td class="px-3 py-1">
                <input type="text" name={"skin[#{i}][key]"} value={key} class="w-full border rounded px-2 py-1 text-sm" />
              </td>
              <td class="px-3 py-1">
                <select name={"skin[#{i}][building_type]"} class="w-full border rounded px-2 py-1 text-sm">
                  <option value="plot" selected={skin["building_type"] == "plot"}>plot</option>
                  <option value="vase" selected={skin["building_type"] == "vase"}>vase</option>
                  <option value="mallum_house" selected={skin["building_type"] == "mallum_house"}>mallum_house</option>
                </select>
              </td>
              <td class="px-3 py-1">
                <input type="text" name={"skin[#{i}][cost_item_key]"} value={skin["cost_item_key"]} class="w-full border rounded px-2 py-1 text-sm" />
              </td>
              <td class="px-3 py-1">
                <input type="number" name={"skin[#{i}][cost_quantity]"} value={skin["cost_quantity"]} class="w-full border rounded px-2 py-1 text-sm w-16" />
              </td>
              <td class="px-3 py-1">
                <button type="button" phx-click="remove_skin" phx-value-index={i} class="text-red-500 hover:text-red-700 text-xs">X</button>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>

      <div class="flex gap-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end

  defp render_json_editor(assigns) do
    ~H"""
    <form phx-submit="save" class="mt-3">
      <input type="hidden" name="key" value={@config.key} />
      <div id={"economy-editor-#{@config.id}"} phx-hook="JsonEditor" class="json-editor-wrap" phx-update="ignore">
        <div class="json-toolbar">
          <button type="button" data-action="format">Format</button>
          <button type="button" data-action="minify">Minify</button>
        </div>
        <textarea
          name="json"
          rows="12"
          class="w-full border rounded px-3 py-2"
        >{@edit_json}</textarea>
        <div class="json-error-msg"></div>
      </div>
      <div class="flex gap-2 mt-2">
        <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Save</button>
        <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">Cancel</button>
      </div>
    </form>
    """
  end
end
