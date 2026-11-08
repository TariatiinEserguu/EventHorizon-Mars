using UnityEngine;

namespace EventHorizon.Interaction
{
    /// <summary>Anything the astronaut can use: panels, tanks, crop beds, landmarks.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract(GameObject user);
        void Interact(GameObject user);
    }
}
