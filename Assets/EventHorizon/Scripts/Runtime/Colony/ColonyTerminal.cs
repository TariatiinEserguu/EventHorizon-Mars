using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Colony
{
    public enum TerminalType { Solar, Oxygen, Greenhouse, Water, Command, Observatory, Recycling }

    /// <summary>A facility control console. Interact to open its live control screen.</summary>
    public class ColonyTerminal : MonoBehaviour, IInteractable
    {
        public TerminalType type = TerminalType.Command;

        public string Prompt => Loc.T("PROMPT_TERMINAL", Title);
        public bool CanInteract(GameObject user) => true;

        public string Title => Loc.T("TERM_" + type.ToString().ToUpperInvariant());

        public void Interact(GameObject user)
        {
            UI.TerminalScreen.Open(this);
            GameEvents.Raise("terminal_" + type.ToString().ToLowerInvariant());
        }
    }
}
