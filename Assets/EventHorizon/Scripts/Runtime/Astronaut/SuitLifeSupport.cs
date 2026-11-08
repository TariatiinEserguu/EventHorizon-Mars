using EventHorizon.Colony;
using EventHorizon.Core;
using UnityEngine;

namespace EventHorizon.Astronaut
{
    /// <summary>
    /// Oxygen in the backpack (PLSS). Sized like NASA's EMU suit: ~0.6 kg of O2, used at ~0.07 kg/h during
    /// active EVA work, so about 8 hours outside. Refill at any oxygen <see cref="ResourceTank"/>.
    /// </summary>
    public class SuitLifeSupport : MonoBehaviour
    {
        public float oxygenCapacityKg = 0.6f;
        public float oxygenKg = 0.6f;
        public float useKgPerHour = 0.07f;
        [Tooltip("Set by habitat/airlock triggers: no suit oxygen is used inside.")]
        public bool insidePressurisedVolume;
        public bool showHud = true;

        public float HoursLeft => useKgPerHour > 0f ? oxygenKg / useKgPerHour : float.PositiveInfinity;
        public bool OutOfOxygen => oxygenKg <= 0f;

        void Update()
        {
            if (insidePressurisedVolume) return;
            oxygenKg = Mathf.Max(0f, oxygenKg - useKgPerHour * MarsEnvironment.DeltaSeconds / 3600f);
        }

        /// <summary>Tops up the suit from the colony's oxygen stock.</summary>
        public void Refill(ColonyGrid grid)
        {
            oxygenKg += grid.Take(ResourceType.Oxygen, oxygenCapacityKg - oxygenKg);
        }

        void OnGUI()
        {
            if (!showHud) return;
            float f = oxygenCapacityKg > 0f ? oxygenKg / oxygenCapacityKg : 0f;
            var r = new Rect(20, Screen.height - 50, 220, 22);
            GUI.Box(r, GUIContent.none);
            GUI.color = f > 0.25f ? Color.cyan : Color.red;
            GUI.Box(new Rect(r.x + 2, r.y + 2, (r.width - 4) * f, r.height - 4), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 6, r.y + 2, r.width, r.height), $"Suit O2 {oxygenKg:0.00} kg  ({HoursLeft:0.0} h)");
        }
    }
}
