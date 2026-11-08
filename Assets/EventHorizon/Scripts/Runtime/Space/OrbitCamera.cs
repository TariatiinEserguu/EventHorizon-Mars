using EventHorizon.Core;
using EventHorizon.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EventHorizon.Space
{
    /// <summary>
    /// Space camera for planetarium and orbit scenes. Right/middle mouse or the arrow keys orbit, the scroll wheel
    /// or +/- zoom, and Tab or 1-9 jump between targets with a smooth fly-over. Selecting a target that has a
    /// ScienceInspectable shows its facts on the HUD.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform[] targets = new Transform[0];
        public float[] targetDistances = new float[0];
        public int targetIndex;
        public float distance = 80f;
        public Vector2 distanceRange = new Vector2(3f, 3000f);
        public float yaw, pitch = 8f;
        public float sensitivity = 0.25f;
        [Tooltip("If set, selecting a target swings the camera to its sunlit side.")]
        public Transform sun;

        Vector3 _focus;
        int _shownIndex = -1;
        float _targetDistance;

        public Transform CurrentTarget => targets.Length > 0 ? targets[Mathf.Clamp(targetIndex, 0, targets.Length - 1)] : null;

        void Start()
        {
            if (CurrentTarget != null) _focus = CurrentTarget.position;
            _targetDistance = distance;
        }

        public void Select(int index)
        {
            if (targets.Length == 0) return;
            targetIndex = (index % targets.Length + targets.Length) % targets.Length;
            if (targetDistances != null && targetIndex < targetDistances.Length && targetDistances[targetIndex] > 0f)
                distance = targetDistances[targetIndex];
            var t = CurrentTarget;
            if (sun != null && t != null && t != sun)
            {
                // Look at the target from 40 degrees off the Sun's direction: lit face plus a visible terminator.
                var fromSun = (t.position - sun.position).normalized;
                yaw = Mathf.Atan2(fromSun.x, fromSun.z) * Mathf.Rad2Deg + 40f;
                pitch = 12f;
            }
        }

        void Update()
        {
            if (GameInput.GameplayBlocked) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed))
            {
                var d = mouse.delta.ReadValue() * sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            if (kb != null)
            {
                float k = 60f * Time.unscaledDeltaTime;
                if (kb.leftArrowKey.isPressed) yaw += k;
                if (kb.rightArrowKey.isPressed) yaw -= k;
                if (kb.upArrowKey.isPressed) pitch = Mathf.Clamp(pitch + k, -85f, 85f);
                if (kb.downArrowKey.isPressed) pitch = Mathf.Clamp(pitch - k, -85f, 85f);
                if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) distance *= 1f - 1.2f * Time.unscaledDeltaTime;
                if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) distance *= 1f + 1.2f * Time.unscaledDeltaTime;
                if (kb.tabKey.wasPressedThisFrame) Select(targetIndex + (kb.shiftKey.isPressed ? -1 : 1));
                for (int i = 0; i < targets.Length && i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) Select(i);
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) distance *= scroll > 0 ? 0.9f : 1.1f;
            }
            distance = Mathf.Clamp(distance, distanceRange.x, distanceRange.y);

            if (_shownIndex != targetIndex && CurrentTarget != null)
            {
                _shownIndex = targetIndex;
                var info = CurrentTarget.GetComponentInParent<ScienceInspectable>();
                if (info != null) UI.Hud.ShowInfo(info.title, info.facts, 10f);
                else UI.Hud.Toast($"TARGET  //  {CurrentTarget.name.Replace("Planet_", "").Replace("Moon_", "").ToUpperInvariant()}");
            }
        }

        void LateUpdate()
        {
            var t = CurrentTarget;
            if (t != null) _focus = Vector3.Lerp(_focus, t.position, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            _targetDistance = Mathf.Lerp(_targetDistance, distance, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(_focus - rot * Vector3.forward * _targetDistance, rot);
        }
    }
}
