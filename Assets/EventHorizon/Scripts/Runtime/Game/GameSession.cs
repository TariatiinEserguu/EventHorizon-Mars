using System;
using System.Collections.Generic;
using EventHorizon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventHorizon.Game
{
    public enum GameMode { None, Campaign, Sandbox, Science }

    public static class Scenes
    {
        public const string MainMenu = "MainMenu";
        public const string MarsColony = "MarsColony";
        public const string SpaceLab = "SpaceLab";
        public const string GraphicsTest = "GraphicsTest";
    }

    /// <summary>Options on the Free Exploration screen.</summary>
    [Serializable]
    public class SandboxSettings
    {
        public float simulationSpeed = 120f;
        public float startingResources = 2f;   // multiplier on tank contents
        public bool infiniteSuitOxygen = true;
        public bool dustStorms = true;
        public int crew = 4;
    }

    /// <summary>One saved game. Also used as the live state of the running session.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string saveName = "New save";
        public string characterName = "Alex Rivera";
        public string callsign = "PATHFINDER";
        public string suitColor = "#E8E8E2";
        public Difficulty difficulty = Difficulty.Standard;
        public TutorialMode tutorial = TutorialMode.Basic;
        public GameMode mode = GameMode.Campaign;
        public SandboxSettings sandbox = new();
        public int chapter = 1;
        public string missionTitle = "";
        public int population = 4;
        public float colonyDevelopment;
        public double playtimeSeconds;
        public string savedAtUtc = "";
        public string scene = Scenes.MarsColony;

        public bool hasWorldState;
        public Vector3 playerPosition;
        public float playerYaw;
        public int sol;
        public double simTimeSeconds;
        public float solarLongitude;
        public float opticalDepth = 0.5f;
        public float oxygenKg, waterKg, foodKg, batteryWh, suitOxygenKg;
        public List<string> completedObjectives = new();
        public Dictionary<string, int> Counters => _counters ??= ParseCounters();
        public List<string> counterKeys = new();
        public List<int> counterValues = new();
        [NonSerialized] Dictionary<string, int> _counters;

        Dictionary<string, int> ParseCounters()
        {
            var d = new Dictionary<string, int>();
            for (int i = 0; i < counterKeys.Count && i < counterValues.Count; i++) d[counterKeys[i]] = counterValues[i];
            return d;
        }

        /// <summary>Copies the counter dictionary into the serialisable lists before writing JSON.</summary>
        public void FlattenCounters()
        {
            counterKeys.Clear();
            counterValues.Clear();
            foreach (var kv in Counters) { counterKeys.Add(kv.Key); counterValues.Add(kv.Value); }
        }
    }

    /// <summary>
    /// The running game: mode, character, chapter and the state to restore when a gameplay scene loads.
    /// </summary>
    public static class GameSession
    {
        public static GameMode Mode { get; private set; } = GameMode.None;
        public static SaveData State { get; private set; } = new();
        /// <summary>True when <see cref="State"/> came from a save file and its world state should be restored.</summary>
        public static bool RestoreWorld { get; set; }
        public static int ActiveSlot { get; set; } = -1;
        public static bool Paused { get; set; }
        public static bool HasUnsavedProgress { get; set; }

        static float s_sessionStart;

        public static bool InGameplay
        {
            get
            {
                if (Mode == GameMode.None) return SceneManager.GetActiveScene().name != Scenes.MainMenu && Application.isPlaying
                                                   && UnityEngine.Object.FindAnyObjectByType<Astronaut.AstronautController>() != null;
                return SceneManager.GetActiveScene().name != Scenes.MainMenu;
            }
        }

        public static double Playtime => State.playtimeSeconds + (Mode == GameMode.None ? 0 : Time.unscaledTime - s_sessionStart);

        public static void StartCampaign(SaveData character, int chapter)
        {
            State = character;
            State.mode = GameMode.Campaign;
            State.chapter = chapter;
            State.hasWorldState = false;
            State.completedObjectives.Clear();
            Begin(GameMode.Campaign, restore: false, slot: -1);
            SceneLoader.Load(Scenes.MarsColony, Campaign.Get(chapter).title);
        }

        public static void StartSandbox(SandboxSettings settings, SaveData existing = null)
        {
            State = existing ?? new SaveData { saveName = "Free Exploration", characterName = "Explorer", callsign = "NOMAD" };
            State.mode = GameMode.Sandbox;
            State.sandbox = settings;
            Begin(GameMode.Sandbox, restore: existing != null && existing.hasWorldState, slot: SaveSystem.SandboxSlot);
            SceneLoader.Load(Scenes.MarsColony, "Free Exploration");
        }

        public static void StartScience(string scene)
        {
            State = new SaveData { saveName = "Science Laboratory", characterName = "Researcher", callsign = "LAB" };
            State.mode = GameMode.Science;
            State.sandbox = new SandboxSettings { infiniteSuitOxygen = true, dustStorms = false };
            Begin(GameMode.Science, restore: false, slot: -1);
            SceneLoader.Load(scene, "Science Laboratory");
        }

        public static void LoadSave(SaveData data, int slot)
        {
            State = data;
            Begin(data.mode, restore: data.hasWorldState, slot: slot);
            SceneLoader.Load(string.IsNullOrEmpty(data.scene) ? Scenes.MarsColony : data.scene, data.saveName);
        }

        public static void ReturnToMenu()
        {
            State.playtimeSeconds = Playtime;
            Mode = GameMode.None;
            Paused = false;
            Time.timeScale = 1f;
            GameInput.ClearBlocks();
            SceneLoader.Load(Scenes.MainMenu, "Main menu");
        }

        static void Begin(GameMode mode, bool restore, int slot)
        {
            Mode = mode;
            RestoreWorld = restore;
            ActiveSlot = slot;
            Paused = false;
            HasUnsavedProgress = false;
            Time.timeScale = 1f;
            s_sessionStart = Time.unscaledTime;
            GameInput.ClearBlocks();
        }

        /// <summary>Resets the playtime clock after a save so it isn't counted twice.</summary>
        public static void MarkSaved()
        {
            State.playtimeSeconds = Playtime;
            s_sessionStart = Time.unscaledTime;
            HasUnsavedProgress = false;
        }
    }
}
