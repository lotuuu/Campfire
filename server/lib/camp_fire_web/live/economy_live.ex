defmodule CampFireWeb.EconomyLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  @known_keys ~w(flame_config vase_config mallum_house_config)

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
      {:noreply, assign(socket, editing_key: config.key, edit_data: config.value)}
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
            %{"itemName" => ing["itemName"], "count" => parse_int(ing["count"])}
          end)
          |> Enum.reject(fn ing -> ing["itemName"] == "" or ing["itemName"] == nil end)

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
      "craft_cost" => parse_int(params["craft_cost"]),
      "fill_seconds_per_unit" => parse_int(params["fill_seconds_per_unit"]),
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
            %{"itemName" => h["item"], "count" => parse_int(h["count"])}
          end)
          |> Enum.reject(fn h -> h["itemName"] == "" or h["itemName"] == nil end)

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
    ingredients = (recipe["ingredients"] || []) ++ [%{"itemName" => "", "count" => 1}]
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
    harvests = (house["harvestCosts"] || []) ++ [%{"itemName" => "", "count" => 1}]
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
    harvests = (entry["harvestCosts"] || []) ++ [%{"itemName" => "", "count" => 1}]
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
          %{"itemName" => h["itemName"], "count" => parse_int(h["count"])}
        end)
        |> Enum.reject(fn h -> h["itemName"] == "" or h["itemName"] == nil end)

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
                        {ing["count"]}x {ing["itemName"]}
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
        craft_cost: v["craft_cost"],
        fill_rate: v["fill_seconds_per_unit"],
        tiers: v["capacity_tiers"] || [],
        costs: v["upgrade_costs"] || []
      )

    ~H"""
    <div class="mt-3 space-y-3">
      <div class="grid grid-cols-3 gap-4 text-sm">
        <div><span class="text-gray-500">Default Capacity:</span> <span class="font-medium">{@default_cap}</span></div>
        <div><span class="text-gray-500">Craft Cost:</span> <span class="font-medium">{@craft_cost} mana</span></div>
        <div><span class="text-gray-500">Fill Rate:</span> <span class="font-medium">{@fill_rate}s/unit</span></div>
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
                      {h["count"]}x {h["itemName"]}
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
                          <input type="text" name={"recipe[#{i - 1}][ingredient][#{j}][itemName]"} value={ing["itemName"]} placeholder="item" class="border rounded px-1 py-0.5 text-xs w-24" />
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
        craft_cost: d["craft_cost"],
        fill_rate: d["fill_seconds_per_unit"],
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
          <input type="number" name="craft_cost" value={@craft_cost} class="mt-1 w-full border rounded px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-700">Fill Rate (s/unit)</label>
          <input type="number" name="fill_seconds_per_unit" value={@fill_rate} class="mt-1 w-full border rounded px-3 py-2" />
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
                      <input type="text" name={"house_cost[#{i}][harvest][#{j}][item]"} value={h["itemName"]} placeholder="item_name" class="border rounded px-2 py-1 text-sm w-40" />
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
