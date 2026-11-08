using EventHorizon.Core;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>
    /// Shallow subsurface water ice, like the mid-latitude deposits found by Mars Reconnaissance Orbiter
    /// about 1 m below the surface. Interact to dig ice; it melts into the grid's water stock.
    /// </summary>
    public class IceDeposit : MonoBehaviour, IInteractable
    {
        [Min(0f)] public float iceKg = 2000f;
        [Tooltip("Ice dug per use: one powered-drill session, not a shovelful.")]
        public float kgPerExtraction = 150f;
        [Range(0f, 1f)] public float purity = 0.9f;
        [Tooltip("Shrinks the visible formation as it is mined.")]
        public Transform visual;

        float _startKg;
        Vector3 _startScale;

        void Awake()
        {
            _startKg = Mathf.Max(iceKg, 0.001f);
            if (visual == null) visual = transform;
            _startScale = visual.localScale;
        }

        public string Prompt => Loc.T("PROMPT_ICE", iceKg.ToString("0"));
        public bool CanInteract(GameObject user) => iceKg > 0f;

        public void Interact(GameObject user)
        {
            var grid = ColonyGrid.Find(this);
            if (grid == null) return;
            float dug = Mathf.Min(kgPerExtraction, iceKg);
            float stored = grid.Add(ResourceType.Water, dug * purity);
            // Only remove ice that actually fit in the tanks.
            iceKg -= purity > 0f ? stored / purity : dug;
            visual.localScale = _startScale * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(iceKg / _startKg));
            Game.GameEvents.Raise("ice_extracted");
            if (iceKg <= 0f) visual.gameObject.SetActive(false);
        }
    }
}
