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
    /// <summary>Ticks <see cref="StoryWork"/> every frame while work is in progress.</summary>
    public class StoryWorkRunner : MonoBehaviour
    {
        static StoryWorkRunner s_runner;
        public static void Ensure()
        {
            if (s_runner != null) return;
            s_runner = new GameObject("Story Work").AddComponent<StoryWorkRunner>();
        }
        void Update() => StoryWork.Tick();
        void OnDestroy() { if (s_runner == this) { s_runner = null; StoryWork.Cancel(); } }
    }
}
