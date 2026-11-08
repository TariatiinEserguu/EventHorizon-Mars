using EventHorizon.Core;
using EventHorizon.Astronaut;
using EventHorizon.UI;
using UnityEngine;

namespace EventHorizon.Game
{
    /// <summary>Raises a game event once when the player comes within <see cref="radius"/> metres.</summary>
    public class ProximityTrigger : MonoBehaviour
    {
        public string eventId = "reached_habitat";
        public float radius = 12f;
        public string message;
        Transform _player;
        bool _fired;
        float _next;

        void Update()
        {
            if (_fired || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            if (_player == null)
            {
                var p = FindAnyObjectByType<AstronautController>();
                if (p == null) return;
                _player = p.transform;
            }
            if ((_player.position - transform.position).sqrMagnitude > radius * radius) return;
            _fired = true;
            GameEvents.Raise(eventId);
            if (!string.IsNullOrEmpty(message)) Hud.Toast(Loc.TOr(message, message));
        }
    }
}
