using EventHorizon.Space;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Research console for the black-hole lab: mass, spin, viewing distance and disk-intensity controls, live
    /// physical readouts, and a plain statement of what the rendering computes exactly and what it approximates.
    /// </summary>
    public class BlackHoleLabUI : MonoBehaviour
    {
        public BlackHole blackHole;
        public OrbitCamera orbit;

        Label _readout;
        float _logMass;
        VisualElement _panel;
        bool _collapsed;

        void Start()
        {
            if (blackHole == null) blackHole = FindAnyObjectByType<BlackHole>();
            if (orbit == null) orbit = FindAnyObjectByType<OrbitCamera>();
            if (blackHole == null) return;
            _logMass = Mathf.Log10((float)blackHole.massSolar);

            var doc = UiRoot.Create("Black Hole Lab", 20);
            _panel = UiKit.Box(doc.rootVisualElement, "eh-panel");
            _panel.style.position = Position.Absolute;
            _panel.style.right = 24;
            _panel.style.top = 130;
            _panel.style.width = 470;
            var head = UiKit.Box(_panel, "eh-row");
            UiKit.Text(head, "GRAVITATIONAL RESEARCH", "eh-kicker");
            UiKit.Box(head, "eh-spacer");
            var body = UiKit.Box(_panel, "eh-col");
            UiKit.Small(head, "HIDE", () => { _collapsed = !_collapsed; body.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex; });

            UiKit.Slider(body, "Mass (log10 M_sun)", 0.5f, 10f, _logMass, v => _logMass = v, v => $"{Mathf.Pow(10f, v):0.##E+0}");
            UiKit.Slider(body, "Spin a", 0f, 0.998f, blackHole.spin, v => blackHole.spin = v, v => $"{v:0.000}");
            if (orbit != null) UiKit.Slider(body, "Viewing distance", 25f, 400f, orbit.distance, v => orbit.distance = v, v => $"{v / blackHole.WorldUnitsPerRs:0} r_s");
            UiKit.Slider(body, "Disk intensity", 0.3f, 8f, blackHole.diskIntensity, v => blackHole.diskIntensity = v, v => $"{v:0.0}");
            _readout = UiKit.Text(body, "", "eh-mono", "eh-small");
            _readout.style.whiteSpace = WhiteSpace.Normal;
            _readout.style.marginTop = 8;
            UiKit.Divider(body);
            var note = UiKit.Text(body,
                "EXACT: horizon, ISCO and photon orbit (Kerr), time dilation and tides (Schwarzschild), light bending (Schwarzschild geodesics), Doppler and gravitational redshift.\n" +
                "APPROXIMATED: light paths ignore spin (no frame dragging of light); spin moves the disk edge and boosts the Doppler asymmetry; gas texture, disk thickness and colours are artistic.",
                "eh-small", "eh-dim");
            note.style.whiteSpace = WhiteSpace.Normal;
        }

        void Update()
        {
            if (blackHole == null || _readout == null) return;
            blackHole.massSolar = Mathf.Pow(10f, _logMass);
            double rs = blackHole.SchwarzschildRadiusM;
            double km(double rsUnits) => rsUnits * rs / 1000.0;
            float distRs = orbit != null ? orbit.distance / blackHole.WorldUnitsPerRs : 30f;
            double r = distRs * rs;
            double tidal = blackHole.TidalAcceleration(r);
            _readout.text =
                $"Schwarzschild radius  {Sci(rs / 1000.0)} km\n" +
                $"Event horizon (Kerr)  {Sci(km(blackHole.HorizonRs))} km  ({blackHole.HorizonRs:0.000} r_s)\n" +
                $"ISCO, prograde        {Sci(km(blackHole.IscoRs))} km  ({blackHole.IscoRs:0.000} r_s)\n" +
                $"Photon orbit          {Sci(km(blackHole.PhotonOrbitRs))} km  ({blackHole.PhotonOrbitRs:0.000} r_s)\n" +
                $"Shadow radius (a=0)   {Sci(blackHole.ShadowRadiusM / 1000.0)} km\n" +
                $"At {distRs:0} r_s: clocks run at {blackHole.TimeDilationFactor(r) * 100.0:0.00}% of far-away rate\n" +
                $"Tidal stretch on a 2 m astronaut: {Sci(tidal)} m/s^2 ({tidal / 9.81:0.##E+0} g)";
        }

        static string Sci(double v) => v >= 1e5 || (v < 0.01 && v > 0) ? v.ToString("0.00E+0") : v.ToString("#,0.##");
    }
}
