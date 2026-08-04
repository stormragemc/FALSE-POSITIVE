using UnityEngine;

namespace FalsePositive.CabinNight
{
    /// <summary>Low-amplitude firelight variation that avoids obvious random popping.</summary>
    public sealed class CabinFireFlicker : MonoBehaviour
    {
        [SerializeField] private Light[] lights = System.Array.Empty<Light>();
        [SerializeField] private float intensityVariation = 0.16f;
        [SerializeField] private float speed = 7f;

        private float[] _baseIntensities = System.Array.Empty<float>();
        private float _seed;

        public void Configure(params Light[] targetLights)
        {
            lights = targetLights ?? System.Array.Empty<Light>();
        }

        private void Awake()
        {
            _seed = transform.position.sqrMagnitude + 17.31f;
            _baseIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                _baseIntensities[i] = lights[i] != null ? lights[i].intensity : 0f;
            }
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(_seed, Time.time * speed);
            float multiplier = 1f + (noise - 0.5f) * 2f * intensityVariation;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity = _baseIntensities[i] * multiplier;
                }
            }
        }
    }
}
