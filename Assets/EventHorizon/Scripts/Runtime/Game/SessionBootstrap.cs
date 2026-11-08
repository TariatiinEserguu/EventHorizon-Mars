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
                Hud.Toast($"Loaded  //  {state.saveName}");
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
            var ch = Campaign.Get(state.chapter);
            Hud.ShowInfo($"Chapter {ch.number:00} // {ch.title}", ch.summary, 12f);
            var tut = state.tutorial;
            if (tut == TutorialMode.None) return;
            Hud.Toast($"[{GameInput.DisplayBinding(GameInput.Move)}] move   [MOUSE] look   [{GameInput.DisplayBinding(GameInput.Run)}] run");
            Hud.Toast($"[{GameInput.DisplayBinding(GameInput.Interact)}] interact   [{GameInput.DisplayBinding(GameInput.ToggleView)}] camera   [ESC] pause");
            if (tut == TutorialMode.Full)
                Hud.Toast("Watch your suit oxygen (bottom left). Refill it at the oxygen tank next to the habitat.");
        }
    }
}
