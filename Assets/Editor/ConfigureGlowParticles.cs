using UnityEngine;
using UnityEditor;

public static class ConfigureGlowParticles
{
    [MenuItem("Tools/Configure Glow Particles")]
    public static void Configure()
    {
        var go = GameObject.Find("GlowParticles");
        if (go == null) { Debug.LogError("GlowParticles not found"); return; }

        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) { Debug.LogError("No ParticleSystem on GlowParticles"); return; }

        // Color over lifetime: fade in, hold, fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.75f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.35f, 0.15f),
                new GradientAlphaKey(0.2f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // Size over lifetime: gentle grow then shrink
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(new Keyframe(0f, 0.3f, 0f, 2f));
        sizeCurve.AddKey(new Keyframe(0.3f, 1f, 0f, 0f));
        sizeCurve.AddKey(new Keyframe(1f, 0.5f, -1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Shape: smaller radius so particles stay near plant
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;

        Debug.Log("GlowParticles configured successfully");
    }
}
