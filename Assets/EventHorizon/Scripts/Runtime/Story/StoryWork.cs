using System;
using System.Collections.Generic;
using EventHorizon.Astronaut;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Interaction;
using EventHorizon.UI;
using UnityEngine;

namespace EventHorizon.Story
{
    /// <summary>
    /// A few seconds of hands-on work (repairing, replacing a filter, downloading data). The HUD shows progress; walking
    /// away cancels it.
    /// </summary>
    public static class StoryWork
    {
        public static Transform Target { get; private set; }
        public static float Progress01 { get; private set; }
        public static string Label { get; private set; }
        public static bool Busy => Target != null;

        static float s_duration, s_started;
        static Action s_done;

        public static void Begin(Transform target, float seconds, string label, Action done)
        {
            if (Busy) return;
            Target = target;
            s_duration = Mathf.Max(0.1f, seconds);
            s_started = Time.time;
            Label = label;
            s_done = done;
            Progress01 = 0f;
            StoryWorkRunner.Ensure();
        }

        public static void Cancel()
        {
            Target = null;
            s_done = null;
            Progress01 = 0f;
        }

        internal static void Tick()
        {
            if (Target == null) return;
            var player = UnityEngine.Object.FindAnyObjectByType<AstronautController>();
            if (player == null || (player.transform.position - Target.position).sqrMagnitude > 7f * 7f)
            {
                Hud.Toast(Loc.T("WORK_CANCELLED"), true);
                Cancel();
                return;
            }
            Progress01 = Mathf.Clamp01((Time.time - s_started) / s_duration);
            if (Progress01 < 1f) return;
            var done = s_done;
            Cancel();
            done?.Invoke();
        }
    }
}
