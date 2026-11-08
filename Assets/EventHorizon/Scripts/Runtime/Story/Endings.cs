using System.Collections.Generic;
using System.Linq;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// The four endings. Which one you reach depends on what you did across the campaign, not on a last choice:
    /// reputation from decisions, the council's strategy, which kinds of side missions you completed, research
    /// points, analyses solved first time and the ship you built. The secret ending needs all four classified
    /// archives and the decision to open them.
    /// </summary>
    public static class Endings
    {
        public const string Home = "HOME", Beyond = "BEYOND", Horizon = "HORIZON", Unknown = "UNKNOWN";
        public static readonly string[] All = { Home, Beyond, Horizon, Unknown };

        static int Done(StoryState s, params QuestCategory[] cats) =>
            s.completed.Count(id => Story.Quest(id) is QuestDef q && cats.Contains(q.category));

        public static Dictionary<string, int> Scores(StoryState s)
        {
            string council = s.Decision("council");
            string ship = s.Decision("ship_upgrades") ?? "";
            int firstTries = Mathf.Min(10, s.flags.Count(f => f.StartsWith("firsttry_")));
            return new Dictionary<string, int>
            {
                [Home] = s.Rep(Axis.Independence) + s.Rep(Axis.Community) + 10 * Done(s, QuestCategory.Colony, QuestCategory.Emergency)
                         + (council == "independence" ? 25 : 0) + (s.Decision("power_priority") == "greenhouse" ? 5 : 0),
                [Beyond] = s.Rep(Axis.Exploration) + s.Rep(Axis.Earth) + 10 * Done(s, QuestCategory.Exploration, QuestCategory.Side)
                           + (council == "earth" ? 25 : 0) + (ship.Contains("drive") ? 15 : 0),
                [Horizon] = s.Rep(Axis.Science) + s.research / 10 + 10 * Done(s, QuestCategory.Research) + 3 * firstTries
                            + (council == "science" ? 25 : 0),
            };
        }

        public static string Determine(StoryState s)
        {
            if (Story.Archives >= 4 && s.Decision("open_ergosphere") == "open") return Unknown;
            var scores = Scores(s);
            // Ties go to the story's own question, the Horizon Project.
            return scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key == Horizon ? 0 : 1).First().Key;
        }

        /// <summary>Epilogue lines (keys) for the colonists, chosen by your decisions.</summary>
        public static List<string> Epilogue(StoryState s)
        {
            var lines = new List<string> { "EPI_REYES" };
            lines.Add(s.Decision("council") == "independence" ? "EPI_ERDENE_A" : "EPI_ERDENE_B");
            lines.Add(s.Decision("power_priority") == "greenhouse" ? "EPI_OKAFOR_A" : "EPI_OKAFOR_B");
            lines.Add(s.Decision("council") == "science" ? "EPI_CHEN_A" : "EPI_CHEN_B");
            lines.Add(s.Decision("comms") switch { "independent" => "EPI_PARK_A", "isa" => "EPI_PARK_B", _ => "EPI_PARK_C" });
            lines.Add("EPI_VARGA");
            lines.Add("EPI_PLAYER");
            return lines;
        }
    }
}
