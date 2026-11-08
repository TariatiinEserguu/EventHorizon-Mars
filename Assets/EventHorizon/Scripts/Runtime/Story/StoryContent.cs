using System;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Colony;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventHorizon.Story
{
    /// <summary>
    /// The campaign's content: quests (StoryContent.Quests.cs, StoryContent.Side.cs), conversations
    /// (StoryContent.Dialogues.cs) and experiments (StoryContent.Experiments.cs). Structure lives in code because
    /// objectives test the live world; every visible word lives in the localization tables.
    /// This file holds the shared helpers and the glue to the world: sites, terminals, colonists and scenes.
    /// </summary>
    public static partial class StoryContent
    {
        static Dictionary<string, QuestDef> s_quests;
        static List<QuestDef> s_main;
        static Dictionary<string, DialogueDef> s_dialogues;
        static Dictionary<string, ExperimentDef> s_experiments;

        public static Dictionary<string, QuestDef> Quests { get { Init(); return s_quests; } }
        public static List<QuestDef> MainQuests { get { Init(); return s_main; } }
        public static Dictionary<string, DialogueDef> Dialogues { get { Init(); return s_dialogues; } }
        public static Dictionary<string, ExperimentDef> Experiments { get { Init(); return s_experiments; } }

        static StoryState S => Story.State;

        static void Init()
        {
            if (s_quests != null) return;
            s_quests = new Dictionary<string, QuestDef>();
            s_main = new List<QuestDef>();
            s_dialogues = new Dictionary<string, DialogueDef>();
            s_experiments = new Dictionary<string, ExperimentDef>();
            BuildDialogues();
            BuildExperiments();
            BuildMainQuests();
            BuildSideQuests();
        }

        // ------------------------------------------------------------------ quest builders

        static QuestDef Add(QuestDef q)
        {
            s_quests[q.id] = q;
            if (q.IsMain) s_main.Add(q);
            return q;
        }

        /// <summary>Builds objectives for one quest; counts are measured from when that quest starts.</summary>
        sealed class Obj
        {
            readonly QuestDef _q;
            public Obj(QuestDef q) => _q = q;

            ObjectiveDef Push(ObjectiveDef o)
            {
                _q.objectives.Add(o);
                return o;
            }

            float Since(string evt, int n) => Mathf.Clamp01(Story.Since(_q.id, evt) / (float)Mathf.Max(1, n));

            public ObjectiveDef Event(string id, string evt, int n = 1, Func<Vector3?> where = null) =>
                Push(new ObjectiveDef { id = id, countedEvent = evt, progress = () => Since(evt, n), waypoint = where });

            public ObjectiveDef Reach(string id, string site) =>
                Push(new ObjectiveDef { id = id, site = site, countedEvent = "reach_" + site, progress = () => Since("reach_" + site, 1), waypoint = () => Waypoints.Site(site) });

            public ObjectiveDef Use(string id, string site, string experiment = null, string dialogue = null)
            {
                string evt = experiment != null ? "exp_" + experiment : "use_" + site;
                return Push(new ObjectiveDef { id = id, site = site, experiment = experiment, dialogue = dialogue, countedEvent = evt, progress = () => Since(evt, 1), waypoint = () => Waypoints.Site(site) });
            }

            public ObjectiveDef Talk(string id, Character who, string dialogue) =>
                Push(new ObjectiveDef { id = id, talkTo = who, dialogue = dialogue, countedEvent = "dlg_" + dialogue, progress = () => Since("dlg_" + dialogue, 1), waypoint = () => Waypoints.Person(who) });

            public ObjectiveDef Terminal(string id, TerminalType t, string experiment) =>
                Push(new ObjectiveDef { id = id, terminal = t, experiment = experiment, countedEvent = "exp_" + experiment, progress = () => Since("exp_" + experiment, 1), waypoint = () => Waypoints.Terminal(t) });

            public ObjectiveDef OpenTerminal(string id, TerminalType t) =>
                Push(new ObjectiveDef { id = id, terminal = t, countedEvent = "terminal_" + t.ToString().ToLowerInvariant(), progress = () => Since("terminal_" + t.ToString().ToLowerInvariant(), 1), waypoint = () => Waypoints.Terminal(t) });

            public ObjectiveDef Space(string id, string scene, string experiment) =>
                Push(new ObjectiveDef { id = id, scene = scene, experiment = experiment, countedEvent = "exp_" + experiment, progress = () => Since("exp_" + experiment, 1) });

            public ObjectiveDef Target(string id, string scene, string target, string actionKey, string evt) =>
                Push(new ObjectiveDef { id = id, scene = scene, spaceTarget = target, actionKey = actionKey, countedEvent = evt, progress = () => Since(evt, 1) });

            public ObjectiveDef Custom(string id, Func<float> progress, Func<Vector3?> where = null, string scene = Scenes.MarsColony) =>
                Push(new ObjectiveDef { id = id, progress = progress, waypoint = where, scene = scene });

            public ObjectiveDef Stock(string id, ResourceType type, float kg, Func<Vector3?> where = null) =>
                Custom(id, () => ColonyGrid.Instance != null ? Mathf.Clamp01(ColonyGrid.Instance.Amount(type) / kg) : 0f, where);
        }

        static ObjectiveDef Optional(this ObjectiveDef o) { o.optional = true; return o; }
        static ObjectiveDef In(this ObjectiveDef o, string scene) { o.scene = scene; return o; }
        static ObjectiveDef When(this ObjectiveDef o, Func<bool> visible) { o.visible = visible; return o; }

        static Reward R(int research, string[] tech = null, string[] upgrades = null, string[] codex = null,
                        (Character, int)[] rel = null, (string, int)[] rep = null, string[] items = null) => new()
        {
            research = research,
            tech = tech ?? Array.Empty<string>(),
            upgrades = upgrades ?? Array.Empty<string>(),
            codex = codex ?? Array.Empty<string>(),
            relationships = rel ?? Array.Empty<(Character, int)>(),
            reputation = rep ?? Array.Empty<(string, int)>(),
            items = items ?? Array.Empty<string>(),
        };

        // ------------------------------------------------------------------ open objectives

        /// <summary>Every visible, unfinished objective of every active quest.</summary>
        public static IEnumerable<(QuestDef quest, ObjectiveDef objective)> OpenObjectives()
        {
            foreach (var id in S.active)
            {
                var q = Story.Quest(id);
                if (q == null) continue;
                foreach (var o in q.objectives)
                    if (!Story.ObjectiveDone(o.id) && o.IsVisible) yield return (q, o);
            }
        }

        static bool InScene(string scene) => SceneManager.GetActiveScene().name == scene;

        /// <summary>Should the site at <paramref name="siteId"/> offer its prompt right now?</summary>
        public static bool SiteNeeded(string siteId)
        {
            if (siteId == "lander") return InScene(Scenes.MarsColony) && TravelNeeded() != null;
            foreach (var (_, o) in OpenObjectives()) if (o.site == siteId) return true;
            var def = StorySites.Get(siteId);
            return def != null && def.always;
        }

        static ObjectiveDef SiteObjective(string siteId) => OpenObjectives().Select(p => p.objective).FirstOrDefault(o => o.site == siteId);

        /// <summary>Where the lander would take you for the current objectives (orbit, the expedition), or null.</summary>
        static string TravelNeeded()
        {
            foreach (var (_, o) in OpenObjectives())
            {
                if (o.id == "c10_launch") return "expedition";
                if (o.scene == Scenes.MarsOrbit) return Scenes.MarsOrbit;
            }
            return null;
        }

        public static void OnSiteUsed(string siteId)
        {
            var o = SiteObjective(siteId);
            switch (siteId)
            {
                case "lander":
                {
                    string where = TravelNeeded();
                    if (where == "expedition") GameEvents.Raise("expedition_launch");
                    else if (where != null) Cinematic.Launch(() => GameSession.Travel(Scenes.MarsOrbit, GameSession.Arrival.Lander, Loc.T("SCENE_MARS_ORBIT")));
                    return;
                }
                case "scan_a": case "scan_b": case "scan_c":
                    Hud.ShowInfo(Loc.T("SITE_" + siteId), Loc.T("SCAN_RESULT_" + siteId.Substring(5).ToUpperInvariant()), 14f);
                    break;
                case "min_1": case "min_2": case "min_3":
                case "skylight_probe": case "ares0_lander": case "rec_1": case "rec_2": case "rec_3": case "notes_drive":
                    Hud.ShowInfo(Loc.T("SITE_" + siteId), Loc.T("RESULT_" + siteId.ToUpperInvariant()), 14f);
                    break;
            }
            if (o == null) return;
            if (!string.IsNullOrEmpty(o.dialogue) && o.talkTo == Character.None) DialoguePlayer.Play(o.dialogue);
            if (!string.IsNullOrEmpty(o.experiment)) ExperimentPanel.Open(o.experiment);
        }

        /// <summary>The conversation a colonist starts: a mission talk if one is due, else something about now.</summary>
        public static string TalkTo(Character c)
        {
            foreach (var (_, o) in OpenObjectives())
                if (o.talkTo == c && !string.IsNullOrEmpty(o.dialogue)) return o.dialogue;
            string bio = "bio_" + c.ToString().ToLowerInvariant();
            if (!S.codex.Contains(bio)) Story.Grant(R(0, codex: new[] { bio }), announce: true);
            return IdleDialogue(c);
        }

        /// <summary>Story actions offered on a facility terminal.</summary>
        public static List<(string label, Action action)> TerminalActions(TerminalType t)
        {
            var list = new List<(string, Action)>();
            foreach (var (_, o) in OpenObjectives())
                if (o.terminal == t && !string.IsNullOrEmpty(o.experiment))
                {
                    var exp = o.experiment;
                    list.Add((Loc.T($"X_{exp}_TITLE"), () => { TerminalScreen.Close(); ExperimentPanel.Open(exp); }));
                }
            if (t == TerminalType.Observatory && GameSession.Mode == GameMode.Campaign && Story.HasTech("lab_blackhole"))
                list.Add((Loc.T("ACTION_OPEN_LAB"), () => { TerminalScreen.Close(); GameSession.Travel(Scenes.BlackHoleLab, GameSession.Arrival.Lab, Loc.T("SCENE_BLACK_HOLE_LAB")); }));
            return list;
        }

        /// <summary>Buttons on the space mission panel (orbit and lab scenes, campaign only).</summary>
        public static List<(string label, Action action, bool enabled)> SpaceActions(Transform selected)
        {
            var list = new List<(string, Action, bool)>();
            string scene = SceneManager.GetActiveScene().name;
            foreach (var (_, o) in OpenObjectives())
            {
                if (o.scene != scene) continue;
                if (!string.IsNullOrEmpty(o.experiment))
                {
                    var exp = o.experiment;
                    list.Add((Loc.T($"X_{exp}_TITLE"), () => ExperimentPanel.Open(exp), true));
                }
                else if (!string.IsNullOrEmpty(o.spaceTarget))
                {
                    var evt = o.countedEvent;
                    bool on = selected != null && selected.name.StartsWith(o.spaceTarget);
                    list.Add((Loc.T(o.actionKey) + (on ? "" : "  " + Loc.T("ACTION_SELECT_TARGET", Loc.T("TARGET_" + o.spaceTarget))), () => GameEvents.Raise(evt), on));
                }
            }
            if (Expedition && OpenObjectives().Any(p => p.objective.id == "c10_pods"))
                for (int i = 1; i <= 3; i++)
                {
                    if (GameEvents.Count("exp_pod" + i) > 0) continue;
                    string exp = "pod" + i;
                    bool on = selected != null && selected.name.StartsWith("Pod_" + i);
                    list.Add((Loc.T("ACTION_RETRIEVE_POD", i) + (on ? "" : "  " + Loc.T("ACTION_SELECT_TARGET", Loc.T("TARGET_Pod", i))), () => ExperimentPanel.Open(exp), on));
                }
            if (!Expedition)
                list.Add((Loc.T("ACTION_RETURN_COLONY"), () => GameSession.Travel(Scenes.MarsColony,
                    scene == Scenes.MarsOrbit ? GameSession.Arrival.Lander : GameSession.Arrival.Lab, Loc.T("SCENE_COLONY")), true));
            return list;
        }

        /// <summary>The lab scene is the expedition to HORIZON-1 (chapter 10) rather than the simulation.</summary>
        public static bool Expedition => GameSession.Mode == GameMode.Campaign && Story.ObjectiveDone("c10_launch");

        /// <summary>Quests start in the colony, where their intro and world setup make sense.</summary>
        public static bool ReadyToStart(QuestDef q)
        {
            if (StoryTest.Running) return true;
            if (!InScene(Scenes.MarsColony) || DialoguePlayer.IsBlocking) return false;
            if (q.id == "PRO") return S.Has("pro_landed");
            return S.Has("pro_landed") || GameSession.State.chapter > 0;
        }

        // ------------------------------------------------------------------ scenes

        public static void OnSceneStarted(string scene)
        {
            var arrival = GameSession.PendingArrival;
            GameSession.PendingArrival = GameSession.Arrival.None;
            switch (scene)
            {
                case Scenes.MarsColony:
                    if (GameSession.State.chapter == 0 && !S.Has("pro_landed"))
                        Cinematic.Landing(() => Story.SetFlag("pro_landed"));
                    else if (arrival == GameSession.Arrival.Lander)
                        Cinematic.Arrival(() => GameEvents.Raise("arrived_colony"));
                    else if (arrival == GameSession.Arrival.Lab)
                        GameEvents.Raise("arrived_colony");
                    if (GameSession.State.chapter > 0) S.Set("pro_landed");
                    break;
                case Scenes.MarsOrbit:
                    GameEvents.Raise("arrived_orbit");
                    SpaceMissionPanel.Ensure();
                    break;
                case Scenes.BlackHoleLab:
                    if (Expedition)
                    {
                        Space.BlackHoleLab.BeginExpedition();
                        GameEvents.Raise("reach_horizon1");
                        DialoguePlayer.Play("C10_ARRIVE");
                    }
                    else
                    {
                        GameEvents.Raise("arrived_lab");
                        if (S.IsActive("C09")) DialoguePlayer.Play("C09_LAB");
                    }
                    SpaceMissionPanel.Ensure();
                    break;
            }
            Story.NotifyChanged();
        }

        public static void OnObjectiveCompleted(string id)
        {
            switch (id)
            {
                case "c03_log": Story.SetFlag("archive_1"); break;
                case "c08_fragments": DialoguePlayer.Play("C08_FRAGMENTS"); break;
                case "c10_launch":
                    Cinematic.Launch(() => Cinematic.TitleCard("TITLE_TIME_SKIP_KICKER", "TITLE_TIME_SKIP", "TITLE_TIME_SKIP_BODY",
                        () => GameSession.Travel(Scenes.BlackHoleLab, GameSession.Arrival.None, Loc.T("SCENE_HORIZON1"))));
                    break;
                case "c10_reconstruct": DialoguePlayer.Play("C10_VARGA"); break;
            }
        }

        // ------------------------------------------------------------------ world state from the story

        static readonly int[] FaultedPanelIndices = { 3, 9, 14, 22, 31 };

        public static void ApplyWorld(StoryState s)
        {
            if (!InScene(Scenes.MarsColony)) return;
            bool c01 = s.IsActive("C01");

            // Chapter 1: five panels with damaged connections, and tracking off until restored.
            var panels = UnityEngine.Object.FindObjectsByType<SolarPanel>()
                .OrderBy(p => Mathf.Round(p.transform.position.x)).ThenBy(p => Mathf.Round(p.transform.position.z)).ToArray();
            for (int i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                if (!p.name.StartsWith("SolarPanel_")) p.name = $"SolarPanel_{i:00}";
                p.faulted = c01 && Array.IndexOf(FaultedPanelIndices, i) >= 0 && !s.Has("panel_fixed_" + p.name);
                if (c01 && Story.Since("C01", "tracking_on") == 0) p.sunTracking = false;
            }

            // Chapter 2: clogged intake filter, so the plant needs ~3x the energy per kg of oxygen.
            bool degraded = s.IsActive("C02") && !Story.ObjectiveDone("c02_fix");
            float kwh = degraded ? 34f : s.upgrades.Contains("moxie_stack") ? 9.7f : 11.4f;
            foreach (var g in UnityEngine.Object.FindObjectsByType<OxygenGenerator>())
                if (g.process == OxygenGenerator.Process.SolidOxideCO2) g.kWhPerKgO2 = kwh;

            // Chapter 3: the ice site chosen from the survey.
            var dir = StoryDirector.Instance;
            if (dir != null && dir.siteBDeposit != null) dir.siteBDeposit.SetActive(s.Has("ice_site_b"));

            // Chapter 4: pumps down stop growth; an earlier power cut for the greenhouse slows it.
            float grow = 1f;
            if (s.IsActive("C04") && !Story.ObjectiveDone("c04_pumps")) grow = 0f;
            else if (s.Decision("power_priority") is string pp && pp != "greenhouse" && GameSession.State.chapter <= 3) grow = 0.6f;
            Agriculture.HydroponicBed.GrowthMultiplier = grow;

            // Chapter 5: losses in the water loop until the recycling challenge is solved.
            bool leaky = s.IsActive("C05") && !Story.ObjectiveDone("c05_optimize");
            foreach (var c in UnityEngine.Object.FindObjectsByType<CrewLifeSupport>())
                c.waterRecovery = leaky ? 0.85f : s.upgrades.Contains("recycling") ? 0.97f : 0.93f;

            // Emergency: dust storm.
            if (s.IsActive("EM_STORM") && MarsEnvironment.Instance != null)
                MarsEnvironment.Instance.opticalDepth = Mathf.Max(MarsEnvironment.Instance.opticalDepth, 1.6f);
        }

        /// <summary>Decisions assumed when a chapter is started from Chapter Select.</summary>
        public static IEnumerable<(string key, string value)> DefaultDecisions(int chapter)
        {
            if (chapter > 1) yield return ("power_priority", "greenhouse");
            if (chapter > 2) yield return ("c02_report", "local");
            if (chapter > 4) yield return ("crop_priority", "variety");
            if (chapter > 6) yield return ("comms", "both");
            if (chapter > 6) yield return ("council", "science");
            if (chapter > 8) yield return ("c08_share", "verify");
        }

        // ------------------------------------------------------------------ world setup helpers

        static void SetBattery(float fraction)
        {
            var g = ColonyGrid.Instance;
            if (g != null) g.SetBattery(g.BatteryCapacityWh * fraction);
        }

        static void DustPanels(float min, float max)
        {
            var rng = new System.Random(7);
            foreach (var p in UnityEngine.Object.FindObjectsByType<SolarPanel>())
                p.dustCoverage = Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        static void CapStock(ResourceType t, float kg)
        {
            var g = ColonyGrid.Instance;
            if (g != null && g.Amount(t) > kg) g.SetAmount(t, kg);
        }

        static Vector3? FaultedPanel()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<SolarPanel>()) if (p.faulted) return p.transform.position;
            return null;
        }

        static float Battery(float target) => ColonyGrid.Instance != null ? Mathf.Clamp01(ColonyGrid.Instance.BatteryFraction / target) : 0f;
    }
}
