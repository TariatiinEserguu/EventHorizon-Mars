using EventHorizon.Colony;
using EventHorizon.Game;
using UnityEngine;

namespace EventHorizon.Story
{
    public static partial class StoryContent
    {
        /// <summary>
        /// The main story: a prologue and ten chapters. Each sets up a real problem in the colony simulation, mixes
        /// travel, repairs, diagnosis and analysis, and ends with a debrief, rewards and sometimes a decision.
        /// </summary>
        static void BuildMainQuests()
        {
            // ---------------------------------------------------------------- PROLOGUE: A NEW WORLD
            var q = Add(new QuestDef
            {
                id = "PRO", category = QuestCategory.Main, chapter = 0, giver = Character.Reyes, artwork = "mars",
                characters = new[] { Character.Reyes, Character.Control },
                introDialogue = "PRO_ARRIVED", completeDialogue = "PRO_DONE",
                reward = R(10, tech: new[] { "colony_ops" }, codex: new[] { "mars_gravity", "mars_pressure", "life_support" },
                    rel: new[] { (Character.Reyes, 5) }),
            });
            var o = new Obj(q);
            o.Use("pro_suit", "pro_suitport", experiment: "suit_check");
            o.Reach("pro_beacon", "pro_beacon");
            o.Reach("pro_rim", "pro_rim").Optional();
            o.Event("pro_controls", "interacted");
            o.Use("pro_enter", "hab_airlock", dialogue: "PRO_HABITAT");
            o.Talk("pro_meet", Character.Reyes, "PRO_REYES");
            o.OpenTerminal("pro_dashboard", TerminalType.Command);

            // ---------------------------------------------------------------- 1: THE FRAGILE LIGHT
            q = Add(new QuestDef
            {
                id = "C01", category = QuestCategory.Main, chapter = 1, giver = Character.Reyes, artwork = "sun",
                characters = new[] { Character.Reyes, Character.Park },
                prerequisites = new[] { "PRO" },
                introDialogue = "C01_INTRO", completeDialogue = "C01_DONE",
                onStart = () => { SetBattery(0.18f); DustPanels(0.35f, 0.7f); },
                reward = R(20, upgrades: new[] { "dust_coating" }, codex: new[] { "solar_power", "batteries" },
                    rel: new[] { (Character.Reyes, 5) }),
            });
            o = new Obj(q);
            o.Reach("c01_farm", "farm_gate");
            o.OpenTerminal("c01_eff", TerminalType.Solar);
            o.Event("c01_dust", "panel_cleaned", 6, Waypoints.DirtyPanel);
            o.Event("c01_wire", "panel_repaired", 5, FaultedPanel);
            o.Event("c01_track", "tracking_on", 1, () => Waypoints.Terminal(TerminalType.Solar));
            o.Use("c01_charge", "inverter");
            o.Terminal("c01_priority", TerminalType.Command, "priority");
            o.Custom("c01_full", () => Battery(0.6f), Waypoints.Battery).Optional();

            // ---------------------------------------------------------------- 2: THE BREATH OF A WORLD
            q = Add(new QuestDef
            {
                id = "C02", category = QuestCategory.Main, chapter = 2, giver = Character.Erdene, artwork = "mars",
                characters = new[] { Character.Erdene, Character.Reyes },
                prerequisites = new[] { "C01" },
                introDialogue = "C02_INTRO", completeDialogue = "C02_DONE",
                reward = R(25, upgrades: new[] { "moxie_stack" }, codex: new[] { "moxie", "mars_atmosphere" },
                    rel: new[] { (Character.Erdene, 10) }),
            });
            o = new Obj(q);
            o.Talk("c02_meet", Character.Erdene, "C02_NARA");
            o.Use("c02_intake", "o2_intake", experiment: "intake");
            o.Terminal("c02_diag", TerminalType.Oxygen, "o2diag");
            o.Use("c02_fix", "o2_filter");
            o.Terminal("c02_purity", TerminalType.Oxygen, "purity");
            o.Use("c02_store", "o2_valve");
            o.Use("c02_connect", "hab_o2_line");
            o.Talk("c02_report", Character.Erdene, "C02_REPORT");
            o.Stock("c02_reserve", ResourceType.Oxygen, 180f, Waypoints.OxygenPlant).Optional();

            // ---------------------------------------------------------------- 3: THE SILENT RESERVOIR
            q = Add(new QuestDef
            {
                id = "C03", category = QuestCategory.Main, chapter = 3, giver = Character.Erdene, artwork = "moon",
                characters = new[] { Character.Erdene, Character.Halvorsen },
                prerequisites = new[] { "C02" },
                introDialogue = "C03_INTRO", completeDialogue = "C03_DONE",
                onStart = () => CapStock(ResourceType.Water, 1800f),
                reward = R(30, tech: new[] { "underground_water" }, upgrades: new[] { "ice_drill" }, codex: new[] { "water_ice", "distillation" },
                    rel: new[] { (Character.Erdene, 5) }, rep: new[] { (Axis.Independence, 5) }),
            });
            o = new Obj(q);
            o.Talk("c03_brief", Character.Erdene, "C03_NARA");
            o.Reach("c03_find", "survey");
            o.Use("c03_data", "survey_core", dialogue: "C03_DATA");
            o.Use("c03_log", "ares0_log", dialogue: "C03_HALVORSEN").Optional();
            o.Custom("c03_scan", () => (Mathf.Min(1, Story.Since("C03", "use_scan_a")) + Mathf.Min(1, Story.Since("C03", "use_scan_b")) + Mathf.Min(1, Story.Since("C03", "use_scan_c"))) / 3f,
                () => Story.Since("C03", "use_scan_a") == 0 ? Waypoints.Site("scan_a") : Story.Since("C03", "use_scan_b") == 0 ? Waypoints.Site("scan_b") : Waypoints.Site("scan_c"));
            q.objectives[^1].countedEvent = "use_scan_a";
            o.Use("c03_site", "survey_console", experiment: "icesite");
            o.Event("c03_extract", "ice_extracted", 2, () => Waypoints.Site("scan_b"));
            o.Terminal("c03_process", TerminalType.Water, "purify");
            o.Stock("c03_return", ResourceType.Water, 2500f, () => Waypoints.Tank(ResourceType.Water));

            // ---------------------------------------------------------------- 4: THE GARDEN OF TOMORROW
            q = Add(new QuestDef
            {
                id = "C04", category = QuestCategory.Main, chapter = 4, giver = Character.Okafor, artwork = "earth",
                characters = new[] { Character.Okafor },
                prerequisites = new[] { "C03" },
                introDialogue = "C04_INTRO", completeDialogue = "C04_DONE",
                onStart = () =>
                {
                    var beds = Object.FindObjectsByType<Agriculture.HydroponicBed>();
                    if (beds.Length > 0) beds[0].SetGrowth(0.97f);
                },
                reward = R(25, upgrades: new[] { "led" }, codex: new[] { "photosynthesis", "hydroponics" },
                    rel: new[] { (Character.Okafor, 10) }, rep: new[] { (Axis.Community, 5) }),
            });
            o = new Obj(q);
            o.Talk("c04_meet", Character.Okafor, "C04_AMARA");
            o.Use("c04_pumps", "gh_pump");
            o.Terminal("c04_nutrients", TerminalType.Greenhouse, "nutrients");
            o.Terminal("c04_lights", TerminalType.Greenhouse, "lights");
            o.Terminal("c04_crops", TerminalType.Greenhouse, "crops");
            o.Terminal("c04_temp", TerminalType.Greenhouse, "climate");
            o.Terminal("c04_humid", TerminalType.Greenhouse, "humidity");
            o.Event("c04_harvest", "harvest", 1, Waypoints.Greenhouse);

            // ---------------------------------------------------------------- 5: NO WASTE
            q = Add(new QuestDef
            {
                id = "C05", category = QuestCategory.Main, chapter = 5, giver = Character.Erdene, artwork = "earth",
                characters = new[] { Character.Erdene, Character.Okafor },
                prerequisites = new[] { "C04" },
                introDialogue = "C05_INTRO", completeDialogue = "C05_DONE",
                reward = R(30, tech: new[] { "recycling_adv" }, upgrades: new[] { "recycling" }, codex: new[] { "closed_loop" },
                    rel: new[] { (Character.Erdene, 5), (Character.Okafor, 5) }, rep: new[] { (Axis.Independence, 5), (Axis.Community, 5) }),
            });
            o = new Obj(q);
            o.Talk("c05_brief", Character.Erdene, "C05_NARA");
            o.Use("c05_inspect", "rec_tank", experiment: "waste");
            o.Use("c05_filter", "rec_membrane");
            o.Use("c05_nutrients", "rec_bioreactor");
            o.Use("c05_waste", "rec_compactor");
            o.Terminal("c05_connect", TerminalType.Recycling, "routing");
            o.Terminal("c05_optimize", TerminalType.Recycling, "optimize");

            // ---------------------------------------------------------------- 6: THE PEOPLE OF ARES
            q = Add(new QuestDef
            {
                id = "C06", category = QuestCategory.Main, chapter = 6, giver = Character.Reyes, artwork = "earth",
                characters = new[] { Character.Reyes, Character.Park, Character.Sorensen, Character.Erdene, Character.Chen },
                prerequisites = new[] { "C05" },
                introDialogue = "C06_INTRO", completeDialogue = "C06_DONE",
                reward = R(20, codex: new[] { "governance" }, rel: new[] { (Character.Reyes, 5) }),
            });
            o = new Obj(q);
            o.Talk("c06_brief", Character.Reyes, "C06_REYES");
            o.Use("c06_res", "hab_air");
            o.Talk("c06_comms", Character.Park, "C06_JUN");
            o.Use("c06_pickup", "med_supplies");
            o.Talk("c06_deliver", Character.Sorensen, "C06_LENA");
            o.Custom("c06_requests", () => Story.State.completed.Exists(id => Story.Quest(id)?.category == QuestCategory.Colony) ? 1f : 0f).Optional();
            o.Use("c06_council", "hab_airlock", dialogue: "C06_COUNCIL");

            // ---------------------------------------------------------------- 7: THE EMPTY OBSERVATORY
            q = Add(new QuestDef
            {
                id = "C07", category = QuestCategory.Main, chapter = 7, giver = Character.Chen, artwork = "stars",
                characters = new[] { Character.Chen },
                prerequisites = new[] { "C06" },
                introDialogue = "C07_INTRO", completeDialogue = "C07_DONE",
                reward = R(40, tech: new[] { "astro_adv" }, codex: new[] { "microlensing", "observatory" },
                    rel: new[] { (Character.Chen, 10) }, rep: new[] { (Axis.Science, 5) }),
            });
            o = new Obj(q);
            o.Talk("c07_brief", Character.Chen, "C07_CHEN");
            o.Event("c07_launch", "arrived_orbit", 1, () => Waypoints.Site("lander")).site = "lander";
            o.Space("c07_power", Scenes.MarsOrbit, "obs_power");
            o.Space("c07_comms", Scenes.MarsOrbit, "obs_antenna");
            o.Space("c07_instruments", Scenes.MarsOrbit, "obs_instruments");
            o.Space("c07_archive", Scenes.MarsOrbit, "obs_archive");
            o.Space("c07_analyze", Scenes.MarsOrbit, "lensing1");
            o.Space("c07_classified", Scenes.MarsOrbit, "classified").Optional().When(() => Story.Flag("archive_1"));
            o.Event("c07_return", "arrived_colony").In(null);

            // ---------------------------------------------------------------- 8: THE LAST TRANSMISSION
            q = Add(new QuestDef
            {
                id = "C08", category = QuestCategory.Main, chapter = 8, giver = Character.Park, artwork = "stars",
                characters = new[] { Character.Park, Character.Chen, Character.Varga },
                prerequisites = new[] { "C07" },
                introDialogue = "C08_INTRO", completeDialogue = "C08_DONE",
                reward = R(50, tech: new[] { "lab_blackhole" }, codex: new[] { "radio_astronomy", "project_horizon" },
                    rel: new[] { (Character.Park, 10) }),
            });
            o = new Obj(q);
            o.Talk("c08_brief", Character.Park, "C08_JUN");
            o.Terminal("c08_fragments", TerminalType.Observatory, "signal");
            o.Terminal("c08_timeline", TerminalType.Observatory, "timeline");
            o.Terminal("c08_compare", TerminalType.Observatory, "compare");
            o.Event("c08_launch", "arrived_orbit", 1, () => Waypoints.Site("lander")).site = "lander";
            o.Target("c08_relay", Scenes.MarsOrbit, "Relay_Horizon", "ACTION_TRACK_RELAY", "found_relay");
            o.Space("c08_decode", Scenes.MarsOrbit, "decode");
            o.Space("c08_hidden", Scenes.MarsOrbit, "hidden").Optional().When(() => Story.Archives >= 2);
            o.Event("c08_return", "arrived_colony").In(null);
            o.Talk("c08_share", Character.Reyes, "C08_SHARE");

            // ---------------------------------------------------------------- 9: EVENT HORIZON
            q = Add(new QuestDef
            {
                id = "C09", category = QuestCategory.Main, chapter = 9, giver = Character.Chen, artwork = "blackhole",
                characters = new[] { Character.Chen },
                prerequisites = new[] { "C08" },
                introDialogue = "C09_INTRO", completeDialogue = "C09_DONE",
                reward = R(60, codex: new[] { "kerr", "lensing_theory", "pbh" }, rel: new[] { (Character.Chen, 10) }, rep: new[] { (Axis.Science, 10) }),
            });
            o = new Obj(q);
            o.Event("c09_open", "arrived_lab", 1, () => Waypoints.Terminal(TerminalType.Observatory)).terminal = TerminalType.Observatory;
            o.Custom("c09_calibrate", () => Space.BlackHoleLab.MassMatchesHorizon1 ? 1f : 0f, null, Scenes.BlackHoleLab);
            o.Custom("c09_configure", () => Space.BlackHoleLab.ConfiguredForHorizon1 ? 1f : 0f, null, Scenes.BlackHoleLab);
            o.Custom("c09_lensing", () => Space.BlackHoleLab.LensingStudyProgress, null, Scenes.BlackHoleLab);
            o.Space("c09_rs", Scenes.BlackHoleLab, "rs");
            o.Space("c09_bending", Scenes.BlackHoleLab, "bending");
            o.Space("c09_trajectory", Scenes.BlackHoleLab, "trajectory");
            o.Custom("c09_source", () => Space.BlackHoleLab.SpinMatchesSignal ? 1f : 0f, null, Scenes.BlackHoleLab);
            o.Event("c09_return", "arrived_colony").In(null);

            // ---------------------------------------------------------------- 10: BEYOND THE HORIZON
            q = Add(new QuestDef
            {
                id = "C10", category = QuestCategory.Main, chapter = 10, giver = Character.Reyes, artwork = "saturn",
                characters = new[] { Character.Reyes, Character.Chen, Character.Varga },
                prerequisites = new[] { "C09" },
                introDialogue = "C10_INTRO",
                reward = R(80, codex: new[] { "horizon_fate" }),
            });
            o = new Obj(q);
            o.Talk("c10_brief", Character.Reyes, "C10_REYES");
            o.Terminal("c10_upgrade", TerminalType.Command, "upgrade");
            o.Terminal("c10_lifesupport", TerminalType.Command, "provision");
            o.Terminal("c10_navigation", TerminalType.Command, "nav");
            o.Event("c10_launch", "expedition_launch", 1, () => Waypoints.Site("lander")).site = "lander";
            o.Event("c10_arrive", "reach_horizon1").In(Scenes.BlackHoleLab);
            o.Custom("c10_pods", () => (Mathf.Min(1, GameEvents.Count("exp_pod1")) + Mathf.Min(1, GameEvents.Count("exp_pod2")) + Mathf.Min(1, GameEvents.Count("exp_pod3"))) / 3f, null, Scenes.BlackHoleLab);
            o.Space("c10_reconstruct", Scenes.BlackHoleLab, "final");
            // Only appears with all four classified archives; then it is a decision the ending depends on.
            o.Space("c10_unknown", Scenes.BlackHoleLab, "ergo").When(() => Story.Archives >= 4);
        }
    }
}
