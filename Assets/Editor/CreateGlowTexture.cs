using UnityEngine;
using UnityEditor;
using System.IO;

public static class CreateGlowTexture
{
    [MenuItem("Tools/Create Glow Particle Texture")]
    public static void Create()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float maxDist = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(dist / maxDist);
                // Gaussian falloff for soft ethereal glow
                float alpha = Mathf.Exp(-4.5f * t * t);
                // Kill the outermost ring completely
                if (t > 0.95f) alpha = 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        string path = "Assets/Textures/GlowParticle.png";
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // Set import settings
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Debug.Log("GlowParticle texture created at " + path);
    }
}
