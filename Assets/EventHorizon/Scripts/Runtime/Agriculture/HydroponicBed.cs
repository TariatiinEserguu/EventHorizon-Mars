using System.Collections.Generic;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Agriculture
{
    /// <summary>
    /// One hydroponic tray. It draws grow-light power and net water from the <see cref="ColonyGrid"/>, releases O2,
    /// and swaps or scales its plant models as the crop grows. Interact to plant, harvest or clear.
    /// </summary>
    public class HydroponicBed : MonoBehaviour, IPowerConsumer, IInteractable
    {
        public CropDefinition crop;
        [Tooltip("Growing area in local X/Z, metres.")]
        public Vector2 bedSize = new Vector2(2f, 1f);
        [Tooltip("Local height of the grow surface.")]
        public float surfaceHeight = 0.8f;
        [Tooltip("Fraction of transpired water the greenhouse condenses back.")]
        [Range(0f, 1f)] public float waterRecovery = 0.95f;
        public bool plantOnStart = true;

        public bool Planted { get; private set; }
        public bool Dead { get; private set; }
        public float Growth01 { get; private set; }
        public float Health { get; private set; } = 1f;
        public float AreaM2 => bedSize.x * bedSize.y;
        public float OxygenKgPerSol { get; private set; }

        static readonly Dictionary<CropDefinition, float> s_modelScale = new();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        ColonyGrid _grid;
        readonly List<GameObject> _plants = new();
        readonly List<Vector3> _plantBaseScale = new();
        readonly List<Vector3> _anchors = new();
        int _shownStage = -1;
        MaterialPropertyBlock _mpb;

        void OnEnable()
        {
            _grid = ColonyGrid.Find(this);
            _grid?.Register(this);
        }

        void OnDisable() => _grid?.Unregister(this);

        void Start()
        {
            BuildAnchors();
            if (plantOnStart && crop != null) Plant(crop);
        }

        public void Plant(CropDefinition definition)
        {
            crop = definition;
            Planted = crop != null;
            Dead = false;
            Growth01 = 0f;
            Health = 1f;
            _shownStage = -1;
            RefreshVisuals(force: true);
        }

        public float DemandWatts => Planted && !Dead ? crop.lightWattsPerM2 * AreaM2 : 0f;

        public void Supply(float satisfaction, float dt)
        {
            OxygenKgPerSol = 0f;
            if (!Planted || Dead || _grid == null || dt <= 0f) return;

            float sols = dt / MarsEnvironment.SolSeconds;
            float needWater = crop.transpirationKgPerM2PerSol * AreaM2 * sols * (1f - waterRecovery);
            float waterFraction = needWater > 0f ? _grid.Take(ResourceType.Water, needWater) / needWater : 1f;
            float f = Mathf.Min(satisfaction, waterFraction);

            if (Growth01 < 1f) Growth01 = Mathf.Min(1f, Growth01 + sols / Mathf.Max(crop.SolsToHarvest, 0.01f) * f);
            // Two sols with no light or water kill a crop; good conditions slowly heal it.
            Health = Mathf.Clamp01(Health + (f > 0.9f ? 0.2f * sols : -(1f - f) * 0.5f * sols));
            if (Health <= 0f) Dead = true;

            float o2 = crop.oxygenKgPerM2PerSol * AreaM2 * sols * f;
            _grid.Add(ResourceType.Oxygen, o2);
            OxygenKgPerSol = o2 / sols;

            RefreshVisuals(force: false);
        }

        public string Prompt
        {
            get
            {
                if (!Planted) return crop != null ? $"Plant {crop.displayName}" : "Empty hydroponic bed";
                if (Dead) return $"Clear dead {crop.displayName}";
                if (Growth01 >= 1f) return $"Harvest {crop.displayName} (~{HarvestKg:0.0} kg)";
                return $"{crop.displayName}: {Growth01:P0} grown, health {Health:P0}";
            }
        }

        float HarvestKg => crop.yieldKgPerM2 * AreaM2 * Health;

        public bool CanInteract(GameObject user) => crop != null;

        public void Interact(GameObject user)
        {
            if (!Planted)
            {
                Plant(crop);
            }
            else if (Dead)
            {
                Planted = false;
                ClearPlants();
            }
            else if (Growth01 >= 1f)
            {
                _grid?.Add(ResourceType.Food, HarvestKg);
                Game.GameEvents.Raise("harvest");
                Plant(crop); // replant immediately
            }
        }

        // ------------------------------------------------------------------ visuals

        void BuildAnchors()
        {
            _anchors.Clear();
            float spacing = crop != null ? Mathf.Max(0.1f, crop.plantSpacing) : 0.3f;
            int nx = Mathf.Max(1, Mathf.FloorToInt(bedSize.x / spacing));
            int nz = Mathf.Max(1, Mathf.FloorToInt(bedSize.y / spacing));
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                _anchors.Add(new Vector3(
                    (x + 0.5f) / nx * bedSize.x - bedSize.x * 0.5f,
                    surfaceHeight,
                    (z + 0.5f) / nz * bedSize.y - bedSize.y * 0.5f));
            }
        }

        int StageCount => crop != null && crop.stagePrefabs != null && crop.stagePrefabs.Length > 0 ? crop.stagePrefabs.Length : 0;

        void RefreshVisuals(bool force)
        {
            if (crop == null) return;
            if (_anchors.Count == 0) BuildAnchors();

            int stages = StageCount;
            if (stages > 0)
            {
                int stage = Mathf.Min(stages - 1, Mathf.FloorToInt(Growth01 * stages));
                if (!force && stage == _shownStage) { TintIfDead(); return; }
                _shownStage = stage;
                ClearPlants();
                float scale = ModelScale(crop);
                foreach (var a in _anchors) Spawn(crop.stagePrefabs[stage], a, scale);
            }
            else
            {
                if (force || _plants.Count == 0)
                {
                    ClearPlants();
                    foreach (var a in _anchors) Spawn(crop.maturePrefab, a, crop.maturePrefab != null ? ModelScale(crop) : 1f);
                }
                // Logistic-looking growth: slow start, fast middle, plateau.
                float s = Mathf.Lerp(0.08f, 1f, Mathf.SmoothStep(0f, 1f, Growth01));
                for (int i = 0; i < _plants.Count; i++)
                    if (_plants[i] != null) _plants[i].transform.localScale = _plantBaseScale[i] * s;
            }
            TintIfDead();
        }

        void Spawn(GameObject prefab, Vector3 localPos, float scale)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
            }
            else
            {
                // Fallback when no crop model exists: a green blob at the crop's mature size.
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                scale = crop.matureHeight;
                SetColor(go, crop.fallbackColor);
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var baseScale = Vector3.one * scale;
            go.transform.localScale = baseScale;
            _plants.Add(go);
            _plantBaseScale.Add(baseScale);
        }

        void ClearPlants()
        {
            foreach (var p in _plants) if (p != null) Destroy(p);
            _plants.Clear();
            _plantBaseScale.Clear();
        }

        void TintIfDead()
        {
            if (!Dead) return;
            foreach (var p in _plants) if (p != null) SetColor(p, new Color(0.35f, 0.25f, 0.12f));
        }

        void SetColor(GameObject go, Color c)
        {
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>One scale for every stage, taken from the mature model, so stages keep their relative sizes.</summary>
        static float ModelScale(CropDefinition c)
        {
            if (s_modelScale.TryGetValue(c, out var cached)) return cached;
            var reference = c.stagePrefabs != null && c.stagePrefabs.Length > 0 ? c.stagePrefabs[^1] : c.maturePrefab;
            float scale = 1f;
            if (reference != null)
            {
                var probe = Instantiate(reference);
                var renderers = probe.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    if (b.size.y > 1e-4f) scale = c.matureHeight / b.size.y;
                }
                probe.SetActive(false); // Destroy is deferred: hide the probe this frame
                Destroy(probe);
            }
            s_modelScale[c] = scale;
            return scale;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(0f, surfaceHeight, 0f), new Vector3(bedSize.x, 0.02f, bedSize.y));
        }
    }
}
