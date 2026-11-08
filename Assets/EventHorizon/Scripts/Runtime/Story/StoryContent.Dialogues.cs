using System;
using EventHorizon.Game;

namespace EventHorizon.Story
{
    public static partial class StoryContent
    {
        /// <summary>Builds a conversation; line keys are numbered D_&lt;id&gt;_01, _02 ... in order.</summary>
        sealed class DB
        {
            readonly DialogueDef _d;
            int _n;

            public DB(string id)
            {
                _d = new DialogueDef { id = id };
                s_dialogues[id] = _d;
            }

            public DB Radio() { _d.radio = true; return this; }
            public DB Cine() { _d.cinematic = true; _d.radio = true; return this; }

            public DB L(Character who, int count = 1)
            {
                for (int i = 0; i < count; i++)
                {
                    _n++;
                    _d.lines.Add(new DialogueLine { speaker = who, key = $"D_{_d.id}_{_n:00}" });
                }
                return this;
            }

            /// <summary>A line followed by <paramref name="count"/> choices (keys ..._C1, _C2 ...).</summary>
            public DB C(Character who, int count, Action<int> pick)
            {
                _n++;
                string key = $"D_{_d.id}_{_n:00}";
                var choices = new string[count];
                for (int i = 0; i < count; i++) choices[i] = $"{key}_C{i + 1}";
                _d.lines.Add(new DialogueLine { speaker = who, key = key, choices = choices, onChoice = pick });
                return this;
            }

            public DB Done(Action a) { _d.onComplete = a; return this; }
        }

        static void React(string dialogue) => DialoguePlayer.Play(dialogue);

        static void BuildDialogues()
        {
            const Character Reyes = Character.Reyes, Chen = Character.Chen, Nara = Character.Erdene, Amara = Character.Okafor,
                Jun = Character.Park, Rafael = Character.Ortiz, Lena = Character.Sorensen, Varga = Character.Varga,
                Halvorsen = Character.Halvorsen, Control = Character.Control, Me = Character.Player, Narrator = Character.Narrator;

            // ---------------------------------------------------------------- prologue
            new DB("PRO_LANDING").Cine().L(Control).L(Me).L(Control).L(Reyes).L(Me).L(Control);
            new DB("PRO_ARRIVED").Radio().L(Reyes, 2);
            new DB("PRO_HABITAT").L(Reyes, 3);
            new DB("PRO_REYES").L(Reyes).L(Me).L(Reyes, 3);
            new DB("PRO_DONE").Radio().L(Reyes, 2);

            // ---------------------------------------------------------------- 1
            new DB("C01_INTRO").Radio().L(Jun).L(Reyes, 2);
            new DB("C01_REACT_A").Radio().L(Amara);
            new DB("C01_REACT_B").Radio().L(Chen).L(Amara);
            new DB("C01_REACT_C").Radio().L(Nara);
            new DB("C01_DONE").Radio().L(Reyes, 2);

            // ---------------------------------------------------------------- 2
            new DB("C02_INTRO").Radio().L(Nara, 2);
            new DB("C02_NARA").L(Nara, 2).L(Me).L(Nara);
            new DB("C02_REPORT").L(Nara).C(Nara, 2, i =>
            {
                if (i == 0) Story.Decide("c02_report", "earth", new[] { (Axis.Earth, 5) }, new[] { (Nara, 0) });
                else Story.Decide("c02_report", "local", new[] { (Axis.Independence, 5) }, new[] { (Nara, 5) });
                React(i == 0 ? "C02_REPORT_A" : "C02_REPORT_B");
            });
            new DB("C02_REPORT_A").Radio().L(Nara);
            new DB("C02_REPORT_B").Radio().L(Nara);
            new DB("C02_DONE").Radio().L(Nara).L(Reyes);

            // ---------------------------------------------------------------- 3
            new DB("C03_INTRO").Radio().L(Nara, 2);
            new DB("C03_NARA").L(Nara, 4);
            new DB("C03_DATA").Radio().L(Nara, 2);
            new DB("C03_HALVORSEN").Radio().L(Halvorsen, 4);
            new DB("C03_DONE").Radio().L(Nara).L(Reyes);

            // ---------------------------------------------------------------- 4
            new DB("C04_INTRO").Radio().L(Amara, 2);
            new DB("C04_AMARA").L(Amara, 4);
            new DB("C04_DONE").Radio().L(Amara, 2);

            // ---------------------------------------------------------------- 5
            new DB("C05_INTRO").Radio().L(Nara, 2);
            new DB("C05_NARA").L(Nara, 3);
            new DB("C05_DONE").Radio().L(Nara).L(Amara);

            // ---------------------------------------------------------------- 6
            new DB("C06_INTRO").Radio().L(Reyes, 2);
            new DB("C06_REYES").L(Reyes, 4);
            new DB("C06_JUN").L(Jun, 2).C(Jun, 3, i =>
            {
                switch (i)
                {
                    case 0: Story.Decide("comms", "independent", new[] { (Axis.Independence, 5) }, new[] { (Jun, 10) }); break;
                    case 1: Story.Decide("comms", "isa", new[] { (Axis.Earth, 5) }, new[] { (Jun, -5) }); break;
                    default: Story.Decide("comms", "both", new[] { (Axis.Earth, 2), (Axis.Independence, 2) }, new[] { (Jun, 5) }); break;
                }
                React("C06_JUN_" + (char)('A' + i));
            });
            new DB("C06_JUN_A").Radio().L(Jun);
            new DB("C06_JUN_B").Radio().L(Jun);
            new DB("C06_JUN_C").Radio().L(Jun);
            new DB("C06_LENA").L(Lena, 3);
            new DB("C06_COUNCIL").L(Reyes).L(Nara).L(Chen).L(Jun).C(Reyes, 3, i =>
            {
                switch (i)
                {
                    case 0: Story.Decide("council", "independence", new[] { (Axis.Independence, 15) }, new[] { (Nara, 10), (Reyes, -3) }); break;
                    case 1: Story.Decide("council", "earth", new[] { (Axis.Earth, 15) }, new[] { (Jun, 10), (Reyes, 5) }); break;
                    default: Story.Decide("council", "science", new[] { (Axis.Science, 15) }, new[] { (Chen, 10) }); break;
                }
                React("C06_COUNCIL_" + (char)('A' + i));
            });
            new DB("C06_COUNCIL_A").Radio().L(Reyes);
            new DB("C06_COUNCIL_B").Radio().L(Reyes);
            new DB("C06_COUNCIL_C").Radio().L(Reyes);
            new DB("C06_DONE").Radio().L(Reyes);

            // ---------------------------------------------------------------- 7
            new DB("C07_INTRO").Radio().L(Chen, 2);
            new DB("C07_CHEN").L(Chen, 4);
            new DB("C07_DONE").Radio().L(Chen, 2);

            // ---------------------------------------------------------------- 8
            new DB("C08_INTRO").Radio().L(Jun, 2);
            new DB("C08_JUN").L(Jun, 2).L(Chen).L(Jun);
            new DB("C08_FRAGMENTS").Radio().L(Varga, 4);
            new DB("C08_SHARE").L(Reyes).C(Reyes, 2, i =>
            {
                if (i == 0) Story.Decide("c08_share", "now", new[] { (Axis.Earth, 10) }, new[] { (Reyes, 5) });
                else Story.Decide("c08_share", "verify", new[] { (Axis.Independence, 5), (Axis.Science, 5) }, new[] { (Chen, 5) });
                React(i == 0 ? "C08_SHARE_A" : "C08_SHARE_B");
            });
            new DB("C08_SHARE_A").Radio().L(Reyes);
            new DB("C08_SHARE_B").Radio().L(Reyes);
            new DB("C08_DONE").Radio().L(Chen, 2);

            // ---------------------------------------------------------------- 9
            new DB("C09_INTRO").Radio().L(Chen, 2);
            new DB("C09_LAB").Radio().L(Chen, 2);
            new DB("C09_DONE").Radio().L(Chen, 3);

            // ---------------------------------------------------------------- 10
            new DB("C10_INTRO").Radio().L(Reyes, 2);
            new DB("C10_REYES").L(Reyes, 4);
            new DB("C10_ARRIVE").Radio().L(Chen, 2);
            new DB("C10_VARGA").Radio().L(Varga, 4);

            // ---------------------------------------------------------------- side and emergency
            new DB("DS_INTRO").Radio().L(Chen);
            new DB("EM_STORM_INTRO").Radio().L(Jun).L(Reyes);
            new DB("EM_LEAK_INTRO").Radio().L(Lena);
            new DB("S_ROVER_TALK").L(Rafael, 3);
            new DB("S_NOTES_TALK").L(Chen, 2);
            new DB("S_NOTES_RETURN").L(Chen, 2);
            new DB("S_CROP_TALK").L(Amara, 2);
            new DB("S_CROP_REPORT").L(Amara, 2);
            new DB("S_RADIO_TALK").L(Jun, 2);
            new DB("S_GLITCH_INTRO").Radio().L(Jun);
            new DB("S_GLITCH_REPORT").L(Jun, 3);
            new DB("E_LANDING_REYES").L(Reyes, 3);

            // ---------------------------------------------------------------- everyday conversations (A: ch 0-3, B: 4-6, C: 7-10)
            foreach (var c in new[] { Reyes, Chen, Nara, Amara, Jun, Rafael, Lena })
                foreach (var phase in new[] { "A", "B", "C" })
                    new DB($"IDLE_{c.ToString().ToUpperInvariant()}_{phase}").L(c, 2);
            // ... and conversations that react to your decisions
            new DB("IDLE_OKAFOR_CUT").L(Amara, 2);
            new DB("IDLE_REYES_HALVORSEN").L(Reyes, 2);
            new DB("IDLE_ERDENE_INDEP").L(Nara, 2);
            new DB("IDLE_CHEN_SCIENCE").L(Chen, 2);
            new DB("IDLE_PARK_EARTH").L(Jun, 2);
            new DB("IDLE_PARK_RELAY").L(Jun, 2);
            new DB("IDLE_REYES_SHARE").L(Reyes, 2);
            new DB("IDLE_REYES_VERIFY").L(Reyes, 2);

            // ---------------------------------------------------------------- endings
            foreach (var e in Endings.All) new DB("END_" + e).Cine().L(Narrator, 7);
        }

        /// <summary>What a colonist talks about when no mission needs them: reactions first, then the chapter.</summary>
        static string IdleDialogue(Character c)
        {
            var s = S;
            int ch = GameSession.State.chapter;
            switch (c)
            {
                case Character.Okafor when ch <= 4 && s.Decision("power_priority") is string p && p != "greenhouse": return "IDLE_OKAFOR_CUT";
                case Character.Reyes when s.Has("archive_1") && ch >= 3 && ch <= 6: return "IDLE_REYES_HALVORSEN";
                case Character.Reyes when ch >= 9 && s.Decision("c08_share") == "now": return "IDLE_REYES_SHARE";
                case Character.Reyes when ch >= 9 && s.Decision("c08_share") == "verify": return "IDLE_REYES_VERIFY";
                case Character.Erdene when ch >= 7 && s.Decision("council") == "independence": return "IDLE_ERDENE_INDEP";
                case Character.Chen when ch >= 7 && s.Decision("council") == "science": return "IDLE_CHEN_SCIENCE";
                case Character.Park when ch >= 7 && s.Decision("council") == "earth": return "IDLE_PARK_EARTH";
                case Character.Park when ch >= 7 && s.Decision("comms") == "independent": return "IDLE_PARK_RELAY";
            }
            string phase = ch <= 3 ? "A" : ch <= 6 ? "B" : "C";
            string id = $"IDLE_{c.ToString().ToUpperInvariant()}_{phase}";
            return s_dialogues.ContainsKey(id) ? id : null;
        }
    }
}
