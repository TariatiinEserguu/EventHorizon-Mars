using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.Story;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventHorizon.Game
{
    /// <summary>One objective as the HUD and journal show it.</summary>
    public struct ObjectiveView
    {
        public string text;
        public float progress;
        public bool done;
        public bool optional;
        public Vector3? waypoint;
        public string elsewhere;   // e.g. "IN ORBIT" when the objective is in another scene
    }

    /// <summary>
    /// Drives the campaign: keeps the current chapter's main quest running, starts side missions when their
    /// prerequisites are met, checks objectives against the live world, completes quests, runs emergency timers and
    /// starts the next chapter. Also handles arriving in a scene (the prologue landing, returning from orbit).
    /// </summary>
    public class MissionTracker : MonoBehaviour
    {
        static MissionTracker s_instance;
        float _nextCheck;
        bool _started;

        public static bool CampaignActive => GameSession.Mode == GameMode.Campaign || GameSession.Mode == GameMode.None;

        public static string CurrentTitle
        {
            get
            {
                switch (GameSession.Mode)
                {
                    case GameMode.Sandbox: return Loc.T("MODE_SANDBOX");
                    case GameMode.Science: return SceneTitle();
                }
                var q = Story.Story.Tracked;
                return q == null ? Loc.T("MODE_CAMPAIGN") : q.IsMain ? $"{ChapterLabel(q.chapter)} // {Loc.T(q.TitleKey)}" : Loc.T(q.TitleKey);
            }
        }

        public static string ChapterLabel(int chapter) => chapter == 0 ? Loc.T("CHAPTER_PROLOGUE") : Loc.T("CHAPTER_N", chapter);

        static string SceneTitle() => SceneManager.GetActiveScene().name switch
        {
            Scenes.MarsOrbit => Loc.T("SCENE_MARS_ORBIT"),
            Scenes.SpaceLab => Loc.T("SCENE_PLANETARIUM"),
            Scenes.BlackHoleLab => Loc.T("SCENE_BLACK_HOLE_LAB"),
            _ => Loc.T("SCENE_SCIENCE_LAB"),
        };

        /// <summary>Objectives of the tracked quest (or the mode's hints outside the campaign).</summary>
        public static List<ObjectiveView> CurrentObjectives()
        {
            var list = new List<ObjectiveView>();
            if (GameSession.Mode == GameMode.Sandbox) { list.Add(new ObjectiveView { text = Loc.T("HINT_SANDBOX") }); return list; }
            if (GameSession.Mode == GameMode.Science)
            {
                if (SceneManager.GetActiveScene().name == Scenes.BlackHoleLab) list.Add(new ObjectiveView { text = Loc.T("HINT_LAB_CONTROLS") });
                else list.Add(new ObjectiveView { text = Loc.T("HINT_ORBIT_CONTROLS") });
                list.Add(new ObjectiveView { text = Loc.T("HINT_TARGETS") });
                return list;
            }
            var q = Story.Story.Tracked;
            if (q != null) list.AddRange(Objectives(q, includeHidden: false));
            return list;
        }

        public static List<ObjectiveView> Objectives(QuestDef q, bool includeHidden)
        {
            var list = new List<ObjectiveView>();
            string scene = SceneManager.GetActiveScene().name;
            foreach (var o in q.objectives)
            {
                if (!includeHidden && !o.IsVisible) continue;
                bool done = Story.Story.ObjectiveDone(o.id);
                bool here = o.scene == null || o.scene == scene;
                list.Add(new ObjectiveView
                {
                    text = Loc.T(o.Key),
                    done = done,
                    optional = o.optional,
                    progress = done ? 1f : Mathf.Clamp01(SafeProgress(o)),
                    waypoint = !done && here ? o.waypoint?.Invoke() : null,
                    elsewhere = done || here ? null : Loc.T(o.scene == Scenes.MarsOrbit ? "WHERE_ORBIT" : o.scene == Scenes.BlackHoleLab ? "WHERE_LAB" : "WHERE_COLONY"),
                });
            }
            return list;
        }

        static float SafeProgress(ObjectiveDef o)
        {
            try { return o.progress != null ? o.progress() : 0f; }
            catch (System.Exception e) { Debug.LogWarning($"[Event Horizon] Objective {o.id}: {e.Message}"); return 0f; }
        }

        void Awake() => s_instance = this;

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        void Start()
        {
            if (!CampaignActive) return;
            StoryWorld.Apply();
            StoryContent.OnSceneStarted(SceneManager.GetActiveScene().name);
            _started = true;
        }

        void Update()
        {
            if (!CampaignActive || !_started || Cinematic.IsPlaying || EndingSequence.IsPlaying) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;
            Drive();
        }

        /// <summary>One campaign step. Public so the story test can drive the campaign without a player.</summary>
        public static void Drive()
        {
            var s = Story.Story.State;
            var state = GameSession.State;

            // Main quest of the current chapter.
            var main = Story.Story.CurrentMain;
            if (!s.IsDone(main.id) && !s.IsActive(main.id) && StoryContent.ReadyToStart(main)) Story.Story.Start(main);

            // Side and emergency missions whose prerequisites are met.
            // One at a time, so their announcements and intro calls don't pile up.
            foreach (var q in StoryContent.Quests.Values)
                if (!q.IsMain && Story.Story.CanStart(q) && StoryContent.ReadyToStart(q)) { Story.Story.Start(q); break; }

            // Objectives and completion.
            var active = new List<string>(s.active);
            foreach (var id in active)
            {
                var q = Story.Story.Quest(id);
                if (q == null) continue;
                bool allRequired = true;
                foreach (var o in q.objectives)
                {
                    if (Story.Story.ObjectiveDone(o.id)) continue;
                    if (SafeProgress(o) >= 1f)
                    {
                        state.completedObjectives.Add(o.id);
                        Hud.Toast($"{Loc.T(o.optional ? "TOAST_OPTIONAL_DONE" : "TOAST_OBJECTIVE_DONE")}  //  {Loc.T(o.Key)}");
                        StoryContent.OnObjectiveCompleted(o.id);
                        Story.Story.NotifyChanged();
                    }
                    else if (!o.optional && o.IsVisible) allRequired = false;
                }
                if (q.timeLimitSols > 0f && !allRequired && MarsEnvironment.Instance != null &&
                    MarsEnvironment.Instance.Sol - StoryState.Get(s.startSol, q.id) >= q.timeLimitSols)
                {
                    Story.Story.Fail(q);
                    continue;
                }
                if (!allRequired) continue;
                Story.Story.Complete(q);
                if (q.IsMain) AdvanceChapter(q);
            }
            StoryWorld.Apply();
        }

        static void AdvanceChapter(QuestDef finished)
        {
            var state = GameSession.State;
            if (finished.chapter >= StoryContent.MainQuests.Count - 1)
            {
                EndingSequence.Begin();
                return;
            }
            state.chapter = finished.chapter + 1;
            var next = Story.Story.CurrentMain;
            Profile.Current.Unlock(state.chapter);
            if (Application.isPlaying && !StoryTest.Running)
                Cinematic.TitleCard("TITLE_NEXT_CHAPTER", next.TitleKey, next.DescKey, null);
        }
    }
}
