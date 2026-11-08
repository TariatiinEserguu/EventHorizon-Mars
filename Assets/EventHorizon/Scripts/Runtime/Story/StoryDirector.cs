using System;
using System.Collections;
using EventHorizon.Astronaut;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// Scene object (built by the colony scene builder) holding what the cinematics need: the lander, its engine
    /// effects and where the player steps out.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public Transform lander;
        public ParticleSystem engineDust;
        public Light engineGlow;
        public Vector3 colonyCentre = new Vector3(10f, 0f, 8f);
        [Tooltip("Ice deposit revealed when the player picks survey site B (chapter 3).")]
        public GameObject siteBDeposit;

        public static StoryDirector Instance { get; private set; }
        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
