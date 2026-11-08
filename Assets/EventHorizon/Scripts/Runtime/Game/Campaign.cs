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
                    Count("c1_hab", "Walk to the habitat", "reached_habitat", 1),
                    Count("c1_o2", "Refill your suit at the oxygen tank", "suit_refilled", 1),
                    Count("c1_dish", "Inspect the relay dish", "inspect_relay", 1),
                },
            },
            new Chapter
            {
                number = 2, title = "POWER OF THE SUN", artwork = "sun",
                summary = "Mars receives less than half of Earth's sunlight, and dust steals more of it every sol. Keep the solar field alive.",
                discovery = "Solar irradiance at 1.52 AU, dust optical depth and why panels lose output every sol.",
                objectives = new[]
                {
                    Count("c2_clean", "Clean 3 solar panels", "panel_cleaned", 3),
                    Count("c2_term", "Check the solar power terminal", "terminal_solar", 1),
                    new Objective { id = "c2_batt", text = "Charge the batteries above 80%",
                        progress = () => Colony.ColonyGrid.Instance != null ? Mathf.Clamp01(Colony.ColonyGrid.Instance.BatteryFraction / 0.8f) : 0f },
                },
            },
            new Chapter
            {
                number = 3, title = "BREATH OF LIFE", artwork = "mars",
                summary = "Bring the MOXIE-derived oxygen plant online and build an oxygen reserve for the crew.",
                discovery = "Solid-oxide electrolysis: pulling oxygen out of the carbon dioxide atmosphere.",
                objectives = new[]
                {
                    Count("c3_term", "Open the oxygen plant terminal", "terminal_oxygen", 1),
                    Stock("c3_o2", "Store 180 kg of oxygen", Colony.ResourceType.Oxygen, 180f),
                },
            },
            new Chapter
            {
                number = 4, title = "LIQUID GOLD", artwork = "moon",
                summary = "Water is fuel, air and food. Mine the buried ice sheet and fill the colony's tanks.",
                discovery = "Mid-latitude subsurface ice and how a closed loop recycles over 90% of water.",
                objectives = new[]
                {
                    Count("c4_ice", "Extract ice 4 times", "ice_extracted", 4),
                    Stock("c4_water", "Store 4,400 kg of water", Colony.ResourceType.Water, 4400f),
                },
            },
            new Chapter
            {
                number = 5, title = "THE LAST HARVEST", artwork = "earth",
                summary = "Supply ships are years apart. The greenhouse must feed the crew.",
                discovery = "Photosynthesis under LED light and how much growing area one person needs.",
                objectives = new[]
                {
                    Count("c5_harvest", "Harvest a crop", "harvest", 1),
                    Count("c5_term", "Check the greenhouse terminal", "terminal_greenhouse", 1),
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
