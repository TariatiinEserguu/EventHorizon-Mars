using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Sizes and spins a planet, moon or star from real data. Rotation follows the colony clock, so at the
    /// default time scale Mars turns once per sol. Put the textured sphere on this object; optional cloud and
    /// ring children are handled too.
    /// </summary>
    [ExecuteAlways]
    public class CelestialBody : MonoBehaviour
    {
        public string bodyName = "Mars";
        public float meanRadiusKm = 3389.5f;
        public float rotationHours = 24.6229f;
        public float axialTiltDeg = 25.19f;

        [Tooltip("Kilometres per Unity unit, for a scaled-space view (e.g. 100 = Mars is 68 units wide).")]
        public float kmPerUnit = 100f;
        [Tooltip("Unity's built-in sphere has diameter 1; set 2 for meshes of radius 1.")]
        public float meshDiameter = 1f;
        [Tooltip("Extra multiplier for sky dressing, e.g. a huge Earth seen from orbit.")]
        public float displayScale = 1f;

        [Header("Optional layers")]
        public Transform clouds;
        [Tooltip("Cloud layer spin relative to the surface, as a multiple of the body's spin.")]
        public float cloudSpeedFactor = 1.1f;

        public void Apply(PlanetCatalog.Body b)
        {
            bodyName = b.name;
            meanRadiusKm = b.meanRadiusKm;
            rotationHours = b.rotationHours;
            axialTiltDeg = b.axialTiltDeg;
        }

        void Update()
        {
            float diameterUnits = meanRadiusKm * 2f / Mathf.Max(kmPerUnit, 1e-6f) * displayScale;
            transform.localScale = Vector3.one * (diameterUnits / meshDiameter);

            double t = Application.isPlaying ? MarsEnvironment.TimeSeconds : 0.0;
            float spin = rotationHours > 0f ? (float)(t / (rotationHours * 3600.0) % 1.0) * 360f : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, axialTiltDeg) * Quaternion.Euler(0f, -spin, 0f);

            if (clouds != null)
                clouds.localRotation = Quaternion.Euler(0f, -spin * (cloudSpeedFactor - 1f), 0f);
        }
    }
}
