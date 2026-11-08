using System;
using System.Collections;
using EventHorizon.Astronaut;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>
    /// Camera sequences: the prologue landing (exterior and cockpit views), launches to orbit, arrivals, title cards
    /// and endings. Every sequence can be skipped with Space, Enter or Esc.
    /// </summary>
    public class Cinematic : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }

        UIDocument _doc;
        VisualElement _fade, _cockpit, _letterTop, _letterBottom;
        Label _readout, _skip;
        Camera _cam;
        AstronautController _player;
        bool _skipRequested;

        // ------------------------------------------------------------------ entry points

        /// <summary>Prologue: descent from orbit to the pad, then the player steps out.</summary>
        public static void Landing(Action done) => Run(c => c.LandingRoutine(full: true), done);

        /// <summary>Short touchdown after returning from orbit.</summary>
        public static void Arrival(Action done) => Run(c => c.LandingRoutine(full: false), done);

        /// <summary>Lift-off, then <paramref name="done"/> (usually a scene load).</summary>
        public static void Launch(Action done) => Run(c => c.LaunchRoutine(), done);

        /// <summary>Black card with a kicker, a title and a line of text, e.g. a time skip.</summary>
        public static void TitleCard(string kickerKey, string titleKey, string bodyKey, Action done) =>
            Run(c => c.TitleRoutine(kickerKey, titleKey, bodyKey), done);

        static void Run(Func<Cinematic, IEnumerator> routine, Action done)
        {
            if (IsPlaying || StoryTest.Running) { if (!IsPlaying) done?.Invoke(); return; }
            var doc = UiRoot.Create("Cinematic", 60);
            var c = doc.gameObject.AddComponent<Cinematic>();
            c._doc = doc;
            c.Build();
            c.StartCoroutine(c.Wrap(routine(c), done));
        }

        // ------------------------------------------------------------------ plumbing

        void Build()
        {
            var root = _doc.rootVisualElement;
            _cockpit = UiKit.Box(root, "eh-fill");
            _cockpit.pickingMode = PickingMode.Ignore;
            UiKit.Box(_cockpit, "eh-cockpit-frame").pickingMode = PickingMode.Ignore;
            _readout = UiKit.Text(_cockpit, "", "eh-mono", "eh-accent");
            _readout.style.position = Position.Absolute;
            _readout.style.left = 170;
            _readout.style.bottom = 40;
            _readout.style.fontSize = 22;
            _cockpit.style.display = DisplayStyle.None;
            _letterTop = UiKit.Box(root, "eh-letterbox");
            _letterTop.style.top = 0;
            _letterBottom = UiKit.Box(root, "eh-letterbox");
            _letterBottom.style.bottom = 0;
            _skip = UiKit.Text(root, Loc.T("CINE_SKIP"), "eh-mono", "eh-small", "eh-dim");
            _skip.style.position = Position.Absolute;
            _skip.style.right = 30;
            _skip.style.bottom = 20;
            _fade = UiKit.Box(root, "eh-fade");
        }

        IEnumerator Wrap(IEnumerator inner, Action done)
        {
            IsPlaying = true;
            ModalState.Open(this);
            Hud.Hidden = true;
            _cam = Camera.main;
            _player = FindAnyObjectByType<AstronautController>();
            if (_player != null) _player.enabled = false;
            yield return inner;
            if (_player != null) _player.enabled = true;
            Hud.Hidden = false;
            ModalState.Close(this);
            IsPlaying = false;
            GameInput.UpdateCursor();
            // Run the follow-up after cleaning up, so it can start another cinematic or load a scene. The overlay
            // stays black for a moment if the follow-up loads a scene.
            done?.Invoke();
            Destroy(gameObject, 0.1f);
        }

        void OnDestroy()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            Hud.Hidden = false;
            ModalState.Close(this);
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) _skipRequested = true;
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) _skipRequested = true;
        }

        IEnumerator Fade(float to, float seconds = 0.6f)
        {
            _fade.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new(seconds, TimeUnit.Second) });
            _fade.style.opacity = to;
            yield return new WaitForSecondsRealtime(seconds + 0.05f);
        }

        void Look(Vector3 from, Vector3 at)
        {
            if (_cam == null) return;
            _cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation((at - from).normalized, Vector3.up));
        }

        // ------------------------------------------------------------------ landing

        IEnumerator LandingRoutine(bool full)
        {
            var dir = StoryDirector.Instance;
            var lander = dir != null ? dir.lander : null;
            if (lander == null || _cam == null) yield break;
            Vector3 pad = lander.position;
            Quaternion padRot = lander.rotation;
            Vector3 colony = dir.colonyCentre;
            Vector3 start = pad + (full ? new Vector3(-210f, 330f, -230f) : new Vector3(0f, 90f, 0f));
            // The concrete landing pad is part of the lander prefab: leave it on the ground while the craft flies.
            var padSlab = DetachPad(lander);
            var hull = HullRenderers(lander, padSlab);
            Vector3 hover = pad + Vector3.up * 35f;
            float duration = full ? 24f : 9f;

            if (full) DialoguePlayer.Play("PRO_LANDING");
            _fade.style.opacity = 1f;
            yield return Fade(0f, 1f);
            if (dir.engineGlow != null) dir.engineGlow.enabled = true;

            float fuel0 = 19f;
            float t = 0f;
            Vector3 prev = start;
            while (t < duration && !_skipRequested)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                Vector3 pos;
                if (full && u < 0.72f)
                {
                    float a = Mathf.SmoothStep(0f, 1f, u / 0.72f);
                    Vector3 ctrl = new Vector3(hover.x - 120f, hover.y + 160f, hover.z - 40f);
                    pos = Vector3.Lerp(Vector3.Lerp(start, ctrl, a), Vector3.Lerp(ctrl, hover, a), a);
                }
                else
                {
                    float a = full ? (u - 0.72f) / 0.28f : u;
                    pos = Vector3.Lerp(full ? hover : start, pad, 1f - (1f - a) * (1f - a));
                }
                var vel = (pos - prev) / Mathf.Max(Time.deltaTime, 1e-4f);
                prev = pos;
                lander.SetPositionAndRotation(pos, padRot);
                if (dir.engineDust != null)
                {
                    bool near = pos.y - pad.y < 25f;
                    if (near && !dir.engineDust.isPlaying) dir.engineDust.Play();
                    dir.engineDust.transform.position = new Vector3(pos.x, pad.y + 0.2f, pos.z);
                }

                // Shots: exterior chase, cockpit, ground.
                bool cockpit = full && u > 0.24f && u < 0.66f;
                _cockpit.style.display = cockpit ? DisplayStyle.Flex : DisplayStyle.None;
                _letterTop.style.display = _letterBottom.style.display = cockpit ? DisplayStyle.None : DisplayStyle.Flex;
                foreach (var r in hull) r.enabled = !cockpit; // in the cockpit view we look out, not at our own hull
                if (full && u <= 0.24f) Look(pos + new Vector3(-34f, -14f, -30f), pos + Vector3.up * 6f); // from below, against the sky
                else if (cockpit)
                {
                    var forward = Vector3.Lerp(vel.sqrMagnitude > 1f ? vel.normalized : Vector3.down, (colony - pos).normalized, 0.65f);
                    var eye = pos + Vector3.up * 11f;
                    Look(eye, eye + forward * 60f);
                }
                else Look(pad + new Vector3(24f, 2.4f, -30f), pos + Vector3.up * 7f);

                float alt = pos.y - pad.y;
                _readout.text = Loc.T("CINE_COCKPIT", alt.ToString("0"), (-vel.y).ToString("0.0"),
                    Mathf.Lerp(fuel0, 6f, u).ToString("0"), new Vector2(pos.x - pad.x, pos.z - pad.z).magnitude.ToString("0"));
                yield return null;
            }
            if (_skipRequested) DialoguePlayer.StopAll();
            if (dir.engineDust != null) dir.engineDust.Stop();
            if (dir.engineGlow != null) dir.engineGlow.enabled = false;
            _cockpit.style.display = DisplayStyle.None;
            foreach (var r in hull) r.enabled = true;
            yield return Fade(1f, 0.5f);
            lander.SetPositionAndRotation(pad, padRot);
            AttachPad(lander, padSlab);
            if (_player != null)
            {
                var exit = pad + padRot * new Vector3(5.5f, 0f, 3f);
                var face = colony - exit;
                _player.Teleport(exit, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
            }
            if (_player != null) _player.enabled = true;
            yield return null;
            yield return Fade(0f, 0.8f);
        }

        static Transform DetachPad(Transform lander)
        {
            var pad = lander.Find("Cylinder_Pad");
            if (pad != null) pad.SetParent(lander.parent, true);
            return pad;
        }

        static void AttachPad(Transform lander, Transform pad)
        {
            if (pad != null) pad.SetParent(lander, true);
        }

        static System.Collections.Generic.List<Renderer> HullRenderers(Transform lander, Transform pad)
        {
            var list = new System.Collections.Generic.List<Renderer>(lander.GetComponentsInChildren<Renderer>());
            if (pad != null) list.RemoveAll(r => r.transform.IsChildOf(pad));
            return list;
        }

        // ------------------------------------------------------------------ launch

        IEnumerator LaunchRoutine()
        {
            var dir = StoryDirector.Instance;
            var lander = dir != null ? dir.lander : null;
            if (lander != null && _cam != null)
            {
                Vector3 pad = lander.position;
                DetachPad(lander);
                if (dir.engineGlow != null) dir.engineGlow.enabled = true;
                if (dir.engineDust != null) { dir.engineDust.transform.position = pad + Vector3.up * 0.2f; dir.engineDust.Play(); }
                float t = 0f;
                while (t < 7f && !_skipRequested)
                {
                    t += Time.deltaTime;
                    float h = 0.5f * 7f * Mathf.Max(0f, t - 1.2f) * Mathf.Max(0f, t - 1.2f);
                    lander.position = pad + Vector3.up * h;
                    Look(pad + new Vector3(24f, 1.8f, -30f), lander.position + Vector3.up * 3f);
                    if (t > 5.5f) _fade.style.opacity = (t - 5.5f) / 1.5f;
                    yield return null;
                }
                if (dir.engineDust != null) dir.engineDust.Stop();
            }
            yield return Fade(1f, 0.4f);
        }

        // ------------------------------------------------------------------ title card

        IEnumerator TitleRoutine(string kickerKey, string titleKey, string bodyKey)
        {
            _letterTop.style.display = _letterBottom.style.display = DisplayStyle.None;
            var root = _doc.rootVisualElement;
            var card = UiKit.Box(root, "eh-fill", "eh-center");
            card.style.backgroundColor = Color.black;
            card.style.opacity = 0f;
            card.style.transitionProperty = new StyleList<StylePropertyName>(new System.Collections.Generic.List<StylePropertyName> { new("opacity") });
            card.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new(0.8f, TimeUnit.Second) });
            if (!string.IsNullOrEmpty(kickerKey)) UiKit.Text(card, Loc.T(kickerKey), "eh-kicker");
            var title = UiKit.Text(card, Loc.T(titleKey), "eh-logo-title");
            title.style.fontSize = 64;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (!string.IsNullOrEmpty(bodyKey))
            {
                var body = UiKit.Text(card, Loc.T(bodyKey), "eh-body");
                body.style.maxWidth = 1100;
                body.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            yield return null;
            card.style.opacity = 1f;
            float t = 0f;
            while (t < 5f && !_skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
            card.style.opacity = 0f;
            yield return new WaitForSecondsRealtime(0.8f);
        }
    }
}
