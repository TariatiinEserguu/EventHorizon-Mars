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
    /// <summary>Invisible point of interest: raises reach_&lt;id&gt; once when the player comes close.</summary>
    public class StoryMarker : MonoBehaviour
    {
        public string siteId;
        public float radius = 10f;
        float _next;
        bool _inside;

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.3f;
            var p = FindAnyObjectByType<AstronautController>();
            bool inside = p != null && (p.transform.position - transform.position).sqrMagnitude <= radius * radius;
            // Fires on entering, so returning later (a new objective) counts again.
            if (inside && !_inside)
            {
                GameEvents.Raise("reach_" + siteId);
                StoryContent.OnSiteUsed(siteId);
            }
            _inside = inside;
        }
    }
}
