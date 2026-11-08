using System;
using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Space
{
    /// <summary>
    /// The black-hole laboratory's simulation: presets, quality, visual guides (horizon, photon orbits, ISCO,
    /// ergosphere), the spacetime-curvature grid, test-particle orbits, the two-clock time-dilation demo, the tidal
    /// probe, and the expedition setup for chapter 10. Independent of the colony simulation, so it can also be opened
    /// on its own from the Science Laboratory.
    ///
    /// Orbits are integrated as exact Kerr equatorial geodesics (Boyer-Lindquist, Carter's equations with Q = 0) in
    /// proper time with RK4, next to a Newtonian orbit with the same start, so precession, plunges inside the ISCO and
    /// relativistic capture can be compared with what Newton predicts.
    /// </summary>
    public class BlackHoleLab : MonoBehaviour
    {
        public enum Preset { Schwarzschild, Kerr, Stellar, Supermassive, Horizon1 }
        public enum Quality { Low, Medium, High }
        public enum Mode { Beginner, Research, Experiment, Observation }

        public BlackHole blackHole;
        public OrbitCamera orbitCamera;
        public Material lineMaterial;
        public Material podMaterial;

        public static BlackHoleLab Instance { get; private set; }

        public Preset preset = Preset.Supermassive;
        public Quality quality = Quality.High;
        public Mode mode = Mode.Beginner;
        public bool showGuides = true, showGrid, showLabels = true;

        /// <summary>HORIZON-1: six Earth masses, spin 0.8 (the story's primordial black hole).</summary>
        public static readonly double Horizon1MassSolar = 6.0 * BlackHole.EarthMass / BlackHole.SolarMass;
        public const double Horizon1Spin = 0.8;
        public static double Horizon1SignalHz => BlackHole.IscoFrequencyHz(Horizon1MassSolar, Horizon1Spin);

        // ------------------------------------------------------------------ guides
        readonly List<LineRenderer> _horizonRings = new(), _ergoLines = new();
        LineRenderer _photonPro, _photonRetro, _iscoPro, _iscoRetro;
        GameObject _grid;
        public readonly List<(string key, Vector3 world)> Labels = new();

        // ------------------------------------------------------------------ orbits
        public class OrbitResult
        {
            public readonly List<Vector3> rel = new();     // (x, z in r_s, coordinate time t in M)
            public readonly List<float> tau = new();       // proper time along the path (M)
            public readonly List<Vector3> newton = new();  // Newtonian (x, z, t)
            public string outcome = "", newtonOutcome = "";
            public double E, L, peri = double.NaN, apo = double.NaN, periodT, precessionDeg, bindingFraction;
            public int periapses;
        }

        public float orbitStartRs = 8f, orbitSpeedFactor = 1f, orbitRadialSpeed;
        public OrbitResult Orbit { get; private set; }
        LineRenderer _orbitLine, _newtonLine;
        Transform _probe, _newtonProbe;
        float _playT;
        public float PlayTau { get; private set; }
        public float PlayT => _playT;

        // ------------------------------------------------------------------ clocks and tides
        public bool clockOrbiting;
        public float clockRadiusRs = 3f;
        public double DistantClock { get; private set; }
        public double LocalClock { get; private set; }
        public float tideRadiusRs = 10f, tideLengthM = 2f;
        public bool showTides;
        Transform _tideProbe;
        LineRenderer _tideArrows;

        // ------------------------------------------------------------------ lensing study
        float _lastYaw;
        public float YawTravelled { get; private set; }

        public float ViewingDistanceRs => orbitCamera != null && blackHole != null ? orbitCamera.distance / blackHole.WorldUnitsPerRs : 30f;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (blackHole == null) blackHole = FindAnyObjectByType<BlackHole>();
            if (orbitCamera == null) orbitCamera = FindAnyObjectByType<OrbitCamera>();
            if (blackHole == null) return;
            BuildGuides();
            BuildGrid();
            ApplyQuality(SettingsService.Current.graphics.preset switch
            {
                GraphicsPreset.Low => Quality.Low, GraphicsPreset.Medium => Quality.Medium, _ => Quality.High,
            });
            if (orbitCamera != null) _lastYaw = orbitCamera.yaw;
        }

        // ------------------------------------------------------------------ presets and quality

        public void ApplyPreset(Preset p)
        {
            preset = p;
            var bh = blackHole;
            bh.hotSpot = 0f;
            switch (p)
            {
                case Preset.Schwarzschild: bh.massSolar = 1e6; bh.spin = 0f; bh.diskTemperature = 5500f; bh.diskDensity = 0.8f; break;
                case Preset.Kerr: bh.massSolar = 1e6; bh.spin = 0.9f; bh.diskTemperature = 5500f; bh.diskDensity = 0.8f; break;
                case Preset.Stellar: bh.massSolar = 10; bh.spin = 0.7f; bh.diskTemperature = 16000f; bh.diskDensity = 0.75f; break;
                case Preset.Supermassive: bh.massSolar = 6.5e9; bh.spin = 0.9f; bh.diskTemperature = 4300f; bh.diskDensity = 0.85f; break;
                case Preset.Horizon1:
                    bh.massSolar = Horizon1MassSolar; bh.spin = (float)Horizon1Spin; bh.diskTemperature = 7000f; bh.diskDensity = 0.08f; bh.hotSpot = 2.5f;
                    break;
            }
            Orbit = null;
            ClearOrbit();
        }

        public void ApplyQuality(Quality q)
        {
            quality = q;
            switch (q)
            {
                case Quality.Low: blackHole.steps = 110; blackHole.stepScale = 1.8f; blackHole.detail = 1; break;
                case Quality.Medium: blackHole.steps = 200; blackHole.stepScale = 1.25f; blackHole.detail = 2; break;
                default: blackHole.steps = 340; blackHole.stepScale = 0.85f; blackHole.detail = 3; break;
            }
        }

        public void ResetSimulation()
        {
            ApplyPreset(preset);
            blackHole.paused = false;
            blackHole.simulationSpeed = 1f;
            blackHole.sourceBrightness = 0f;
            DistantClock = LocalClock = 0;
            if (orbitCamera != null) { orbitCamera.pitch = 4f; orbitCamera.distance = 58f; }
        }

        /// <summary>Puts the background star directly behind the hole as seen from the camera: an Einstein ring.</summary>
        public void AlignSource()
        {
            if (orbitCamera == null) return;
            blackHole.sourceDirection = LineOfSight;
            if (blackHole.sourceBrightness <= 0f) blackHole.sourceBrightness = 6f;
        }

        /// <summary>Direction from the camera through the hole, from the orbit camera's own angles (not its smoothed position).</summary>
        Vector3 LineOfSight => Quaternion.Euler(orbitCamera.pitch, orbitCamera.yaw, 0f) * Vector3.forward;

        /// <summary>Background star position as azimuth/elevation (degrees) around the hole.</summary>
        public void SetSource(float azimuth, float elevation)
        {
            blackHole.sourceDirection = Quaternion.Euler(-elevation, azimuth, 0f) * Vector3.forward;
            if (blackHole.sourceBrightness <= 0f) blackHole.sourceBrightness = 6f;
        }

        /// <summary>Angle (degrees) between the background star and the line of sight through the hole.</summary>
        public float SourceMisalignmentDeg
        {
            get
            {
                if (orbitCamera == null || blackHole.sourceBrightness <= 0f) return 180f;
                return Vector3.Angle(LineOfSight, blackHole.sourceDirection);
            }
        }

        // ------------------------------------------------------------------ campaign checks (chapter 9)

        public static bool MassMatchesHorizon1 =>
            Instance != null && Math.Abs(Math.Log10(Instance.blackHole.massSolar / Horizon1MassSolar)) <= Math.Log10(1.1);

        public static bool ConfiguredForHorizon1 => Instance != null && Instance.blackHole.spin >= 0.3f && Instance.blackHole.diskDensity <= 0.25f;

        public static float LensingStudyProgress =>
            Instance == null ? 0f : 0.5f * Mathf.Clamp01(Instance.YawTravelled / 180f) + (Instance.SourceMisalignmentDeg <= 2f ? 0.5f : 0f);

        public static bool SpinMatchesSignal =>
            MassMatchesHorizon1 && Math.Abs(Instance.blackHole.IscoFrequency - Horizon1SignalHz) / Horizon1SignalHz <= 0.02;

        /// <summary>Chapter 10: the lab scene becomes the view from the expedition ship at HORIZON-1.</summary>
        public static void BeginExpedition()
        {
            var lab = Instance;
            if (lab == null || lab.blackHole == null) return;
            lab.ApplyPreset(Preset.Horizon1);
            lab.mode = Mode.Observation;
            var targets = new List<Transform>(lab.orbitCamera != null ? lab.orbitCamera.targets : new Transform[0]);
            var distances = new List<float>(lab.orbitCamera != null ? lab.orbitCamera.targetDistances : new float[0]);
            while (distances.Count < targets.Count) distances.Add(0f);
            float sphere = lab.blackHole.transform.lossyScale.x * 0.5f;
            for (int i = 1; i <= 3; i++)
            {
                var pod = lab.MakePod(i);
                var orbit = pod.AddComponent<OrbitMotion>();
                orbit.center = lab.blackHole.transform;
                orbit.radius = sphere * (1.25f + 0.35f * i);
                orbit.periodHours = 0.02f * i;
                orbit.timeScale = 1f;
                orbit.inclination = 6f * i;
                orbit.startAngle = 110f * i;
                targets.Add(pod.transform);
                distances.Add(6f);
            }
            if (lab.orbitCamera != null)
            {
                lab.orbitCamera.targets = targets.ToArray();
                lab.orbitCamera.targetDistances = distances.ToArray();
                lab.orbitCamera.distanceRange = new Vector2(2f, 900f);
            }
        }

        GameObject MakePod(int index)
        {
            var root = new GameObject($"Pod_{index}");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.transform.SetParent(root.transform, false);
            wing.transform.localScale = new Vector3(4.2f, 0.06f, 1.1f);
            var light = new GameObject("Beacon").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * 1.6f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 12f;
            light.intensity = 3f;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) if (podMaterial != null) r.sharedMaterial = podMaterial;
            foreach (var c in root.GetComponentsInChildren<Collider>()) Destroy(c);
            var info = root.AddComponent<ScienceInspectable>();
            info.key = "Pod";
            info.title = $"Horizon instrument pod {index}";
            info.facts = "";
            return root;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (blackHole == null) return;
            if (orbitCamera != null)
            {
                YawTravelled += Mathf.Abs(Mathf.DeltaAngle(_lastYaw, orbitCamera.yaw));
                _lastYaw = orbitCamera.yaw;
            }
            float dt = blackHole.paused ? 0f : Time.deltaTime * blackHole.simulationSpeed;
            UpdateGuides();
            UpdateClocks(dt);
            UpdateOrbitPlayback(dt);
            UpdateTides();
        }

        // ------------------------------------------------------------------ guides

        LineRenderer Line(string name, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMaterial;
            lr.startColor = lr.endColor = color;
            lr.widthMultiplier = width;
            lr.loop = loop;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }

        void BuildGuides()
        {
            for (int i = 0; i < 3; i++) _horizonRings.Add(Line("Horizon " + i, new Color(1f, 1f, 1f, 0.55f), 0.06f, true));
            for (int i = 0; i < 4; i++) _ergoLines.Add(Line("Ergosphere " + i, new Color(0.65f, 0.45f, 1f, 0.6f), 0.05f, true));
            _photonPro = Line("Photon orbit (prograde)", new Color(1f, 0.85f, 0.3f, 0.75f), 0.05f, true);
            _photonRetro = Line("Photon orbit (retrograde)", new Color(1f, 0.6f, 0.2f, 0.45f), 0.04f, true);
            _iscoPro = Line("ISCO (prograde)", new Color(0.3f, 0.9f, 1f, 0.75f), 0.05f, true);
            _iscoRetro = Line("ISCO (retrograde)", new Color(0.3f, 0.6f, 1f, 0.4f), 0.04f, true);
        }

        static void Circle(LineRenderer lr, Func<float, Vector3> point, int n = 96)
        {
            lr.positionCount = n;
            for (int i = 0; i < n; i++) lr.SetPosition(i, point(i / (float)n * Mathf.PI * 2f));
        }

        void UpdateGuides()
        {
            Labels.Clear();
            bool on = showGuides && mode != Mode.Observation;
            foreach (var l in _horizonRings) l.enabled = on;
            foreach (var l in _ergoLines) l.enabled = on && blackHole.spin > 0.01f;
            _photonPro.enabled = _iscoPro.enabled = on;
            _photonRetro.enabled = _iscoRetro.enabled = on && blackHole.spin > 0.01f;
            if (!on) return;

            float rh = (float)blackHole.HorizonRs, a = blackHole.spin;
            var bh = blackHole;
            Circle(_horizonRings[0], t => bh.RsToWorld(new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * rh));
            Circle(_horizonRings[1], t => bh.RsToWorld(new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * rh));
            Circle(_horizonRings[2], t => bh.RsToWorld(new Vector3(0f, Mathf.Sin(t), Mathf.Cos(t)) * rh));
            for (int i = 0; i < _ergoLines.Count; i++)
            {
                float az = i * Mathf.PI / _ergoLines.Count;
                Circle(_ergoLines[i], t =>
                {
                    // t runs around a meridian: polar angle theta = t, both sides of the axis.
                    float r = (float)BlackHole.Ergosphere(a, t) * 0.5f;
                    return bh.RsToWorld(new Vector3(Mathf.Sin(t) * Mathf.Cos(az), Mathf.Cos(t), Mathf.Sin(t) * Mathf.Sin(az)) * r);
                }, 72);
            }
            float rp = (float)bh.PhotonOrbitRs, rr = (float)bh.PhotonOrbitRetroRs, ip = (float)bh.IscoRs, ir = (float)bh.IscoRetroRs;
            Circle(_photonPro, t => bh.RsToWorld(new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * rp));
            Circle(_photonRetro, t => bh.RsToWorld(new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * rr));
            Circle(_iscoPro, t => bh.RsToWorld(new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * ip));
            Circle(_iscoRetro, t => bh.RsToWorld(new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * ir));

            if (!showLabels) return;
            Labels.Add(("LAB_GUIDE_HORIZON", bh.RsToWorld(new Vector3(0f, rh * 1.05f, 0f))));
            Labels.Add(("LAB_GUIDE_PHOTON", bh.RsToWorld(new Vector3(-rp, 0f, 0f))));
            Labels.Add(("LAB_GUIDE_ISCO", bh.RsToWorld(new Vector3(-ip * 0.71f, 0f, -ip * 0.71f))));
            if (a > 0.01f)
            {
                Labels.Add(("LAB_GUIDE_ERGO", bh.RsToWorld(new Vector3(0f, 0f, (float)bh.ErgoEquatorRs * 1.02f))));
                Labels.Add(("LAB_GUIDE_ISCO_RETRO", bh.RsToWorld(new Vector3(ir * 0.71f, 0f, ir * 0.71f))));
            }
            if (_grid != null && _grid.activeSelf) Labels.Add(("LAB_GUIDE_GRID", bh.RsToWorld(new Vector3(22f, -12f, 0f))));
        }

        /// <summary>
        /// Flamm's paraboloid: the curved space around a Schwarzschild hole drawn as a surface, z = 2 sqrt(r_s (r - r_s)).
        /// It shows distances stretching near the horizon. (An embedding picture of space, not a gravity well for objects.)
        /// </summary>
        void BuildGrid()
        {
            _grid = new GameObject("Spacetime Grid");
            _grid.transform.SetParent(blackHole.transform, false);
            var mesh = new Mesh { name = "Flamm paraboloid" };
            var verts = new List<Vector3>();
            var idx = new List<int>();
            const float rMax = 26f, depthOffset = -14f, scale = 0.7f;
            float Y(float r) => depthOffset - (2f * Mathf.Sqrt(rMax - 1f) - 2f * Mathf.Sqrt(Mathf.Max(r - 1f, 0f))) * scale;
            float inv = 1f / blackHole.boundRadius * 0.5f; // r_s -> object space (sphere radius 0.5 = boundRadius)
            for (float r = 1f; r <= rMax; r += 1.5f)
            {
                int start = verts.Count;
                for (int i = 0; i < 72; i++)
                {
                    float t = i / 72f * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(t) * r, Y(r), Mathf.Sin(t) * r) * inv);
                    idx.Add(start + i); idx.Add(start + (i + 1) % 72);
                }
            }
            for (int k = 0; k < 24; k++)
            {
                float t = k / 24f * Mathf.PI * 2f;
                int start = verts.Count;
                int n = 0;
                for (float r = 1f; r <= rMax; r += 0.5f, n++) verts.Add(new Vector3(Mathf.Cos(t) * r, Y(r), Mathf.Sin(t) * r) * inv);
                for (int i = 0; i < n - 1; i++) { idx.Add(start + i); idx.Add(start + i + 1); }
            }
            mesh.SetVertices(verts);
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            _grid.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = _grid.AddComponent<MeshRenderer>();
            mr.sharedMaterial = lineMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _grid.SetActive(showGrid);
        }

        public void SetGrid(bool on)
        {
            showGrid = on;
            if (_grid != null) _grid.SetActive(on);
        }

        // ------------------------------------------------------------------ clocks

        void UpdateClocks(float dt)
        {
            double rate = ClockRate(clockRadiusRs, clockOrbiting);
            DistantClock += dt;
            LocalClock += dt * (double.IsNaN(rate) ? 0.0 : rate);
        }

        /// <summary>
        /// Local clock rate relative to a distant clock at r (r_s units). Static: sqrt(1 - r_s/r), only possible outside
        /// the ergosphere. Orbiting: 1/u^t of a prograde circular orbit, only possible outside the photon orbit.
        /// </summary>
        public double ClockRate(double rRs, bool orbiting)
        {
            double rM = rRs * 2.0;
            if (orbiting) { double ut = BlackHole.OrbitUt(rM, blackHole.spin); return double.IsNaN(ut) ? double.NaN : 1.0 / ut; }
            double x = 1.0 - 2.0 / rM;
            return rRs > blackHole.ErgoEquatorRs ? Math.Sqrt(Math.Max(0.0, x)) : double.NaN;
        }

        public void ResetClocks() => DistantClock = LocalClock = 0;

        // ------------------------------------------------------------------ orbits

        void ClearOrbit()
        {
            if (_orbitLine != null) _orbitLine.positionCount = 0;
            if (_newtonLine != null) _newtonLine.positionCount = 0;
            if (_probe != null) _probe.gameObject.SetActive(false);
            if (_newtonProbe != null) _newtonProbe.gameObject.SetActive(false);
        }

        /// <summary>Launches a test particle from (r0, 0) with the current settings and integrates both orbits.</summary>
        public void LaunchOrbit()
        {
            Orbit = Integrate(orbitStartRs * 2.0, orbitSpeedFactor, orbitRadialSpeed, blackHole.spin);
            _playT = 0f;
            PlayTau = 0f;
            if (_orbitLine == null)
            {
                _orbitLine = Line("Orbit (relativistic)", new Color(0.35f, 1f, 0.6f, 0.9f), 0.06f, false);
                _newtonLine = Line("Orbit (Newtonian)", new Color(1f, 1f, 1f, 0.35f), 0.04f, false);
                _probe = MakeProbe("Test particle", new Color(0.4f, 1f, 0.7f));
                _newtonProbe = MakeProbe("Newtonian particle", new Color(0.8f, 0.8f, 0.8f));
            }
            Draw(_orbitLine, Orbit.rel);
            Draw(_newtonLine, Orbit.newton);
            _probe.gameObject.SetActive(true);
            _newtonProbe.gameObject.SetActive(true);
            if (Orbit.periapses >= 2 && Math.Abs(Orbit.precessionDeg) > 0.5) GameEvents.Raise("orbit_precession");
        }

        Transform MakeProbe(string name, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.5f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = lineMaterial;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = lineMaterial;
            trail.time = 2.5f;
            trail.widthMultiplier = 0.15f;
            trail.startColor = c;
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
            return go.transform;
        }

        void Draw(LineRenderer lr, List<Vector3> pts)
        {
            int step = Mathf.Max(1, pts.Count / 1500);
            var list = new List<Vector3>();
            for (int i = 0; i < pts.Count; i += step) list.Add(blackHole.RsToWorld(new Vector3(pts[i].x, 0f, pts[i].y)));
            lr.positionCount = list.Count;
            lr.SetPositions(list.ToArray());
        }

        void UpdateOrbitPlayback(float dt)
        {
            if (Orbit == null || Orbit.rel.Count < 2) return;
            // Playback in coordinate time (what a distant observer sees), 25 M per second at 1x.
            _playT += dt * 25f;
            Vector3 Sample(List<Vector3> pts, float t, out int index)
            {
                index = pts.Count - 1;
                if (t >= pts[^1].z) return pts[^1];
                int lo = 0, hi = pts.Count - 1;
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (pts[mid].z < t) lo = mid; else hi = mid; }
                index = lo;
                float f = Mathf.InverseLerp(pts[lo].z, pts[hi].z, t);
                return Vector3.Lerp(pts[lo], pts[hi], f);
            }
            var p = Sample(Orbit.rel, _playT, out int ri);
            PlayTau = Orbit.tau[Mathf.Clamp(ri, 0, Orbit.tau.Count - 1)];
            _probe.position = blackHole.RsToWorld(new Vector3(p.x, 0f, p.y));
            if (Orbit.newton.Count > 1)
            {
                var n = Sample(Orbit.newton, _playT, out _);
                _newtonProbe.position = blackHole.RsToWorld(new Vector3(n.x, 0f, n.y));
            }
            float end = Mathf.Max(Orbit.rel[^1].z, Orbit.newton.Count > 0 ? Orbit.newton[^1].z : 0f);
            if (_playT > end + 50f) _playT = 0f; // loop
        }

        /// <summary>
        /// Kerr equatorial geodesic from radius r0 (M units). L = speedFactor x circular-orbit L; E then follows from
        /// the radial equation R(r0) = r0^4 (dr/dtau)^2. Same start for a Newtonian orbit (L = speedFactor sqrt(r0)).
        /// </summary>
        public static OrbitResult Integrate(double r0, double speedFactor, double radialSpeed, double a)
        {
            var res = new OrbitResult();
            double rh = BlackHole.HorizonOuter(a);
            r0 = Math.Max(r0, rh * 1.05);
            double s = Math.Pow(r0, 1.5) - 3.0 * Math.Sqrt(r0) + 2.0 * a;
            double Lc = s > 0 ? (r0 * r0 - 2.0 * a * Math.Sqrt(r0) + a * a) / (Math.Pow(r0, 0.75) * Math.Sqrt(s)) : Math.Sqrt(r0);
            double L = speedFactor * Lc;
            double D0 = r0 * r0 - 2.0 * r0 + a * a;
            double A = (r0 * r0 + a * a) * (r0 * r0 + a * a) - D0 * a * a;
            double B = -2.0 * a * L * (r0 * r0 + a * a) + 2.0 * D0 * a * L;
            double Cc = a * a * L * L - D0 * (r0 * r0 + L * L) - Math.Pow(r0, 4) * radialSpeed * radialSpeed;
            double disc = Math.Max(0.0, B * B - 4.0 * A * Cc);
            double E = (-B + Math.Sqrt(disc)) / (2.0 * A);
            res.E = E;
            res.L = L;
            res.bindingFraction = 1.0 - E;

            double Rfun(double r)
            {
                double D = r * r - 2.0 * r + a * a;
                double P = E * (r * r + a * a) - a * L;
                return (P * P - D * (r * r + (L - a * E) * (L - a * E))) / Math.Pow(r, 4);
            }
            double Acc(double r) { double h = 1e-5 * r; return 0.5 * (Rfun(r + h) - Rfun(r - h)) / (2.0 * h); }
            void Rates(double r, out double dphi, out double dt)
            {
                double D = r * r - 2.0 * r + a * a;
                double P = E * (r * r + a * a) - a * L;
                dphi = (-(a * E - L) + a * P / D) / (r * r);
                dt = (-a * (a * E - L) + (r * r + a * a) * P / D) / (r * r);
            }

            double rr = r0, pr = radialSpeed, phi = 0.0, t = 0.0, tau = 0.0;
            double lastPeriPhi = double.NaN, lastPeriT = double.NaN, precSum = 0.0;
            int precCount = 0;
            double prevPr = pr;
            for (int i = 0; i < 80000; i++)
            {
                if (i % 4 == 0) { res.rel.Add(new Vector3((float)(rr * 0.5 * Math.Cos(phi)), (float)(rr * 0.5 * Math.Sin(phi)), (float)t)); res.tau.Add((float)tau); }
                if (rr <= rh * 1.01) { res.outcome = "CAPTURED"; break; }
                if (rr > 400.0 && pr > 0) { res.outcome = "ESCAPED"; break; }
                if (t > 60000.0) { res.outcome = res.periapses > 0 ? "BOUND" : "BOUND"; break; }
                double h = 0.004 * Math.Pow(rr, 1.5);
                // RK4 for (r, pr, phi, t) in proper time.
                Rates(rr, out double k1phi, out double k1t);
                double k1r = pr, k1p = Acc(rr);
                double r2 = rr + 0.5 * h * k1r; Rates(r2, out double k2phi, out double k2t);
                double k2r = pr + 0.5 * h * k1p, k2p = Acc(r2);
                double r3 = rr + 0.5 * h * k2r; Rates(r3, out double k3phi, out double k3t);
                double k3r = pr + 0.5 * h * k2p, k3p = Acc(r3);
                double r4 = rr + h * k3r; Rates(r4, out double k4phi, out double k4t);
                double k4r = pr + h * k3p, k4p = Acc(r4);
                rr += h / 6.0 * (k1r + 2 * k2r + 2 * k3r + k4r);
                pr += h / 6.0 * (k1p + 2 * k2p + 2 * k3p + k4p);
                phi += h / 6.0 * (k1phi + 2 * k2phi + 2 * k3phi + k4phi);
                t += h / 6.0 * (k1t + 2 * k2t + 2 * k3t + k4t);
                tau += h;
                if (prevPr < 0 && pr >= 0)
                {
                    res.periapses++;
                    res.peri = double.IsNaN(res.peri) ? rr : Math.Min(res.peri, rr);
                    if (!double.IsNaN(lastPeriPhi)) { precSum += phi - lastPeriPhi - 2 * Math.PI; precCount++; res.periodT = t - lastPeriT; }
                    lastPeriPhi = phi;
                    lastPeriT = t;
                }
                if (prevPr > 0 && pr <= 0) res.apo = double.IsNaN(res.apo) ? rr : Math.Max(res.apo, rr);
                prevPr = pr;
                if (res.periapses >= 12) { res.outcome = "BOUND"; break; }
            }
            if (string.IsNullOrEmpty(res.outcome)) res.outcome = "BOUND";
            if (Math.Abs(radialSpeed) < 1e-6 && Math.Abs(speedFactor - 1.0) < 1e-3 && s > 0 && res.outcome == "BOUND") res.outcome = "CIRCULAR";
            if (precCount > 0) res.precessionDeg = precSum / precCount * 180.0 / Math.PI;

            // Newtonian orbit with the same start: d2r/dt2 = -1/r^2 + L^2/r^3, dphi/dt = L/r^2.
            double Ln = speedFactor * Math.Sqrt(r0), nr = r0, nv = radialSpeed, nphi = 0.0, nt = 0.0;
            for (int i = 0; i < 80000; i++)
            {
                if (i % 4 == 0) res.newton.Add(new Vector3((float)(nr * 0.5 * Math.Cos(nphi)), (float)(nr * 0.5 * Math.Sin(nphi)), (float)nt));
                if (nr <= 2.0) { res.newtonOutcome = "HIT"; break; }
                if (nr > 400.0 && nv > 0) { res.newtonOutcome = "ESCAPED"; break; }
                if (nt > Math.Max(t, 2000.0)) { res.newtonOutcome = "BOUND"; break; }
                double h = 0.004 * Math.Pow(nr, 1.5);
                double acc = -1.0 / (nr * nr) + Ln * Ln / (nr * nr * nr);
                nv += acc * h;            // semi-implicit Euler: stable for orbits
                nr += nv * h;
                nphi += Ln / (nr * nr) * h;
                nt += h;
            }
            if (string.IsNullOrEmpty(res.newtonOutcome)) res.newtonOutcome = "BOUND";
            return res;
        }

        // ------------------------------------------------------------------ tides

        void UpdateTides()
        {
            if (!showTides || mode == Mode.Observation)
            {
                if (_tideProbe != null) { _tideProbe.gameObject.SetActive(false); _tideArrows.enabled = false; }
                return;
            }
            if (_tideProbe == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Tidal probe";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = podMaterial != null ? podMaterial : lineMaterial;
                _tideProbe = go.transform;
                _tideArrows = Line("Tidal arrows", new Color(1f, 0.55f, 0.3f, 0.9f), 0.06f, false);
            }
            _tideProbe.gameObject.SetActive(true);
            _tideArrows.enabled = true;
            double rM = tideRadiusRs * blackHole.SchwarzschildRadiusM;
            double stretch = blackHole.TidalAcceleration(rM, tideLengthM);
            // Visual exaggeration grows with log(stretch / g), never torn apart: a capsule that lengthens and thins.
            float k = Mathf.Clamp((float)Math.Log10(1.0 + stretch / 9.81), 0f, 4f);
            float u = blackHole.WorldUnitsPerRs;
            var pos = blackHole.RsToWorld(new Vector3(tideRadiusRs, 4f, 0f));
            _tideProbe.position = pos;
            var radial = (pos - blackHole.transform.position).normalized;
            _tideProbe.rotation = Quaternion.FromToRotation(Vector3.up, radial);
            _tideProbe.localScale = new Vector3(0.6f / (1f + 0.25f * k), 0.8f * (1f + 0.6f * k), 0.6f / (1f + 0.25f * k)) * Mathf.Max(u, 0.5f);
            float len = (1.5f + k) * Mathf.Max(u, 0.5f);
            _tideArrows.positionCount = 4;
            _tideArrows.SetPositions(new[] { pos - radial * len * 1.6f, pos - radial * len * 0.7f, pos + radial * len * 0.7f, pos + radial * len * 1.6f });
        }
    }
}
