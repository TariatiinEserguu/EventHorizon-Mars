using EventHorizon.Core;
using EventHorizon.Astronaut;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Colony
{
    /// <summary>
    /// Adds storage capacity to the grid. An oxygen tank also refills the suit of an astronaut who uses it.
    /// </summary>
    public class ResourceTank : MonoBehaviour, IInteractable
    {
        public ResourceType resource = ResourceType.Water;
        [Min(0f)] public float capacityKg = 5000f;
        [Min(0f)] public float initialKg = 2000f;

        ColonyGrid _grid;
        bool _seeded;

        void OnEnable()
        {
            _grid = ColonyGrid.Find(this);
            // Seed the starting stock once, so disabling and re-enabling a tank doesn't create resources.
            _grid?.AddCapacity(resource, capacityKg, _seeded ? 0f : initialKg);
            _seeded = true;
        }

        void OnDisable() => _grid?.RemoveCapacity(resource, capacityKg);

        public string Prompt
        {
            get
            {
                string res = Loc.T("RES_" + resource.ToString().ToUpperInvariant());
                if (_grid == null) return Loc.T("PROMPT_TANK_OFFLINE", res);
                string level = Loc.T("PROMPT_TANK_LEVEL", res, _grid.Amount(resource).ToString("0"), _grid.Capacity(resource).ToString("0"));
                return resource == ResourceType.Oxygen ? Loc.T("PROMPT_REFILL", level) : level;
            }
        }

        public bool CanInteract(GameObject user) => true;

        public void Interact(GameObject user)
        {
            if (resource != ResourceType.Oxygen || _grid == null) return;
            var suit = user.GetComponentInParent<SuitLifeSupport>();
            if (suit != null && suit.Refill(_grid)) Game.GameEvents.Raise("suit_refilled");
        }
    }
}
