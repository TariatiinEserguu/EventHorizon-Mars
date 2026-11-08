using System.Collections.Generic;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// Plays conversations, radio calls and recordings with subtitles in the subtitle language. Face-to-face talks
    /// hold the player still (Space / E / click for the next line, Esc skips to the next choice); radio and
    /// recordings play over gameplay and advance on their own.
    ///
    /// Voice: if Resources/Voice/&lt;voice language&gt;/&lt;line key&gt; exists it plays and the line lasts as long as the
    /// clip. No recordings ship yet, so lines are timed by length (adjustable with Settings > Subtitle speed). The
    /// Subtitles setting hides text only while a recorded voice is speaking, so the story can always be followed.
    /// </summary>
    public class DialoguePlayer : MonoBehaviour
    {
        static DialoguePlayer s_instance;
        static readonly Queue<string> s_queue = new();

        UIDocument _doc;
        VisualElement _box, _portrait, _choices, _letterTop, _letterBottom;
        Label _initials, _speaker, _text, _hint;
        AudioSource _voice;

        DialogueDef _def;
        int _line;
        float _lineEnds;
        bool _waitingChoice;
        bool _blocking;

        public static bool IsPlaying => s_instance != null && s_instance._def != null;
        public static bool IsBlocking => IsPlaying && s_instance._blocking;

        public static void Play(string id)
        {
            if (string.IsNullOrEmpty(id) || !StoryContent.Dialogues.ContainsKey(id))
            {
                if (!string.IsNullOrEmpty(id)) Debug.LogWarning($"[Event Horizon] Unknown dialogue {id}");
                return;
            }
            if (StoryTest.Running) { StoryTest.RunDialogue(StoryContent.Dialogues[id]); return; }
            Ensure();
            if (s_instance._def != null && s_instance._def.id == id) return;
            if (s_queue.Contains(id)) return;
            s_queue.Enqueue(id);
            if (s_instance._def == null) s_instance.Next();
        }

        /// <summary>Stops everything (scene change, ending).</summary>
        public static void StopAll()
        {
            s_queue.Clear();
            if (s_instance != null) s_instance.Finish(skipCallbacks: true);
        }

        static void Ensure()
        {
            if (s_instance != null) return;
            var doc = UiRoot.Create("Dialogue", 35);
            s_instance = doc.gameObject.AddComponent<DialoguePlayer>();
            s_instance._doc = doc;
            s_instance.Build();
        }

        void Build()
        {
            var root = _doc.rootVisualElement;
            _letterTop = UiKit.Box(root, "eh-letterbox");
            _letterTop.style.top = 0;
            _letterBottom = UiKit.Box(root, "eh-letterbox");
            _letterBottom.style.bottom = 0;
            var wrap = UiKit.Box(root, "eh-subtitles");
            wrap.pickingMode = PickingMode.Ignore;
            _box = UiKit.Box(wrap, "eh-subtitle-box", "eh-row");
            _box.style.alignItems = Align.FlexStart;
            _portrait = UiKit.Box(_box, "eh-portrait");
            _initials = UiKit.Text(_portrait, "", "eh-portrait-initials");
            var col = UiKit.Box(_box, "eh-col", "eh-grow");
            _speaker = UiKit.Text(col, "", "eh-subtitle-speaker");
            _text = UiKit.Text(col, "", "eh-subtitle-text");
            _choices = UiKit.Box(col, "eh-col");
            _hint = UiKit.Text(col, "", "eh-subtitle-hint");
            _box.RegisterCallback<ClickEvent>(_ => { if (!_waitingChoice) Advance(); });
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            Show(false);
            Loc.Changed += Refresh;
        }

        void OnDestroy()
        {
            Loc.Changed -= Refresh;
            if (s_instance == this) { s_instance = null; s_queue.Clear(); }
            ModalState.Close(this);
        }

        void Show(bool on)
        {
            _box.parent.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            bool bars = on && _def != null && _def.cinematic;
            _letterTop.style.display = _letterBottom.style.display = bars ? DisplayStyle.Flex : DisplayStyle.None;
            // Above the letterbox bars during cinematics, otherwise low between the HUD panels.
            _box.parent.style.bottom = bars || Cinematic.IsPlaying ? 205 : 28;
        }

        void Next()
        {
            if (s_queue.Count == 0) { _def = null; Show(false); return; }
            var id = s_queue.Dequeue();
            _def = StoryContent.Dialogues[id];
            _line = -1;
            _blocking = !_def.radio && !_def.cinematic && GameSession.InGameplay;
            if (_blocking) ModalState.Open(this);
            Show(true);
            Advance();
        }

        void Advance()
        {
            if (_def == null) return;
            _line++;
            if (_line >= _def.lines.Count) { Finish(false); return; }
            ShowLine();
        }

        void ShowLine()
        {
            var line = _def.lines[_line];
            var c = line.speaker;
            string name = c == Character.Player ? GameSession.State.characterName : Loc.Sub($"CHAR_{c.ToString().ToUpperInvariant()}_NAME");
            _speaker.text = Loc.Upper(name);
            _initials.text = Initials(name);
            var color = CharacterColor(c);
            _portrait.style.backgroundColor = color * 0.55f;
            _box.style.borderLeftColor = color;
            _speaker.style.color = color;

            string text = Loc.Sub(line.key, GameSession.State.characterName, GameSession.State.callsign);
            var clip = line.voiced ? Resources.Load<AudioClip>($"Voice/{Loc.VoiceLanguage}/{line.key}") : null;
            bool subtitlesOn = SettingsService.Current.gameplay.subtitles;
            _text.text = clip != null && !subtitlesOn ? "" : text;
            _text.style.fontSize = 24f * Mathf.Clamp(SettingsService.Current.accessibility.subtitleSize, 0.75f, 1.75f);

            float duration = (2.2f + text.Length * 0.045f) * Mathf.Clamp(SettingsService.Current.gameplay.subtitleSpeed, 0.5f, 3f);
            if (clip != null)
            {
                _voice.Stop();
                _voice.clip = clip;
                _voice.volume = SettingsService.CategoryVolume(AudioCategory.Voice);
                _voice.Play();
                duration = clip.length + 0.35f;
            }
            _lineEnds = Time.unscaledTime + Mathf.Max(2.5f, duration);

            _choices.Clear();
            _waitingChoice = line.choices != null && line.choices.Length > 0;
            if (_waitingChoice)
            {
                for (int i = 0; i < line.choices.Length; i++)
                {
                    int pick = i;
                    var b = UiKit.Button(_choices, $"{i + 1}.  {Loc.Sub(line.choices[i])}", () => Choose(pick), "eh-choice-btn");
                    b.style.whiteSpace = WhiteSpace.Normal;
                }
                if (!_blocking) ModalState.Open(this); // a decision always needs the mouse
                GameInput.UpdateCursor();
            }
            _hint.text = _waitingChoice ? Loc.T("DLG_HINT_CHOICE") : _blocking ? Loc.T("DLG_HINT_NEXT") : "";
        }

        void Refresh()
        {
            if (_def != null && _line >= 0 && _line < _def.lines.Count) ShowLine();
        }

        void Choose(int index)
        {
            var line = _def.lines[_line];
            _waitingChoice = false;
            _choices.Clear();
            if (!_blocking) ModalState.Close(this);
            line.onChoice?.Invoke(index);
            Advance();
        }

        void Finish(bool skipCallbacks)
        {
            if (_def == null) return;
            var def = _def;
            _def = null;
            _voice.Stop();
            ModalState.Close(this);
            GameInput.UpdateCursor();
            Show(false);
            if (!skipCallbacks)
            {
                if (!Story.State.seenDialogues.Contains(def.id)) Story.State.seenDialogues.Add(def.id);
                GameEvents.Raise("dlg_" + def.id);
                def.onComplete?.Invoke();
                Story.NotifyChanged();
            }
            Next();
        }

        void Update()
        {
            if (_def == null || GameSession.Paused) return;
            if (_waitingChoice)
            {
                var kb = Keyboard.current;
                if (kb != null)
                    for (int i = 0; i < _def.lines[_line].choices.Length && i < 4; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame) { Choose(i); return; }
                return;
            }
            bool next = false, skip = false;
            var k = Keyboard.current;
            if (k != null)
            {
                next = k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame;
                skip = k.escapeKey.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null) { next |= pad.buttonSouth.wasPressedThisFrame; skip |= pad.startButton.wasPressedThisFrame; }

            if (skip && (_blocking || _def.cinematic)) { SkipToChoice(); return; }
            if ((next && _blocking) || Time.unscaledTime >= _lineEnds) Advance();
        }

        /// <summary>Esc: jump to the next decision, or end the conversation if there is none.</summary>
        void SkipToChoice()
        {
            for (int i = _line + 1; i < _def.lines.Count; i++)
                if (_def.lines[i].choices != null && _def.lines[i].choices.Length > 0) { _line = i; ShowLine(); return; }
            _line = _def.lines.Count;
            Finish(false);
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Replace("Dr.", "").Replace("Cmdr.", "").Trim().Split(' ');
            string s = "";
            foreach (var p in parts) if (p.Length > 0 && char.IsLetter(p[0]) && s.Length < 2) s += char.ToUpperInvariant(p[0]);
            return s.Length > 0 ? s : name.Substring(0, 1);
        }

        public static Color CharacterColor(Character c) => c switch
        {
            Character.Reyes => new Color(0.31f, 0.62f, 1f),
            Character.Chen => new Color(0.65f, 0.55f, 0.98f),
            Character.Erdene => new Color(1f, 0.54f, 0.24f),
            Character.Okafor => new Color(0.42f, 0.8f, 0.47f),
            Character.Park => new Color(1f, 0.82f, 0.4f),
            Character.Ortiz => new Color(0.88f, 0.48f, 0.37f),
            Character.Sorensen => new Color(0.95f, 0.52f, 0.51f),
            Character.Varga => new Color(0.6f, 0.82f, 0.83f),
            Character.Halvorsen => new Color(0.69f, 0.66f, 0.73f),
            Character.Control => new Color(0.31f, 0.82f, 1f),
            Character.Player => new Color(0.9f, 0.96f, 0.98f),
            _ => new Color(0.8f, 0.8f, 0.8f),
        };
    }
}
