using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Esc during play: darkens the view, freezes simulation time (Time.timeScale = 0) and blocks gameplay input.
    /// Offers resume, save, load, the mission journal, inventory, science database, language, settings, main menu
    /// and exit, and asks before abandoning unsaved progress. J opens the journal directly.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        UIDocument _doc;
        VisualElement _root, _layer, _menu, _page;
        string _openPage;
        public static bool IsPaused => GameSession.Paused;

        void Start()
        {
            _doc = UiRoot.Create("Pause", 40);
            _root = _doc.rootVisualElement;
            Build();
            Loc.Changed += OnLanguage;
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguage;
            if (GameSession.Paused) { Time.timeScale = 1f; AudioListener.pause = false; GameSession.Paused = false; }
            GameInput.Unblock(this);
        }

        void OnLanguage()
        {
            bool visible = _layer.style.display == DisplayStyle.Flex;
            string page = _openPage;
            _root.Clear();
            Build();
            _layer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (page != null) Open(page);
        }

        void Build()
        {
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
            _menu.style.width = 400;
            UiKit.Text(_menu, Loc.T("PAUSE_KICKER"), "eh-kicker");
            UiKit.Text(_menu, "ARES-1", "eh-logo-title").style.fontSize = 64;
            UiKit.Divider(_menu);
            UiKit.Button(_menu, Loc.T("PAUSE_RESUME"), Resume);
            UiKit.Button(_menu, Loc.T("PAUSE_SAVE"), () =>
            {
                // Free Exploration has its own slot, so it never touches campaign saves.
                if (GameSession.Mode == GameMode.Sandbox)
                {
                    bool ok = SaveSystem.Save(SaveSystem.SandboxSlot, Loc.T("MODE_SANDBOX"));
                    Hud.Toast(Loc.T(ok ? "TOAST_SANDBOX_SAVED" : "TOAST_SAVE_FAILED"), !ok);
                }
                else Open("SAVE");
            });
            UiKit.Button(_menu, Loc.T("PAUSE_LOAD"), () => Open("LOAD"));
            UiKit.Button(_menu, Loc.T("PAUSE_JOURNAL"), () => Open("JOURNAL"));
            UiKit.Button(_menu, Loc.T("PAUSE_INVENTORY"), () => Open("INVENTORY"));
            UiKit.Button(_menu, Loc.T("PAUSE_DATABASE"), () => Open("DATABASE"));
            UiKit.Button(_menu, Loc.T("PAUSE_LANGUAGE"), () => Open("LANGUAGE"));
            UiKit.Button(_menu, Loc.T("PAUSE_SETTINGS"), () => Open("SETTINGS"));
            UiKit.Button(_menu, Loc.T("PAUSE_MAIN_MENU"), () => ConfirmLeave(Loc.T("PAUSE_MAIN_MENU"), GameSession.ReturnToMenu));
            UiKit.Button(_menu, Loc.T("PAUSE_EXIT"), () => ConfirmLeave(Loc.T("PAUSE_EXIT"), Quit));

            _page = UiKit.Box(wrap, "eh-col", "eh-grow");
            _page.style.marginLeft = 40;
        }

        /// <summary>Opens a page by name: SAVE, LOAD, JOURNAL, INVENTORY, DATABASE, LANGUAGE, SETTINGS.</summary>
        public void Open(string page)
        {
            _openPage = page;
            _page.Clear();
            VisualElement content = page switch
            {
                "SAVE" => new SaveLoadPanel(_root, true, ClosePage).Element,
                "LOAD" => new SaveLoadPanel(_root, false, ClosePage).Element,
                "JOURNAL" => new JournalPanel(ClosePage).Element,
                "INVENTORY" => BuildInventory(),
                "DATABASE" => new SciencePanel(ClosePage, allowLaunch: false).Element,
                "LANGUAGE" => BuildLanguage(),
                _ => new SettingsPanel(_root, ClosePage).Element,
            };
            _page.Add(content);
        }

        void Update()
        {
            if (Cinematic.IsPlaying || EndingSequence.IsPlaying) return;
            if (!GameSession.Paused && GameInput.Missions.WasPressedThisFrame() && !ModalState.Any && GameSession.InGameplay)
            {
                Pause();
                Open("JOURNAL");
                return;
            }
            if (!GameInput.Pause.WasPressedThisFrame() || TerminalScreen.IsOpen || Time.frameCount == TerminalScreen.LastClosedFrame || ModalState.OwnsEscape) return;
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
            GameInput.UpdateCursor();
        }

        public void Resume()
        {
            ClosePage();
            GameSession.Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            _layer.style.display = DisplayStyle.None;
            GameInput.Unblock(this);
            GameInput.UpdateCursor();
        }

        void ClosePage()
        {
            _openPage = null;
            _page.Clear();
        }

        void ConfirmLeave(string title, System.Action action)
        {
            void Go()
            {
                AudioListener.pause = false;
                action();
            }
            if (GameSession.HasUnsavedProgress && GameSession.Mode != GameMode.Science)
                UiKit.Confirm(_root, title, Loc.T("CONFIRM_UNSAVED"), Loc.T("BTN_LEAVE"), Go, danger: true);
            else
                UiKit.Confirm(_root, title, Loc.T("CONFIRM_SURE"), Loc.T("BTN_CONFIRM"), Go);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        VisualElement BuildLanguage()
        {
            var p = new VisualElement();
            p.AddToClassList("eh-panel");
            var head = UiKit.Box(p, "eh-row");
            UiKit.Text(head, Loc.T("PAUSE_LANGUAGE"), "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T("BTN_BACK"), ClosePage);
            UiKit.Text(p, Loc.T("LANG_INTERFACE"), "eh-kicker");
            LanguagePicker.Cards(p);
            UiKit.Text(p, Loc.T("LANG_PROGRESS_SAFE"), "eh-small", "eh-dim");
            UiKit.Text(p, Loc.T("LANG_MORE_IN_SETTINGS"), "eh-small", "eh-dim");
            return p;
        }

        VisualElement BuildInventory()
        {
            var p = new VisualElement();
            p.AddToClassList("eh-panel");
            var head = UiKit.Box(p, "eh-row");
            UiKit.Text(head, Loc.T("PAUSE_INVENTORY"), "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T("BTN_BACK"), ClosePage);
            var suit = FindAnyObjectByType<SuitLifeSupport>();
            UiKit.Text(p, Loc.T("INV_SUIT"), "eh-kicker");
            if (suit != null)
            {
                UiKit.Text(p, Loc.T("INV_OXYGEN", suit.oxygenKg.ToString("0.000"), suit.oxygenCapacityKg.ToString("0.0"), suit.HoursLeft.ToString("0.0")), "eh-mono");
                UiKit.Text(p, Loc.T("INV_BATTERY", suit.energyWh.ToString("0"), suit.energyCapacityWh.ToString("0")), "eh-mono");
                UiKit.Text(p, Loc.T("INV_INTEGRITY", (suit.integrity * 100f).ToString("0")), "eh-mono");
            }
            UiKit.Text(p, Loc.T("INV_TOOLS"), "eh-mono", "eh-dim");
            var items = Story.Story.State.items;
            foreach (var i in items) UiKit.Text(p, "+ " + Loc.T("ITEM_" + i), "eh-mono");
            UiKit.Divider(p);
            UiKit.Text(p, Loc.T("INV_STORES"), "eh-kicker");
            var grid = ColonyGrid.Instance;
            if (grid != null)
                foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
                    UiKit.Text(p, Loc.T("INV_RESOURCE", Loc.T("RES_" + t.ToString().ToUpperInvariant()), grid.Amount(t).ToString("0.0"), grid.Capacity(t).ToString("0")), "eh-mono");
            else UiKit.Text(p, Loc.T("INV_STORES_REMOTE"), "eh-dim");
            return p;
        }
    }
}
