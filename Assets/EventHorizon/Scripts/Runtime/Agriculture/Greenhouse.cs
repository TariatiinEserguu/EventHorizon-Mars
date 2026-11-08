using System.Collections.Generic;
using UnityEngine;

namespace EventHorizon.Agriculture
{
    /// <summary>
    /// A pressurised growing dome. If it has no beds as children, it lays out a grid of
    /// <see cref="HydroponicBed"/>s on start and cycles through <see cref="crops"/>.
    /// </summary>
    public class Greenhouse : MonoBehaviour
    {
        public CropDefinition[] crops = new CropDefinition[0];
        [Tooltip("Optional bed model. It must contain a HydroponicBed, or one is added.")]
        public GameObject bedPrefab;
        public int rows = 2;
        public int columns = 3;
        public Vector2 bedSize = new Vector2(2f, 0.8f);
        public float aisle = 1.2f;
        public Vector3 layoutOffset = Vector3.zero;

        readonly List<HydroponicBed> _beds = new();
        public IReadOnlyList<HydroponicBed> Beds => _beds;

        public float GrowingAreaM2
        {
            get { float a = 0f; foreach (var b in _beds) a += b.AreaM2; return a; }
        }

        public float OxygenKgPerSol
        {
            get { float o = 0f; foreach (var b in _beds) o += b.OxygenKgPerSol; return o; }
        }

        void Awake()
        {
            GetComponentsInChildren(_beds);
            if (_beds.Count == 0) LayOutBeds();
        }

        void LayOutBeds()
        {
            float pitchX = bedSize.x + aisle;
            float pitchZ = bedSize.y + aisle;
            int i = 0;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++, i++)
            {
                var pos = layoutOffset + new Vector3(
                    (c - (columns - 1) * 0.5f) * pitchX, 0f,
                    (r - (rows - 1) * 0.5f) * pitchZ);
                var bed = CreateBed($"Bed {r + 1}-{c + 1}");
                bed.transform.localPosition = pos;
                bed.bedSize = bedSize;
                if (crops.Length > 0) bed.crop = crops[i % crops.Length];
                _beds.Add(bed);
            }
        }

        HydroponicBed CreateBed(string bedName)
        {
            GameObject go;
            if (bedPrefab != null)
            {
                go = Instantiate(bedPrefab, transform);
            }
            else
            {
                // Primitive fallback: a white trough at bench height, with a collider for interaction.
                go = new GameObject();
                go.transform.SetParent(transform, false);
                var trough = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trough.name = "Trough";
                trough.transform.SetParent(go.transform, false);
                trough.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                trough.transform.localScale = new Vector3(bedSize.x, 0.8f, bedSize.y);
            }
            go.name = bedName;
            var bed = go.GetComponent<HydroponicBed>();
            if (bed == null) bed = go.AddComponent<HydroponicBed>();
            return bed;
        }
    }
}
