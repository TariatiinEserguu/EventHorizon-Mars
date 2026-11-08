using EventHorizon.Game;
using UnityEngine;

namespace EventHorizon.Interaction
{
    /// <summary>
    /// For landmarks such as the NASA Perseverance model: shows a short science note on the HUD when inspected.
    /// </summary>
    public class ScienceInspectable : MonoBehaviour, IInteractable
    {
        public string title = "Mars 2020 Perseverance Rover";
        [TextArea(3, 8)]
        public string facts =
            "Landed in Jezero crater on 18 Feb 2021. Mass 1,025 kg, about 3 m long. " +
            "Powered by an MMRTG: ~110 W electrical from the heat of plutonium-238 decay. " +
            "Its MOXIE experiment made the first oxygen on another planet, up to 12 g per hour.";
        [Tooltip("Game event raised on inspection, used by mission objectives (e.g. inspect_relay).")]
        public string eventId = "inspect";

        public string Prompt => $"Inspect {title}";
        public bool CanInteract(GameObject user) => true;

        public void Interact(GameObject user)
        {
            UI.Hud.ShowInfo(title, facts);
            if (!string.IsNullOrEmpty(eventId)) GameEvents.Raise(eventId);
        }
    }
}
