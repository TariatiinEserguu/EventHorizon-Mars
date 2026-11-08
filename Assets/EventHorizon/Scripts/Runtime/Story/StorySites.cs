using UnityEngine;

namespace EventHorizon.Story
{
    /// <summary>A place in the colony scene the story uses. Positions are on the ground (y is snapped by the builder).</summary>
    public class SiteDef
    {
        public string id;
        /// <summary>marker, console, panel, valve, pump, scanner, drive, wreck, pit, outcrop, seismometer, pendulum, cache, door.</summary>
        public string kind;
        public Vector3 pos;
        public float yaw;
        public float work;
        public float radius = 10f;
        public bool always;
    }

    /// <summary>
    /// Every story location, shared by the scene builder (which places a prop and a <see cref="StoryInteractable"/> or
    /// <see cref="StoryMarker"/> at each) and the quests (which point waypoints at them).
    /// </summary>
    public static class StorySites
    {
        static SiteDef S(string id, string kind, float x, float z, float work = 0f, float yaw = 0f, float radius = 10f, bool always = false) =>
            new() { id = id, kind = kind, pos = new Vector3(x, 0f, z), work = work, yaw = yaw, radius = radius, always = always };

        public static readonly SiteDef[] All =
        {
            // Prologue: the landing area
            S("pro_suitport", "console", -36f, -39f, 0f, 210f),
            S("pro_beacon", "marker", -28f, -58f, radius: 6f),
            S("pro_rim", "marker", -72f, -66f, radius: 8f),
            S("hab_airlock", "door", 1.8f, 18.6f, 1.2f, 180f),
            S("lander", "console", -36.5f, -46.5f, 0f, 240f),

            // 1: the solar farm
            S("farm_gate", "marker", 46f, -13f, radius: 9f),
            S("inverter", "console", 34.5f, -27f, 3f, 90f),

            // 2: the oxygen plant
            S("o2_intake", "scanner", 20.5f, 9.5f, 2f),
            S("o2_filter", "panel", 19.5f, 2.6f, 4f, 180f),
            S("o2_valve", "valve", 21f, 14.5f, 3f),
            S("hab_o2_line", "valve", 5.8f, 19.2f, 3f),

            // 3: the abandoned survey station and the ice field
            S("survey", "marker", -175f, 150f, radius: 16f),
            S("survey_core", "console", -171f, 146f, 3f, 200f),
            S("survey_console", "console", -178.5f, 145.5f, 0f, 160f),
            S("scan_a", "scanner", -158f, 171f, 2f),
            S("scan_b", "scanner", -193f, 169f, 2f),
            S("scan_c", "scanner", -187f, 127f, 2f),
            S("ares0_log", "wreck", -203f, 118f, 2f, 35f),

            // 4: greenhouse
            S("gh_pump", "pump", -20.5f, 11f, 4f, 90f),
            S("gh_testbed", "panel", -22f, 14.5f, 2f, 90f),

            // 5: recycling facility
            S("rec_tank", "scanner", 10f, 32f, 2f),
            S("rec_membrane", "panel", 14f, 30.5f, 4f, 180f),
            S("rec_bioreactor", "pump", 18.5f, 33f, 3f, 270f),
            S("rec_compactor", "panel", 16f, 37.5f, 3f),

            // 6: people of ARES
            S("hab_air", "panel", -6f, 22f, 4f, 90f),
            S("med_supplies", "console", -9f, 12.5f, 2f, 180f),

            // Side missions
            S("rover_fix", "panel", -5.5f, -14.5f, 3f, 60f),
            S("notes_drive", "drive", 134f, 82f, 1.5f),
            S("hab_mast", "panel", 3.5f, 30.5f, 3f),
            S("dish_drive", "panel", -21f, 37f, 2f, 200f),
            S("ares0_site", "marker", 160f, -170f, radius: 14f),
            S("ares0_lander", "wreck", 163f, -166f, 2f, 120f),
            S("skylight", "marker", -140f, -150f, radius: 12f),
            S("skylight_probe", "scanner", -133.5f, -145.5f, 3f),
            S("min_1", "outcrop", 70f, 110f, 2f),
            S("min_2", "outcrop", 96f, 126f, 2f),
            S("min_3", "outcrop", 58f, 142f, 2f),
            S("seis", "seismometer", 100f, 60f, 3f),
            S("rec_1", "cache", 156f, -175f, 1.5f),
            S("rec_2", "cache", -168f, 156f, 1.5f),
            S("rec_3", "cache", -147f, -141f, 1.5f),
            S("pendulum", "pendulum", -7.5f, 8f, 0f, 90f),
            S("hab_leak", "panel", 6.5f, 26f, 4f, 270f),
        };

        public static SiteDef Get(string id)
        {
            foreach (var s in All) if (s.id == id) return s;
            return null;
        }

        public static Vector3? Position(string id)
        {
            var s = Get(id);
            return s != null ? s.pos : (Vector3?)null;
        }
    }
}
