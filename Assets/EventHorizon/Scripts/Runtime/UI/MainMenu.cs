using System;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// The MainMenu scene's interface: logo, nine working buttons, a language switch, and the New Game, Load,
    /// Chapter Select, Free Exploration, Science Laboratory, Settings and Credits screens. On first start it asks for
    /// a language. Changing language rebuilds the menu in place, keeping the open screen.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        UIDocument _doc;
        VisualElement _root, _home, _page, _status;
        Label _clock, _uplink;
        Button _continue;
        string _openScreen;

        void Start()
        {
            if (StoryTest.RequestedPath != null) { StoryTest.RunAndQuit(); return; }
            GameInput.ClearBlocks();
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _doc = UiRoot.Create("Main Menu", 0);
            _root = _doc.rootVisualElement;
            Build();
            Loc.Changed += OnLanguage;
            if (!SettingsService.Current.language.chosen && ScreenshotDirector.Folder == null)
                LanguagePicker.FirstStart(_root, null);
        }

        void OnDestroy() => Loc.Changed -= OnLanguage;

        void OnLanguage()
        {
            string screen = _openScreen;
            _root.Clear();
            Build();
            if (screen != null) OpenForCapture(screen);
        }

        void Build()
        {
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
            LanguagePicker.Chip(_root);
        }

        // ------------------------------------------------------------------ home

        void BuildHome()
        {
            _home = UiKit.Box(_root, "eh-screen");
            _home.style.paddingLeft = 110;
            _home.style.paddingTop = 90;

            UiKit.Text(_home, Loc.T("MENU_KICKER"), "eh-kicker");
            var logo = UiKit.Box(_home, "eh-col");
            logo.style.marginTop = 10;
            UiKit.Text(logo, "EVENT HORIZON", "eh-logo-title");
            UiKit.Text(logo, Loc.T("MENU_LOGO_MARS"), "eh-logo-mars");
            if (Loc.Current != Loc.English) UiKit.Text(logo, Loc.T("MENU_LOGO_LOCAL"), "eh-logo-sub");
            UiKit.Text(_home, Loc.T("MENU_TAGLINE"), "eh-logo-sub");

            var buttons = UiKit.Box(_home, "eh-col");
            buttons.style.marginTop = 40;
            buttons.style.width = 460;
            UiKit.Button(buttons, Loc.T("MENU_NEW_GAME"), () => Open("NEW GAME"));
            _continue = UiKit.Button(buttons, Loc.T("MENU_CONTINUE"), ContinueLatest);
            UiKit.Button(buttons, Loc.T("MENU_LOAD_GAME"), () => Open("LOAD GAME"));
            UiKit.Button(buttons, Loc.T("MENU_CHAPTER_SELECT"), () => Open("CHAPTER SELECT"));
            UiKit.Button(buttons, Loc.T("MENU_FREE_EXPLORATION"), () => Open("FREE EXPLORATION"));
            UiKit.Button(buttons, Loc.T("MENU_SCIENCE_LAB"), () => Open("SCIENCE LABORATORY"));
            UiKit.Button(buttons, Loc.T("MENU_SETTINGS"), () => Open("SETTINGS"));
            UiKit.Button(buttons, Loc.T("MENU_CREDITS"), () => Open("CREDITS"));
            UiKit.Button(buttons, Loc.T("MENU_EXIT"), () => UiKit.Confirm(_root, Loc.T("MENU_EXIT"), Loc.T("CONFIRM_EXIT"), Loc.T("BTN_EXIT"), Quit));

            var latest = SaveSystem.MostRecent();
            _continue.SetEnabled(latest != null);
            _continue.tooltip = latest != null ? Loc.T("MENU_CONTINUE_TIP", latest.data.saveName) : Loc.T("MENU_NO_SAVES");
        }

        void BuildStatus()
        {
            _status = UiKit.Box(_root, "eh-panel", "eh-panel--glass");
            _status.style.position = Position.Absolute;
            _status.style.right = 40;
            _status.style.bottom = 36;
            _status.style.width = 400;
            _status.pickingMode = PickingMode.Ignore;
            UiKit.Text(_status, Loc.T("STATUS_NETWORK"), "eh-kicker");
            UiKit.Text(_status, Loc.T("STATUS_YEAR"), "eh-card-title");
            var row = UiKit.Box(_status, "eh-row");
            UiKit.Text(row, Loc.T("STATUS_SYSTEM"), "eh-mono", "eh-small");
            UiKit.Text(row, " " + Loc.T("STATUS_OPERATIONAL"), "eh-mono", "eh-small", "eh-ok");
            _uplink = UiKit.Text(_status, Loc.T("STATUS_UPLINK"), "eh-mono", "eh-small", "eh-dim", "eh-blink");
            _clock = UiKit.Text(_status, "", "eh-mono", "eh-small", "eh-dim");
            UiKit.Text(_status, Loc.T("STATUS_SITE"), "eh-mono", "eh-small", "eh-dim");
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

        void Open(string screen)
        {
            _openScreen = screen;
            var content = screen switch
            {
                "NEW GAME" => BuildNewGame(),
                "LOAD GAME" => new SaveLoadPanel(_root, false, Home).Element,
                "CHAPTER SELECT" => BuildChapters(),
                "FREE EXPLORATION" => BuildSandbox(),
                "SCIENCE LABORATORY" => new SciencePanel(Home, allowLaunch: true).Element,
                "SETTINGS" => new SettingsPanel(_root, Home).Element,
                _ => new CreditsPanel(Home).Element,
            };
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
            _openScreen = null;
            _page.AddToClassList("eh-screen--out");
            _page.schedule.Execute(() => { _page.style.display = DisplayStyle.None; _page.Clear(); }).StartingIn(280);
            _home.style.display = DisplayStyle.Flex;
            _home.schedule.Execute(() => _home.RemoveFromClassList("eh-screen--out")).StartingIn(16);
            _status.style.display = DisplayStyle.Flex;
            var latest = SaveSystem.MostRecent();
            _continue.SetEnabled(latest != null);
        }

        /// <summary>Opens a screen by its internal name (used by the screenshot tour and language changes).</summary>
        public void OpenForCapture(string screen)
        {
            if (string.IsNullOrEmpty(screen) || screen == "HOME") Home();
            else Open(screen);
        }

        void ContinueLatest()
        {
            var latest = SaveSystem.MostRecent();
            if (latest == null) { UiKit.Message(_root, Loc.T("MENU_CONTINUE"), Loc.T("MENU_NO_SAVES_LONG")); return; }
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
            UiKit.Small(head, Loc.T("BTN_BACK"), () => { UiAudio.Back(); back(); });
            body = UiKit.Scroll(p);
            body.style.flexGrow = 1;
            return p;
        }

        // ------------------------------------------------------------------ new game

        VisualElement BuildNewGame()
        {
            var page = Page(Loc.T("MENU_NEW_GAME"), Home, out var body);
            var data = new SaveData
            {
                difficulty = SettingsService.Current.gameplay.difficulty,
                tutorial = SettingsService.Current.gameplay.tutorial,
            };

            var intro = UiKit.Box(body, "eh-col");
            UiKit.Text(intro, Loc.T("NEW_COLONY"), "eh-card-title");
            UiKit.Text(intro, Loc.T("NEW_KICKER"), "eh-kicker");
            var status = UiKit.Box(intro, "eh-row");
            UiKit.Text(status, Loc.T("NEW_STATUS"), "eh-mono", "eh-small");
            UiKit.Text(status, " " + Loc.T("NEW_STATUS_VALUE"), "eh-mono", "eh-small", "eh-warn");
            var quote = UiKit.Text(intro, Loc.T("NEW_QUOTE"), "eh-body");
            quote.style.unityFontStyleAndWeight = FontStyle.Italic;
            quote.style.marginTop = 8;
            UiKit.Text(intro, Loc.T("NEW_PREMISE"), "eh-body", "eh-small").style.marginTop = 6;
            UiKit.Divider(body);

            UiKit.Text(body, Loc.T("NEW_ASTRONAUT"), "eh-kicker");
            UiKit.Field(body, Loc.T("NEW_NAME"), data.characterName, v => data.characterName = v, 28);
            UiKit.Field(body, Loc.T("NEW_CALLSIGN"), data.callsign, v => data.callsign = v.ToUpperInvariant(), 16);
            string[] suitKeys = { "SUIT_WHITE", "SUIT_ORANGE", "SUIT_BLUE", "SUIT_GREY" };
            string[] suitHex = { "#E8E8E2", "#E0742E", "#3F7FBF", "#8C9094" };
            UiKit.Choice(body, Loc.T("NEW_SUIT"), Array.ConvertAll(suitKeys, Loc.T), 0, i => data.suitColor = suitHex[i]);
            UiKit.Divider(body);

            UiKit.Text(body, Loc.T("NEW_DIFFICULTY"), "eh-kicker");
            var cards = UiKit.Box(body, "eh-row");
            cards.style.flexWrap = Wrap.Wrap;
            var cardMap = new System.Collections.Generic.Dictionary<Difficulty, VisualElement>();
            foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            {
                var diff = d;
                string key = d.ToString().ToUpperInvariant();
                var card = UiKit.Box(cards, "eh-card");
                card.style.width = 300;
                UiKit.Text(card, Loc.T("ENUM_" + key), "eh-card-title");
                UiKit.Text(card, Loc.T("DIFF_" + key + "_DESC"), "eh-body", "eh-small");
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
            UiKit.Text(body, Loc.T("NEW_LATER_HINT"), "eh-dim", "eh-small");
            UiKit.Divider(body);

            UiKit.Text(body, Loc.T("NEW_TUTORIAL"), "eh-kicker");
            UiKit.Choice(body, Loc.T("NEW_TUTORIAL"), new[] { Loc.T("ENUM_FULL"), Loc.T("ENUM_BASIC"), Loc.T("ENUM_NONE") }, (int)data.tutorial, i => data.tutorial = (TutorialMode)i);

            var start = UiKit.Box(page, "eh-row");
            start.style.justifyContent = Justify.FlexEnd;
            start.style.marginTop = 12;
            UiKit.Small(start, Loc.T("NEW_BEGIN"), () =>
            {
                if (string.IsNullOrWhiteSpace(data.characterName)) data.characterName = "Alex Rivera";
                if (string.IsNullOrWhiteSpace(data.callsign)) data.callsign = "PATHFINDER";
                data.saveName = $"{data.callsign} - {Loc.T(Story.Story.MainQuest(0).TitleKey)}";
                UiAudio.Confirm();
                GameSession.StartCampaign(data, 0);
            }, "eh-btn-primary");
            return page;
        }

        // ------------------------------------------------------------------ chapters

        VisualElement BuildChapters()
        {
            var page = Page(Loc.T("MENU_CHAPTER_SELECT"), Home, out var body);
            var layout = UiKit.Box(body, "eh-row");
            layout.style.alignItems = Align.FlexStart;
            var grid = UiKit.Box(layout, "eh-row");
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = Length.Percent(64);
            var detail = UiKit.Box(layout, "eh-panel", "eh-grow");
            detail.style.marginLeft = 12;
            var profile = Profile.Current;

            void ShowDetail(QuestDef ch)
            {
                detail.Clear();
                UiKit.Text(detail, MissionTracker.ChapterLabel(ch.chapter), "eh-kicker");
                UiKit.Text(detail, Loc.T(ch.TitleKey), "eh-panel-title").style.whiteSpace = WhiteSpace.Normal;
                UiKit.Text(detail, Loc.T(ch.DescKey), "eh-body");
                UiKit.Text(detail, Loc.T("JOURNAL_ACTIVE_OBJECTIVES"), "eh-kicker").style.marginTop = 12;
                foreach (var o in ch.objectives)
                    if (o.visible == null) UiKit.Text(detail, (o.optional ? "o " : "- ") + Loc.T(o.Key), "eh-body", "eh-small");
                UiKit.Text(detail, Loc.T("DEBRIEF_SCIENCE"), "eh-kicker").style.marginTop = 12;
                bool done = profile.IsCompleted(ch.chapter);
                UiKit.Text(detail, done ? Loc.T(ch.LearnKey) : Loc.T("CHAPTER_LEARN_LOCKED"), "eh-body", "eh-small");
                if (profile.IsUnlocked(ch.chapter))
                {
                    var b = UiKit.Small(detail, Loc.T("CHAPTER_START"), () =>
                    {
                        var latest = SaveSystem.MostRecent();
                        var who = latest != null ? latest.data : new SaveData();
                        var data = new SaveData
                        {
                            characterName = who.characterName, callsign = who.callsign, suitColor = who.suitColor,
                            difficulty = who.difficulty, tutorial = TutorialMode.Basic, saveName = $"{who.callsign} - {Loc.T(ch.TitleKey)}",
                        };
                        GameSession.StartCampaign(data, ch.chapter);
                    }, "eh-btn-primary");
                    b.style.marginTop = 16;
                    if (ch.chapter > 0) UiKit.Text(detail, Loc.T("CHAPTER_DEFAULTS_HINT"), "eh-small", "eh-dim").style.marginTop = 6;
                }
                else UiKit.Text(detail, Loc.T("CHAPTER_LOCKED_LONG"), "eh-warn").style.marginTop = 16;
            }

            foreach (var ch in StoryContent.MainQuests)
            {
                var chapter = ch;
                bool unlocked = profile.IsUnlocked(ch.chapter);
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
                UiKit.Text(card, MissionTracker.ChapterLabel(ch.chapter), "eh-kicker");
                var title = UiKit.Text(card, Loc.T(ch.TitleKey), "eh-card-title");
                title.style.fontSize = 19;
                title.style.whiteSpace = WhiteSpace.Normal;
                bool complete = profile.IsCompleted(ch.chapter);
                var badge = UiKit.Text(card, Loc.T(complete ? "BADGE_COMPLETE" : unlocked ? "BADGE_AVAILABLE" : "BADGE_LOCKED"), "eh-badge");
                badge.AddToClassList(complete ? "eh-badge--done" : unlocked ? "eh-badge" : "eh-badge--locked");
                card.RegisterCallback<ClickEvent>(_ => { UiAudio.Click(); ShowDetail(chapter); });
                card.RegisterCallback<PointerEnterEvent>(_ => UiAudio.Hover());
            }
            ShowDetail(Story.Story.MainQuest(Mathf.Clamp(profile.highestUnlocked, 0, StoryContent.MainQuests.Count - 1)));
            return page;
        }

        // ------------------------------------------------------------------ sandbox

        VisualElement BuildSandbox()
        {
            var page = Page(Loc.T("MENU_FREE_EXPLORATION"), Home, out var body);
            UiKit.Text(body, Loc.T("SANDBOX_INTRO"), "eh-body");
            UiKit.Divider(body);
            var s = new SandboxSettings();
            float[] speeds = { 30f, 60f, 120f, 300f, 600f };
            UiKit.Choice(body, Loc.T("SET_SIM_SPEED"), new[] { "x30", "x60", "x120", "x300", "x600" }, 2, i => s.simulationSpeed = speeds[i]);
            UiKit.Slider(body, Loc.T("SANDBOX_RESOURCES"), 0.25f, 3f, s.startingResources, v => s.startingResources = v, v => $"{v * 100f:0}%");
            UiKit.Toggle(body, Loc.T("SANDBOX_INFINITE_O2"), s.infiniteSuitOxygen, v => s.infiniteSuitOxygen = v);
            UiKit.Toggle(body, Loc.T("SANDBOX_STORMS"), s.dustStorms, v => s.dustStorms = v);
            UiKit.Slider(body, Loc.T("SANDBOX_CREW"), 1f, 12f, s.crew, v => s.crew = Mathf.RoundToInt(v), v => $"{Mathf.RoundToInt(v)}");

            var existing = SaveSystem.Read(SaveSystem.SandboxSlot);
            var row = UiKit.Box(page, "eh-row");
            row.style.justifyContent = Justify.FlexEnd;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 12;
            var reset = UiKit.Small(row, Loc.T("SANDBOX_RESET"), () => UiKit.Confirm(_root, Loc.T("SANDBOX_RESET"),
                Loc.T("SANDBOX_RESET_Q"), Loc.T("BTN_RESET"), () =>
                {
                    SaveSystem.Delete(SaveSystem.SandboxSlot);
                    Open("FREE EXPLORATION");
                }, danger: true), "eh-btn-danger");
            reset.SetEnabled(existing.Exists);
            UiKit.Small(row, Loc.T("SCENE_PLANETARIUM_SHORT"), () => GameSession.StartScience(Scenes.SpaceLab));
            UiKit.Small(row, Loc.T("SCENE_MARS_ORBIT"), () => GameSession.StartScience(Scenes.MarsOrbit));
            UiKit.Small(row, Loc.T("SCENE_BLACK_HOLE_LAB"), () => GameSession.StartScience(Scenes.BlackHoleLab));
            if (existing.Exists)
                UiKit.Small(row, Loc.T("SANDBOX_CONTINUE"), () => GameSession.StartSandbox(existing.data.sandbox ?? s, existing.data));
            UiKit.Small(row, Loc.T(existing.Exists ? "SANDBOX_FRESH" : "SANDBOX_START"), () => GameSession.StartSandbox(s), "eh-btn-primary");
            return page;
        }
    }
}
