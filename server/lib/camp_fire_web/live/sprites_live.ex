defmodule CampFireWeb.SpritesLive do
  use CampFireWeb, :live_view

  alias CampFire.Sprites

  @max_file_size 512_000
  @accept ~w(.png)

  def mount(_params, _session, socket) do
    {:ok,
     socket
     |> assign(
       active_tab: :sprites,
       sprites: Sprites.list_sprites(),
       new_key: "",
       upload_category: nil,
       replace_key: nil
     )
     |> allow_upload(:sprite,
       accept: @accept,
       max_file_size: @max_file_size,
       max_entries: 1
     )}
  end

  def handle_event("start_upload", %{"category" => category}, socket) do
    {:noreply, assign(socket, upload_category: category, replace_key: nil, new_key: "")}
  end

  def handle_event("start_replace", %{"key" => key}, socket) do
    {:noreply, assign(socket, replace_key: key, upload_category: nil)}
  end

  def handle_event("noop", _params, socket), do: {:noreply, socket}

  def handle_event("cancel_upload", _params, socket) do
    {:noreply, assign(socket, upload_category: nil, replace_key: nil, new_key: "")}
  end

  def handle_event("update_new_key", %{"key" => key}, socket) do
    {:noreply, assign(socket, new_key: key)}
  end

  def handle_event("save_upload", _params, socket) do
    key =
      if socket.assigns.replace_key do
        socket.assigns.replace_key
      else
        category = socket.assigns.upload_category
        name = String.trim(socket.assigns.new_key)
        if name == "", do: nil, else: "#{category}/#{name}"
      end

    if key do
      consume_uploaded_entries(socket, :sprite, fn %{path: path}, _entry ->
        data = File.read!(path)
        Sprites.upload_sprite(key, data)
        {:ok, key}
      end)

      {:noreply,
       socket
       |> put_flash(:info, "Sprite '#{key}' uploaded")
       |> assign(sprites: Sprites.list_sprites(), upload_category: nil, replace_key: nil, new_key: "")}
    else
      {:noreply, put_flash(socket, :error, "Please enter a sprite name")}
    end
  end

  def handle_event("delete_sprite", %{"key" => key}, socket) do
    case Sprites.delete_sprite(key) do
      :ok ->
        {:noreply,
         socket
         |> put_flash(:info, "Sprite '#{key}' deleted")
         |> assign(sprites: Sprites.list_sprites())}

      {:error, _} ->
        {:noreply, put_flash(socket, :error, "Failed to delete sprite")}
    end
  end

  def render(assigns) do
    categories =
      assigns.sprites
      |> Enum.group_by(& &1.category)
      |> Enum.sort_by(fn {cat, _} -> cat end)

    assigns = assign(assigns, categories: categories)

    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-6">Sprites</h2>
      <p class="text-sm text-gray-500 mb-6">
        {length(@sprites)} sprites across {length(@categories)} categories.
        Sprites are served at <code class="bg-gray-100 px-1 rounded">/assets/sprites/{"{key}"}.png</code>
      </p>

      <%= for {category, sprites} <- @categories do %>
        <div class="mb-8">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-lg font-semibold capitalize">{category} <span class="text-sm text-gray-400 font-normal">({length(sprites)})</span></h3>
            <button
              phx-click="start_upload"
              phx-value-category={category}
              class="text-sm bg-green-100 text-green-700 px-3 py-1 rounded hover:bg-green-200"
            >
              + Add to {category}
            </button>
          </div>

          <%= if @upload_category == category do %>
            <div class="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
              <form phx-submit="save_upload" phx-change="update_new_key" class="flex items-end gap-3">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Sprite name</label>
                  <div class="flex items-center gap-1">
                    <span class="text-sm text-gray-500">{category}/</span>
                    <input type="text" name="key" value={@new_key}
                      class="border rounded px-2 py-1 text-sm w-48" placeholder="e.g. basil/icon" />
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">PNG file</label>
                  <.live_file_input upload={@uploads.sprite} class="text-sm" />
                </div>
                <button type="submit" class="bg-blue-600 text-white px-3 py-1.5 rounded text-sm hover:bg-blue-700">Upload</button>
                <button type="button" phx-click="cancel_upload" class="text-gray-500 hover:text-gray-700 text-sm">Cancel</button>
              </form>
            </div>
          <% end %>

          <div class="grid grid-cols-6 gap-3">
            <%= for sprite <- sprites do %>
              <div class="bg-white border rounded-lg p-3 text-center group relative">
                <img
                  src={"#{Sprites.sprite_url(sprite.key)}?v=#{sprite.hash}"}
                  class="w-16 h-16 mx-auto mb-2 object-contain bg-gray-100 rounded"
                  onerror="this.style.display='none'"
                />
                <div class="text-xs text-gray-600 truncate" title={sprite.key}>
                  {sprite.key |> String.split("/", parts: 2) |> List.last()}
                </div>
                <div class="text-xs text-gray-400">{format_size(sprite.size)}</div>
                <div class="absolute top-1 right-1 hidden group-hover:flex gap-1">
                  <button
                    phx-click="start_replace"
                    phx-value-key={sprite.key}
                    class="text-xs bg-blue-100 text-blue-600 px-1.5 py-0.5 rounded hover:bg-blue-200"
                    title="Replace"
                  >R</button>
                  <button
                    phx-click="delete_sprite"
                    phx-value-key={sprite.key}
                    data-confirm={"Delete sprite '#{sprite.key}'?"}
                    class="text-xs bg-red-100 text-red-600 px-1.5 py-0.5 rounded hover:bg-red-200"
                    title="Delete"
                  >X</button>
                </div>

                <%= if @replace_key == sprite.key do %>
                  <div class="mt-2 border-t pt-2">
                    <form phx-submit="save_upload" phx-change="noop" class="space-y-1">
                      <.live_file_input upload={@uploads.sprite} class="text-xs w-full" />
                      <div class="flex gap-1 justify-center">
                        <button type="submit" class="text-xs bg-blue-600 text-white px-2 py-0.5 rounded">Replace</button>
                        <button type="button" phx-click="cancel_upload" class="text-xs text-gray-500">Cancel</button>
                      </div>
                    </form>
                  </div>
                <% end %>
              </div>
            <% end %>
          </div>
        </div>
      <% end %>

      <%= if @categories == [] do %>
        <p class="text-gray-400 italic text-center py-12">No sprites found in priv/static/assets/sprites/</p>
      <% end %>
    </div>
    """
  end

  defp format_size(bytes) when bytes < 1024, do: "#{bytes} B"
  defp format_size(bytes), do: "#{Float.round(bytes / 1024, 1)} KB"
end
