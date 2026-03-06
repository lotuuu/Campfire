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
    path = if prefix == "", do: base, else: Path.join(base, prefix)

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
          hash = full |> File.read!() |> then(&:crypto.hash(:md5, &1)) |> Base.encode16(case: :lower) |> String.slice(0, 8)
          [%{key: key, size: size, category: category(key), hash: hash}]

        true ->
          []
      end
    end)
  end

  defp category(key) do
    key |> String.split("/") |> hd()
  end
end
