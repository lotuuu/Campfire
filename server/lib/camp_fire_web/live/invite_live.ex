defmodule CampFireWeb.InviteLive do
  use CampFireWeb, :live_view

  alias CampFire.Accounts

  @impl true
  def mount(%{"code" => code}, _session, socket) do
    player = Accounts.get_player_by_friend_code(code)
    friend_code = String.upcase(code)

    display_name =
      if player && player.display_name && player.display_name != "",
        do: player.display_name,
        else: "A friend"

    {:ok,
     assign(socket,
       friend_code: friend_code,
       display_name: display_name,
       found: player != nil
     ), layout: false}
  end

  @impl true
  def render(assigns) do
    ~H"""
    <html>
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1" />
      <title>Camp Fire - Friend Invite</title>
      <script src="https://cdn.tailwindcss.com">
      </script>
    </head>
    <body>
    <div class="min-h-screen flex flex-col items-center justify-center bg-gradient-to-b from-amber-50 to-orange-100">
      <div class="text-center max-w-lg px-6">
        <div class="text-6xl mb-4">&#128293;</div>
        <h1 class="text-4xl font-bold text-amber-900 mb-2">Camp Fire</h1>

        <%= if @found do %>
          <p class="text-lg text-amber-700 mb-6">
            <strong><%= @display_name %></strong> wants to be your friend!
          </p>

          <div class="bg-white/60 rounded-xl p-6 mb-6 shadow-sm">
            <div class="text-sm text-amber-600 mb-1">Friend code</div>
            <div class="text-2xl font-bold text-amber-800 font-mono"><%= @friend_code %></div>
          </div>

          <a
            id="open-app"
            href={"campfire://invite/#{@friend_code}"}
            class="inline-block px-8 py-3 bg-amber-700 text-white rounded-lg hover:bg-amber-800 transition font-medium text-lg mb-4"
          >
            Open in Camp Fire
          </a>

          <p id="fallback" class="text-sm text-amber-600 hidden mt-4">
            Don't have Camp Fire yet? Copy the friend code above and add it after you install the game.
          </p>

          <script>
            // Show fallback message after a short delay (app didn't open)
            setTimeout(function() {
              document.getElementById('fallback').classList.remove('hidden');
            }, 2000);
          </script>
        <% else %>
          <p class="text-lg text-amber-700 mb-6">
            This invite link is invalid or has expired.
          </p>
        <% end %>
      </div>

      <footer class="mt-16 text-sm text-amber-500">
        Camp Fire
      </footer>
    </div>
    </body>
    </html>
    """
  end
end
