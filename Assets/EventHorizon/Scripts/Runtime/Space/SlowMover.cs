using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>Moves an object along a straight path and loops: a distant spacecraft crossing the sky.</summary>
    public class SlowMover : MonoBehaviour
    {
        public Vector3 from = new Vector3(-400f, 60f, 600f);
        public Vector3 to = new Vector3(400f, 90f, 600f);
        public float seconds = 120f;
        public float spinDegreesPerSecond;

        float _t;

        void Update()
        {
            _t = (_t + Time.deltaTime / Mathf.Max(seconds, 1f)) % 1f;
            transform.position = Vector3.Lerp(from, to, _t);
            if (spinDegreesPerSecond != 0f) transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime, UnityEngine.Space.Self);
            else transform.rotation = Quaternion.LookRotation((to - from).normalized, Vector3.up);
        }
    }
}
