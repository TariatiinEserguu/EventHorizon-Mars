using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EventHorizon.Astronaut;
using EventHorizon.Colony;
using EventHorizon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventHorizon.Game
{
    /// <summary>A save slot as listed in the Load screen.</summary>
    public class SaveSlotInfo
    {
        public int slot;
        public SaveData data;
        public string thumbnailPath;
        public DateTime savedAt;
        public bool Exists => data != null;
    }

    /// <summary>
    /// Save files live in Application.persistentDataPath/saves/ (Windows: %USERPROFILE%\AppData\LocalLow\...,
    /// Linux: ~/.config/unity3d/...) as slot_N.json plus a slot_N.png thumbnail. Slot 0 is the sandbox.
    /// </summary>
    public static class SaveSystem
    {
        public const int SandboxSlot = 0;
        public const int SlotCount = 8;

        static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        static string JsonPath(int slot) => Path.Combine(Dir, $"slot_{slot}.json");
        static string PngPath(int slot) => Path.Combine(Dir, $"slot_{slot}.png");

        public static List<SaveSlotInfo> ListCampaignSlots()
        {
            var list = new List<SaveSlotInfo>();
            for (int i = 1; i <= SlotCount; i++) list.Add(Read(i));
            return list;
        }

        public static SaveSlotInfo Read(int slot)
        {
            var info = new SaveSlotInfo { slot = slot, thumbnailPath = PngPath(slot) };
            try
            {
                if (File.Exists(JsonPath(slot)))
                {
                    info.data = JsonUtility.FromJson<SaveData>(File.ReadAllText(JsonPath(slot)));
                    DateTime.TryParse(info.data.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out info.savedAt);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Event Horizon] Save slot {slot} is unreadable: {e.Message}");
                info.data = null;
            }
            return info;
        }

        /// <summary>The newest campaign save, for the Continue button.</summary>
        public static SaveSlotInfo MostRecent()
        {
            SaveSlotInfo best = null;
            foreach (var s in ListCampaignSlots())
                if (s.Exists && (best == null || s.savedAt > best.savedAt)) best = s;
            return best;
        }

        public static bool Save(int slot, string saveName)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var state = GameSession.State;
                WorldState.Capture(state);
                state.saveName = string.IsNullOrWhiteSpace(saveName) ? Loc.T("SAVE_DEFAULT_NAME", state.sol, state.missionTitle) : saveName.Trim();
                state.scene = SceneManager.GetActiveScene().name;
                GameSession.MarkSaved();
                state.savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                state.FlattenCounters();
                File.WriteAllText(JsonPath(slot), JsonUtility.ToJson(state, true));
                CaptureThumbnail(PngPath(slot));
                GameSession.ActiveSlot = slot;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Event Horizon] Save failed: {e}");
                return false;
            }
        }

        public static void Rename(int slot, string newName)
        {
            var info = Read(slot);
            if (!info.Exists || string.IsNullOrWhiteSpace(newName)) return;
            info.data.saveName = newName.Trim();
            File.WriteAllText(JsonPath(slot), JsonUtility.ToJson(info.data, true));
        }

        public static void Delete(int slot)
        {
            if (File.Exists(JsonPath(slot))) File.Delete(JsonPath(slot));
            if (File.Exists(PngPath(slot))) File.Delete(PngPath(slot));
        }

        public static Texture2D LoadThumbnail(SaveSlotInfo info)
        {
            if (info == null || !File.Exists(info.thumbnailPath)) return null;
            var tex = new Texture2D(2, 2);
            return tex.LoadImage(File.ReadAllBytes(info.thumbnailPath)) ? tex : null;
        }

        /// <summary>Renders the main camera (without UI) to a 320x180 PNG.</summary>
        static void CaptureThumbnail(string path)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rt = RenderTexture.GetTemporary(320, 180, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(320, 180, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }

    /// <summary>Copies live world state into a <see cref="SaveData"/> and back.</summary>
    public static class WorldState
    {
        public static void Capture(SaveData s)
        {
            // Only the colony scene owns the colony's world state; saving in orbit or the lab keeps what was captured
            // when the player left the colony.
            if (SceneManager.GetActiveScene().name != Scenes.MarsColony)
            {
                s.missionTitle = MissionTracker.CurrentTitle;
                return;
            }
            var env = MarsEnvironment.Instance;
            if (env != null)
            {
                s.sol = env.Sol;
                s.simTimeSeconds = env.SolClockSeconds;
                s.solarLongitude = env.solarLongitudeDeg;
                s.opticalDepth = env.opticalDepth;
            }
            var grid = ColonyGrid.Instance;
            if (grid != null)
            {
                s.oxygenKg = grid.Amount(ResourceType.Oxygen);
                s.waterKg = grid.Amount(ResourceType.Water);
                s.foodKg = grid.Amount(ResourceType.Food);
                s.batteryWh = grid.BatteryStoredWh;
                s.colonyDevelopment = ColonyStats.DevelopmentPercent(grid);
                s.population = ColonyStats.Population();
            }
            var player = UnityEngine.Object.FindAnyObjectByType<AstronautController>();
            if (player != null)
            {
                s.playerPosition = player.transform.position;
                s.playerYaw = player.Yaw;
                var suit = player.GetComponent<SuitLifeSupport>();
                if (suit != null) s.suitOxygenKg = suit.oxygenKg;
            }
            s.missionTitle = MissionTracker.CurrentTitle;
            s.hasWorldState = true;
        }

        public static void Apply(SaveData s)
        {
            var env = MarsEnvironment.Instance;
            if (env != null) env.SetClock(s.sol, s.simTimeSeconds, s.solarLongitude, s.opticalDepth);
            var grid = ColonyGrid.Instance;
            if (grid != null)
            {
                grid.SetAmount(ResourceType.Oxygen, s.oxygenKg);
                grid.SetAmount(ResourceType.Water, s.waterKg);
                grid.SetAmount(ResourceType.Food, s.foodKg);
                grid.SetBattery(s.batteryWh);
            }
            var player = UnityEngine.Object.FindAnyObjectByType<AstronautController>();
            if (player != null)
            {
                player.Teleport(s.playerPosition, s.playerYaw);
                var suit = player.GetComponent<SuitLifeSupport>();
                if (suit != null) suit.oxygenKg = s.suitOxygenKg;
            }
        }
    }

    public static class ColonyStats
    {
        public static int Population()
        {
            int n = 0;
            foreach (var c in UnityEngine.Object.FindObjectsByType<CrewLifeSupport>()) n += c.crew;
            return n;
        }

        /// <summary>A rough "how built-up and healthy is the colony" score for save slots and the command center.</summary>
        public static float DevelopmentPercent(ColonyGrid grid)
        {
            int panels = UnityEngine.Object.FindObjectsByType<SolarPanel>().Length;
            int beds = UnityEngine.Object.FindObjectsByType<Agriculture.HydroponicBed>().Length;
            float infra = Mathf.Clamp01(panels / 40f) * 40f + Mathf.Clamp01(beds / 12f) * 20f;
            float stocks = 0f;
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                if (grid.Capacity(t) > 0f) stocks += grid.Amount(t) / grid.Capacity(t);
            return Mathf.Round(infra + stocks / 3f * 30f + grid.BatteryFraction * 10f);
        }
    }
}
