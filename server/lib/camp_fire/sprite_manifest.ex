defmodule CampFire.SpriteManifest do
  require Logger

  def build do
    base = Path.join(to_string(:code.priv_dir(:camp_fire)), "static/assets/sprites")

    if File.dir?(base) do
      result = base |> scan_dir("") |> Map.new()
      Logger.info("SpriteManifest: found #{map_size(result)} sprites in #{base}")
      result
    else
      Logger.warning("SpriteManifest: directory not found at #{base}")
      %{}
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
          hash = hash_file(full)
          [{key, hash}]

        true ->
          []
      end
    end)
  end

  defp hash_file(path) do
    path
    |> File.read!()
    |> then(&:crypto.hash(:md5, &1))
    |> Base.encode16(case: :lower)
    |> String.slice(0, 8)
  end
end
