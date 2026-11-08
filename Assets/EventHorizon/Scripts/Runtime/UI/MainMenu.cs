using System;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// The MainMenu scene's interface: logo, nine working buttons, and the New Game, Load, Chapter Select,
    /// Free Exploration, Science Laboratory, Settings and Credits screens. The 3D background is in the scene.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        UIDocument _doc;
        VisualElement _root, _home, _page, _status;
        Label _clock, _uplink;
        Button _continue;

        void Start()
        {
            GameInput.ClearBlocks();
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _doc = UiRoot.Create("Main Menu", 0);
            _root = _doc.rootVisualElement;
            var shade = UiKit.Box(_root, "eh-shade-left");
            shade.pickingMode = PickingMode.Ignore;
            shade.style.backgroundImage = new StyleBackground(GradientTexture());
            shade.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            BuildHome();
            _page = UiKit.Box(_root, "eh-screen", "eh-screen--out");
            _page.style.paddingLeft = 90;
            _page.style.paddingRight = 90;
            _page.style.paddingTop = 70;
            _page.style.paddingBottom = 70;
            _page.style.display = DisplayStyle.None;
            BuildStatus();
        }

        // ------------------------------------------------------------------ home

        void BuildHome()
        {
            _home = UiKit.Box(_root, "eh-screen");
            _home.style.paddingLeft = 110;
            _home.style.paddingTop = 90;

            UiKit.Text(_home, "MARS COLONY NETWORK // YEAR 2189", "eh-kicker");
            var logo = UiKit.Box(_home, "eh-col");
            logo.style.marginTop = 10;
            UiKit.Text(logo, "EVENT HORIZON", "eh-logo-title");
            UiKit.Text(logo, "MARS", "eh-logo-mars");
            UiKit.Text(_home, "BUILD. SURVIVE. RESEARCH. EXPLORE.", "eh-logo-sub");

            var buttons = UiKit.Box(_home, "eh-col");
            buttons.style.marginTop = 46;
            buttons.style.width = 420;
            UiKit.Button(buttons, "NEW GAME", () => Open(BuildNewGame()));
            _continue = UiKit.Button(buttons, "CONTINUE", ContinueLatest);
            UiKit.Button(buttons, "LOAD GAME", () => Open(new SaveLoadPanel(_root, false, Home).Element));
            UiKit.Button(buttons, "CHAPTER SELECT", () => Open(BuildChapters()));
            UiKit.Button(buttons, "FREE EXPLORATION", () => Open(BuildSandbox()));
            UiKit.Button(buttons, "SCIENCE LABORATORY", () => Open(new SciencePanel(Home, allowLaunch: true).Element));
            UiKit.Button(buttons, "SETTINGS", () => Open(new SettingsPanel(_root, Home).Element));
            UiKit.Button(buttons, "CREDITS", () => Open(new CreditsPanel(Home).Element));
            UiKit.Button(buttons, "EXIT GAME", () => UiKit.Confirm(_root, "EXIT GAME", "Close EVENT HORIZON: MARS?", "EXIT", Quit));

            var latest = SaveSystem.MostRecent();
            _continue.SetEnabled(latest != null);
            _continue.tooltip = latest != null ? $"Continue \"{latest.data.saveName}\"" : "No saved games yet";
        }

        void BuildStatus()
        {
            _status = UiKit.Box(_root, "eh-panel", "eh-panel--glass");
            _status.style.position = Position.Absolute;
            _status.style.right = 40;
            _status.style.bottom = 36;
            _status.style.width = 380;
            _status.pickingMode = PickingMode.Ignore;
            UiKit.Text(_status, "ARES-1 COLONY NETWORK", "eh-kicker");
            UiKit.Text(_status, "YEAR 2189", "eh-card-title");
            var row = UiKit.Box(_status, "eh-row");
            UiKit.Text(row, "SYSTEM STATUS:", "eh-mono", "eh-small");
            UiKit.Text(row, " OPERATIONAL", "eh-mono", "eh-small", "eh-ok");
            _uplink = UiKit.Text(_status, "EARTH UPLINK  //  ONLINE", "eh-mono", "eh-small", "eh-dim", "eh-blink");
            _clock = UiKit.Text(_status, "", "eh-mono", "eh-small", "eh-dim");
            UiKit.Text(_status, "JEZERO CRATER  18.38 N  77.58 E", "eh-mono", "eh-small", "eh-dim");
            _status.schedule.Execute(() =>
            {
                if (SettingsService.Current.accessibility.motionEffects) _uplink.ToggleInClassList("eh-blink--off");
                // Mars Sol Date (Allison & McEwen): continuous sol count since 29 Dec 1873.
                double jdTT = DateTime.UtcNow.Subtract(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays + 2451545.0 + 69.184 / 86400.0;
                double msd = (jdTT - 2405522.0028779) / 1.0274912517;
                double mtc = (msd % 1.0) * 24.0;
                _clock.text = $"MSD {Math.Floor(msd):0}  //  MTC {(int)mtc:00}:{(int)((mtc % 1) * 60):00}";
            }).Every(800);
        }

        /// <summary>Dark-to-clear horizontal gradient behind the menu, so the planet isn't cut by a hard edge.</summary>
        static Texture2D GradientTexture()
        {
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
            {
                float t = x / 255f;
                float a = Mathf.Lerp(0.82f, 0f, Mathf.SmoothStep(0.35f, 1f, t));
                tex.SetPixel(x, 0, new Color(0.012f, 0.035f, 0.08f, a));
            }
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ navigation

        void Open(VisualElement content)
        {
            _page.Clear();
            _page.Add(content);
            _home.AddToClassList("eh-screen--out");
            _home.schedule.Execute(() => { if (_home.ClassListContains("eh-screen--out")) _home.style.display = DisplayStyle.None; }).StartingIn(280);
            _page.style.display = DisplayStyle.Flex;
            _page.schedule.Execute(() => _page.RemoveFromClassList("eh-screen--out")).StartingIn(16);
            _status.style.display = DisplayStyle.None;
        }

        void Home()
        {
            _page.AddToClassList("eh-screen--out");
            _page.schedule.Execute(() => { _page.style.display = DisplayStyle.None; _page.Clear(); }).StartingIn(280);
            _home.style.display = DisplayStyle.Flex;
            _home.schedule.Execute(() => _home.RemoveFromClassList("eh-screen--out")).StartingIn(16);
            _status.style.display = DisplayStyle.Flex;
            var latest = SaveSystem.MostRecent();
            _continue.SetEnabled(latest != null);
        }

        /// <summary>Opens a screen by its button label (used by the screenshot tour).</summary>
        public void OpenForCapture(string screen)
        {
            switch (screen)
            {
                case "NEW GAME": Open(BuildNewGame()); break;
                case "CHAPTER SELECT": Open(BuildChapters()); break;
                case "SCIENCE LABORATORY": Open(new SciencePanel(Home, allowLaunch: true).Element); break;
                case "SETTINGS": Open(new SettingsPanel(_root, Home).Element); break;
                case "LOAD GAME": Open(new SaveLoadPanel(_root, false, Home).Element); break;
                case "CREDITS": Open(new CreditsPanel(Home).Element); break;
                default: Home(); break;
            }
        }

        void ContinueLatest()
        {
            var latest = SaveSystem.MostRecent();
            if (latest == null) { UiKit.Message(_root, "CONTINUE", "There are no saved games yet. Start a NEW GAME first."); return; }
            GameSession.LoadSave(latest.data, latest.slot);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static VisualElement Page(string title, Action back, out VisualElement body)
        {
            var p = new VisualElement();
            p.AddToClassList("eh-panel");
            p.style.flexGrow = 1;
            var head = UiKit.Box(p, "eh-row");
            UiKit.Text(head, title, "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, "BACK", () => { UiAudio.Back(); back(); });
            body = UiKit.Scroll(p);
            body.style.flexGrow = 1;
            return p;
        }

        // ------------------------------------------------------------------ new game

        VisualElement BuildNewGame()
        {
            var page = Page("NEW GAME", Home, out var body);
            var data = new SaveData
            {
                difficulty = SettingsService.Current.gameplay.difficulty,
                tutorial = SettingsService.Current.gameplay.tutorial,
            };

            var intro = UiKit.Box(body, "eh-col");
            UiKit.Text(intro, "ARES-1 COLONY", "eh-card-title");
            UiKit.Text(intro, "MARS // YEAR 2189", "eh-kicker");
            var status = UiKit.Box(intro, "eh-row");
            UiKit.Text(status, "MISSION STATUS: ", "eh-mono", "eh-small");
            UiKit.Text(status, "INITIALIZATION", "eh-mono", "eh-small", "eh-warn");
            var quote = UiKit.Text(intro, "\"Humanity has reached another world. Now it must learn to survive.\"", "eh-body");
            quote.style.unityFontStyleAndWeight = FontStyle.Italic;
            quote.style.marginTop = 8;
            UiKit.Divider(body);

            UiKit.Text(body, "ASTRONAUT", "eh-kicker");
            UiKit.Field(body, "Name", data.characterName, v => data.characterName = v, 28);
            UiKit.Field(body, "Callsign", data.callsign, v => data.callsign = v.ToUpperInvariant(), 16);
            string[] suitNames = { "WHITE", "ORANGE", "BLUE", "GREY" };
            string[] suitHex = { "#E8E8E2", "#E0742E", "#3F7FBF", "#8C9094" };
            UiKit.Choice(body, "Suit color", suitNames, 0, i => data.suitColor = suitHex[i]);
            UiKit.Text(body, "More appearance options arrive with the realistic astronaut model.", "eh-dim", "eh-small");
            UiKit.Divider(body);

            UiKit.Text(body, "DIFFICULTY", "eh-kicker");
            var cards = UiKit.Box(body, "eh-row");
            cards.style.flexWrap = Wrap.Wrap;
            var cardMap = new System.Collections.Generic.Dictionary<Difficulty, VisualElement>();
            (Difficulty d, string title, string desc)[] diffs =
            {
                (Difficulty.Casual, "CASUAL", "More generous supplies, slower consumption, helpful tutorials."),
                (Difficulty.Standard, "STANDARD", "Balanced survival with normal resource consumption."),
                (Difficulty.Scientist, "SCIENTIST", "Detailed scientific parameters and tighter resource margins."),
                (Difficulty.Engineer, "ENGINEER", "Faster equipment wear, lean stocks, complex maintenance."),
            };
            foreach (var (d, title, desc) in diffs)
            {
                var diff = d;
                var card = UiKit.Box(cards, "eh-card");
                card.style.width = 300;
                UiKit.Text(card, title, "eh-card-title");
                UiKit.Text(card, desc, "eh-body", "eh-small");
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    UiAudio.Click();
                    data.difficulty = diff;
                    foreach (var kv in cardMap) kv.Value.EnableInClassList("eh-card--selected", kv.Key == diff);
                });
                card.RegisterCallback<PointerEnterEvent>(_ => UiAudio.Hover());
                cardMap[diff] = card;
                card.EnableInClassList("eh-card--selected", diff == data.difficulty);
            }
            UiKit.Text(body, "Non-critical options (tutorials, subtitles, simulation speed) can be changed later in Settings.", "eh-dim", "eh-small");
            UiKit.Divider(body);

            UiKit.Text(body, "TUTORIAL", "eh-kicker");
            UiKit.Choice(body, "Tutorial", new[] { "FULL TUTORIAL", "BASIC TUTORIAL", "NO TUTORIAL" }, (int)data.tutorial, i => data.tutorial = (TutorialMode)i);

            var start = UiKit.Box(page, "eh-row");
            start.style.justifyContent = Justify.FlexEnd;
            start.style.marginTop = 12;
            UiKit.Small(start, "BEGIN MISSION", () =>
            {
                if (string.IsNullOrWhiteSpace(data.characterName)) data.characterName = "Alex Rivera";
                if (string.IsNullOrWhiteSpace(data.callsign)) data.callsign = "PATHFINDER";
                data.saveName = $"{data.callsign} - Arrival";
                UiAudio.Confirm();
                GameSession.StartCampaign(data, 1);
            }, "eh-btn-primary");
            return page;
        }

        // ------------------------------------------------------------------ chapters

        VisualElement BuildChapters()
        {
            var page = Page("CHAPTER SELECT", Home, out var body);
            var layout = UiKit.Box(body, "eh-row");
            layout.style.alignItems = Align.FlexStart;
            var grid = UiKit.Box(layout, "eh-row");
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = Length.Percent(64);
            var detail = UiKit.Box(layout, "eh-panel", "eh-grow");
            detail.style.marginLeft = 12;
            var profile = Profile.Current;

            void ShowDetail(Chapter ch)
            {
                detail.Clear();
                UiKit.Text(detail, $"CHAPTER {ch.number:00}", "eh-kicker");
                UiKit.Text(detail, ch.title, "eh-panel-title");
                UiKit.Text(detail, ch.summary, "eh-body");
                if (ch.objectives.Length > 0)
                {
                    UiKit.Text(detail, "OBJECTIVES", "eh-kicker").style.marginTop = 12;
                    foreach (var o in ch.objectives) UiKit.Text(detail, "- " + o.text, "eh-body", "eh-small");
                }
                UiKit.Text(detail, "DISCOVERY", "eh-kicker").style.marginTop = 12;
                bool done = profile.IsCompleted(ch.number);
                UiKit.Text(detail, done ? ch.discovery : "??? (complete the chapter to log this discovery)", "eh-body", "eh-small");
                if (profile.IsUnlocked(ch.number))
                {
                    var b = UiKit.Small(detail, "START CHAPTER", () =>
                    {
                        var latest = SaveSystem.MostRecent();
                        var who = latest != null ? latest.data : new SaveData();
                        var data = new SaveData
                        {
                            characterName = who.characterName, callsign = who.callsign, suitColor = who.suitColor,
                            difficulty = who.difficulty, tutorial = TutorialMode.Basic, saveName = $"{who.callsign} - {ch.title}",
                        };
                        GameSession.StartCampaign(data, ch.number);
                    }, "eh-btn-primary");
                    b.style.marginTop = 16;
                }
                else
                {
                    UiKit.Text(detail, ch.inDevelopment ? "IN DEVELOPMENT" : "LOCKED: complete the previous chapter to unlock.", "eh-warn").style.marginTop = 16;
                }
            }

            foreach (var ch in Campaign.Chapters)
            {
                var chapter = ch;
                bool unlocked = profile.IsUnlocked(ch.number);
                var card = UiKit.Box(grid, "eh-card");
                card.style.width = 236;
                if (!unlocked) card.AddToClassList("eh-card--locked");
                var art = UiKit.Box(card, "eh-chapter-art");
                var tex = UiResources.Instance != null ? UiResources.Instance.Art(ch.artwork) : null;
                if (tex != null)
                {
                    art.style.backgroundImage = new StyleBackground(tex);
                    art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    art.style.unityBackgroundImageTintColor = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.35f);
                }
                UiKit.Text(card, $"{ch.number:00}", "eh-kicker");
                UiKit.Text(card, ch.title, "eh-card-title").style.fontSize = 20;
                var badge = UiKit.Text(card,
                    profile.IsCompleted(ch.number) ? "COMPLETE" : unlocked ? "AVAILABLE" : ch.inDevelopment ? "IN DEVELOPMENT" : "LOCKED",
                    "eh-badge");
                badge.AddToClassList(profile.IsCompleted(ch.number) ? "eh-badge--done" : unlocked ? "eh-badge" : "eh-badge--locked");
                card.RegisterCallback<ClickEvent>(_ => { UiAudio.Click(); ShowDetail(chapter); });
                card.RegisterCallback<PointerEnterEvent>(_ => UiAudio.Hover());
            }
            ShowDetail(Campaign.Get(Mathf.Clamp(profile.highestUnlocked, 1, 5)));
            return page;
        }

        // ------------------------------------------------------------------ sandbox

        VisualElement BuildSandbox()
        {
            var page = Page("FREE EXPLORATION", Home, out var body);
            UiKit.Text(body, "Explore Mars without the story. Run the colony, experiment with resources and visit the black-hole lab. Sandbox progress is saved separately from campaign saves.", "eh-body");
            UiKit.Divider(body);
            var s = new SandboxSettings();
            float[] speeds = { 30f, 60f, 120f, 300f, 600f };
            UiKit.Choice(body, "Simulation speed", new[] { "x30", "x60", "x120", "x300", "x600" }, 2, i => s.simulationSpeed = speeds[i]);
            UiKit.Slider(body, "Starting resources", 0.25f, 3f, s.startingResources, v => s.startingResources = v, v => $"{v * 100f:0}%");
            UiKit.Toggle(body, "Infinite suit oxygen", s.infiniteSuitOxygen, v => s.infiniteSuitOxygen = v);
            UiKit.Toggle(body, "Dust storms", s.dustStorms, v => s.dustStorms = v);
            UiKit.Slider(body, "Colony crew", 1f, 12f, s.crew, v => s.crew = Mathf.RoundToInt(v), v => $"{Mathf.RoundToInt(v)}");

            var existing = SaveSystem.Read(SaveSystem.SandboxSlot);
            var row = UiKit.Box(page, "eh-row");
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 12;
            var reset = UiKit.Small(row, "RESET SANDBOX", () => UiKit.Confirm(_root, "RESET SANDBOX",
                "Delete the Free Exploration save? Campaign saves are not affected.", "RESET", () =>
                {
                    SaveSystem.Delete(SaveSystem.SandboxSlot);
                    Open(BuildSandbox());
                }, danger: true), "eh-btn-danger");
            reset.SetEnabled(existing.Exists);
            UiKit.Small(row, "BLACK HOLE LAB", () => GameSession.StartScience(Scenes.SpaceLab));
            if (existing.Exists)
                UiKit.Small(row, "CONTINUE SANDBOX", () => GameSession.StartSandbox(existing.data.sandbox ?? s, existing.data));
            UiKit.Small(row, existing.Exists ? "START FRESH" : "START", () => GameSession.StartSandbox(s), "eh-btn-primary");
            return page;
        }
    }
}
