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
    <div style="max-width: 1000px; margin: 0 auto; padding: 20px;">
      <h1>Translations</h1>

      <div style="display: flex; gap: 16px; margin-bottom: 20px; align-items: center;">
        <form phx-change="filter_locale">
          <select name="locale" style="padding: 6px;">
            <%= for l <- @locales do %>
              <option value={l} selected={l == @locale_filter}><%= l %></option>
            <% end %>
          </select>
        </form>

        <form phx-change="filter_prefix">
          <input type="text" name="prefix" value={@prefix_filter} placeholder="Filter by key prefix..." style="padding: 6px; width: 300px;" />
        </form>

        <button phx-click="toggle_new" style="padding: 6px 12px;">
          <%= if @show_new_form, do: "Cancel", else: "+ Add Translation" %>
        </button>
      </div>

      <%= if @show_new_form do %>
        <form phx-submit="save_new" style="display: flex; gap: 8px; margin-bottom: 20px; padding: 12px; background: #f5f5f5; border-radius: 4px;">
          <input type="text" name="locale" value={@new_locale} placeholder="Locale (e.g. ja)" style="padding: 6px; width: 80px;" />
          <input type="text" name="key" value={@new_key} placeholder="Key (e.g. ui.button.harvest)" style="padding: 6px; width: 300px;" />
          <input type="text" name="value" value={@new_value} placeholder="Value" style="padding: 6px; flex: 1;" />
          <button type="submit" style="padding: 6px 12px;">Save</button>
        </form>
      <% end %>

      <p style="color: #666;"><%= length(@filtered) %> translations</p>

      <table style="width: 100%; border-collapse: collapse;">
        <thead>
          <tr style="border-bottom: 2px solid #ddd;">
            <th style="text-align: left; padding: 8px;">Key</th>
            <th style="text-align: left; padding: 8px;">Value</th>
            <th style="text-align: right; padding: 8px; width: 120px;">Actions</th>
          </tr>
        </thead>
        <tbody>
          <%= for t <- @filtered do %>
            <tr style="border-bottom: 1px solid #eee;">
              <td style="padding: 8px; font-family: monospace; font-size: 13px;"><%= t.key %></td>
              <td style="padding: 8px;">
                <%= if @editing_id == t.id do %>
                  <form phx-submit="save_edit" style="display: flex; gap: 4px;">
                    <input type="text" name="value" value={@edit_value} style="padding: 4px; flex: 1;" autofocus />
                    <button type="submit" style="padding: 4px 8px;">Save</button>
                    <button type="button" phx-click="cancel_edit" style="padding: 4px 8px;">Cancel</button>
                  </form>
                <% else %>
                  <%= t.value %>
                <% end %>
              </td>
              <td style="text-align: right; padding: 8px;">
                <%= if @editing_id != t.id do %>
                  <button phx-click="edit" phx-value-id={t.id} style="padding: 2px 8px;">Edit</button>
                  <button phx-click="delete" phx-value-id={t.id} style="padding: 2px 8px; color: red;" data-confirm="Delete this translation?">Delete</button>
                <% end %>
              </td>
            </tr>
          <% end %>
        </tbody>
      </table>
    </div>
    """
  end
end
