defmodule CampFireWeb.HomeLive do
  use CampFireWeb, :live_view

  alias CampFire.Accounts

  @impl true
  def mount(_params, _session, socket) do
    player_count = Accounts.count_players()
    {:ok, assign(socket, player_count: player_count), layout: false}
  end

  def render(assigns) do
    ~H"""
    <div class="min-h-screen flex flex-col items-center justify-center bg-gradient-to-b from-amber-50 to-orange-100">
      <div class="text-center max-w-lg px-6">
        <div class="text-6xl mb-4">&#128293;</div>
        <h1 class="text-4xl font-bold text-amber-900 mb-2">Camp Fire</h1>
        <p class="text-lg text-amber-700 mb-8">
          A campsite management game built around the Spark of Ara.
          Grow plants, send Mallums on quests, and tend your magical flame.
        </p>

        <div class="bg-white/60 rounded-xl p-6 mb-8 shadow-sm">
          <div class="text-3xl font-bold text-amber-800"><%= @player_count %></div>
          <div class="text-sm text-amber-600 mt-1">registered players</div>
        </div>

        <div class="flex gap-4 justify-center">
          <a href="/admin/login" class="px-5 py-2.5 bg-amber-700 text-white rounded-lg hover:bg-amber-800 transition font-medium">
            Admin Panel
          </a>
          <a href="/health" class="px-5 py-2.5 bg-white text-amber-700 border border-amber-300 rounded-lg hover:bg-amber-50 transition font-medium">
            Health Check
          </a>
        </div>
      </div>

      <footer class="mt-16 text-sm text-amber-500">
        Camp Fire Server
      </footer>
    </div>
    """
  end
end
