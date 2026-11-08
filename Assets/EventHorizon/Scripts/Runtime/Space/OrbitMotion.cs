using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Circular orbit around a center (the Sun, Mars...). The period comes from a real value scaled by the scene's
    /// time scale, or from Kepler's third law (T ~ a^1.5) if no period is given. Moons and satellites stay
    /// face-locked when <see cref="tidallyLocked"/> is set, as Phobos and Deimos are.
    /// </summary>
    public class OrbitMotion : MonoBehaviour
    {
        public Transform center;
        public float radius = 50f;
        [Tooltip("Real orbital period in hours (0 = derive from radius with Kepler's law).")]
        public float periodHours;
        [Tooltip("Speeds orbits up for viewing: simulated seconds per real second (on top of MarsEnvironment).")]
        public float timeScale = 1f;
        [Range(-90f, 90f)] public float inclination;
        public float startAngle;
        public bool tidallyLocked;

        float _angle;

        void Start() => _angle = startAngle;

        void Update()
        {
            if (center == null) return;
            float period = periodHours > 0f ? periodHours * 3600f : Mathf.Pow(radius, 1.5f) * 60f;
            _angle += 360f * MarsEnvironment.DeltaSeconds * timeScale / period;
            var rot = Quaternion.Euler(inclination, 0f, 0f) * Quaternion.Euler(0f, _angle, 0f);
            transform.position = center.position + rot * Vector3.forward * radius;
            if (tidallyLocked) transform.rotation = Quaternion.LookRotation(center.position - transform.position, rot * Vector3.up);
        }
    }
}
