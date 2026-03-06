# Admin Sprite Management Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add admin UI for viewing, uploading, replacing, and deleting sprites — both on a dedicated sprites page and inline on entity edit forms.

**Architecture:** New `CampFire.Sprites` context module for filesystem operations. New `SpritesLive` LiveView for the dedicated page. Modify `ItemsLive` and `QuestsLive` to add inline thumbnails and uploads. All use Phoenix LiveView's built-in `allow_upload` / `consume_uploaded_entries`.

**Tech Stack:** Elixir/Phoenix LiveView, Tailwind CSS, filesystem I/O.

---

## Task 1: Sprites Context Module

Create `CampFire.Sprites` with filesystem operations for listing, uploading, and deleting sprites.

**Files:**
- Create: `server/lib/camp_fire/sprites.ex`

**Step 1: Create the module**

```elixir
defmodule CampFire.Sprites do
  @sprites_dir "priv/static/assets/sprites"

  def sprites_dir do
    Application.app_dir(:camp_fire, @sprites_dir)
  end

  def list_sprites do
    base = sprites_dir()

    if File.dir?(base) do
      base
      |> scan_dir("")
      |> Enum.sort_by(fn s -> s.key end)
    else
      []
    end
  end

  def upload_sprite(key, binary_data) do
    path = sprite_path(key)
    File.mkdir_p!(Path.dirname(path))
    File.write!(path, binary_data)
    refresh_manifest()
    :ok
  end

  def delete_sprite(key) do
    path = sprite_path(key)

    if File.exists?(path) do
      File.rm!(path)
      cleanup_empty_dirs(Path.dirname(path))
      refresh_manifest()
      :ok
    else
      {:error, :not_found}
    end
  end

  def sprite_exists?(key) do
    File.exists?(sprite_path(key))
  end

  def sprite_url(key) do
    "/assets/sprites/#{key}.png"
  end

  defp sprite_path(key) do
    Path.join(sprites_dir(), "#{key}.png")
  end

  defp refresh_manifest do
    sprite_manifest = CampFire.SpriteManifest.build()
    :ets.insert(:config_cache, {"sprite_manifest", sprite_manifest})
  end

  defp cleanup_empty_dirs(dir) do
    base = sprites_dir()
    if dir != base and File.ls!(dir) == [] do
      File.rmdir!(dir)
      cleanup_empty_dirs(Path.dirname(dir))
    end
  end

  defp scan_dir(base, prefix) do
    path = Path.join(base, prefix)

    path
    |> File.ls!()
    |> Enum.flat_map(fn entry ->
      full = Path.join(path, entry)
      rel = if prefix == "", do: entry, else: "#{prefix}/#{entry}"

      cond do
        File.dir?(full) ->
          scan_dir(base, rel)

        String.ends_with?(entry, ".png") ->
          key = String.replace_suffix(rel, ".png", "")
          %File.Stat{size: size} = File.stat!(full)
          [%{key: key, size: size, category: category(key)}]

        true ->
          []
      end
    end)
  end

  defp category(key) do
    key |> String.split("/") |> hd()
  end
end
```

**Step 2: Commit**

```bash
git add server/lib/camp_fire/sprites.ex
git commit -m "feat(server): Sprites context module for filesystem sprite operations"
```

---

## Task 2: Sprites LiveView — Dedicated Page

Create the `/admin/sprites` page with grid view, upload, replace, and delete.

**Files:**
- Create: `server/lib/camp_fire_web/live/sprites_live.ex`
- Modify: `server/lib/camp_fire_web/router.ex:39-53` (add route)
- Modify: `server/lib/camp_fire_web/components/layouts/admin.html.heex` (add nav link)

**Step 1: Add route**

In `router.ex`, inside the authenticated admin scope (after line 52, the weather route), add:

```elixir
    live "/sprites", SpritesLive, :index
```

**Step 2: Add nav link**

In `admin.html.heex`, after the Weather link (line 11), add:

```heex
    <.link navigate="/admin/sprites" class={"block px-4 py-2 hover:bg-gray-800 #{if @active_tab == :sprites, do: "bg-gray-800 text-white", else: ""}"}>Sprites</.link>
```

**Step 3: Create SpritesLive**

```elixir
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

  # ── Events ──

  def handle_event("start_upload", %{"category" => category}, socket) do
    {:noreply, assign(socket, upload_category: category, replace_key: nil, new_key: "")}
  end

  def handle_event("start_replace", %{"key" => key}, socket) do
    {:noreply, assign(socket, replace_key: key, upload_category: nil)}
  end

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

  # ── Render ──

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
        {@sprites |> length()} sprites across {length(@categories)} categories.
        Sprites are served at <code>/assets/sprites/{"{key}"}.png</code>
      </p>

      <%= for {category, sprites} <- @categories do %>
        <div class="mb-8">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-lg font-semibold capitalize">{category}</h3>
            <button
              phx-click="start_upload"
              phx-value-category={category}
              class="text-sm bg-green-100 text-green-700 px-3 py-1 rounded hover:bg-green-200"
            >
              + Add to {category}
            </button>
          </div>

          <%!-- Upload form for this category --%>
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
                  src={Sprites.sprite_url(sprite.key)}
                  class="w-16 h-16 mx-auto mb-2 object-contain bg-gray-100 rounded"
                  onerror="this.style.display='none'"
                />
                <div class="text-xs text-gray-600 truncate" title={sprite.key}>
                  {sprite.key |> String.split("/") |> List.last()}
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

                <%!-- Replace upload inline --%>
                <%= if @replace_key == sprite.key do %>
                  <div class="mt-2 border-t pt-2">
                    <form phx-submit="save_upload" class="space-y-1">
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
```

**Step 4: Commit**

```bash
git add server/lib/camp_fire_web/live/sprites_live.ex server/lib/camp_fire_web/router.ex server/lib/camp_fire_web/components/layouts/admin.html.heex
git commit -m "feat(server): admin sprites page with grid view, upload, replace, delete"
```

---

## Task 3: Inline Sprite Thumbnail + Upload on Seed Edit Form

Add a sprite preview and upload field to the seed edit form in `ItemsLive`.

**Files:**
- Modify: `server/lib/camp_fire_web/live/items_live.ex`

**Step 1: Add upload allowance in mount**

In `mount/3`, add `allow_upload` to the socket:

```elixir
    {:ok,
     socket
     |> assign(...)
     |> allow_upload(:icon,
       accept: ~w(.png),
       max_file_size: 512_000,
       max_entries: 1
     )}
```

**Step 2: Add thumbnail + upload to seed edit form render**

In `render_seeds/1`, inside the edit form (after the `<h3>` with "Edit: ..."), add a sprite preview section:

```heex
          <%!-- Sprite preview --%>
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center">
              <img
                src={CampFire.Sprites.sprite_url("seeds/#{String.downcase(@editing.seed_name)}/icon")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
```

**Step 3: Handle icon upload on save**

In `handle_event("save_seed", ...)`, before the existing save logic, consume any uploaded icon:

```elixir
    # Upload icon if provided
    uploaded =
      consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
        key = "seeds/#{String.downcase(seed.seed_name)}/icon"
        data = File.read!(path)
        CampFire.Sprites.upload_sprite(key, data)
        {:ok, key}
      end)
```

This runs before the seed save — the icon is written to disk regardless of whether the form save succeeds (acceptable for admin tooling).

**Step 4: Commit**

```bash
git add server/lib/camp_fire_web/live/items_live.ex
git commit -m "feat(server): inline sprite thumbnail + upload on seed edit form"
```

---

## Task 4: Inline Sprite Thumbnail + Upload on Quest Edit Form

Same pattern as Task 3 but for quests.

**Files:**
- Modify: `server/lib/camp_fire_web/live/quests_live.ex`

**Step 1: Add upload allowance in mount**

Add `allow_upload(:icon, ...)` to `QuestsLive.mount/3`.

**Step 2: Add thumbnail + upload to quest edit form**

In the quest edit form render, add after the `<h3>`:

```heex
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center">
              <img
                src={CampFire.Sprites.sprite_url("quests/#{String.downcase(@editing.quest_name)}")}
                class="w-14 h-14 object-contain"
                onerror="this.parentElement.innerHTML='<span class=\'text-xs text-gray-400\'>No icon</span>'"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Replace icon</label>
              <.live_file_input upload={@uploads.icon} class="text-sm" />
            </div>
          </div>
```

**Step 3: Handle icon upload on save**

In `handle_event("save", ...)` for quests, consume uploaded icon:

```elixir
    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      key = "quests/#{String.downcase(quest.quest_name)}"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)
```

**Step 4: Commit**

```bash
git add server/lib/camp_fire_web/live/quests_live.ex
git commit -m "feat(server): inline sprite thumbnail + upload on quest edit form"
```

---

## Task 5: Inline Sprite Thumbnail + Upload on Skin Edit Form

Same pattern for skins, which are already on the Items page.

**Files:**
- Modify: `server/lib/camp_fire_web/live/items_live.ex`

**Step 1: Add thumbnail + upload to skin edit form**

In `render_skins/1`, inside the edit form, add after the `<h3>`:

```heex
          <div class="flex items-center gap-4 mb-4">
            <div class="w-16 h-16 bg-gray-100 rounded border flex items-center justify-center">
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
```

**Step 2: Handle icon upload on skin save**

In `handle_event("save_skin", ...)`, consume uploaded icon:

```elixir
    consume_uploaded_entries(socket, :icon, fn %{path: path}, _entry ->
      key = "skins/#{String.downcase(old_name)}"
      data = File.read!(path)
      CampFire.Sprites.upload_sprite(key, data)
      {:ok, key}
    end)
```

**Step 3: Commit**

```bash
git add server/lib/camp_fire_web/live/items_live.ex
git commit -m "feat(server): inline sprite thumbnail + upload on skin edit form"
```

---

## Task 6: Add Thumbnails to Entity List Tables

Show small sprite thumbnails in the list/table views (not just edit forms).

**Files:**
- Modify: `server/lib/camp_fire_web/live/items_live.ex` (seed table, skin table)
- Modify: `server/lib/camp_fire_web/live/quests_live.ex` (quest table)

**Step 1: Seed table — add icon column**

In `render_seeds/1`, add a column to the table header:

```heex
<th class="px-4 py-3 text-left text-sm font-medium text-gray-500 w-12"></th>
```

And in each row, before the seed name cell:

```heex
<td class="px-4 py-3">
  <img src={CampFire.Sprites.sprite_url("seeds/#{String.downcase(seed.seed_name)}/icon")}
    class="w-8 h-8 object-contain" onerror="this.style.display='none'" />
</td>
```

**Step 2: Quest table — same pattern**

Add icon column showing `quests/{quest_name_lowercase}`.

**Step 3: Skin table — same pattern**

Add icon column showing `skins/{skin_name_lowercase}`.

**Step 4: Commit**

```bash
git add server/lib/camp_fire_web/live/items_live.ex server/lib/camp_fire_web/live/quests_live.ex
git commit -m "feat(server): show sprite thumbnails in entity list tables"
```

---

## Task 7: Manual Test

**Step 1: Start the server**

```bash
cd server && mix phx.server
```

**Step 2: Verify sprites page**

Navigate to `http://localhost:4000/admin/sprites`. Verify:
- All 57 sprites show in a grid grouped by category
- Thumbnails render correctly
- Upload a new sprite to a category
- Replace an existing sprite
- Delete a sprite

**Step 3: Verify inline uploads**

Navigate to Items > Seeds > Edit any seed. Verify:
- Current icon thumbnail shows
- Upload a new icon — it replaces the file and thumbnail updates

Same for Quests and Skins.

**Step 4: Verify manifest updates**

After uploading/deleting, verify the configs endpoint reflects changes:
```bash
curl -s localhost:4000/game/configs -H "Authorization: Bearer <token>" | jq '.sprites | length'
```

**Step 5: Commit any fixes**

```bash
git commit -m "fix: address issues from admin sprites testing"
```
