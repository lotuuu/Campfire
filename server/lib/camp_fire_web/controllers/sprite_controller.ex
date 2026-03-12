defmodule CampFireWeb.SpriteController do
  use CampFireWeb, :controller

  alias CampFire.Sprites

  @max_bundle_keys 500

  def bundle(conn, %{"keys" => keys}) when is_list(keys) do
    if length(keys) > @max_bundle_keys do
      conn |> put_status(400) |> json(%{error: "Too many keys (max #{@max_bundle_keys})"})
    else
      build_and_send_zip(conn, keys)
    end
  end

  def bundle(conn, _params) do
    # No keys param = send all sprites
    manifest = CampFire.ConfigCache.get("sprite_manifest") || %{}
    build_and_send_zip(conn, Map.keys(manifest))
  end

  defp build_and_send_zip(conn, keys) do
    sprites_dir = Sprites.sprites_dir()

    zip_entries =
      keys
      |> Enum.reduce([], fn key, acc ->
        path = Path.join(sprites_dir, "#{key}.png")

        if File.exists?(path) do
          # :zip expects charlist filenames and binary content
          [{~c"#{key}.png", File.read!(path)} | acc]
        else
          acc
        end
      end)

    case :zip.create(~c"sprites.zip", zip_entries, [:memory]) do
      {:ok, {_name, zip_binary}} ->
        conn
        |> put_resp_content_type("application/zip")
        |> put_resp_header("content-disposition", "attachment; filename=\"sprites.zip\"")
        |> send_resp(200, zip_binary)

      {:error, reason} ->
        conn |> put_status(500) |> json(%{error: "Failed to create bundle: #{inspect(reason)}"})
    end
  end
end
