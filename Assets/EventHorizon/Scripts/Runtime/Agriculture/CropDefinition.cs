using UnityEngine;

namespace EventHorizon.Agriculture
{
    /// <summary>
    /// Growth and yield data for one crop under hydroponic, LED-lit conditions.
    /// The visuals are either stage models (e.g. Quaternius Ultimate Crops, 5 stages) or one model scaled by growth.
    /// </summary>
    [CreateAssetMenu(menuName = "Event Horizon/Crop Definition", fileName = "Crop")]
    public class CropDefinition : ScriptableObject
    {
        public string displayName = "Lettuce";

        [Header("Biology (controlled-environment values, approximate)")]
        [Tooltip("Earth days from seeding to harvest.")]
        public float daysToHarvest = 42f;
        [Tooltip("Edible fresh mass per m^2 per harvest.")]
        public float yieldKgPerM2 = 3.5f;
        [Tooltip("Water transpired per m^2 per sol. The greenhouse recovers most of it as condensate.")]
        public float transpirationKgPerM2PerSol = 4f;
        [Tooltip("Net photosynthetic O2 released per m^2 per sol.")]
        public float oxygenKgPerM2PerSol = 0.02f;
        [Tooltip("Grow-light electrical power per m^2, averaged over the photoperiod.")]
        public float lightWattsPerM2 = 65f;

        [Header("Visuals")]
        [Tooltip("Growth stages in order (seedling ... mature). Leave empty to scale the mature prefab instead.")]
        public GameObject[] stagePrefabs = new GameObject[0];
        public GameObject maturePrefab;
        [Tooltip("Distance between plants in a bed, metres.")]
        public float plantSpacing = 0.3f;
        [Tooltip("Height of a mature plant, metres. Used to scale models and the primitive fallback.")]
        public float matureHeight = 0.25f;
        public Color fallbackColor = new Color(0.35f, 0.7f, 0.25f);

        /// <summary>One Earth day is 0.973 sols.</summary>
        public float SolsToHarvest => daysToHarvest * 86400f / Core.MarsEnvironment.SolSeconds;
    }
}
