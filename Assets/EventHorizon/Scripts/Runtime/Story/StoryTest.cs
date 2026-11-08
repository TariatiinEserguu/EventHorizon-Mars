using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;

namespace EventHorizon.Story
{
    /// <summary>
    /// Automated campaign and localization test. Start the player with "-eh-storytest &lt;report.txt&gt;": it plays the
    /// whole campaign four times with different decision profiles (one aimed at each ending), completing every
    /// objective through the same events the world raises, saving and reloading halfway and switching language on
    /// the way. Then it checks that every key the content uses exists in every language with matching placeholders.
    /// World-measured objectives (battery level, stocks, lab settings) are marked done directly and listed.
    /// </summary>
    public static class StoryTest
    {
        public static bool Running { get; private set; }

        public static string RequestedPath
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-eh-storytest") return args[i + 1];
                return null;
            }
        }

        class Scenario
        {
            public string name, expected;
            public Dictionary<string, int> choices = new();   // dialogue or experiment id -> option index
            public bool[] upgrades = { true, true, false, false };
            public Func<QuestDef, bool> side = _ => false;
            public bool archives;
        }

        static Scenario s_current;
        static StringBuilder s_log;
        static readonly HashSet<string> s_forced = new();

        public static void RunAndQuit()
        {
            string path = RequestedPath;
            bool ok = Run(out string report);
            try { File.WriteAllText(path, report); } catch (Exception e) { Debug.LogError(e); }
            Debug.Log(report);
            Debug.Log($"[Event Horizon] Story test {(ok ? "PASSED" : "FAILED")}: {path}");
            Application.Quit(ok ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            s_log = new StringBuilder();
            s_log.AppendLine($"EVENT HORIZON: MARS story and localization test  {DateTime.Now:yyyy-MM-dd HH:mm}");
            string startLanguage = Loc.Current;
            bool ok = true;
            Running = true;
            Profile.UseTemporary();
            try
            {
                foreach (var sc in Scenarios())
                {
                    s_current = sc;
                    bool pass = PlayCampaign(sc, out string ending, out string detail);
                    s_log.AppendLine($"[{(pass ? "PASS" : "FAIL")}] {sc.name}: ending {ending} (expected {sc.expected}). {detail}");
                    ok &= pass;
                }
                ok &= CheckLocalization();
            }
            catch (Exception e)
            {
                s_log.AppendLine("[FAIL] exception: " + e);
                ok = false;
            }
            finally
            {
                Running = false;
                if (Loc.Current != startLanguage) Loc.SetLanguage(startLanguage);
            }
            if (s_forced.Count > 0) s_log.AppendLine("World-measured objectives marked done by the test: " + string.Join(", ", s_forced.OrderBy(x => x)));
            s_log.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
            report = s_log.ToString();
            return ok;
        }

        static IEnumerable<Scenario> Scenarios()
        {
            var home = new Scenario { name = "Colony first", expected = Endings.Home, upgrades = new[] { true, true, false, false },
                side = q => q.category == QuestCategory.Colony || q.category == QuestCategory.Emergency };
            home.choices["priority"] = 0; home.choices["C02_REPORT"] = 1; home.choices["C06_JUN"] = 0; home.choices["C06_COUNCIL"] = 0; home.choices["C08_SHARE"] = 1;
            yield return home;

            var beyond = new Scenario { name = "Explorer", expected = Endings.Beyond, upgrades = new[] { false, true, false, true },
                side = q => q.category == QuestCategory.Exploration || q.category == QuestCategory.Side };
            beyond.choices["priority"] = 2; beyond.choices["C02_REPORT"] = 0; beyond.choices["C06_JUN"] = 1; beyond.choices["C06_COUNCIL"] = 1; beyond.choices["C08_SHARE"] = 0;
            yield return beyond;

            var horizon = new Scenario { name = "Scientist", expected = Endings.Horizon, upgrades = new[] { true, true, false, false },
                side = q => q.category == QuestCategory.Research };
            horizon.choices["priority"] = 1; horizon.choices["C06_JUN"] = 2; horizon.choices["C06_COUNCIL"] = 2; horizon.choices["C08_SHARE"] = 1;
            yield return horizon;

            var unknown = new Scenario { name = "Archivist (secret)", expected = Endings.Unknown, archives = true, upgrades = new[] { false, true, true, false },
                side = q => q.id == "S_GLITCH" || q.category == QuestCategory.Research };
            unknown.choices["C06_COUNCIL"] = 2; unknown.choices["ergo"] = 0;
            yield return unknown;
        }

        // ------------------------------------------------------------------ campaign

        static bool PlayCampaign(Scenario sc, out string ending, out string detail)
        {
            var data = new SaveData { characterName = "Test Pilot", callsign = "TEST", mode = GameMode.Campaign };
            data.story = new StoryState();
            GameSession.BeginTest(data);
            Story.State.Set("pro_landed");
            var order = new List<string>();
            bool reloaded = false, switched = false;
            string problems = "";

            for (int step = 0; step < 4000 && string.IsNullOrEmpty(Story.State.ending); step++)
            {
                MissionTracker.Drive();
                foreach (var id in Story.State.completed) if (!order.Contains(id)) order.Add(id);

                if (!reloaded && GameSession.State.chapter >= 5)
                {
                    // Save and load halfway: the campaign must continue from the restored state.
                    GameSession.State.FlattenCounters();
                    string json = JsonUtility.ToJson(GameSession.State);
                    var loaded = JsonUtility.FromJson<SaveData>(json);
                    if (loaded.story.completed.Count != Story.State.completed.Count || loaded.Counters.Count != GameSession.State.Counters.Count)
                        problems += " save/load lost progress;";
                    GameSession.BeginTest(loaded);
                    reloaded = true;
                }
                if (!switched && GameSession.State.chapter >= 3)
                {
                    int before = Story.State.completed.Count;
                    foreach (var l in Loc.Languages) Loc.SetLanguage(l.code);
                    Loc.SetLanguage(Loc.English);
                    if (Story.State.completed.Count != before) problems += " language switch changed progress;";
                    switched = true;
                }

                // Work on one open objective per step.
                // Side missions first, so optional story threads finish before the chapter that needs them ends.
                var next = StoryContent.OpenObjectives().OrderBy(p => p.quest.IsMain ? 1 : 0).FirstOrDefault(p => Wanted(sc, p.quest, p.objective));
                if (next.objective != null) Complete(next.quest, next.objective);
            }

            ending = Story.State.ending;
            // Prerequisites: no quest may complete before the quests it requires.
            foreach (var id in order)
            {
                var q = Story.Quest(id);
                foreach (var p in q.prerequisites)
                    if (order.IndexOf(p) < 0 || order.IndexOf(p) > order.IndexOf(id)) problems += $" {id} completed before {p};";
            }
            int mains = StoryContent.MainQuests.Count(q => Story.State.IsDone(q.id));
            var scores = Endings.Scores(Story.State);
            detail = $"main quests {mains}/{StoryContent.MainQuests.Count}, side completed {Story.State.completed.Count - mains}, " +
                     $"archives {Story.Archives}/4, scores {string.Join(" ", scores.Select(kv => kv.Key + "=" + kv.Value))}, " +
                     $"research {Story.State.research}, saved+loaded {reloaded}, language switch {switched}.{problems}";
            return ending == sc.expected && mains == StoryContent.MainQuests.Count && problems.Length == 0;
        }

        static bool Wanted(Scenario sc, QuestDef q, ObjectiveDef o)
        {
            bool secret = o.id is "c03_log" or "c07_classified" or "c08_hidden";
            if (secret) return sc.archives;
            return q.IsMain || sc.side(q);
        }

        static void Complete(QuestDef q, ObjectiveDef o)
        {
            if (o.talkTo != Character.None && !string.IsNullOrEmpty(o.dialogue))
            {
                RunDialogue(StoryContent.Dialogues[o.dialogue]);
                return;
            }
            if (!string.IsNullOrEmpty(o.site) && !(o.countedEvent ?? "").StartsWith("reach_") && o.id != "c07_launch" && o.id != "c08_launch" && o.id != "c10_launch")
                GameEvents.Raise("use_" + o.site);
            if (!string.IsNullOrEmpty(o.dialogue) && StoryContent.Dialogues.TryGetValue(o.dialogue, out var d)) RunDialogue(d);
            if (!string.IsNullOrEmpty(o.experiment) && StoryContent.Experiments.TryGetValue(o.experiment, out var e))
            {
                RunExperiment(e);
                return;
            }
            if (!string.IsNullOrEmpty(o.countedEvent))
            {
                for (int i = 0; i < 12 && o.progress() < 1f; i++) GameEvents.Raise(o.countedEvent);
                if (o.progress() >= 1f) return;
            }
            // Objectives measured from the live world (battery, stocks, lab settings): mark done.
            s_forced.Add(o.id);
            if (!GameSession.State.completedObjectives.Contains(o.id)) GameSession.State.completedObjectives.Add(o.id);
            if (o.id == "c10_pods") for (int i = 1; i <= 3; i++) GameEvents.Raise("exp_pod" + i);
        }

        public static void RunDialogue(DialogueDef d)
        {
            foreach (var line in d.lines)
                if (line.choices != null && line.choices.Length > 0)
                {
                    int pick = s_current != null && s_current.choices.TryGetValue(d.id, out var c) ? c : 0;
                    line.onChoice?.Invoke(Mathf.Clamp(pick, 0, line.choices.Length - 1));
                }
            if (!Story.State.seenDialogues.Contains(d.id)) Story.State.seenDialogues.Add(d.id);
            GameEvents.Raise("dlg_" + d.id);
            d.onComplete?.Invoke();
        }

        public static void RunExperiment(ExperimentDef e)
        {
            int pick = s_current != null && s_current.choices.TryGetValue(e.id, out var c) ? c : 0;
            if (e.kind == ExperimentDef.Kind.Choice) e.onChoice?.Invoke(e.correct >= 0 ? e.correct : pick);
            if (e.kind == ExperimentDef.Kind.Checklist && e.checklistGoal != null)
            {
                var checks = e.id == "upgrade" ? (bool[])s_current.upgrades.Clone() : Enumerable.Repeat(true, e.items).ToArray();
                if (e.id == "obs_instruments") checks = new[] { true, true, true, false, false };
                if (!e.checklistGoal(checks)) s_log.AppendLine($"  note: checklist {e.id} rejected the test's selection");
            }
            e.onSolved?.Invoke();
            GameEvents.Raise("exp_" + e.id);
        }

        // ------------------------------------------------------------------ localization

        static readonly Regex Placeholder = new(@"\{(\d+)[^}]*\}");

        static bool CheckLocalization()
        {
            var keys = new HashSet<string>();
            foreach (var q in StoryContent.Quests.Values)
            {
                keys.Add(q.TitleKey); keys.Add(q.DescKey); keys.Add(q.DebriefKey);
                if (q.IsMain) keys.Add(q.LearnKey);
                if (q.category == QuestCategory.Emergency) keys.Add(q.FailKey);
                foreach (var o in q.objectives)
                {
                    keys.Add(o.Key);
                    if (!string.IsNullOrEmpty(o.actionKey)) keys.Add(o.actionKey);
                    if (!string.IsNullOrEmpty(o.spaceTarget)) keys.Add("TARGET_" + o.spaceTarget);
                }
                var r = q.reward;
                foreach (var t in r.tech) keys.Add("TECH_" + t);
                foreach (var u in r.upgrades) keys.Add("UPG_" + u);
                foreach (var c in r.codex) { keys.Add("CODEX_" + c + "_TITLE"); keys.Add("CODEX_" + c + "_BODY"); }
            }
            foreach (var d in StoryContent.Dialogues.Values)
            {
                if (d.id == "END_EPILOGUE") continue;
                foreach (var l in d.lines)
                {
                    keys.Add(l.key);
                    if (l.choices != null) foreach (var c in l.choices) keys.Add(c);
                }
            }
            foreach (var k in new[] { "EPI_REYES", "EPI_ERDENE_A", "EPI_ERDENE_B", "EPI_OKAFOR_A", "EPI_OKAFOR_B", "EPI_CHEN_A", "EPI_CHEN_B", "EPI_PARK_A", "EPI_PARK_B", "EPI_PARK_C", "EPI_VARGA", "EPI_PLAYER" })
                keys.Add(k);
            foreach (var e in Endings.All) keys.Add("ENDING_" + e + "_TITLE");
            foreach (Character c in Enum.GetValues(typeof(Character)))
            {
                if (c == Character.None || c == Character.Player) continue;
                keys.Add($"CHAR_{c.ToString().ToUpperInvariant()}_NAME");
                keys.Add($"CHAR_{c.ToString().ToUpperInvariant()}_ROLE");
                string bio = "bio_" + c.ToString().ToLowerInvariant();
                if (c != Character.Control && c != Character.Narrator) { keys.Add("CODEX_" + bio + "_TITLE"); keys.Add("CODEX_" + bio + "_BODY"); }
            }
            foreach (var s in StorySites.All) if (s.kind != "marker") keys.Add("SITE_" + s.id);
            foreach (var x in new[] { "A", "B", "C" }) keys.Add("SCAN_RESULT_" + x);
            foreach (var r in new[] { "MIN_1", "MIN_2", "MIN_3", "SKYLIGHT_PROBE", "ARES0_LANDER", "REC_1", "REC_2", "REC_3", "NOTES_DRIVE" }) keys.Add("RESULT_" + r);
            foreach (var e in StoryContent.Experiments.Values)
            {
                keys.Add(e.TitleKey); keys.Add(e.IntroKey); keys.Add(e.OkKey);
                switch (e.kind)
                {
                    case ExperimentDef.Kind.Choice: keys.Add(e.QuestionKey); for (int i = 0; i < e.options; i++) keys.Add(e.Option(i)); break;
                    case ExperimentDef.Kind.Numeric: keys.Add(e.QuestionKey); keys.Add(e.UnitKey); break;
                    case ExperimentDef.Kind.Order:
                    case ExperimentDef.Kind.Checklist: for (int i = 0; i < e.items; i++) keys.Add(e.Item(i)); break;
                    case ExperimentDef.Kind.Sliders: foreach (var sl in e.sliders) keys.Add(sl.key); break;
                }
            }

            bool ok = true;
            int missing = 0;
            foreach (var lang in Loc.Languages)
            {
                var table = Loc.Table(lang.code);
                var absent = keys.Where(k => !table.ContainsKey(k)).OrderBy(k => k).ToList();
                missing += absent.Count;
                s_log.AppendLine($"[{(absent.Count == 0 ? "PASS" : "FAIL")}] {lang.englishName}: {table.Count} strings, {absent.Count} of {keys.Count} story keys missing" +
                                 (absent.Count > 0 ? ": " + string.Join(", ", absent.Take(60)) : ""));
                ok &= absent.Count == 0;
            }

            // Every language must have the same keys as English, with the same placeholders.
            var en = Loc.Table(Loc.English);
            foreach (var lang in Loc.Languages.Where(l => l.code != Loc.English))
            {
                var t = Loc.Table(lang.code);
                var extra = t.Keys.Where(k => !en.ContainsKey(k)).ToList();
                var lacking = en.Keys.Where(k => !t.ContainsKey(k)).ToList();
                var badArgs = en.Keys.Where(k => t.ContainsKey(k) && Args(en[k]) != Args(t[k])).ToList();
                bool good = extra.Count == 0 && lacking.Count == 0 && badArgs.Count == 0;
                s_log.AppendLine($"[{(good ? "PASS" : "FAIL")}] {lang.englishName} vs English: {lacking.Count} missing, {extra.Count} extra, {badArgs.Count} placeholder mismatches" +
                                 (lacking.Count > 0 ? "; missing: " + string.Join(", ", lacking.Take(40)) : "") +
                                 (badArgs.Count > 0 ? "; placeholders: " + string.Join(", ", badArgs.Take(40)) : ""));
                ok &= good;
            }

            // Experiment readouts must resolve (a missing key shows up as the key itself).
            foreach (var lang in Loc.Languages)
            {
                Loc.SetLanguage(lang.code);
                foreach (var e in StoryContent.Experiments.Values)
                {
                    string text = (e.readout?.Invoke() ?? "") + (e.sliderReadout != null ? e.sliderReadout(Array.ConvertAll(e.sliders, s => s.value)) : "");
                    if (text.Contains("X_" + e.id)) { s_log.AppendLine($"[FAIL] {lang.englishName}: readout of {e.id} has a missing key"); ok = false; }
                }
            }
            Loc.SetLanguage(Loc.English);
            return ok;
        }

        static string Args(string s) => string.Join(",", Placeholder.Matches(s).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));
    }
}
