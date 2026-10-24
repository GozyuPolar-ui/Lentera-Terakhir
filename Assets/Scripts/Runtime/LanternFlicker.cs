using UnityEngine;

namespace JRPG.Menu
{
    [RequireComponent(typeof(Light))]
    public sealed class LanternFlicker : MonoBehaviour
    {
        public Light lanternLight;
        public Renderer flameRenderer;
        public float intensity = 2.7f;
        public float intensityVariation = 0.55f;
        public float noiseSpeed = 2.2f;
        public float colorVariation = 0.08f;
        [Range(0f, 1f)] public float dipChance = 0.035f;
        public float dipDuration = 0.2f;
        public float dipStrength = 0.65f;

        private MaterialPropertyBlock propertyBlock;
        private Color baseColor;
        private float seed;
        private float dipRemaining;
        private float nextDipTime;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (lanternLight == null)
            {
                lanternLight = GetComponent<Light>();
            }

            baseColor = lanternLight != null ? lanternLight.color : new Color(1f, 0.55f, 0.24f);
            seed = Random.value * 100f;
            propertyBlock = new MaterialPropertyBlock();
            nextDipTime = Time.unscaledTime + Random.Range(2f, 7f);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            float flicker = Mathf.PerlinNoise(seed, time * noiseSpeed);
            float dip = 1f;
            if (time >= nextDipTime)
            {
                if (Random.value <= dipChance)
                {
                    dipRemaining = Mathf.Max(dipRemaining, dipDuration);
                }
                nextDipTime = time + Random.Range(3f, 9f);
            }

            if (dipRemaining > 0f)
            {
                dipRemaining -= Time.unscaledDeltaTime;
                dip = 1f - dipStrength * Mathf.Clamp01(dipRemaining / Mathf.Max(0.01f, dipDuration));
            }

            Color tint = baseColor * (1f + (flicker - 0.5f) * colorVariation);
            if (lanternLight != null)
            {
                lanternLight.intensity = intensity + (flicker - 0.5f) * intensityVariation;
                lanternLight.intensity *= dip;
                lanternLight.color = tint;
            }

            if (flameRenderer != null)
            {
                flameRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColor, tint * (1.1f + flicker * 0.8f) * dip);
                propertyBlock.SetColor(BaseColor, tint);
                flameRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
