using System;
using EventHorizon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Scrolling credits. Third-party attribution comes from ThirdPartyCredits.txt, which is generated from
    /// tools/asset_manifest.json (the same source as ASSET_CREDITS.md), so no creator can be left out.
    /// </summary>
    public class CreditsPanel
    {
        public VisualElement Element { get; }
        readonly VisualElement _scroll;
        float _offset;
        readonly IVisualElementScheduledItem _tick;

        public CreditsPanel(Action onBack)
        {
            Element = new VisualElement();
            Element.style.flexGrow = 1;
            Element.style.overflow = Overflow.Hidden;
            Element.AddToClassList("eh-panel");
            Element.AddToClassList("eh-panel--glass");

            var head = UiKit.Box(Element, "eh-row");
            UiKit.Text(head, Loc.T("MENU_CREDITS"), "eh-panel-title");
            UiKit.Box(head, "eh-spacer");
            UiKit.Small(head, Loc.T("BTN_BACK"), () => { UiAudio.Back(); onBack?.Invoke(); });

            var viewport = UiKit.Box(Element, "eh-grow");
            viewport.style.overflow = Overflow.Hidden;
            _scroll = UiKit.Box(viewport, "eh-col");
            _scroll.style.position = Position.Absolute;
            _scroll.style.left = 0;
            _scroll.style.right = 0;
            _scroll.style.alignItems = Align.Center;

            Section("EVENT HORIZON: MARS", null, big: true);
            Line(Loc.T("CREDITS_HACKATHON"));
            Section(Loc.T("CREDITS_CREATED_BY"), "Tariatiin Eserguu");
            Section(Loc.T("CREDITS_PROGRAMMING"), "Tariatiin Eserguu");
            Section(Loc.T("CREDITS_DESIGN"), "Tariatiin Eserguu");
            Section(Loc.T("CREDITS_STORY"), "Tariatiin Eserguu");
            Section(Loc.T("CREDITS_TRANSLATION"), Loc.T("CREDITS_TRANSLATION_BODY"));
            Section(Loc.T("CREDITS_REFERENCES"), null);
            foreach (var r in References) Line(r);
            Section(Loc.T("CREDITS_SOUND"), Loc.T("CREDITS_SOUND_BODY"));
            Section(Loc.T("CREDITS_FONTS"), null);
            Line("Rajdhani - Indian Type Foundry, SIL Open Font License 1.1");
            Line("Share Tech Mono - Carrois Type Design, SIL Open Font License 1.1");
            Line("Fira Sans Condensed, Fira Mono - The Mozilla Foundation and Telefonica S.A., SIL Open Font License 1.1");
            Section(Loc.T("CREDITS_ASSETS"), null);
            foreach (var line in GameCredits.Load().Split('\n'))
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("THIRD-PARTY")) Line(line.Trim());
            Section(Loc.T("CREDITS_THANKS"), Loc.T("CREDITS_THANKS_1"));
            Line(Loc.T("CREDITS_THANKS_2"));
            Line(Loc.T("CREDITS_INSPIRATION"));
            Section("", Loc.T("NEW_QUOTE"));

            _offset = 600f;
            _tick = Element.schedule.Execute(() =>
            {
                float speed = SettingsService.Current.accessibility.motionEffects ? 0.9f : 0.6f;
                _offset -= speed;
                float h = _scroll.resolvedStyle.height;
                if (!float.IsNaN(h) && h > 0 && _offset < -h) _offset = viewport.resolvedStyle.height;
                _scroll.style.top = _offset;
            }).Every(16);
        }

        static readonly string[] References =
        {
            "NASA Mars Fact Sheet and Planetary Fact Sheets (NSSDCA)",
            "NASA Baseline Values and Assumptions Document, NASA/TP-2015-218570",
            "Hecht et al. (2021), Mars Oxygen ISRU Experiment (MOXIE), Space Science Reviews",
            "Appelbaum & Flood (1990), Solar radiation on Mars, Solar Energy",
            "Landis (1996), Dust obscuration of Mars solar arrays (Mars Pathfinder)",
            "Rodriguez-Manfredi et al. (2023), MEDA weather at Jezero crater",
            "Schwarzschild (1916); Misner, Thorne & Wheeler, Gravitation",
            "Bardeen, Press & Teukolsky (1972), Rotating black holes: ISCO and photon orbits",
            "Kerr (1963); Penrose (1969), energy extraction from rotating black holes",
            "Scholtz & Unwin (2020), What if Planet 9 is a primordial black hole?",
            "Paczynski (1986), gravitational microlensing; Turyshev & Toth (2020), solar gravitational lens",
            "Eddington, Dyson & Davidson (1920), deflection of starlight",
        };

        void Section(string title, string body, bool big = false)
        {
            var t = UiKit.Text(_scroll, title, big ? "eh-logo-title" : "eh-kicker");
            t.style.marginTop = big ? 0 : 40;
            t.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (big) t.style.fontSize = 64;
            if (body != null) Line(body);
        }

        void Line(string text)
        {
            var l = UiKit.Text(_scroll, text, "eh-body");
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.maxWidth = 1000;
        }
    }
}
