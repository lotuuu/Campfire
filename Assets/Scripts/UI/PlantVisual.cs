using UnityEngine;

namespace Garden
{
    public class PlantVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer stemRenderer;
        [SerializeField] private SpriteRenderer petalRenderer;
        [SerializeField] private SpriteRenderer potRenderer;
        [SerializeField] private Transform stemTransform;
        [SerializeField] private Transform petalTransform;
        [SerializeField] private ParticleSystem glowParticles;

        [Header("Growth")]
        [SerializeField] private float maxStemHeight = 2f;
        [SerializeField] private float maxPetalScale = 1f;

        private VariantData currentVariant;

        public void SetVariant(VariantData variant)
        {
            currentVariant = variant;
            if (variant == null)
            {
                stemRenderer.enabled = false;
                petalRenderer.enabled = false;
                glowParticles.Stop();
                return;
            }

            stemRenderer.enabled = true;
            petalRenderer.enabled = true;
            stemRenderer.color = variant.primaryColor;
            petalRenderer.color = variant.secondaryColor;

            if (variant.rarity >= Rarity.Rare && glowParticles != null)
            {
                var main = glowParticles.main;
                main.startColor = variant.primaryColor;
                glowParticles.Play();
            }
            else
            {
                glowParticles?.Stop();
            }
        }

        public void SetGrowth(float progress)
        {
            float p = Mathf.Clamp01(progress);

            if (stemTransform != null)
            {
                float h = Mathf.Lerp(0.1f, maxStemHeight, p);
                stemTransform.localScale = new Vector3(0.15f, h, 1f);
                stemTransform.localPosition = new Vector3(0, h * 0.5f, 0);
            }

            if (petalTransform != null)
            {
                float petalProgress = Mathf.Clamp01((p - 0.6f) / 0.4f);
                float s = Mathf.Lerp(0f, maxPetalScale, petalProgress);
                petalTransform.localScale = new Vector3(s, s, 1f);
                if (stemTransform != null)
                    petalTransform.localPosition = new Vector3(0, stemTransform.localScale.y, 0);
            }
        }

        public void Clear()
        {
            currentVariant = null;
            if (stemRenderer != null) stemRenderer.enabled = false;
            if (petalRenderer != null) petalRenderer.enabled = false;
            glowParticles?.Stop();
            if (stemTransform != null) stemTransform.localScale = Vector3.zero;
            if (petalTransform != null) petalTransform.localScale = Vector3.zero;
        }
    }
}
