using UnityEngine;

namespace EventHorizon.Core
{
    /// <summary>Habitat floodlight that switches on at dusk and off at dawn (and during dark dust storms).</summary>
    [RequireComponent(typeof(Light))]
    public class NightLight : MonoBehaviour
    {
        public float onBelowSunElevation = 4f;
        public Renderer lampRenderer;
        Light _light;
        float _intensity;

        void Awake()
        {
            _light = GetComponent<Light>();
            _intensity = _light.intensity;
        }

        void Update()
        {
            var env = MarsEnvironment.Instance;
            bool on = env == null || env.SunElevationDeg < onBelowSunElevation || env.opticalDepth > 3f;
            _light.enabled = on;
            if (lampRenderer != null) lampRenderer.enabled = on;
            _light.intensity = _intensity;
        }
    }
}
