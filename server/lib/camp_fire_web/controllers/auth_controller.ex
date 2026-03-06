defmodule CampFireWeb.AuthController do
  use CampFireWeb, :controller
  alias CampFire.Accounts

  def register(conn, _params) do
    case Accounts.register_player() do
      {:ok, result} ->
        conn
        |> put_status(201)
        |> json(%{
          uid: result.uid,
          authToken: result.auth_token,
          friendCode: result.friend_code,
          displayName: result.display_name
        })

      {:error, _reason} ->
        conn |> put_status(500) |> json(%{error: "Registration failed"})
    end
  end

  def me(conn, _params) do
    player = conn.assigns.current_player
    json(conn, %{uid: player.uid, friendCode: player.friend_code, displayName: player.display_name})
  end

  def update_display_name(conn, %{"displayName" => display_name}) do
    player = conn.assigns.current_player

    case Accounts.update_display_name(player, display_name) do
      {:ok, updated} ->
        json(conn, %{displayName: updated.display_name})

      {:error, changeset} ->
        message = format_errors(changeset)
        conn |> put_status(400) |> json(%{error: message})
    end
  end

  def update_display_name(conn, _params) do
    conn |> put_status(400) |> json(%{error: "displayName is required"})
  end

  defp format_errors(changeset) do
    Ecto.Changeset.traverse_errors(changeset, fn {msg, opts} ->
      Regex.replace(~r"%{(\w+)}", msg, fn _, key ->
        opts |> Keyword.get(String.to_existing_atom(key), key) |> to_string()
      end)
    end)
    |> Enum.map_join(", ", fn {_field, errors} -> Enum.join(errors, ", ") end)
  end
end
