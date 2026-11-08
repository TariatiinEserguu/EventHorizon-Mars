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

        /// <summary>A story site (repair point, scan point, landmark) by id.</summary>
        public static Vector3? Site(string id)
        {
            foreach (var s in UnityEngine.Object.FindObjectsByType<Story.StoryInteractable>())
                if (s.siteId == id) return s.transform.position;
            foreach (var m in UnityEngine.Object.FindObjectsByType<Story.StoryMarker>())
                if (m.siteId == id) return m.transform.position;
            return Story.StorySites.Position(id);
        }

        public static Vector3? Person(Story.Character c)
        {
            foreach (var n in Story.Npc.All) if (n.character == c) return n.transform.position;
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

    /// <summary>Campaign progress shared by all save slots: unlocked chapters, discoveries and endings seen.</summary>
    [Serializable]
    public class Profile
    {
        public int version = 2;
        /// <summary>Highest chapter that can be started from Chapter Select (0 = prologue).</summary>
        public int highestUnlocked;
        public List<int> completed = new();
        public List<string> discoveries = new();
        public List<string> endings = new();

        static Profile s_current;
        static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        public static Profile Current
        {
            get
            {
                if (s_current != null) return s_current;
                try { s_current = File.Exists(FilePath) ? JsonUtility.FromJson<Profile>(File.ReadAllText(FilePath)) : null; }
                catch { s_current = null; }
                // Profiles from the first release counted chapters from 1 and had no prologue.
                if (s_current != null && s_current.version < 2) s_current = new Profile();
                return s_current ??= new Profile();
            }
        }

        public bool IsUnlocked(int chapter) => chapter <= highestUnlocked;
        public bool IsCompleted(int chapter) => completed.Contains(chapter);

        public void Unlock(int chapter)
        {
            if (chapter <= highestUnlocked) return;
            highestUnlocked = chapter;
            Save();
        }

        public void Complete(int chapter, string questId)
        {
            if (!completed.Contains(chapter)) completed.Add(chapter);
            highestUnlocked = Mathf.Max(highestUnlocked, chapter + 1);
            Save();
        }

        public void RecordEnding(string ending)
        {
            if (!endings.Contains(ending)) endings.Add(ending);
            Save();
        }

        public void Save()
        {
            if (Story.StoryTest.Running) return;
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogError($"[Event Horizon] Could not save profile: {e.Message}"); }
        }

        /// <summary>Wipes campaign progress (used by "reset" options).</summary>
        public static void Reset()
        {
            s_current = new Profile();
            s_current.Save();
        }

        /// <summary>For the story test: a throwaway profile that is never written to disk.</summary>
        public static void UseTemporary() => s_current = new Profile();
    }
}
