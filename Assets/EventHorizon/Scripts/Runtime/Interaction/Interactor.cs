using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Interaction
{
    /// <summary>Ray-casts from the view camera and calls <see cref="IInteractable.Interact"/> on the Interact action.</summary>
    public class Interactor : MonoBehaviour
    {
        public Camera viewCamera;
        public float range = 2.5f;
        public LayerMask mask = ~0;

        public IInteractable Current { get; private set; }
        public string CurrentPrompt => Current != null ? Current.Prompt : null;
        /// <summary>Raised after a successful interaction, e.g. to play an animation.</summary>
        public event System.Action<IInteractable> Interacted;

        void Update()
        {
            Current = null;
            if (GameInput.GameplayBlocked) return;
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            // Third-person cameras sit behind the body, so extend the ray by the camera-to-body distance.
            float extra = Vector3.Distance(cam.transform.position, transform.position + Vector3.up * 1.5f);
            if (Physics.Raycast(ray, out var hit, range + extra, mask, QueryTriggerInteraction.Collide)
                && hit.collider.GetComponentInParent<IInteractable>() is IInteractable target
                && target.CanInteract(gameObject))
            {
                Current = target;
            }

            if (Current != null && GameInput.Interact.WasPressedThisFrame())
            {
                Current.Interact(gameObject);
                Interacted?.Invoke(Current);
            }
        }
    }
}
