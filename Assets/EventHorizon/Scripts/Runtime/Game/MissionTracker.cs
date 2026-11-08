using System.Collections.Generic;
using EventHorizon.UI;
using UnityEngine;

namespace EventHorizon.Game
{
    /// <summary>
    /// Checks the current chapter's objectives against the live world. When every objective is done, it completes
    /// the chapter in the <see cref="Profile"/>, which unlocks the next one in Chapter Select, and moves on to it.
    /// </summary>
    public class MissionTracker : MonoBehaviour
    {
        static MissionTracker s_instance;
        float _nextCheck;

        public static string CurrentTitle
        {
            get
            {
                var s = GameSession.State;
                return GameSession.Mode switch
                {
                    GameMode.Sandbox => "FREE EXPLORATION",
                    GameMode.Science => SceneTitle(),
                    _ => $"{s.chapter:00} // {Campaign.Get(s.chapter).title}",
                };
            }
        }

        static string SceneTitle() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name switch
        {
            Scenes.MarsOrbit => "MARS ORBIT",
            Scenes.SpaceLab => "PLANETARIUM // NOT TO SCALE",
            Scenes.BlackHoleLab => "BLACK HOLE LABORATORY",
            _ => "SCIENCE LABORATORY",
        };

        public static IEnumerable<(string text, float progress, bool done, Vector3? waypoint)> CurrentObjectives()
        {
            if (GameSession.Mode == GameMode.Sandbox)
            {
                yield return ("Explore the colony and Jezero crater", 0f, false, null);
                yield break;
            }
            if (GameSession.Mode == GameMode.Science)
            {
                yield return ("Right-drag or arrows: orbit   Scroll: zoom", 0f, false, null);
                yield return ("Tab or 1-9: next target   Esc: menu", 0f, false, null);
                yield break;
            }
            var done = GameSession.State.completedObjectives;
            foreach (var o in Campaign.Get(GameSession.State.chapter).objectives)
            {
                bool isDone = done.Contains(o.id);
                yield return (o.text, isDone ? 1f : o.progress(), isDone, isDone ? null : o.waypoint?.Invoke());
            }
        }

        void Awake() => s_instance = this;

        void Update()
        {
            if (GameSession.Mode != GameMode.Campaign && GameSession.Mode != GameMode.None) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;

            var state = GameSession.State;
            var chapter = Campaign.Get(state.chapter);
            if (chapter.objectives.Length == 0) return;
            bool all = true;
            foreach (var o in chapter.objectives)
            {
                if (state.completedObjectives.Contains(o.id)) continue;
                if (o.progress() >= 1f)
                {
                    state.completedObjectives.Add(o.id);
                    GameSession.HasUnsavedProgress = true;
                    Hud.Toast($"OBJECTIVE COMPLETE  //  {o.text}");
                }
                else all = false;
            }
            if (!all) return;

            Profile.Current.Complete(chapter.number);
            Hud.Toast($"CHAPTER {chapter.number:00} COMPLETE  //  {chapter.title}");
            Hud.ShowInfo("Discovery", chapter.discovery);
            var next = chapter.number < Campaign.Chapters.Count ? Campaign.Get(chapter.number + 1) : null;
            if (next != null && !next.inDevelopment)
            {
                state.chapter = next.number;
                Hud.Toast($"NEW MISSION  //  {next.number:00} {next.title}");
            }
            else
            {
                Hud.Toast("Further chapters are in development. Keep exploring!");
                enabled = false;
            }
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
