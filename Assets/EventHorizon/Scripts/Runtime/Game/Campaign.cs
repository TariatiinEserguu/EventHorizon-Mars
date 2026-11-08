using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EventHorizon.Game
{
    /// <summary>Something that happened in the world that objectives can count ("panel_cleaned", "harvest" ...).</summary>
    public static class GameEvents
    {
        public static event Action<string> Raised;

        public static void Raise(string id)
        {
            var c = GameSession.State.Counters;
            c[id] = c.TryGetValue(id, out var n) ? n + 1 : 1;
            GameSession.HasUnsavedProgress = true;
            Raised?.Invoke(id);
        }

        public static int Count(string id) => GameSession.State.Counters.TryGetValue(id, out var n) ? n : 0;
    }

    public class Objective
    {
        public string id;
        public string text;
        /// <summary>Returns progress 0..1 from the live world.</summary>
        public Func<float> progress;
        /// <summary>Where to go for this objective (HUD waypoint), or null.</summary>
        public Func<Vector3?> waypoint;

        public Objective At(Func<Vector3?> where) { waypoint = where; return this; }
    }

    /// <summary>Finds the world positions objectives point at. Cheap enough to call a few times per second.</summary>
    public static class Waypoints
    {
        static Vector3? Pos(Component c) => c != null ? c.transform.position : (Vector3?)null;

        public static Vector3? Habitat() => Pos(UnityEngine.Object.FindAnyObjectByType<Colony.CrewLifeSupport>());
        public static Vector3? OxygenPlant() => Pos(UnityEngine.Object.FindAnyObjectByType<Colony.OxygenGenerator>());
        public static Vector3? Greenhouse() => Pos(UnityEngine.Object.FindAnyObjectByType<Agriculture.Greenhouse>());
        public static Vector3? Battery() => Pos(UnityEngine.Object.FindAnyObjectByType<Colony.BatteryBank>());

        public static Vector3? Tank(Colony.ResourceType type)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Colony.ResourceTank>())
                if (t.resource == type) return t.transform.position;
            return null;
        }

        public static Vector3? Terminal(Colony.TerminalType type)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Colony.ColonyTerminal>())
                if (t.type == type) return t.transform.position;
            return null;
        }

        public static Vector3? Inspectable(string eventId)
        {
            foreach (var s in UnityEngine.Object.FindObjectsByType<Interaction.ScienceInspectable>())
                if (s.eventId == eventId) return s.transform.position;
            return null;
        }

        static Vector3 Player()
        {
            var p = UnityEngine.Object.FindAnyObjectByType<Astronaut.AstronautController>();
            return p != null ? p.transform.position : Vector3.zero;
        }

        /// <summary>Nearest solar panel that needs cleaning.</summary>
        public static Vector3? DirtyPanel()
        {
            var me = Player();
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var p in UnityEngine.Object.FindObjectsByType<Colony.SolarPanel>())
            {
                if (p.dustCoverage < 0.01f) continue;
                float d = (p.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p.transform.position; }
            }
            return best;
        }

        public static Vector3? Ice()
        {
            var me = Player();
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var i in UnityEngine.Object.FindObjectsByType<Colony.IceDeposit>())
            {
                if (i.iceKg <= 0f) continue;
                float d = (i.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i.transform.position; }
            }
            return best;
        }
    }

    public class Chapter
    {
        public int number;
        public string title;
        public string summary;
        public string discovery;     // what the chapter teaches or reveals
        public string artwork;       // texture key in UiResources
        public bool inDevelopment;
        public Objective[] objectives = new Objective[0];
    }

    /// <summary>The ten chapters of the ARES-1 campaign and which ones the player has unlocked.</summary>
    public static class Campaign
    {
        static Chapter[] s_chapters;

        public static IReadOnlyList<Chapter> Chapters => s_chapters ??= Build();
        public static Chapter Get(int number) => Chapters[Mathf.Clamp(number, 1, Chapters.Count) - 1];

        static Objective Count(string id, string text, string evt, int target) =>
            new() { id = id, text = text, progress = () => Mathf.Clamp01(GameEvents.Count(evt) / (float)target) };

        static Objective Stock(string id, string text, Colony.ResourceType type, float kg) =>
            new() { id = id, text = text, progress = () => Colony.ColonyGrid.Instance != null ? Mathf.Clamp01(Colony.ColonyGrid.Instance.Amount(type) / kg) : 0f };

        static Chapter[] Build() => new[]
        {
            new Chapter
            {
                number = 1, title = "ARRIVAL", artwork = "mars",
                summary = "Touchdown in Jezero crater. Get your bearings, secure your suit's oxygen supply and report in through the relay dish.",
                discovery = "How long a spacesuit keeps you alive, and why every EVA is planned around the walk back.",
                objectives = new[]
                {
                    Count("c1_hab", "Walk to the habitat", "reached_habitat", 1).At(Waypoints.Habitat),
                    Count("c1_o2", "Refill your suit at the oxygen tank", "suit_refilled", 1).At(() => Waypoints.Tank(Colony.ResourceType.Oxygen)),
                    Count("c1_dish", "Inspect the relay dish", "inspect_relay", 1).At(() => Waypoints.Inspectable("inspect_relay")),
                },
            },
            new Chapter
            {
                number = 2, title = "POWER OF THE SUN", artwork = "sun",
                summary = "Mars receives less than half of Earth's sunlight, and dust steals more of it every sol. Keep the solar field alive.",
                discovery = "Solar irradiance at 1.52 AU, dust optical depth and why panels lose output every sol.",
                objectives = new[]
                {
                    Count("c2_clean", "Clean 3 solar panels", "panel_cleaned", 3).At(Waypoints.DirtyPanel),
                    Count("c2_term", "Check the solar power terminal", "terminal_solar", 1).At(() => Waypoints.Terminal(Colony.TerminalType.Solar)),
                    new Objective { id = "c2_batt", text = "Charge the batteries above 80%",
                        progress = () => Colony.ColonyGrid.Instance != null ? Mathf.Clamp01(Colony.ColonyGrid.Instance.BatteryFraction / 0.8f) : 0f }.At(Waypoints.Battery),
                },
            },
            new Chapter
            {
                number = 3, title = "BREATH OF LIFE", artwork = "mars",
                summary = "Bring the MOXIE-derived oxygen plant online and build an oxygen reserve for the crew.",
                discovery = "Solid-oxide electrolysis: pulling oxygen out of the carbon dioxide atmosphere.",
                objectives = new[]
                {
                    Count("c3_term", "Open the oxygen plant terminal", "terminal_oxygen", 1).At(() => Waypoints.Terminal(Colony.TerminalType.Oxygen)),
                    Stock("c3_o2", "Store 180 kg of oxygen", Colony.ResourceType.Oxygen, 180f).At(Waypoints.OxygenPlant),
                },
            },
            new Chapter
            {
                number = 4, title = "LIQUID GOLD", artwork = "moon",
                summary = "Water is fuel, air and food. Mine the buried ice sheet and fill the colony's tanks.",
                discovery = "Mid-latitude subsurface ice and how a closed loop recycles over 90% of water.",
                objectives = new[]
                {
                    Count("c4_ice", "Extract ice 4 times", "ice_extracted", 4).At(Waypoints.Ice),
                    Stock("c4_water", "Store 4,400 kg of water", Colony.ResourceType.Water, 4400f).At(() => Waypoints.Tank(Colony.ResourceType.Water)),
                },
            },
            new Chapter
            {
                number = 5, title = "THE LAST HARVEST", artwork = "earth",
                summary = "Supply ships are years apart. The greenhouse must feed the crew.",
                discovery = "Photosynthesis under LED light and how much growing area one person needs.",
                objectives = new[]
                {
                    Count("c5_harvest", "Harvest a crop", "harvest", 1).At(Waypoints.Greenhouse),
                    Count("c5_term", "Check the greenhouse terminal", "terminal_greenhouse", 1).At(() => Waypoints.Terminal(Colony.TerminalType.Greenhouse)),
                },
            },
            new Chapter { number = 6, title = "A LIVING WORLD", artwork = "earth", inDevelopment = true,
                summary = "The colony grows into a self-sustaining settlement.", discovery = "Closed ecosystems." },
            new Chapter { number = 7, title = "THE RED FRONTIER", artwork = "mars", inDevelopment = true,
                summary = "An expedition toward Valles Marineris.", discovery = "Martian geology and canyon formation." },
            new Chapter { number = 8, title = "UNKNOWN SIGNAL", artwork = "stars", inDevelopment = true,
                summary = "The relay picks up a signal that should not exist.", discovery = "Radio astronomy." },
            new Chapter { number = 9, title = "EVENT HORIZON", artwork = "blackhole", inDevelopment = true,
                summary = "The signal leads beyond the Solar System.", discovery = "Black holes and general relativity." },
            new Chapter { number = 10, title = "HUMANITY'S FUTURE", artwork = "saturn", inDevelopment = true,
                summary = "What the colony becomes.", discovery = "The long-term future of humans in space." },
        };
    }

    /// <summary>Campaign progress shared by all save slots: unlocked chapters and discoveries.</summary>
    [Serializable]
    public class Profile
    {
        public int highestUnlocked = 1;
        public List<int> completed = new();
        public List<string> discoveries = new();

        static Profile s_current;
        static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        public static Profile Current
        {
            get
            {
                if (s_current != null) return s_current;
                try { s_current = File.Exists(FilePath) ? JsonUtility.FromJson<Profile>(File.ReadAllText(FilePath)) : null; }
                catch { s_current = null; }
                return s_current ??= new Profile();
            }
        }

        public bool IsUnlocked(int chapter) => chapter <= highestUnlocked && !Campaign.Get(chapter).inDevelopment;
        public bool IsCompleted(int chapter) => completed.Contains(chapter);

        public void Complete(int chapter)
        {
            if (!completed.Contains(chapter)) completed.Add(chapter);
            var d = Campaign.Get(chapter).discovery;
            if (!discoveries.Contains(d)) discoveries.Add(d);
            highestUnlocked = Mathf.Max(highestUnlocked, chapter + 1);
            Save();
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogError($"[Event Horizon] Could not save profile: {e.Message}"); }
        }

        /// <summary>Wipes campaign progress (used by "reset" options).</summary>
        public static void Reset()
        {
            s_current = new Profile();
            s_current.Save();
        }
    }
}
