using UnityEngine;

namespace EventHorizon.Interaction
{
    /// <summary>
    /// For landmarks such as the NASA Perseverance model: shows a short science note when inspected.
    /// </summary>
    public class ScienceInspectable : MonoBehaviour, IInteractable
    {
        public string title = "Mars 2020 Perseverance Rover";
        [TextArea(3, 8)]
        public string facts =
            "Landed in Jezero crater on 18 Feb 2021. Mass 1,025 kg, about 3 m long. " +
            "Powered by an MMRTG: ~110 W electrical from the heat of plutonium-238 decay. " +
            "Its MOXIE experiment made the first oxygen on another planet, up to 12 g per hour.";
        public float showSeconds = 8f;

        float _hideAt;

        public string Prompt => $"Inspect {title}";
        public bool CanInteract(GameObject user) => true;
        public void Interact(GameObject user) => _hideAt = Time.unscaledTime + showSeconds;

        void OnGUI()
        {
            if (Time.unscaledTime > _hideAt) return;
            var box = new Rect(20, 20, Mathf.Min(520, Screen.width - 40), 140);
            GUI.Box(box, title);
            GUI.Label(new Rect(box.x + 10, box.y + 25, box.width - 20, box.height - 30), facts);
        }
    }
}
