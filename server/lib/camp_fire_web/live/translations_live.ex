defmodule CampFireWeb.TranslationsLive do
  use CampFireWeb, :live_view

  alias CampFire.Translations

  def mount(_params, _session, socket) do
    locales = Translations.supported_locales()
    locales = if "en" not in locales, do: ["en" | locales], else: locales

    {:ok,
     assign(socket,
       active_tab: :translations,
       locale_filter: "en",
       prefix_filter: "",
       translations: Translations.list_translations("en"),
       locales: locales,
       editing_id: nil,
       edit_value: "",
       new_locale: "",
       new_key: "",
       new_value: "",
       show_new_form: false
     )}
  end

  def handle_params(_params, _uri, socket) do
    {:noreply, socket}
  end

  def handle_event("filter_locale", %{"locale" => locale}, socket) do
    translations = Translations.list_translations(locale)
    {:noreply, assign(socket, locale_filter: locale, translations: translations, editing_id: nil)}
  end

  def handle_event("filter_prefix", %{"prefix" => prefix}, socket) do
    {:noreply, assign(socket, prefix_filter: prefix)}
  end

  def handle_event("edit", %{"id" => id}, socket) do
    id = String.to_integer(id)
    t = Enum.find(socket.assigns.translations, &(&1.id == id))
    {:noreply, assign(socket, editing_id: id, edit_value: (t && t.value) || "")}
  end

  def handle_event("cancel_edit", _params, socket) do
    {:noreply, assign(socket, editing_id: nil, edit_value: "")}
  end

  def handle_event("save_edit", %{"value" => value}, socket) do
    t = Enum.find(socket.assigns.translations, &(&1.id == socket.assigns.editing_id))

    if t do
      Translations.upsert_translation(%{locale: t.locale, key: t.key, value: value})
      CampFire.ConfigCache.refresh()
    end

    translations = Translations.list_translations(socket.assigns.locale_filter)
    {:noreply, assign(socket, translations: translations, editing_id: nil, edit_value: "")}
  end

  def handle_event("toggle_new", _params, socket) do
    {:noreply, assign(socket, show_new_form: !socket.assigns.show_new_form)}
  end

  def handle_event("save_new", %{"locale" => locale, "key" => key, "value" => value}, socket) do
    if locale != "" and key != "" and value != "" do
      Translations.upsert_translation(%{locale: locale, key: key, value: value})
      CampFire.ConfigCache.refresh()
      locales = Translations.supported_locales()
      locales = if "en" not in locales, do: ["en" | locales], else: locales
      translations = Translations.list_translations(socket.assigns.locale_filter)

      {:noreply,
       assign(socket,
         translations: translations,
         locales: locales,
         show_new_form: false,
         new_locale: "",
         new_key: "",
         new_value: ""
       )}
    else
      {:noreply, socket}
    end
  end

  def handle_event("delete", %{"id" => id}, socket) do
    Translations.delete_translation(String.to_integer(id))
    CampFire.ConfigCache.refresh()
    translations = Translations.list_translations(socket.assigns.locale_filter)
    {:noreply, assign(socket, translations: translations)}
  end

  def render(assigns) do
    filtered =
      assigns.translations
      |> Enum.filter(fn t ->
        assigns.prefix_filter == "" or String.starts_with?(t.key, assigns.prefix_filter)
      end)
      |> Enum.sort_by(& &1.key)

    assigns = assign(assigns, :filtered, filtered)

    ~H"""
    <div>
      <div class="flex items-center justify-between mb-4">
        <h1 class="text-xl font-semibold text-gray-900">Translations</h1>
        <span class="text-sm text-gray-500"><%= length(@filtered) %> translations</span>
      </div>

      <div class="flex items-center gap-3 mb-4">
        <form phx-change="filter_locale">
          <select name="locale" class="rounded border border-gray-300 px-2 py-1 text-sm">
            <%= for l <- @locales do %>
              <option value={l} selected={l == @locale_filter}><%= l %></option>
            <% end %>
          </select>
        </form>

        <form phx-change="filter_prefix" class="flex-1 max-w-xs">
          <input type="text" name="prefix" value={@prefix_filter} placeholder="Filter by key prefix..."
            class="w-full rounded border border-gray-300 px-2 py-1 text-sm" />
        </form>

        <button phx-click="toggle_new" class="rounded bg-blue-600 px-3 py-1 text-sm text-white hover:bg-blue-700">
          <%= if @show_new_form, do: "Cancel", else: "+ Add Translation" %>
        </button>
      </div>

      <%= if @show_new_form do %>
        <form phx-submit="save_new" class="flex items-center gap-2 mb-4 p-3 bg-gray-50 rounded border border-gray-200">
          <input type="text" name="locale" value={@new_locale} placeholder="Locale (e.g. ja)"
            class="rounded border border-gray-300 px-2 py-1 text-sm w-20" />
          <input type="text" name="key" value={@new_key} placeholder="Key (e.g. ui.button.harvest)"
            class="rounded border border-gray-300 px-2 py-1 text-sm w-64" />
          <input type="text" name="value" value={@new_value} placeholder="Value"
            class="rounded border border-gray-300 px-2 py-1 text-sm flex-1" />
          <button type="submit" class="rounded bg-green-600 px-3 py-1 text-sm text-white hover:bg-green-700">Save</button>
        </form>
      <% end %>

      <div class="rounded border border-gray-200 overflow-hidden">
        <table class="w-full text-sm">
          <thead>
            <tr class="bg-gray-50 border-b border-gray-200">
              <th class="text-left px-3 py-2 font-medium text-gray-600 w-2/5">Key</th>
              <th class="text-left px-3 py-2 font-medium text-gray-600">Value</th>
              <th class="text-right px-3 py-2 font-medium text-gray-600 w-28">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            <%= for t <- @filtered do %>
              <tr class="hover:bg-gray-50">
                <td class="px-3 py-1.5 font-mono text-xs text-gray-700 align-top"><%= t.key %></td>
                <td class="px-3 py-1.5 text-gray-900 align-top">
                  <%= if @editing_id == t.id do %>
                    <form phx-submit="save_edit" class="flex items-center gap-1">
                      <input type="text" name="value" value={@edit_value}
                        class="rounded border border-gray-300 px-2 py-0.5 text-sm flex-1" autofocus />
                      <button type="submit" class="rounded bg-green-600 px-2 py-0.5 text-xs text-white hover:bg-green-700">Save</button>
                      <button type="button" phx-click="cancel_edit" class="rounded border border-gray-300 px-2 py-0.5 text-xs hover:bg-gray-100">Cancel</button>
                    </form>
                  <% else %>
                    <%= t.value %>
                  <% end %>
                </td>
                <td class="text-right px-3 py-1.5 align-top whitespace-nowrap">
                  <%= if @editing_id != t.id do %>
                    <button phx-click="edit" phx-value-id={t.id} class="text-xs text-blue-600 hover:text-blue-800 mr-2">Edit</button>
                    <button phx-click="delete" phx-value-id={t.id} class="text-xs text-red-500 hover:text-red-700" data-confirm="Delete this translation?">Delete</button>
                  <% end %>
                </td>
              </tr>
            <% end %>
          </tbody>
        </table>
      </div>
    </div>
    """
  end
end
