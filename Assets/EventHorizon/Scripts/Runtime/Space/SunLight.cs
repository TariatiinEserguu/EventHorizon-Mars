using System.Collections.Generic;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Makes the Sun the light source of a space scene.
    /// - Publishes the Sun's position (_EH_SunPosition), so planet shaders light their correct day side wherever
    ///   they are.
    /// - Points the scene's directional light from the Sun toward what the camera is looking at, so spacecraft,
    ///   asteroids and stations in view get the right light direction and shadows.
    /// - Eclipses: when a planet covers part of the Sun's disc as seen from the camera, the light and glare dim by
    ///   the covered fraction.
    /// </summary>
    [ExecuteAlways]
    public class SunLight : MonoBehaviour
    {
        public Light directional;
        [Tooltip("Sun visual radius (world units), used for eclipse geometry.")]
        public float sunRadius = 20f;
        public float baseIntensity = 1.6f;
        public Renderer glow;
        [Tooltip("Bodies that can eclipse the Sun.")]
        public List<Transform> occluders = new();

        static readonly int SunPosId = Shader.PropertyToID("_EH_SunPosition");
        public float EclipseFraction { get; private set; }

        void OnDisable() => Shader.SetGlobalVector(SunPosId, Vector4.zero);

        void LateUpdate()
        {
            Shader.SetGlobalVector(SunPosId, new Vector4(transform.position.x, transform.position.y, transform.position.z, 1f));
            var cam = Camera.main;
            if (cam == null || directional == null) return;

            var focus = cam.transform.position + cam.transform.forward * 50f;
            var orbit = cam.GetComponent<OrbitCamera>();
            if (orbit != null && orbit.targets.Length > 0 && orbit.targets[orbit.targetIndex] != null) focus = orbit.targets[orbit.targetIndex].position;
            var dir = (focus - transform.position).normalized;
            directional.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            EclipseFraction = Eclipse(cam.transform.position);
            directional.intensity = baseIntensity * (1f - EclipseFraction);
            if (glow != null) glow.enabled = EclipseFraction < 0.98f;
        }

        /// <summary>Fraction of the Sun's disc hidden behind any occluder, as seen from <paramref name="eye"/> (disc overlap).</summary>
        float Eclipse(Vector3 eye)
        {
            var toSun = transform.position - eye;
            float dSun = toSun.magnitude;
            if (dSun < 1e-3f) return 0f;
            float rs = Mathf.Asin(Mathf.Clamp01(sunRadius / dSun));
            float worst = 0f;
            foreach (var o in occluders)
            {
                if (o == null) continue;
                var toO = o.position - eye;
                float dO = toO.magnitude;
                if (dO >= dSun || dO < 1e-3f) continue;
                float radius = o.lossyScale.x * 0.5f;
                var body = o.GetComponent<CelestialBody>();
                if (body != null) radius = o.lossyScale.x * body.meshDiameter * 0.5f;
                float ro = Mathf.Asin(Mathf.Clamp01(radius / dO));
                float sep = Vector3.Angle(toSun, toO) * Mathf.Deg2Rad;
                worst = Mathf.Max(worst, DiscOverlap(rs, ro, sep));
            }
            return worst;
        }

        /// <summary>Area of the Sun's disc (radius a) covered by a disc of radius b at separation d, as a fraction.</summary>
        static float DiscOverlap(float a, float b, float d)
        {
            if (d >= a + b) return 0f;
            if (d <= Mathf.Abs(b - a)) return b >= a ? 1f : (b * b) / (a * a);
            float a2 = a * a, b2 = b * b;
            float alpha = Mathf.Acos(Mathf.Clamp((d * d + a2 - b2) / (2f * d * a), -1f, 1f));
            float beta = Mathf.Acos(Mathf.Clamp((d * d + b2 - a2) / (2f * d * b), -1f, 1f));
            float area = a2 * (alpha - Mathf.Sin(2f * alpha) * 0.5f) + b2 * (beta - Mathf.Sin(2f * beta) * 0.5f);
            return Mathf.Clamp01(area / (Mathf.PI * a2));
        }
    }
}
