defmodule CampFireWeb.SpritesLive do
  use CampFireWeb, :live_view

  alias CampFire.Sprites

  @max_file_size 5_000_000
  @accept ~w(.png)

  def mount(_params, _session, socket) do
    {:ok,
     socket
     |> assign(
       active_tab: :sprites,
       sprites: Sprites.list_sprites(),
       new_key: "",
       upload_category: nil,
       edit_key: nil,
       edit_name: ""
     )
     |> allow_upload(:sprite,
       accept: @accept,
       max_file_size: @max_file_size,
       max_entries: 1
     )}
  end

  def handle_event("start_upload", %{"category" => category}, socket) do
    {:noreply, assign(socket, upload_category: category, edit_key: nil, new_key: "")}
  end

  def handle_event("start_edit", %{"key" => key}, socket) do
    {:noreply, assign(socket, edit_key: key, edit_name: key, upload_category: nil)}
  end

  def handle_event("noop", _params, socket), do: {:noreply, socket}

  def handle_event("cancel_upload", _params, socket) do
    {:noreply, assign(socket, upload_category: nil, edit_key: nil, new_key: "")}
  end

  def handle_event("update_edit", %{"name" => name}, socket) do
    {:noreply, assign(socket, edit_name: name)}
  end

  def handle_event("update_new_key", %{"key" => key}, socket) do
    {:noreply, assign(socket, new_key: key)}
  end

  def handle_event("save_edit", _params, socket) do
    old_key = socket.assigns.edit_key
    new_key = String.trim(socket.assigns.edit_name)
    has_upload? = socket.assigns.uploads.sprite.entries != []

    cond do
      new_key == "" ->
        {:noreply, put_flash(socket, :error, "Name cannot be empty")}

      new_key != old_key ->
        case Sprites.rename_sprite(old_key, new_key) do
          :ok ->
            if has_upload? do
              consume_uploaded_entries(socket, :sprite, fn %{path: path}, _entry ->
                Sprites.upload_sprite(new_key, File.read!(path))
                {:ok, new_key}
              end)
            end

            {:noreply,
             socket
             |> put_flash(:info, "Updated '#{old_key}' → '#{new_key}'")
             |> assign(sprites: Sprites.list_sprites(), edit_key: nil, edit_name: "")}

          {:error, :not_found} ->
            {:noreply, put_flash(socket, :error, "Sprite not found")}

          {:error, :already_exists} ->
            {:noreply, put_flash(socket, :error, "A sprite named '#{new_key}' already exists")}
        end

      has_upload? ->
        consume_uploaded_entries(socket, :sprite, fn %{path: path}, _entry ->
          Sprites.upload_sprite(old_key, File.read!(path))
          {:ok, old_key}
        end)

        {:noreply,
         socket
         |> put_flash(:info, "Replaced image for '#{old_key}'")
         |> assign(sprites: Sprites.list_sprites(), edit_key: nil, edit_name: "")}

      true ->
        {:noreply, assign(socket, edit_key: nil, edit_name: "")}
    end
  end

  def handle_event("save_upload", _params, socket) do
    category = socket.assigns.upload_category
    name = String.trim(socket.assigns.new_key)
    key = if name == "", do: nil, else: "#{category}/#{name}"

    if key do
      consume_uploaded_entries(socket, :sprite, fn %{path: path}, _entry ->
        Sprites.upload_sprite(key, File.read!(path))
        {:ok, key}
      end)

      {:noreply,
       socket
       |> put_flash(:info, "Sprite '#{key}' uploaded")
       |> assign(sprites: Sprites.list_sprites(), upload_category: nil, new_key: "")}
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

          <%= if category == "portraits" do %>
            <details class="mb-4 text-sm text-gray-500 border border-gray-200 rounded-lg p-3">
              <summary class="cursor-pointer font-medium text-gray-600">Naming guide</summary>
              <div class="mt-2 space-y-1.5">
                <p>Each visitor has a <code class="bg-gray-100 px-1 rounded">portrait_id</code> set in the Visitors admin. The sprite key is <code class="bg-gray-100 px-1 rounded">portraits/{"{portrait_id}"}</code>.</p>
                <div class="grid grid-cols-2 gap-x-6 gap-y-1 mt-2 font-mono text-xs">
                  <div class="font-semibold col-span-2 text-gray-600 text-sm mt-1">Current visitors</div>
                  <div>thorn</div><div class="text-gray-400">Thorn (merchant)</div>
                  <div>willow</div><div class="text-gray-400">Willow (gifter)</div>
                  <div>ember</div><div class="text-gray-400">Ember (quester)</div>
                </div>
                <p class="mt-2">Use lowercase names matching the visitor's <code class="bg-gray-100 px-1 rounded">portrait_id</code>. Add new portraits here when creating new visitor templates.</p>
              </div>
            </details>
          <% end %>

          <%= if category == "hex" do %>
            <details class="mb-4 text-sm text-gray-500 border border-gray-200 rounded-lg p-3">
              <summary class="cursor-pointer font-medium text-gray-600">Naming guide</summary>
              <div class="mt-2 space-y-1.5">
                <p>Sprites with <strong>numeric names</strong> act as percentage thresholds. The client picks the highest number &le; the current %.</p>
                <div class="grid grid-cols-2 gap-x-6 gap-y-1 mt-2 font-mono text-xs">
                  <div class="font-semibold col-span-2 text-gray-600 text-sm mt-1">Vases (water %)</div>
                  <div>vase/<strong>0</strong></div><div class="text-gray-400">0 &ndash; 49%</div>
                  <div>vase/<strong>50</strong></div><div class="text-gray-400">50 &ndash; 99%</div>
                  <div>vase/<strong>100</strong></div><div class="text-gray-400">100%</div>
                  <div class="font-semibold col-span-2 text-gray-600 text-sm mt-1">Crops (growth %)</div>
                  <div>plot/<strong>{"{seed}"}</strong>/0</div><div class="text-gray-400">0 &ndash; 49%</div>
                  <div>plot/<strong>{"{seed}"}</strong>/50</div><div class="text-gray-400">50 &ndash; 99%</div>
                  <div>plot/<strong>{"{seed}"}</strong>/100</div><div class="text-gray-400">100% (mature)</div>
                  <div class="font-semibold col-span-2 text-gray-600 text-sm mt-1">Other</div>
                  <div>terrain</div><div class="text-gray-400">empty cell</div>
                  <div>flame</div><div class="text-gray-400">Spark of Ara</div>
                  <div>plot/empty</div><div class="text-gray-400">plot with no seed</div>
                  <div>garden/empty</div><div class="text-gray-400">garden with no plant</div>
                  <div>garden/{"{plant}"}/mature</div><div class="text-gray-400">mature garden</div>
                  <div>house</div><div class="text-gray-400">Mallum house</div>
                  <div>bird</div><div class="text-gray-400">visiting bird</div>
                  <div>visitor</div><div class="text-gray-400">camp visitor</div>
                  <div>apotheke</div><div class="text-gray-400">mixing station</div>
                </div>
                <p class="mt-2">Add thresholds as needed &mdash; e.g. <code class="bg-gray-100 px-1 rounded">vase/10</code> would show for 10&ndash;49% if 50 is the next threshold.</p>
              </div>
            </details>
          <% end %>

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
              <%= for entry <- @uploads.sprite.entries do %>
                <%= for err <- upload_errors(@uploads.sprite, entry) do %>
                  <p class="text-red-600 text-sm mt-2"><%= upload_error_to_string(err) %></p>
                <% end %>
              <% end %>
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
                    phx-click="start_edit"
                    phx-value-key={sprite.key}
                    class="text-xs bg-blue-100 text-blue-600 px-1.5 py-0.5 rounded hover:bg-blue-200"
                    title="Edit"
                  >E</button>
                  <button
                    phx-click="delete_sprite"
                    phx-value-key={sprite.key}
                    data-confirm={"Delete sprite '#{sprite.key}'?"}
                    class="text-xs bg-red-100 text-red-600 px-1.5 py-0.5 rounded hover:bg-red-200"
                    title="Delete"
                  >X</button>
                </div>

                <%= if @edit_key == sprite.key do %>
                  <div class="mt-2 border-t pt-2">
                    <form phx-submit="save_edit" phx-change="update_edit" class="space-y-1.5">
                      <input type="text" name="name" value={@edit_name}
                        class="text-xs border rounded px-1.5 py-0.5 w-full" />
                      <.live_file_input upload={@uploads.sprite} class="text-xs w-full" />
                      <div class="flex gap-1 justify-center">
                        <button type="submit" class="text-xs bg-blue-600 text-white px-2 py-0.5 rounded">Save</button>
                        <button type="button" phx-click="cancel_upload" class="text-xs text-gray-500">Cancel</button>
                      </div>
                      <%= for entry <- @uploads.sprite.entries do %>
                        <%= for err <- upload_errors(@uploads.sprite, entry) do %>
                          <p class="text-red-600 text-xs mt-1"><%= upload_error_to_string(err) %></p>
                        <% end %>
                      <% end %>
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

  defp upload_error_to_string(:too_large), do: "File is too large (max 5 MB)"
  defp upload_error_to_string(:not_accepted), do: "Only PNG files are accepted"
  defp upload_error_to_string(:too_many_files), do: "Only one file at a time"
  defp upload_error_to_string(err), do: "Upload error: #{inspect(err)}"
end
