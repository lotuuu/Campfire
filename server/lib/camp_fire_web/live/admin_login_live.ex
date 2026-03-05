defmodule CampFireWeb.AdminLoginLive do
  use CampFireWeb, :live_view

  def mount(_params, _session, socket) do
    {:ok, assign(socket, active_tab: nil)}
  end

  def render(assigns) do
    ~H"""
    <div class="min-h-screen flex items-center justify-center bg-gray-50">
      <div class="bg-white p-8 rounded-lg shadow-md w-96">
        <h1 class="text-xl font-bold mb-4">Camp Fire Admin</h1>
        <form method="post" action="/admin/login">
          <input type="hidden" name="_csrf_token" value={Phoenix.Controller.get_csrf_token()} />
          <input
            type="password"
            name="secret"
            placeholder="Admin secret"
            class="w-full border rounded px-3 py-2 mb-4"
            autofocus
          />
          <button type="submit" class="w-full bg-gray-900 text-white py-2 rounded hover:bg-gray-800">
            Login
          </button>
        </form>
      </div>
    </div>
    """
  end
end
