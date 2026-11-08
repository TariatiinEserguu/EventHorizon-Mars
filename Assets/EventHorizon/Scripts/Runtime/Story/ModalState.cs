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
    /// Screens that take over input (dialogue, experiments, cinematics, terminals) register here, so Esc closes the
    /// top one instead of also opening the pause menu.
    /// </summary>
    public static class ModalState
    {
        static readonly List<object> s_open = new();
        public static bool Any => s_open.Count > 0;
        public static int LastClosedFrame { get; private set; } = -1;

        public static void Open(object owner)
        {
            if (!s_open.Contains(owner)) s_open.Add(owner);
            GameInput.Block(owner);
        }

        public static void Close(object owner)
        {
            if (s_open.Remove(owner)) LastClosedFrame = Time.frameCount;
            GameInput.Unblock(owner);
        }

        /// <summary>True if Esc this frame belongs to a modal screen (open now or just closed).</summary>
        public static bool OwnsEscape => Any || Time.frameCount == LastClosedFrame;
    }
}
