using System.Collections;
using EventHorizon.Core;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace EventHorizon.Game
{
    /// <summary>
    /// Loads a scene asynchronously behind a loading screen: a planet illustration with orbit rings, mission
    /// info, a science fact, a tip, status messages and a progress bar. Minimum display time is short (1.4 s).
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        static SceneLoader s_active;

        static readonly string[] Messages =
        {
            "Checking atmospheric pressure...",
            "Initializing ARES-1 life-support network...",
            "Calibrating solar power distribution...",
            "Synchronizing Martian navigation systems...",
            "Pressurizing habitat modules...",
            "Downloading orbital weather data from relay satellite...",
            "Verifying suit seals and oxygen reserves...",
        };

        static readonly string[] Facts =
        {
            "A sol, one Martian day, lasts 24 hours 39 minutes 35 seconds.",
            "Mars' air pressure is about 0.6% of Earth's at sea level, too thin for liquid water to last on the surface.",
            "Olympus Mons rises about 22 km above the surrounding plains, nearly three times Everest's height above sea level.",
            "Valles Marineris is about 4,000 km long and up to 7 km deep.",
            "Martian sunsets are blue: fine dust scatters blue light forward, toward the setting Sun.",
            "Phobos orbits Mars three times a day and is slowly spiralling inward.",
            "Radio signals take between 3 and 22 minutes to travel between Earth and Mars.",
            "MOXIE, aboard Perseverance, made 122 g of oxygen from Martian CO2 between 2021 and 2023.",
        };

        static readonly string[] Tips =
        {
            "Clean the solar panels regularly: dust cuts their output a little every sol.",
            "Watch the oxygen gauge. Refill your suit at any oxygen tank.",
            "Terminals show live data for each facility. Use one to check what is wrong.",
            "In a dust storm, solar output can drop by more than half. Charge the batteries beforehand.",
            "Press V to switch between first-person and third-person view.",
            "The Science Laboratory explains the physics behind every colony system.",
        };

        public static void Load(string scene, string title)
        {
            if (s_active != null) return;
            var go = new GameObject("Scene Loader");
            DontDestroyOnLoad(go);
            s_active = go.AddComponent<SceneLoader>();
            s_active.StartCoroutine(s_active.Run(scene, title));
        }

        IEnumerator Run(string scene, string title)
        {
            Time.timeScale = 1f;
            var doc = UiRoot.Create("Loading", 100, persistent: true);
            var root = doc.rootVisualElement;
            var screen = UiKit.Box(root, "eh-fill", "eh-loading", "eh-row");
            screen.pickingMode = PickingMode.Position;
            screen.style.paddingLeft = 120;
            screen.style.paddingRight = 120;

            var art = UiKit.Box(screen, "eh-center");
            art.style.width = 460;
            art.style.height = 460;
            var orbitA = UiKit.Box(art, "eh-orbit");
            orbitA.style.width = 440; orbitA.style.height = 440; orbitA.style.borderTopLeftRadius = 220; orbitA.style.borderTopRightRadius = 220; orbitA.style.borderBottomLeftRadius = 220; orbitA.style.borderBottomRightRadius = 220;
            var orbitB = UiKit.Box(art, "eh-orbit");
            orbitB.style.width = 380; orbitB.style.height = 380; orbitB.style.borderTopLeftRadius = 190; orbitB.style.borderTopRightRadius = 190; orbitB.style.borderBottomLeftRadius = 190; orbitB.style.borderBottomRightRadius = 190;
            var moon = UiKit.Box(orbitA);
            moon.style.width = 14; moon.style.height = 14; moon.style.backgroundColor = new Color(0.75f, 0.7f, 0.65f);
            moon.style.borderTopLeftRadius = 7; moon.style.borderTopRightRadius = 7; moon.style.borderBottomLeftRadius = 7; moon.style.borderBottomRightRadius = 7;
            moon.style.position = Position.Absolute;
            var planet = UiKit.Box(art, "eh-planet-art");
            var mars = UiResources.Instance != null ? UiResources.Instance.mars : null;
            if (mars != null)
            {
                planet.style.backgroundImage = new StyleBackground(mars);
                planet.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }

            var info = UiKit.Box(screen, "eh-col", "eh-grow");
            info.style.marginLeft = 80;
            UiKit.Text(info, "ARES-1 COLONY NETWORK // YEAR 2189", "eh-kicker");
            UiKit.Text(info, title.ToUpperInvariant(), "eh-logo-title").style.fontSize = 60;
            UiKit.Divider(info);
            UiKit.Text(info, "DID YOU KNOW", "eh-kicker");
            UiKit.Text(info, Facts[Random.Range(0, Facts.Length)], "eh-body");
            UiKit.Text(info, "TIP", "eh-kicker").style.marginTop = 14;
            UiKit.Text(info, Tips[Random.Range(0, Tips.Length)], "eh-body");
            var status = UiKit.Text(info, Messages[0], "eh-mono", "eh-accent");
            status.style.marginTop = 30;
            var bar = UiKit.Bar(info, out var fill);
            bar.style.marginTop = 8;
            UiKit.SetFill(fill, 0f);

            yield return null;
            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null)
            {
                Debug.LogError($"[Event Horizon] Scene '{scene}' is not in the build.");
                Destroy(doc.gameObject);
                Destroy(gameObject);
                s_active = null;
                yield break;
            }
            op.allowSceneActivation = false;
            float start = Time.unscaledTime, shown = 0f;
            int msg = 0;
            float nextMsg = start + 0.35f;
            while (true)
            {
                float t = Time.unscaledTime - start;
                float target = Mathf.Min(op.progress / 0.9f, t / 1.4f);
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2.5f);
                UiKit.SetFill(fill, shown);
                if (Time.unscaledTime > nextMsg) { msg = (msg + 1) % Messages.Length; status.text = Messages[msg]; nextMsg = Time.unscaledTime + 0.35f; }
                float a = t * 40f * Mathf.Deg2Rad;
                moon.style.left = 220 + Mathf.Cos(a) * 220 - 7;
                moon.style.top = 220 + Mathf.Sin(a) * 220 - 7;
                if (op.progress >= 0.9f && shown >= 0.999f) break;
                yield return null;
            }
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null; // let the new scene's Start run behind the screen

            screen.style.transitionProperty = new StyleList<StylePropertyName>(new System.Collections.Generic.List<StylePropertyName> { new("opacity") });
            screen.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new(0.35f, TimeUnit.Second) });
            screen.style.opacity = 0f;
            yield return new WaitForSecondsRealtime(0.4f);
            Destroy(doc.gameObject);
            Destroy(gameObject);
            s_active = null;
            GameInput.UpdateCursor();
        }
    }
}
