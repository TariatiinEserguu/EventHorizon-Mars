using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>Pulsing beacon that marks the ARES-1 colony on the orbital view of Mars.</summary>
    public class BeaconPulse : MonoBehaviour
    {
        public float period = 2.2f;
        public float minScale = 0.6f, maxScale = 1.6f;
        public Color color = new Color(0.3f, 0.85f, 1f);
        public float intensity = 6f;

        Vector3 _base;
        Renderer _r;
        MaterialPropertyBlock _mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            _base = transform.localScale;
            _r = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            float p = (Time.time % period) / period;
            float s = Mathf.Lerp(minScale, maxScale, p);
            transform.localScale = _base * s;
            if (_r == null) return;
            _r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color * intensity * (1f - p * 0.8f));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
