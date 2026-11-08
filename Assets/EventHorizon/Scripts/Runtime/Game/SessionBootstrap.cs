using EventHorizon.Agriculture;
using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.UI;
using UnityEngine;

namespace EventHorizon.Game
{
    /// <summary>
    /// Put one in every gameplay scene. On start it applies difficulty or sandbox options, restores a loaded save,
    /// and creates the HUD, pause menu and mission tracker. Scenes opened directly in the editor play as a
    /// Standard campaign, chapter 1.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class SessionBootstrap : MonoBehaviour
    {
        void Start()
        {
            var state = GameSession.State;
            var mode = GameSession.Mode == GameMode.None ? GameMode.Campaign : GameSession.Mode;

            if (GameSession.RestoreWorld)
            {
                ApplyRules(state, mode, seedStocks: false);
                WorldState.Apply(state);
                GameSession.RestoreWorld = false;
                Hud.Ensure();
                Hud.Toast(Loc.T("TOAST_LOADED", state.saveName));
            }
            else
            {
                ApplyRules(state, mode, seedStocks: true);
            }

            Hud.Ensure();
            if (FindAnyObjectByType<PauseMenu>() == null) new GameObject("Pause Menu").AddComponent<PauseMenu>();
            if (FindAnyObjectByType<MissionTracker>() == null) gameObject.AddComponent<MissionTracker>();
            if (mode == GameMode.Campaign && !state.hasWorldState) ShowIntro(state);
            ApplySuitColor(state.suitColor);
            GameInput.PointerScene = FindAnyObjectByType<AstronautController>() == null;
            GameInput.ClearBlocks();
            GameInput.UpdateCursor();
        }

        static void ApplyRules(SaveData state, GameMode mode, bool seedStocks)
        {
            var d = state.difficulty;
            float use = d switch { Difficulty.Casual => 0.6f, Difficulty.Scientist => 1.15f, Difficulty.Engineer => 1.25f, _ => 1f };
            float stock = d switch { Difficulty.Casual => 1.5f, Difficulty.Scientist => 0.85f, Difficulty.Engineer => 0.75f, _ => 1f };
            float wear = d == Difficulty.Engineer ? 2f : d == Difficulty.Casual ? 0.5f : 1f;
            var sandbox = state.sandbox ?? new SandboxSettings();

            if (mode == GameMode.Sandbox || mode == GameMode.Science)
            {
                use = 1f;
                stock = sandbox.startingResources;
                foreach (var crew in FindObjectsByType<CrewLifeSupport>()) crew.crew = sandbox.crew;
                if (MarsEnvironment.Instance != null) MarsEnvironment.Instance.timeScale = sandbox.simulationSpeed;
                var storms = FindAnyObjectByType<DustStormSystem>();
                if (storms != null) storms.stormsEnabled = sandbox.dustStorms;
            }

            foreach (var suit in FindObjectsByType<SuitLifeSupport>())
            {
                suit.consumptionMultiplier = use;
                suit.infiniteOxygen = (mode == GameMode.Sandbox || mode == GameMode.Science) && sandbox.infiniteSuitOxygen;
            }
            foreach (var crew in FindObjectsByType<CrewLifeSupport>())
            {
                crew.oxygenKgPerPersonPerSol = 0.84f * use;
                crew.foodKgPerPersonPerSol = 1.5f * use;
            }
            foreach (var panel in FindObjectsByType<SolarPanel>()) panel.dustPerSol = 0.0028f * wear;

            var grid = ColonyGrid.Instance;
            if (seedStocks && grid != null && !Mathf.Approximately(stock, 1f))
            {
                foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType))) grid.SetAmount(t, grid.Amount(t) * stock);
            }
        }

        static void ApplySuitColor(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) return;
            foreach (var suit in FindObjectsByType<AstronautSuit>())
            {
                var pack = suit.chest != null ? suit.chest.Find("EH_Backpack") : null;
                if (pack != null && pack.TryGetComponent<Renderer>(out var r))
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_BaseColor", c);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        static void ShowIntro(SaveData state)
        {
            var ch = Story.Story.MainQuest(state.chapter);
            if (state.chapter > 0) Hud.ShowInfo($"{MissionTracker.ChapterLabel(ch.chapter)} // {Loc.T(ch.TitleKey)}", Loc.T(ch.DescKey), 12f);
            var tut = state.tutorial;
            if (tut == TutorialMode.None) return;
            // In the prologue the controls appear once the player has landed, not over the descent.
            if (state.chapter == 0 && FindAnyObjectByType<SessionBootstrap>() is SessionBootstrap boot) { boot.StartCoroutine(TutorialAfterLanding(tut)); return; }
            ShowControls(tut);
        }

        static System.Collections.IEnumerator TutorialAfterLanding(TutorialMode tut)
        {
            yield return null;
            while (Story.Cinematic.IsPlaying || !Story.Story.Flag("pro_landed")) yield return null;
            yield return new WaitForSeconds(2f);
            ShowControls(tut);
        }

        static void ShowControls(TutorialMode tut)
        {
            Hud.Toast(Loc.T("TUT_MOVE", GameInput.DisplayBinding(GameInput.Move), GameInput.DisplayBinding(GameInput.Run)));
            Hud.Toast(Loc.T("TUT_INTERACT", GameInput.DisplayBinding(GameInput.Interact), GameInput.DisplayBinding(GameInput.ToggleView), GameInput.DisplayBinding(GameInput.Missions)));
            if (tut == TutorialMode.Full)
                Hud.Toast(Loc.T("TUT_OXYGEN"));
        }
    }
}
