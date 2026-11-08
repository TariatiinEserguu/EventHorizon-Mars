using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Esc during play: darkens the view, freezes simulation time (Time.timeScale = 0) and blocks gameplay input.
    /// Offers resume, save, load, objectives, inventory, science database, settings, main menu and exit, and asks
    /// before abandoning unsaved progress.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        UIDocument _doc;
        VisualElement _root, _layer, _menu, _page;
        public static bool IsPaused => GameSession.Paused;

        void Start()
        {
            _doc = UiRoot.Create("Pause", 40);
            _root = _doc.rootVisualElement;
            _layer = UiKit.Box(_root, "eh-fill", "eh-vignette");
            _layer.style.display = DisplayStyle.None;
            _layer.pickingMode = PickingMode.Position;

            var wrap = UiKit.Box(_layer, "eh-row", "eh-grow");
            wrap.style.alignItems = Align.Stretch;
            wrap.style.paddingLeft = 80;
            wrap.style.paddingTop = 80;
            wrap.style.paddingBottom = 80;
            wrap.style.paddingRight = 80;

            _menu = UiKit.Box(wrap, "eh-col");
            _menu.style.width = 380;
            UiKit.Text(_menu, "SYSTEM PAUSED", "eh-kicker");
            UiKit.Text(_menu, "ARES-1", "eh-logo-title").style.fontSize = 64;
            UiKit.Divider(_menu);
            UiKit.Button(_menu, "RESUME", Resume);
            UiKit.Button(_menu, "SAVE GAME", () =>
            {
                // Free Exploration has its own slot, so it never touches campaign saves.
                if (GameSession.Mode == GameMode.Sandbox)
                {
                    bool ok = SaveSystem.Save(SaveSystem.SandboxSlot, "Free Exploration");
                    Hud.Toast(ok ? "Sandbox saved" : "Save failed: see the log", !ok);
                }
                else ShowPage(new SaveLoadPanel(_root, true, ClosePage).Element);
            });
            UiKit.Button(_menu, "LOAD GAME", () => ShowPage(new SaveLoadPanel(_root, false, ClosePage).Element));
            UiKit.Button(_menu, "MISSION OBJECTIVES", () => ShowPage(BuildObjectives()));
            UiKit.Button(_menu, "INVENTORY", () => ShowPage(BuildInventory()));
            UiKit.Button(_menu, "SCIENTIFIC DATABASE", () => ShowPage(new SciencePanel(ClosePage, allowLaunch: false).Element));
            UiKit.Button(_menu, "SETTINGS", () => ShowPage(new SettingsPanel(_root, ClosePage).Element));
            UiKit.Button(_menu, "RETURN TO MAIN MENU", () => ConfirmLeave("RETURN TO MAIN MENU", GameSession.ReturnToMenu));
            UiKit.Button(_menu, "EXIT GAME", () => ConfirmLeave("EXIT GAME", Quit));

            _page = UiKit.Box(wrap, "eh-col", "eh-grow");
            _page.style.marginLeft = 40;
        }

        void Update()
        {
            if (!GameInput.Pause.WasPressedThisFrame() || TerminalScreen.IsOpen || Time.frameCount == TerminalScreen.LastClosedFrame) return;
            if (!GameSession.Paused) Pause();
            else if (_page.childCount > 0) ClosePage();
            else Resume();
        }

        public void Pause()
        {
            GameSession.Paused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            GameInput.Block(this);
            _layer.style.display = DisplayStyle.Flex;
            _menu.Q<Button>()?.Focus();
        }

        public void Resume()
        {
            ClosePage();
            GameSession.Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            _layer.style.display = DisplayStyle.None;
            GameInput.Unblock(this);
        }

        void ShowPage(VisualElement page)
        {
            _page.Clear();
            _page.Add(page);
        }

        void ClosePage() => _page.Clear();

        void ConfirmLeave(string title, System.Action action)
        {
            void Go()
            {
                AudioListener.pause = false;
                action();
            }
            if (GameSession.HasUnsavedProgress && GameSession.Mode != GameMode.Science)
                UiKit.Confirm(_root, title, "You have unsaved progress. Leave anyway?", "LEAVE", Go, danger: true);
            else
                UiKit.Confirm(_root, title, "Are you sure?", "CONFIRM", Go);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        VisualElement BuildObjectives()
        {
            var p = new VisualElement();
            p.AddToClassList("eh-panel");
            var head = UiKit.Box(p, "eh-row");
            UiKit.Text(head, "MISSION OBJECTIVES", "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, "BACK", ClosePage);
            UiKit.Text(p, MissionTracker.CurrentTitle, "eh-card-title");
            if (GameSession.Mode == GameMode.Campaign || GameSession.Mode == GameMode.None)
                UiKit.Text(p, Campaign.Get(GameSession.State.chapter).summary, "eh-body");
            UiKit.Divider(p);
            foreach (var (text, progress, done, _) in MissionTracker.CurrentObjectives())
            {
                var row = UiKit.Box(p, "eh-row");
                row.style.marginBottom = 6;
                var l = UiKit.Text(row, (done ? "[x]  " : "[ ]  ") + text, "eh-body");
                if (done) l.AddToClassList("eh-ok");
                l.style.flexGrow = 1;
                var bar = UiKit.Bar(row, out var fill);
                bar.style.width = 160;
                UiKit.SetFill(fill, done ? 1f : progress);
            }
            return p;
        }

        VisualElement BuildInventory()
        {
            var p = new VisualElement();
            p.AddToClassList("eh-panel");
            var head = UiKit.Box(p, "eh-row");
            UiKit.Text(head, "INVENTORY", "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, "BACK", ClosePage);
            var suit = FindAnyObjectByType<SuitLifeSupport>();
            UiKit.Text(p, "SUIT", "eh-kicker");
            if (suit != null)
            {
                UiKit.Text(p, $"Oxygen: {suit.oxygenKg:0.000} / {suit.oxygenCapacityKg:0.0} kg  ({suit.HoursLeft:0.0} h)", "eh-mono");
                UiKit.Text(p, $"Battery: {suit.energyWh:0} / {suit.energyCapacityWh:0} Wh", "eh-mono");
                UiKit.Text(p, $"Suit integrity: {suit.integrity * 100f:0}%", "eh-mono");
            }
            UiKit.Text(p, "Tools: ice drill, panel brush, multimeter, sample bags (12)", "eh-mono", "eh-dim");
            UiKit.Divider(p);
            UiKit.Text(p, "COLONY STORES", "eh-kicker");
            var grid = ColonyGrid.Instance;
            if (grid != null)
                foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
                    UiKit.Text(p, $"{t}: {grid.Amount(t):0.0} / {grid.Capacity(t):0} kg", "eh-mono");
            return p;
        }

        void OnDestroy()
        {
            if (GameSession.Paused) { Time.timeScale = 1f; AudioListener.pause = false; GameSession.Paused = false; }
            GameInput.Unblock(this);
        }
    }
}
