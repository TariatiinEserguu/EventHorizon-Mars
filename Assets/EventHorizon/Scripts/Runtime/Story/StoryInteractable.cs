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
    /// A story location the player can use: repair a junction, scan for ice, download an archive, open an experiment.
    /// Prompt text: SITE_&lt;siteId&gt;. Completing it raises the game event use_&lt;siteId&gt;, which objectives count.
    /// Inactive (no prompt) unless an objective currently needs it, so the world isn't cluttered with prompts.
    /// </summary>
    public class StoryInteractable : MonoBehaviour, IInteractable
    {
        public string siteId;
        [Tooltip("Seconds of work before it completes (0 = instant).")]
        public float workSeconds;
        [Tooltip("Experiment to open after the work (optional).")]
        public string experiment;
        [Tooltip("Dialogue or recording to play after the work (optional).")]
        public string dialogue;
        [Tooltip("Usable at any time, not only when an objective needs it.")]
        public bool alwaysActive;

        public string Prompt => Loc.T("SITE_" + siteId);

        public bool CanInteract(GameObject user) =>
            !StoryWork.Busy && (alwaysActive || StoryContent.SiteNeeded(siteId)) && !ExperimentPanel.IsOpen;

        public void Interact(GameObject user)
        {
            if (workSeconds > 0f) StoryWork.Begin(transform, workSeconds, Loc.T("WORK_" + (string.IsNullOrEmpty(experiment) ? "GENERIC" : "ANALYSING")), Finish);
            else Finish();
        }

        void Finish()
        {
            GameEvents.Raise("use_" + siteId);
            StoryContent.OnSiteUsed(siteId);
            if (!string.IsNullOrEmpty(dialogue)) DialoguePlayer.Play(dialogue);
            if (!string.IsNullOrEmpty(experiment)) ExperimentPanel.Open(experiment);
        }
    }
}
