defmodule CampFire.SpriteManifest do
  @sprites_dir "priv/static/assets/sprites"

  def build do
    base = Application.app_dir(:camp_fire, @sprites_dir)

    if File.dir?(base) do
      base
      |> scan_dir("")
      |> Map.new()
    else
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
