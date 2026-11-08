using UnityEngine;
using UnityEngine.Rendering;

namespace EventHorizon.Core
{
    /// <summary>
    /// A real-time reflection probe that captures the current Mars sky, so smooth surfaces (solar glass, painted
    /// hulls, metal) reflect butterscotch daylight or the night sky instead of black. Re-rendered every few
    /// minutes of game time as the sun moves, using time slicing to stay cheap.
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    public class SkyReflections : MonoBehaviour
    {
        public float refreshRealSeconds = 20f;
        ReflectionProbe _probe;
        float _next;

        void Awake()
        {
            _probe = GetComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + refreshRealSeconds;
            _probe.RenderProbe();
        }
    }
}
