using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// Slow cinematic camera drift for the main menu: a gentle orbit and bob around the start pose.
    /// Turned off by the "Motion effects" accessibility option.
    /// </summary>
    public class MenuMotion : MonoBehaviour
    {
        public Vector3 drift = new Vector3(1.2f, 0.5f, 0.6f);
        public float period = 40f;
        public float yawSway = 1.5f;

        Vector3 _startPos;
        Quaternion _startRot;

        void Start()
        {
            _startPos = transform.position;
            _startRot = transform.rotation;
        }

        void LateUpdate()
        {
            if (!SettingsService.Current.accessibility.motionEffects)
            {
                transform.SetPositionAndRotation(_startPos, _startRot);
                return;
            }
            float t = Time.time / period * Mathf.PI * 2f;
            transform.position = _startPos + new Vector3(Mathf.Sin(t) * drift.x, Mathf.Sin(t * 0.7f) * drift.y, Mathf.Cos(t * 0.5f) * drift.z);
            transform.rotation = _startRot * Quaternion.Euler(Mathf.Sin(t * 0.6f) * yawSway * 0.4f, Mathf.Sin(t) * yawSway, 0f);
        }
    }
}
