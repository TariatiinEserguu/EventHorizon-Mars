using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// A comet: a dark nucleus with two tails that always point away from the Sun.
    /// The ion tail is straight, blue and anti-sunward (blown by the solar wind). The dust tail is broader,
    /// yellowish and curves back along the orbit. Both are particle systems, and they grow as the comet nears the Sun.
    /// </summary>
    public class Comet : MonoBehaviour
    {
        public Transform sun;
        public ParticleSystem ionTail;
        public ParticleSystem dustTail;
        [Tooltip("Distance at which the tails are fully developed.")]
        public float activeDistance = 400f;

        Vector3 _lastPos;

        void Start() => _lastPos = transform.position;

        void LateUpdate()
        {
            if (sun == null) return;
            var away = (transform.position - sun.position);
            float dist = away.magnitude;
            away /= Mathf.Max(dist, 1e-3f);
            var velocity = (transform.position - _lastPos) / Mathf.Max(Time.deltaTime, 1e-4f);
            _lastPos = transform.position;
            float activity = Mathf.Clamp01(activeDistance / Mathf.Max(dist, 1f));

            if (ionTail != null)
            {
                ionTail.transform.rotation = Quaternion.LookRotation(away);
                var e = ionTail.emission;
                e.rateOverTime = 120f * activity;
            }
            if (dustTail != null)
            {
                // Dust lags behind along the orbit: blend the anti-sun direction with the reverse of the motion.
                var trail = velocity.sqrMagnitude > 1e-4f ? -velocity.normalized : away;
                dustTail.transform.rotation = Quaternion.LookRotation(Vector3.Slerp(away, trail, 0.35f));
                var e = dustTail.emission;
                e.rateOverTime = 90f * activity;
            }
        }
    }
}
