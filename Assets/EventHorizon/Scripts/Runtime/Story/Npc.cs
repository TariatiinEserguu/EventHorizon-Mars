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
    /// A colonist. Talking picks the conversation that fits the story right now (mission briefings first, then
    /// lines that react to your decisions and the chapter). Turns toward the player when close.
    /// </summary>
    public class Npc : MonoBehaviour, IInteractable
    {
        public Character character;
        Transform _player;
        float _baseYaw;

        public string DisplayName => Loc.T($"CHAR_{character.ToString().ToUpperInvariant()}_NAME");
        public string Role => Loc.T($"CHAR_{character.ToString().ToUpperInvariant()}_ROLE");

        public static readonly List<Npc> All = new();

        void OnEnable() { All.Add(this); _baseYaw = transform.eulerAngles.y; }

        void Start()
        {
            // Each colonist's backpack carries their colour, the same as their subtitle colour.
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.name == "EH_Backpack")
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_BaseColor", DialoguePlayer.CharacterColor(character));
                    r.SetPropertyBlock(mpb);
                }
        }
        void OnDisable() => All.Remove(this);

        public string Prompt => Loc.T("PROMPT_TALK", DisplayName);
        public bool CanInteract(GameObject user) => !DialoguePlayer.IsPlaying;

        public void Interact(GameObject user)
        {
            var id = StoryContent.TalkTo(character);
            if (id != null) DialoguePlayer.Play(id);
        }

        void Update()
        {
            if (_player == null)
            {
                var p = FindAnyObjectByType<AstronautController>();
                if (p == null) return;
                _player = p.transform;
            }
            var to = _player.position - transform.position;
            to.y = 0f;
            float targetYaw = to.sqrMagnitude < 8f * 8f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : _baseYaw;
            var e = transform.eulerAngles;
            e.y = Mathf.MoveTowardsAngle(e.y, targetYaw, 120f * Time.deltaTime);
            transform.eulerAngles = e;
        }
    }
}
