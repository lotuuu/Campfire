defmodule CampFireWeb.EconomyLive do
  use CampFireWeb, :live_view

  alias CampFire.Admin

  def mount(_params, _session, socket) do
    {:ok,
     assign(socket,
       active_tab: :economy,
       configs: Admin.list_game_configs(),
       editing_id: nil,
       edit_json: nil
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, socket}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    config = Admin.get_game_config!(id)
    json = Jason.encode!(config.value, pretty: true)
    {:noreply, assign(socket, editing_id: config.id, edit_json: json, edit_key: config.key)}
  end

  def handle_event("cancel", _params, socket) do
    {:noreply, assign(socket, editing_id: nil, edit_json: nil)}
  end

  def handle_event("save", %{"json" => json, "key" => key}, socket) do
    case Jason.decode(json) do
      {:ok, value} ->
        case Admin.upsert_game_config(key, value) do
          {:ok, _} ->
            {:noreply,
             socket
             |> put_flash(:info, "Config '#{key}' saved")
             |> assign(configs: Admin.list_game_configs(), editing_id: nil, edit_json: nil)}

          {:error, _} ->
            {:noreply, put_flash(socket, :error, "Failed to save config")}
        end

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid JSON")}
    end
  end

  def handle_event("new", %{"key" => key, "json" => json}, socket) do
    case Jason.decode(json) do
      {:ok, value} ->
        case Admin.upsert_game_config(key, value) do
          {:ok, _} ->
            {:noreply,
             socket
             |> put_flash(:info, "Config '#{key}' created")
             |> assign(configs: Admin.list_game_configs(), editing_id: nil, edit_json: nil)}

          {:error, _} ->
            {:noreply, put_flash(socket, :error, "Failed to create config")}
        end

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Invalid JSON")}
    end
  end

  def render(assigns) do
    ~H"""
    <div>
      <div class="flex justify-between items-center mb-6">
        <h2 class="text-2xl font-bold">Economy / Game Config</h2>
      </div>

      <div class="mb-6 bg-white border rounded-lg p-4">
        <h3 class="text-lg font-semibold mb-3">Add New Config</h3>
        <form phx-submit="new" class="flex gap-3 items-end">
          <div class="flex-shrink-0">
            <label class="block text-sm font-medium text-gray-700">Key</label>
            <input type="text" name="key" placeholder="config_key" class="mt-1 border rounded px-3 py-2" required />
          </div>
          <div class="flex-1">
            <label class="block text-sm font-medium text-gray-700">Value (JSON)</label>
            <input type="text" name="json" value="{}" class="mt-1 w-full border rounded px-3 py-2 font-mono text-sm" required />
          </div>
          <button type="submit" class="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700">
            Create
          </button>
        </form>
      </div>

      <div class="space-y-4">
        <%= for config <- @configs do %>
          <div class="bg-white border rounded-lg p-4">
            <div class="flex justify-between items-start">
              <div>
                <h3 class="font-semibold text-lg">{config.key}</h3>
                <%= if @editing_id != config.id do %>
                  <pre class="mt-2 text-sm text-gray-600 bg-gray-50 rounded p-2 max-h-32 overflow-auto"><code>{Jason.encode!(config.value, pretty: true)}</code></pre>
                <% end %>
              </div>
              <%= if @editing_id != config.id do %>
                <button phx-click="edit" phx-value-id={config.id} class="text-blue-600 hover:underline text-sm">
                  Edit
                </button>
              <% end %>
            </div>

            <%= if @editing_id == config.id do %>
              <form phx-submit="save" class="mt-3">
                <input type="hidden" name="key" value={config.key} />
                <textarea
                  name="json"
                  rows="8"
                  class="w-full border rounded px-3 py-2 font-mono text-sm"
                >{@edit_json}</textarea>
                <div class="flex gap-2 mt-2">
                  <button type="submit" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
                    Save
                  </button>
                  <button type="button" phx-click="cancel" class="bg-gray-300 px-4 py-2 rounded hover:bg-gray-400">
                    Cancel
                  </button>
                </div>
              </form>
            <% end %>
          </div>
        <% end %>
      </div>
    </div>
    """
  end
end
