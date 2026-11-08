using EventHorizon.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EventHorizon.Space
{
    /// <summary>Space-lab camera: hold the right mouse button (or left) to orbit, scroll to zoom, number keys to jump between targets.</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform[] targets = new Transform[0];
        public int targetIndex;
        public float distance = 80f;
        public Vector2 distanceRange = new Vector2(5f, 1500f);
        public float yaw, pitch = 8f;
        public float sensitivity = 0.25f;

        Vector3 _focus;

        void Start()
        {
            if (targets.Length > 0 && targets[0] != null) _focus = targets[0].position;
        }

        void Update()
        {
            if (GameInput.GameplayBlocked) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null && (mouse.rightButton.isPressed || mouse.leftButton.isPressed))
            {
                var d = mouse.delta.ReadValue() * sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance * (scroll > 0 ? 0.9f : 1.1f), distanceRange.x, distanceRange.y);
            }
            if (kb != null)
                for (int i = 0; i < targets.Length && i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) targetIndex = i;
        }

        void LateUpdate()
        {
            if (targets.Length > 0 && targets[targetIndex] != null)
            {
                var t = targets[targetIndex];
                _focus = Vector3.Lerp(_focus, t.position, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            }
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(_focus - rot * Vector3.forward * distance, rot);
        }
    }
}
