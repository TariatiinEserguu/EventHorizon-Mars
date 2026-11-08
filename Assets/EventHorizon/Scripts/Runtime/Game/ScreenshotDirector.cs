using System;
using System.Collections;
using System.IO;
using System.Text;
using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace EventHorizon.Game
{
    /// <summary>
    /// Visual test run. Start the player with "-eh-screenshots &lt;folder&gt;" and it tours the menus, the colony
    /// (day, sunset, night, dust storm, third person, terminal, pause) and the black-hole lab. It saves a PNG of
    /// each view plus a performance report (average/1%-low FPS, memory), then quits.
    /// </summary>
    public class ScreenshotDirector : MonoBehaviour
    {
        string _dir;
        readonly StringBuilder _report = new();

        public static string Folder
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-eh-screenshots") return args[i + 1];
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var dir = Folder;
            if (string.IsNullOrEmpty(dir)) return;
            var go = new GameObject("Screenshot Director");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenshotDirector>()._dir = dir;
        }

        /// <summary>"-eh-lang mn" runs the tour in another language (without saving it to the player's settings).</summary>
        static string Language
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-eh-lang") return args[i + 1];
                return null;
            }
        }

        static bool PrologueOnly => Array.IndexOf(Environment.GetCommandLineArgs(), "-eh-prologue") >= 0;

        /// <summary>"-eh-prologue": captures the landing cinematic and the first moments of the prologue, then quits.</summary>
        IEnumerator Prologue()
        {
            yield return WaitScene(Scenes.MainMenu, 2f);
            GameSession.StartCampaign(new SaveData { tutorial = TutorialMode.Basic }, 0);
            while (SceneManager.GetActiveScene().name != Scenes.MarsColony || FindAnyObjectByType<SceneLoader>() != null) yield return null;
            foreach (var (wait, file) in new[] { (3.5f, "p1_descent"), (5f, "p2_cockpit"), (5f, "p3_cockpit_late"), (6.5f, "p4_touchdown") })
            {
                yield return new WaitForSecondsRealtime(wait);
                yield return Shot(file);
            }
            while (Story.Cinematic.IsPlaying) yield return null;
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("p5_on_mars");
            Application.Quit();
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            if (Language != null) Loc.SetLanguage(Language, true, save: false);
            if (PrologueOnly) { yield return Prologue(); yield break; }
            _report.AppendLine($"EVENT HORIZON: MARS visual test  {DateTime.Now:yyyy-MM-dd HH:mm}");
            _report.AppendLine($"GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {SystemInfo.graphicsMemorySize} MB VRAM; CPU {SystemInfo.processorType}; RAM {SystemInfo.systemMemorySize} MB");
            _report.AppendLine($"Resolution {Screen.width}x{Screen.height}, preset {SettingsService.Current.graphics.preset}, fps limit {SettingsService.Current.graphics.fpsLimit}");

            yield return WaitScene(Scenes.MainMenu, 3f);
            yield return Shot("01_main_menu");
            var menu = FindAnyObjectByType<MainMenu>();
            foreach (var screen in new[] { "NEW GAME", "CHAPTER SELECT", "SCIENCE LABORATORY", "SETTINGS", "LOAD GAME" })
            {
                menu?.OpenForCapture(screen);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("02_menu_" + screen.ToLowerInvariant().Replace(' ', '_'));
            }
            yield return Measure("Main menu");

            GameSession.StartCampaign(new SaveData { tutorial = TutorialMode.None }, 1);
            yield return WaitScene(Scenes.MarsColony, 4f);
            var env = MarsEnvironment.Instance;
            var player = FindAnyObjectByType<AstronautController>();
            var weather = FindAnyObjectByType<DustStormSystem>();
            if (weather != null) weather.stormsEnabled = false;

            void SetHour(float hour) { if (env != null) env.SetClock(env.Sol, hour / 24f * MarsEnvironment.SolSeconds, env.solarLongitudeDeg, env.opticalDepth); }
            void View(Vector3 pos, float yaw, float pitch) { if (player != null) { player.Teleport(pos, yaw); player.SetLook(yaw, pitch); } }

            if (env != null) env.timeScale = 0f;
            SetHour(10.5f);
            View(new Vector3(3f, 0.2f, -16f), 8f, 3f);
            yield return Settle();
            yield return Shot("10_colony_day_first_person");
            yield return Measure("Colony, day");

            View(new Vector3(30f, 0.2f, -40f), 48f, 2f);
            yield return Settle();
            yield return Shot("11_solar_field");
            View(new Vector3(26f, 0.2f, -30f), 75f, 4f);
            yield return Settle();
            yield return Shot("11b_power_station");
            View(new Vector3(-30f, 0.2f, -14f), 0f, 6f);
            yield return Settle();
            yield return Shot("11c_greenhouse");

            if (player != null) player.firstPerson = false;
            View(new Vector3(5f, 0.2f, -4f), 25f, 10f);
            yield return Settle();
            yield return Shot("12_third_person");
            if (player != null) player.firstPerson = true;

            SetHour(17.9f);
            View(new Vector3(0f, 0.2f, -30f), -95f, 6f);
            yield return Settle();
            yield return Shot("13_sunset_west");

            SetHour(22f);
            View(new Vector3(4f, 0.2f, 4f), -8f, 6f);
            yield return Settle();
            yield return Shot("14_night_habitat_lights");

            SetHour(12f);
            if (weather != null) { weather.TriggerStorm(1f, 4f); env.opticalDepth = 4f; }
            View(new Vector3(3f, 0.2f, -16f), 8f, 3f);
            yield return Settle(2f);
            yield return Shot("15_dust_storm");
            if (weather != null && env != null) { env.opticalDepth = 0.5f; weather.TriggerStorm(0f); }

            var terminal = FindAnyObjectByType<ColonyTerminal>();
            if (terminal != null)
            {
                View(terminal.transform.position + terminal.transform.forward * -1.6f + Vector3.up * 0.1f, terminal.transform.eulerAngles.y, 15f);
                yield return Settle();
                TerminalScreen.Open(terminal);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("16_terminal");
                TerminalScreen.Close();
            }
            var pause = FindAnyObjectByType<PauseMenu>();
            if (pause != null)
            {
                yield return null;
                pause.Pause();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("17_pause_menu");
                pause.Open("JOURNAL");
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("18_journal");
                pause.Resume();
            }

            // Story: a conversation with subtitles and a science task.
            var reyes = System.Array.Find(FindObjectsByType<Story.Npc>(), n => n.character == Story.Character.Reyes);
            if (reyes != null)
            {
                View(reyes.transform.position + reyes.transform.forward * 3.2f + Vector3.up * 0.1f, reyes.transform.eulerAngles.y + 180f, 6f);
                yield return Settle(1.5f);
                Story.DialoguePlayer.StopAll();
                Story.DialoguePlayer.Play("PRO_REYES");
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("19_dialogue");
                Story.DialoguePlayer.StopAll();
            }
            Story.ExperimentPanel.Open("o2diag");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("19b_experiment");
            Story.ExperimentPanel.Close();

            // Space: planetarium close-ups, Mars orbit, black-hole lab.
            GameSession.StartScience(Scenes.SpaceLab);
            yield return WaitScene(Scenes.SpaceLab, 4f);
            foreach (var (target, file) in new[] { ("Earth", "20_earth"), ("Saturn", "21_saturn"), ("Jupiter", "22_jupiter"), ("Sun", "23_sun"),
                                                   ("Psyche", "24_asteroid_belt"), ("Comet", "25_comet"), ("Moon", "26_moon") })
            {
                if (Focus(target)) { yield return new WaitForSecondsRealtime(3f); yield return Shot(file); }
            }
            yield return Measure("Planetarium");

            GameSession.StartScience(Scenes.MarsOrbit);
            yield return WaitScene(Scenes.MarsOrbit, 4f);
            yield return Shot("30_mars_orbit");
            foreach (var (target, file) in new[] { ("Phobos", "31_phobos"), ("Station", "32_station"), ("ARES-1", "33_landing_site") })
            {
                if (Focus(target)) { yield return new WaitForSecondsRealtime(3f); yield return Shot(file); }
            }
            yield return Measure("Mars orbit");

            GameSession.StartScience(Scenes.BlackHoleLab);
            yield return WaitScene(Scenes.BlackHoleLab, 1f);
            var bh = FindAnyObjectByType<Space.BlackHole>();
            var orbitCam = FindAnyObjectByType<Space.OrbitCamera>();
            if (orbitCam != null) { orbitCam.pitch = 4f; orbitCam.yaw = 0f; orbitCam.distance = 58f; }
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("40_black_hole");
            if (bh != null && orbitCam != null)
            {
                bh.spin = 0.95f;
                orbitCam.distance = 42f;
                orbitCam.pitch = 3f;
                yield return new WaitForSecondsRealtime(3f);
                yield return Shot("41_black_hole_close_spin");
            }
            var lab = FindAnyObjectByType<Space.BlackHoleLab>();
            var labUi = FindAnyObjectByType<BlackHoleLabUI>();
            if (lab != null && orbitCam != null)
            {
                lab.ApplyPreset(Space.BlackHoleLab.Preset.Kerr);
                orbitCam.distance = 70f;
                orbitCam.pitch = 28f;
                lab.orbitStartRs = 12f; lab.orbitSpeedFactor = 0.92f; lab.orbitRadialSpeed = 0f;
                lab.LaunchOrbit();
                lab.mode = Space.BlackHoleLab.Mode.Research;
                labUi?.ShowTab("ORBITS");
                yield return new WaitForSecondsRealtime(4f);
                yield return Shot("42_lab_orbits");
                lab.ApplyPreset(Space.BlackHoleLab.Preset.Schwarzschild);
                bh.diskDensity = 0.15f;
                orbitCam.distance = 90f;
                orbitCam.pitch = 2f;
                yield return null;
                lab.AlignSource();
                lab.mode = Space.BlackHoleLab.Mode.Beginner;
                labUi?.ShowTab("SIM");
                yield return new WaitForSecondsRealtime(3f);
                yield return Shot("43_lab_einstein_ring");
                lab.SetGrid(true);
                orbitCam.pitch = 22f;
                labUi?.ShowTab("GUIDES");
                yield return new WaitForSecondsRealtime(3f);
                yield return Shot("44_lab_guides_grid");
            }
            yield return Measure("Black-hole lab");

            File.WriteAllText(Path.Combine(_dir, "performance.txt"), _report.ToString());
            Debug.Log("[Event Horizon] Screenshot run complete: " + _dir);
            Application.Quit();
        }

        static bool Focus(string name)
        {
            var orbit = FindAnyObjectByType<Space.OrbitCamera>();
            if (orbit == null) return false;
            for (int i = 0; i < orbit.targets.Length; i++)
                if (orbit.targets[i] != null && orbit.targets[i].name.Contains(name)) { orbit.Select(i); return true; }
            return false;
        }

        static IEnumerator WaitScene(string scene, float extraSeconds)
        {
            while (SceneManager.GetActiveScene().name != scene || FindAnyObjectByType<SceneLoader>() != null) yield return null;
            yield return new WaitForSecondsRealtime(extraSeconds);
        }

        static IEnumerator Settle(float seconds = 1f)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        IEnumerator Measure(string label)
        {
            // 4 seconds of frame times: average FPS and 1% low.
            var times = new System.Collections.Generic.List<float>();
            float end = UnityEngine.Time.realtimeSinceStartup + 4f;
            while (UnityEngine.Time.realtimeSinceStartup < end)
            {
                yield return null;
                times.Add(UnityEngine.Time.unscaledDeltaTime);
            }
            times.Sort();
            float avg = 0f;
            foreach (var t in times) avg += t;
            avg /= Mathf.Max(1, times.Count);
            float low = times.Count > 0 ? times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.99f))] : 0f;
            _report.AppendLine($"{label,-16} avg {1f / avg:0} fps ({avg * 1000f:0.0} ms)  1% low {1f / Mathf.Max(low, 1e-4f):0} fps  " +
                               $"managed {GC.GetTotalMemory(false) / 1048576f:0} MB  allocated {Profiler.GetTotalAllocatedMemoryLong() / 1048576f:0} MB  " +
                               $"gfx {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f:0} MB");
        }
    }
}
