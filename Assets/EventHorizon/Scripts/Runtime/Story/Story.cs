using System;
using System.Collections.Generic;
using System.Linq;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;

namespace EventHorizon.Story
{
    /// <summary>
    /// The running story: which quests are active or done, decisions, reputation, relationships, research points,
    /// unlocked technology and upgrades. All of it lives in <see cref="SaveData.story"/>, so it is saved with the game
    /// and changing the language never touches it.
    /// </summary>
    public static class Story
    {
        public static StoryState State => GameSession.State.story ??= new StoryState();

        /// <summary>Raised when quests, objectives, decisions or rewards change.</summary>
        public static event Action Changed;

        public static void NotifyChanged()
        {
            GameSession.HasUnsavedProgress = true;
            Changed?.Invoke();
        }

        public static QuestDef Quest(string id) => id != null && StoryContent.Quests.TryGetValue(id, out var q) ? q : null;

        public static QuestDef MainQuest(int chapter) => StoryContent.MainQuests[Mathf.Clamp(chapter, 0, StoryContent.MainQuests.Count - 1)];

        public static QuestDef CurrentMain => MainQuest(GameSession.State.chapter);

        public static IEnumerable<QuestDef> Active => State.active.Select(Quest).Where(q => q != null);

        /// <summary>The quest shown on the HUD: the tracked one if it's still active, else the main quest.</summary>
        public static QuestDef Tracked
        {
            get
            {
                var t = Quest(State.tracked);
                if (t != null && State.IsActive(t.id)) return t;
                var main = CurrentMain;
                if (State.IsActive(main.id)) return main;
                return Active.FirstOrDefault();
            }
        }

        public static bool ObjectiveDone(string objectiveId) => GameSession.State.completedObjectives.Contains(objectiveId);

        // ------------------------------------------------------------------ counting

        /// <summary>How many times <paramref name="evt"/> happened since <paramref name="questId"/> started.</summary>
        public static int Since(string questId, string evt)
        {
            int baseline = StoryState.Get(State.baselines, questId + "/" + evt);
            return Mathf.Max(0, GameEvents.Count(evt) - baseline);
        }

        public static bool HasTech(string id) => string.IsNullOrEmpty(id) || State.tech.Contains(id);
        public static bool HasUpgrade(string id) => State.upgrades.Contains(id);
        public static bool Flag(string id) => State.Has(id);

        public static void SetFlag(string id)
        {
            if (State.Has(id)) return;
            State.Set(id);
            NotifyChanged();
        }

        /// <summary>Classified archive fragments found (the secret ending needs all four).</summary>
        public static int Archives => new[] { "archive_1", "archive_2", "archive_3", "archive_4" }.Count(State.Has);

        // ------------------------------------------------------------------ lifecycle

        public static bool CanStart(QuestDef q)
        {
            var s = State;
            if (s.IsActive(q.id) || s.IsDone(q.id) || s.failed.Contains(q.id)) return false;
            foreach (var p in q.prerequisites) if (!s.IsDone(p)) return false;
            if (!HasTech(q.requiredTech)) return false;
            foreach (var i in q.requiredItems) if (!s.items.Contains(i)) return false;
            return true;
        }

        public static void Start(QuestDef q, bool announce = true)
        {
            var s = State;
            if (s.IsActive(q.id) || s.IsDone(q.id)) return;
            s.active.Add(q.id);
            foreach (var o in q.objectives)
                if (!string.IsNullOrEmpty(o.countedEvent))
                    StoryState.SetValue(s.baselines, q.id + "/" + o.countedEvent, GameEvents.Count(o.countedEvent));
            var env = MarsEnvironment.Instance;
            StoryState.SetValue(s.startSol, q.id, env != null ? env.Sol : 0);
            q.onStart?.Invoke();
            if (announce)
            {
                string kind = q.IsMain ? Loc.T("TOAST_NEW_MAIN") : q.category == QuestCategory.Emergency ? Loc.T("TOAST_NEW_EMERGENCY") : Loc.T("TOAST_NEW_SIDE");
                Hud.Toast($"{kind}  //  {Loc.T(q.TitleKey)}", q.category == QuestCategory.Emergency);
                if (!string.IsNullOrEmpty(q.introDialogue)) DialoguePlayer.Play(q.introDialogue);
            }
            NotifyChanged();
        }

        public static void Complete(QuestDef q)
        {
            var s = State;
            if (!s.IsActive(q.id)) return;
            s.active.Remove(q.id);
            s.completed.Add(q.id);
            Grant(q.reward, announce: true);
            q.onComplete?.Invoke();
            Hud.Toast($"{Loc.T("TOAST_MISSION_COMPLETE")}  //  {Loc.T(q.TitleKey)}");
            if (!string.IsNullOrEmpty(q.completeDialogue)) DialoguePlayer.Play(q.completeDialogue);
            DebriefPanel.Show(q);
            if (q.IsMain) Profile.Current.Complete(q.chapter, q.id);
            if (s.tracked == q.id) s.tracked = "";
            NotifyChanged();
        }

        public static void Fail(QuestDef q)
        {
            var s = State;
            if (!s.IsActive(q.id)) return;
            s.active.Remove(q.id);
            s.failed.Add(q.id);
            q.onFail?.Invoke();
            Hud.Toast($"{Loc.T("TOAST_MISSION_FAILED")}  //  {Loc.T(q.TitleKey)}", true);
            Hud.ShowInfo(Loc.T(q.TitleKey), Loc.T(q.FailKey), 12f);
            NotifyChanged();
        }

        public static void Grant(Reward r, bool announce)
        {
            if (r == null) return;
            var s = State;
            s.research += r.research;
            foreach (var t in r.tech) if (!s.tech.Contains(t)) s.tech.Add(t);
            foreach (var i in r.items) if (!s.items.Contains(i)) s.items.Add(i);
            foreach (var u in r.upgrades) if (!s.upgrades.Contains(u)) s.upgrades.Add(u);
            foreach (var c in r.codex)
            {
                if (s.codex.Contains(c)) continue;
                s.codex.Add(c);
                if (!Profile.Current.discoveries.Contains(c)) { Profile.Current.discoveries.Add(c); Profile.Current.Save(); }
            }
            foreach (var (who, d) in r.relationships) StoryState.Add(s.relationships, who.ToString(), d);
            foreach (var (axis, d) in r.reputation) StoryState.Add(s.reputation, axis, d);
            StoryWorld.Apply();
            if (!announce) return;
            if (r.research > 0) Hud.Toast(Loc.T("TOAST_RESEARCH", r.research));
            foreach (var t in r.tech) Hud.Toast(Loc.T("TOAST_TECH", Loc.T("TECH_" + t)));
            foreach (var u in r.upgrades) Hud.Toast(Loc.T("TOAST_UPGRADE", Loc.T("UPG_" + u)));
            foreach (var c in r.codex) Hud.Toast(Loc.T("TOAST_CODEX", Loc.T("CODEX_" + c + "_TITLE")));
        }

        // ------------------------------------------------------------------ decisions

        /// <summary>Records a decision and applies its reputation and relationship effects.</summary>
        public static void Decide(string key, string value, (string axis, int delta)[] reputation = null, (Character who, int delta)[] relations = null)
        {
            var s = State;
            s.Decide(key, value);
            if (reputation != null) foreach (var (a, d) in reputation) StoryState.Add(s.reputation, a, d);
            if (relations != null)
                foreach (var (c, d) in relations)
                {
                    StoryState.Add(s.relationships, c.ToString(), d);
                    if (d != 0) Hud.Toast(Loc.T(d > 0 ? "TOAST_REL_UP" : "TOAST_REL_DOWN", Loc.T($"CHAR_{c.ToString().ToUpperInvariant()}_NAME")));
                }
            GameEvents.Raise("decided_" + key);
            NotifyChanged();
        }

        // ------------------------------------------------------------------ chapter select

        /// <summary>
        /// Builds the state for starting at a later chapter: earlier main quests count as completed (with their
        /// technology and upgrades), and decisions take their neutral default.
        /// </summary>
        public static StoryState StateForChapter(int chapter)
        {
            var s = new StoryState();
            for (int c = 0; c < chapter; c++)
            {
                var q = MainQuest(c);
                s.completed.Add(q.id);
                s.research += q.reward.research;
                s.tech.AddRange(q.reward.tech);
                s.upgrades.AddRange(q.reward.upgrades);
                s.codex.AddRange(q.reward.codex);
            }
            foreach (var (key, value) in StoryContent.DefaultDecisions(chapter)) s.Decide(key, value);
            return s;
        }
    }

    /// <summary>
    /// World conditions that follow from the story (faulted panels in chapter 1, the degraded oxygen plant in
    /// chapter 2 ...). Derived from the saved state every time instead of being stored, so loading a save always
    /// reproduces the right world.
    /// </summary>
    public static class StoryWorld
    {
        public static void Apply()
        {
            if (GameSession.Mode == GameMode.Sandbox || GameSession.Mode == GameMode.Science) return;
            StoryContent.ApplyWorld(Story.State);
            ColonyUpgrades.Apply(Story.State);
        }
    }

    /// <summary>Colony upgrades unlocked by missions. Each changes a real simulation value.</summary>
    public static class ColonyUpgrades
    {
        public static void Apply(StoryState s)
        {
            bool coating = s.upgrades.Contains("dust_coating");
            foreach (var p in UnityEngine.Object.FindObjectsByType<Colony.SolarPanel>())
                p.dustMultiplier = coating ? 0.6f : 1f;
            if (s.upgrades.Contains("ice_drill"))
                foreach (var d in UnityEngine.Object.FindObjectsByType<Colony.IceDeposit>()) d.kgPerExtraction = Mathf.Max(d.kgPerExtraction, 225f);
            if (s.upgrades.Contains("recycling"))
                foreach (var c in UnityEngine.Object.FindObjectsByType<Colony.CrewLifeSupport>()) c.waterRecovery = Mathf.Max(c.waterRecovery, 0.97f);
            Agriculture.HydroponicBed.LightPowerMultiplier = s.upgrades.Contains("led") ? 0.8f : 1f;
        }
    }
}
