using System;
using System.Collections.Generic;
using EventHorizon.Game;
using UnityEngine;

namespace EventHorizon.Story
{
    public enum QuestCategory { Main, Side, Research, Colony, Exploration, Emergency }

    public enum Character { None, Player, Reyes, Chen, Erdene, Okafor, Park, Ortiz, Sorensen, Varga, Halvorsen, Control, Narrator }

    /// <summary>Reputation axes moved by decisions. Endings read them.</summary>
    public static class Axis
    {
        public const string Independence = "independence";
        public const string Earth = "earth";
        public const string Science = "science";
        public const string Exploration = "exploration";
        public const string Community = "community";
        public static readonly string[] All = { Independence, Earth, Science, Exploration, Community };
    }

    /// <summary>One task inside a quest. Text key: O_&lt;id&gt;, optional hint key: O_&lt;id&gt;_HINT.</summary>
    public class ObjectiveDef
    {
        public string id;
        public bool optional;
        public string scene = Scenes.MarsColony;
        /// <summary>Progress 0..1 from the live world and the story state.</summary>
        public Func<float> progress = () => 0f;
        public Func<Vector3?> waypoint;
        /// <summary>Hidden until this returns true (secret objectives).</summary>
        public Func<bool> visible;
        /// <summary>Event counters this objective counts, measured from the moment the quest starts.</summary>
        public string countedEvent;
        /// <summary>Story site used by this objective (its prompt only shows while the objective is open).</summary>
        public string site;
        /// <summary>Colonist to talk to and the conversation that completes the objective.</summary>
        public Character talkTo;
        public string dialogue;
        /// <summary>Experiment opened at the site, terminal or space panel.</summary>
        public string experiment;
        public Colony.TerminalType? terminal;
        /// <summary>Space scenes: the object to select (OrbitCamera target name) and the button label key.</summary>
        public string spaceTarget;
        public string actionKey;

        public string Key => "O_" + id;
        public string HintKey => "O_" + id + "_HINT";
        public bool IsVisible => visible == null || visible();
    }

    public class Reward
    {
        public int research;
        public string[] tech = Array.Empty<string>();
        public string[] items = Array.Empty<string>();
        public string[] upgrades = Array.Empty<string>();
        public string[] codex = Array.Empty<string>();
        public (Character who, int delta)[] relationships = Array.Empty<(Character, int)>();
        public (string axis, int delta)[] reputation = Array.Empty<(string, int)>();
        public bool IsEmpty => research == 0 && tech.Length == 0 && items.Length == 0 && upgrades.Length == 0 && codex.Length == 0;
    }

    /// <summary>
    /// A mission. Text keys are derived from the id: Q_&lt;id&gt;_TITLE, _DESC, _LEARN (science covered), _DEBRIEF.
    /// </summary>
    public class QuestDef
    {
        public string id;
        public QuestCategory category;
        /// <summary>Main story: chapter number (0 = prologue). -1 for side missions.</summary>
        public int chapter = -1;
        public Character giver = Character.Reyes;
        public Character[] characters = Array.Empty<Character>();
        public string[] prerequisites = Array.Empty<string>();
        public string requiredTech;
        public string[] requiredItems = Array.Empty<string>();
        public List<ObjectiveDef> objectives = new();
        public Reward reward = new();
        public string introDialogue, completeDialogue;
        public string artwork = "mars";
        /// <summary>Emergency missions: sols allowed before they fail (0 = no limit).</summary>
        public float timeLimitSols;
        /// <summary>Applied when the quest starts (sets up the problem in the world).</summary>
        public Action onStart;
        public Action onComplete;
        public Action onFail;

        public string TitleKey => $"Q_{id}_TITLE";
        public string DescKey => $"Q_{id}_DESC";
        public string LearnKey => $"Q_{id}_LEARN";
        public string DebriefKey => $"Q_{id}_DEBRIEF";
        public string FailKey => $"Q_{id}_FAIL";
        public bool IsMain => category == QuestCategory.Main;
    }

    // ------------------------------------------------------------------ dialogue

    public class DialogueLine
    {
        public Character speaker;
        public string key;
        /// <summary>Choice keys shown after this line; the index picked goes to <see cref="onChoice"/>.</summary>
        public string[] choices;
        public Action<int> onChoice;
        /// <summary>Optional voice clip, Resources/Voice/&lt;lang&gt;/&lt;key&gt;.</summary>
        public bool voiced = true;
    }

    /// <summary>A conversation, radio call or recording. Line keys: D_&lt;id&gt;_01, _02 ...; choices D_&lt;id&gt;_01_C1 ...</summary>
    public class DialogueDef
    {
        public string id;
        public List<DialogueLine> lines = new();
        /// <summary>Radio and recordings play over gameplay; face-to-face talks pause the player.</summary>
        public bool radio;
        public bool cinematic;
        public Action onComplete;
    }

    // ------------------------------------------------------------------ experiments

    /// <summary>
    /// A science task or puzzle in the experiment panel. Text keys: X_&lt;id&gt;_TITLE, _INTRO, _Q (question),
    /// _A1.. (options), _F1.. (feedback per option), _OK (what you learned), _UNIT (numeric answers).
    /// </summary>
    public class ExperimentDef
    {
        public enum Kind { Readout, Choice, Numeric, Sliders, Order, Checklist }

        public string id;
        public Kind kind;
        /// <summary>Live data shown above the task, already localized.</summary>
        public Func<string> readout;
        // Choice
        public int options;
        public int correct = -1;          // -1: every option is valid (a decision)
        public Action<int> onChoice;
        // Numeric
        public Func<double> answer;
        public double tolerance = 0.03;   // relative
        // Sliders
        public SliderDef[] sliders = Array.Empty<SliderDef>();
        public Func<float[], string> sliderReadout;
        public Func<float[], bool> goal;
        // Order: item keys X_<id>_I1.. in the correct order; shown shuffled
        public int items;
        // Checklist: item keys X_<id>_I1..; each needs a click; optional costs and a budget
        public float[] costs;
        public float budget;
        public Func<bool[], bool> checklistGoal;
        public Func<bool[], string> checklistReadout;

        public Action onSolved;
        public string TitleKey => $"X_{id}_TITLE";
        public string IntroKey => $"X_{id}_INTRO";
        public string QuestionKey => $"X_{id}_Q";
        public string OkKey => $"X_{id}_OK";
        public string UnitKey => $"X_{id}_UNIT";
        public string Option(int i) => $"X_{id}_A{i + 1}";
        public string Feedback(int i) => $"X_{id}_F{i + 1}";
        public string Item(int i) => $"X_{id}_I{i + 1}";
    }

    public class SliderDef
    {
        public string key;      // label key
        public float min, max, value;
        public Func<float, string> format;
    }

    // ------------------------------------------------------------------ saved state

    [Serializable]
    public class KeyInt
    {
        public string key;
        public int value;
        public KeyInt(string k, int v) { key = k; value = v; }
    }

    [Serializable]
    public class KeyString
    {
        public string key;
        public string value;
        public KeyString(string k, string v) { key = k; value = v; }
    }

    /// <summary>Everything the story remembers. Saved inside each <see cref="SaveData"/>.</summary>
    [Serializable]
    public class StoryState
    {
        public List<string> active = new();
        public List<string> completed = new();
        public List<string> failed = new();
        public string tracked = "";
        public List<KeyString> decisions = new();
        public List<KeyInt> reputation = new();
        public List<KeyInt> relationships = new();
        public List<KeyInt> baselines = new();   // "<quest>/<event>" -> counter value when the quest started
        public List<KeyInt> startSol = new();    // quest -> sol it started (emergency timers)
        public List<string> flags = new();
        public List<string> tech = new();
        public List<string> items = new();
        public List<string> upgrades = new();
        public List<string> codex = new();
        public List<string> seenDialogues = new();
        public int research;
        public string ending = "";

        public bool IsActive(string q) => active.Contains(q);
        public bool IsDone(string q) => completed.Contains(q);
        public bool Has(string flag) => flags.Contains(flag);
        public void Set(string flag) { if (!flags.Contains(flag)) flags.Add(flag); }

        public string Decision(string key)
        {
            foreach (var d in decisions) if (d.key == key) return d.value;
            return null;
        }

        public void Decide(string key, string value)
        {
            foreach (var d in decisions) if (d.key == key) { d.value = value; return; }
            decisions.Add(new KeyString(key, value));
        }

        public static int Get(List<KeyInt> list, string key)
        {
            foreach (var kv in list) if (kv.key == key) return kv.value;
            return 0;
        }

        public static void Add(List<KeyInt> list, string key, int delta, int min = -100, int max = 100)
        {
            foreach (var kv in list) if (kv.key == key) { kv.value = Mathf.Clamp(kv.value + delta, min, max); return; }
            list.Add(new KeyInt(key, Mathf.Clamp(delta, min, max)));
        }

        public static void SetValue(List<KeyInt> list, string key, int value)
        {
            foreach (var kv in list) if (kv.key == key) { kv.value = value; return; }
            list.Add(new KeyInt(key, value));
        }

        public int Rep(string axis) => Get(reputation, axis);
        public int Relation(Character c) => Get(relationships, c.ToString());
    }
}
