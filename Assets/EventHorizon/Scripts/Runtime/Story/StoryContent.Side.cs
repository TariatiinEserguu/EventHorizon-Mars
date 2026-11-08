using EventHorizon.Colony;
using EventHorizon.Game;
using UnityEngine;

namespace EventHorizon.Story
{
    public static partial class StoryContent
    {
        /// <summary>
        /// Optional missions: colonists' personal requests, exploration, measurements, deep-space observations and
        /// emergencies with a time limit. Each teaches something and pays out research points, upgrades, database
        /// entries or relationships.
        /// </summary>
        static void BuildSideQuests()
        {
            QuestDef q;
            Obj o;

            // ---------------------------------------------------------------- colonist stories
            q = Add(new QuestDef { id = "S_ROVER", category = QuestCategory.Colony, giver = Character.Ortiz, prerequisites = new[] { "C01" },
                reward = R(10, rel: new[] { (Character.Ortiz, 15) }, rep: new[] { (Axis.Community, 5) }) });
            o = new Obj(q);
            o.Talk("s_rover_talk", Character.Ortiz, "S_ROVER_TALK");
            o.Use("s_rover_diag", "rover_fix", experiment: "rover");
            o.Use("s_rover_fix", "rover_fix");
            o.OpenTerminal("s_rover_report", TerminalType.Command);

            q = Add(new QuestDef { id = "S_NOTES", category = QuestCategory.Colony, giver = Character.Chen, prerequisites = new[] { "C02" },
                reward = R(15, codex: new[] { "chen_notes" }, rel: new[] { (Character.Chen, 10) }, rep: new[] { (Axis.Science, 5) }) });
            o = new Obj(q);
            o.Talk("s_notes_talk", Character.Chen, "S_NOTES_TALK");
            o.Use("s_notes_find", "notes_drive");
            o.Talk("s_notes_return", Character.Chen, "S_NOTES_RETURN");

            q = Add(new QuestDef { id = "S_CROP", category = QuestCategory.Colony, giver = Character.Okafor, prerequisites = new[] { "C04" },
                reward = R(15, rel: new[] { (Character.Okafor, 10) }, rep: new[] { (Axis.Community, 5) }) });
            o = new Obj(q);
            o.Talk("s_crop_talk", Character.Okafor, "S_CROP_TALK");
            o.Use("s_crop_check", "gh_testbed", experiment: "cropsave");
            o.Talk("s_crop_report", Character.Okafor, "S_CROP_REPORT");

            q = Add(new QuestDef { id = "S_RADIO", category = QuestCategory.Colony, giver = Character.Park, prerequisites = new[] { "C05" },
                reward = R(10, rel: new[] { (Character.Park, 10) }, rep: new[] { (Axis.Community, 5) }) });
            o = new Obj(q);
            o.Talk("s_radio_talk", Character.Park, "S_RADIO_TALK");
            o.Use("s_radio_fix", "hab_mast");
            o.OpenTerminal("s_radio_test", TerminalType.Command);

            q = Add(new QuestDef { id = "S_GLITCH", category = QuestCategory.Colony, giver = Character.Park, prerequisites = new[] { "C06" },
                introDialogue = "S_GLITCH_INTRO", onComplete = () => Story.SetFlag("archive_2"),
                reward = R(15, codex: new[] { "ghost_relay" }, rel: new[] { (Character.Park, 5) }, rep: new[] { (Axis.Science, 5) }) });
            o = new Obj(q);
            o.Use("s_glitch_inspect", "dish_drive");
            o.Terminal("s_glitch_logs", TerminalType.Observatory, "glitch");
            o.Talk("s_glitch_report", Character.Park, "S_GLITCH_REPORT");

            // ---------------------------------------------------------------- exploration
            q = Add(new QuestDef { id = "E_LANDING", category = QuestCategory.Exploration, giver = Character.Reyes, prerequisites = new[] { "PRO" },
                reward = R(10, codex: new[] { "ares0" }, rel: new[] { (Character.Reyes, 10) }, rep: new[] { (Axis.Exploration, 5) }) });
            o = new Obj(q);
            o.Reach("e_landing_reach", "ares0_site");
            o.Use("e_landing_inspect", "ares0_lander");
            o.Talk("e_landing_reyes", Character.Reyes, "E_LANDING_REYES");

            q = Add(new QuestDef { id = "E_RECORDS", category = QuestCategory.Exploration, giver = Character.Reyes, prerequisites = new[] { "E_LANDING", "C03" },
                reward = R(20, codex: new[] { "ares0_history" }, rel: new[] { (Character.Reyes, 5) }, rep: new[] { (Axis.Exploration, 5) }) });
            o = new Obj(q);
            o.Use("e_records_1", "rec_1");
            o.Use("e_records_2", "rec_2");
            o.Use("e_records_3", "rec_3");

            q = Add(new QuestDef { id = "E_MINERALS", category = QuestCategory.Exploration, giver = Character.Erdene, prerequisites = new[] { "C01" },
                reward = R(15, codex: new[] { "minerals" }, rep: new[] { (Axis.Exploration, 5), (Axis.Science, 3) }) });
            o = new Obj(q);
            o.Use("e_minerals_1", "min_1");
            o.Use("e_minerals_2", "min_2");
            o.Use("e_minerals_3", "min_3");
            o.Terminal("e_minerals_id", TerminalType.Command, "minerals");

            q = Add(new QuestDef { id = "E_EQUIPMENT", category = QuestCategory.Exploration, giver = Character.Ortiz, prerequisites = new[] { "C02" },
                reward = R(15, codex: new[] { "marsquakes" }, rep: new[] { (Axis.Exploration, 3), (Axis.Science, 3) }) });
            o = new Obj(q);
            o.Use("e_equip_data", "seis");
            o.Use("e_equip_quake", "seis", experiment: "quake");

            q = Add(new QuestDef { id = "E_CAVE", category = QuestCategory.Exploration, giver = Character.Erdene, prerequisites = new[] { "C03" },
                reward = R(15, codex: new[] { "lava_tubes" }, rep: new[] { (Axis.Exploration, 5) }) });
            o = new Obj(q);
            o.Reach("e_cave_reach", "skylight");
            o.Use("e_cave_probe", "skylight_probe");
            o.Terminal("e_cave_analyze", TerminalType.Command, "cave");

            q = Add(new QuestDef { id = "E_MOONS", category = QuestCategory.Exploration, giver = Character.Chen, prerequisites = new[] { "C07" },
                reward = R(10, codex: new[] { "moons" }, rep: new[] { (Axis.Exploration, 5) }) });
            o = new Obj(q);
            o.Target("e_moons_phobos", Scenes.MarsOrbit, "Moon_Phobos", "ACTION_CAPTURE", "photo_phobos");
            o.Target("e_moons_deimos", Scenes.MarsOrbit, "Moon_Deimos", "ACTION_CAPTURE", "photo_deimos");

            // ---------------------------------------------------------------- research
            q = Add(new QuestDef { id = "R_GRAVITY", category = QuestCategory.Research, giver = Character.Chen, prerequisites = new[] { "PRO" },
                reward = R(10, codex: new[] { "pendulum" }, rep: new[] { (Axis.Science, 3) }) });
            new Obj(q).Use("r_gravity_measure", "pendulum", experiment: "gravity");

            q = Add(new QuestDef { id = "R_SOLAR", category = QuestCategory.Research, giver = Character.Erdene, prerequisites = new[] { "C01" },
                reward = R(10, codex: new[] { "dust" }, rep: new[] { (Axis.Science, 3) }) });
            new Obj(q).Terminal("r_solar_compare", TerminalType.Solar, "solarcmp");

            q = Add(new QuestDef { id = "R_CO2", category = QuestCategory.Research, giver = Character.Erdene, prerequisites = new[] { "C02" },
                reward = R(10, rep: new[] { (Axis.Science, 3) }) });
            new Obj(q).Use("r_co2_sample", "o2_intake", experiment: "co2");

            q = Add(new QuestDef { id = "R_PLANTS", category = QuestCategory.Research, giver = Character.Okafor, prerequisites = new[] { "C04" },
                reward = R(10, rel: new[] { (Character.Okafor, 3) }, rep: new[] { (Axis.Science, 3) }) });
            new Obj(q).Terminal("r_plants_study", TerminalType.Greenhouse, "plants");

            q = Add(new QuestDef { id = "R_FILTER", category = QuestCategory.Research, giver = Character.Erdene, prerequisites = new[] { "C05" },
                reward = R(10, rep: new[] { (Axis.Science, 3) }) });
            new Obj(q).Terminal("r_filter_test", TerminalType.Recycling, "filter");

            q = Add(new QuestDef { id = "R_ORBITS", category = QuestCategory.Research, giver = Character.Chen, prerequisites = new[] { "C08" },
                reward = R(15, codex: new[] { "precession" }, rep: new[] { (Axis.Science, 5) }) });
            new Obj(q).Event("r_orbits_run", "orbit_precession").In(Scenes.BlackHoleLab);

            // ---------------------------------------------------------------- deep space (Observatory console, after chapter 7)
            q = Add(new QuestDef { id = "DS_BINARY", category = QuestCategory.Side, giver = Character.Chen, prerequisites = new[] { "C07" },
                requiredTech = "astro_adv", introDialogue = "DS_INTRO",
                reward = R(15, codex: new[] { "binary_stars" }, rep: new[] { (Axis.Science, 3), (Axis.Exploration, 3) }) });
            new Obj(q).Terminal("ds_binary_obs", TerminalType.Observatory, "binary");

            q = Add(new QuestDef { id = "DS_ASTEROID", category = QuestCategory.Side, giver = Character.Park, prerequisites = new[] { "C07" }, requiredTech = "astro_adv",
                reward = R(15, codex: new[] { "asteroids" }, rep: new[] { (Axis.Exploration, 5) }) });
            new Obj(q).Terminal("ds_asteroid_track", TerminalType.Observatory, "asteroid");

            q = Add(new QuestDef { id = "DS_NEUTRON", category = QuestCategory.Side, giver = Character.Chen, prerequisites = new[] { "C07" }, requiredTech = "astro_adv",
                reward = R(15, codex: new[] { "neutron_stars" }, rep: new[] { (Axis.Science, 5) }) });
            new Obj(q).Terminal("ds_neutron_obs", TerminalType.Observatory, "pulsar");

            q = Add(new QuestDef { id = "DS_LENSING", category = QuestCategory.Side, giver = Character.Chen, prerequisites = new[] { "C08" }, requiredTech = "astro_adv",
                reward = R(15, codex: new[] { "einstein_ring" }, rep: new[] { (Axis.Science, 5) }) });
            new Obj(q).Terminal("ds_lensing_obs", TerminalType.Observatory, "microlens");

            q = Add(new QuestDef { id = "DS_ACCRETION", category = QuestCategory.Side, giver = Character.Chen, prerequisites = new[] { "C08" }, requiredTech = "astro_adv",
                reward = R(15, codex: new[] { "accretion" }, rep: new[] { (Axis.Science, 5) }) });
            new Obj(q).Terminal("ds_accretion_obs", TerminalType.Observatory, "accretion");

            q = Add(new QuestDef { id = "DS_TELESCOPE", category = QuestCategory.Side, giver = Character.Park, prerequisites = new[] { "C07" }, requiredTech = "astro_adv",
                reward = R(15, codex: new[] { "mro" }, rep: new[] { (Axis.Exploration, 3), (Axis.Science, 3) }) });
            o = new Obj(q);
            o.Target("ds_tel_downlink", Scenes.MarsOrbit, "Orbiter_MRO", "ACTION_DOWNLINK", "downlink_mro");
            o.Terminal("ds_tel_analyze", TerminalType.Observatory, "telescope");

            // ---------------------------------------------------------------- emergencies (fail if the timer runs out)
            q = Add(new QuestDef { id = "EM_STORM", category = QuestCategory.Emergency, giver = Character.Park, prerequisites = new[] { "C01" },
                timeLimitSols = 1.5f, introDialogue = "EM_STORM_INTRO",
                onFail = () => { var g = ColonyGrid.Instance; if (g != null) g.SetAmount(ResourceType.Food, Mathf.Max(0f, g.Amount(ResourceType.Food) - 30f)); StoryState.Add(Story.State.reputation, Axis.Community, -5); },
                reward = R(15, rep: new[] { (Axis.Community, 5) }) });
            o = new Obj(q);
            o.Custom("em_storm_battery", () => Battery(0.7f), Waypoints.Battery);
            o.Event("em_storm_clean", "panel_cleaned", 4, Waypoints.DirtyPanel);

            q = Add(new QuestDef { id = "EM_LEAK", category = QuestCategory.Emergency, giver = Character.Sorensen, prerequisites = new[] { "C04" },
                timeLimitSols = 0.5f, introDialogue = "EM_LEAK_INTRO",
                onFail = () => { var g = ColonyGrid.Instance; if (g != null) g.SetAmount(ResourceType.Oxygen, Mathf.Max(0f, g.Amount(ResourceType.Oxygen) - 40f)); StoryState.Add(Story.State.relationships, Character.Reyes.ToString(), -5); },
                reward = R(10, rel: new[] { (Character.Sorensen, 5) }, rep: new[] { (Axis.Community, 5) }) });
            new Obj(q).Use("em_leak_patch", "hab_leak");
        }
    }
}
