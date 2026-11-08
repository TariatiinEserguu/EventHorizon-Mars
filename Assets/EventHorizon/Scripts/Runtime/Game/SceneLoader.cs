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

        // Loading-screen text lives in the UI table: LOAD_MSG_1.., FACT_1.., TIP_1..
        const int MessageCount = 7, FactCount = 12, TipCount = 10;
        static string Msg(int i) => Loc.T("LOAD_MSG_" + (i % MessageCount + 1));

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
            UiKit.Text(info, Loc.T("MENU_KICKER"), "eh-kicker");
            var heading = UiKit.Text(info, Loc.Upper(title), "eh-logo-title");
            heading.style.fontSize = 60;
            heading.style.whiteSpace = WhiteSpace.Normal;
            UiKit.Divider(info);
            UiKit.Text(info, Loc.T("LOAD_DID_YOU_KNOW"), "eh-kicker");
            UiKit.Text(info, Loc.T("FACT_" + Random.Range(1, FactCount + 1)), "eh-body");
            UiKit.Text(info, Loc.T("LOAD_TIP"), "eh-kicker").style.marginTop = 14;
            UiKit.Text(info, Loc.T("TIP_" + Random.Range(1, TipCount + 1)), "eh-body");
            var status = UiKit.Text(info, Msg(0), "eh-mono", "eh-accent");
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
                if (Time.unscaledTime > nextMsg) { msg++; status.text = Msg(msg); nextMsg = Time.unscaledTime + 0.35f; }
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
