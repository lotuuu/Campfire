defmodule CampFireWeb.VisitorsLive do
  use CampFireWeb, :live_view

  def mount(_params, _session, socket) do
    {:ok, assign(socket, active_tab: :visitors)}
  end

  def render(assigns) do
    ~H"""
    <div>
      <h2 class="text-2xl font-bold mb-4">Visitors</h2>
      <p class="text-gray-500">Coming soon...</p>
    </div>
    """
  end
end
