using EventHorizon.Game;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Colony
{
    public enum TerminalType { Solar, Oxygen, Greenhouse, Water, Command }

    /// <summary>A facility control console. Interact to open its live control screen.</summary>
    public class ColonyTerminal : MonoBehaviour, IInteractable
    {
        public TerminalType type = TerminalType.Command;

        public string Prompt => $"Use {Title} terminal";
        public bool CanInteract(GameObject user) => true;

        public string Title => type switch
        {
            TerminalType.Solar => "SOLAR POWER CONTROL",
            TerminalType.Oxygen => "OXYGEN PLANT CONTROL",
            TerminalType.Greenhouse => "GREENHOUSE CONTROL",
            TerminalType.Water => "WATER FACILITY CONTROL",
            _ => "COLONY COMMAND CENTER",
        };

        public void Interact(GameObject user)
        {
            UI.TerminalScreen.Open(this);
            GameEvents.Raise("terminal_" + type.ToString().ToLowerInvariant());
        }
    }
}
