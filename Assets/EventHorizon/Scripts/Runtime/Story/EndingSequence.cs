using System.Collections.Generic;
using System.Linq;
using EventHorizon.Core;
using EventHorizon.Game;
using EventHorizon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.Story
{
    /// <summary>Plays the ending: narration over artwork, the colonists' epilogue, then the credits.</summary>
    public class EndingSequence : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }

        UIDocument _doc;
        VisualElement _art;
        string _ending;

        public static void Begin()
        {
            var s = Story.State;
            string ending = Endings.Determine(s);
            s.ending = ending;
            Profile.Current.RecordEnding(ending);
            Story.NotifyChanged();
            if (StoryTest.Running || IsPlaying) return;
            var doc = UiRoot.Create("Ending", 31);
            var seq = doc.gameObject.AddComponent<EndingSequence>();
            seq._doc = doc;
            seq._ending = ending;
            seq.Build();
        }

        void Build()
        {
            IsPlaying = true;
            ModalState.Open(this);
            Hud.Hidden = true;
            var root = _doc.rootVisualElement;
            var bg = UiKit.Box(root, "eh-fill");
            bg.style.backgroundColor = Color.black;
            bg.pickingMode = PickingMode.Position;
            _art = UiKit.Box(bg, "eh-fill");
            var tex = UiResources.Instance != null ? UiResources.Instance.Art(_ending switch
            {
                Endings.Home => "mars", Endings.Beyond => "saturn", Endings.Horizon => "stars", _ => "blackhole",
            }) : null;
            if (tex != null)
            {
                _art.style.backgroundImage = new StyleBackground(tex);
                _art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _art.style.opacity = 0.55f;
            }
            var titles = UiKit.Box(bg, "eh-col");
            titles.style.position = Position.Absolute;
            titles.style.left = 110;
            titles.style.top = 120;
            UiKit.Text(titles, Loc.T("ENDING_KICKER"), "eh-kicker");
            UiKit.Text(titles, Loc.T("ENDING_" + _ending + "_TITLE"), "eh-logo-title").style.fontSize = 72;

            var narration = StoryContent.Dialogues["END_" + _ending];
            narration.onComplete = PlayEpilogue;
            DialoguePlayer.Play(narration.id);
        }

        void PlayEpilogue()
        {
            var def = new DialogueDef { id = "END_EPILOGUE", cinematic = true, radio = true, onComplete = ShowCredits };
            foreach (var key in Endings.Epilogue(Story.State)) def.lines.Add(new DialogueLine { speaker = Character.Narrator, key = key });
            StoryContent.Dialogues[def.id] = def;
            DialoguePlayer.Play(def.id);
        }

        void ShowCredits()
        {
            var root = _doc.rootVisualElement;
            var page = UiKit.Box(root, "eh-fill");
            page.style.paddingLeft = page.style.paddingRight = 120;
            page.style.paddingTop = page.style.paddingBottom = 70;
            page.pickingMode = PickingMode.Position;
            var credits = new CreditsPanel(GameSession.ReturnToMenu);
            page.Add(credits.Element);
            var row = UiKit.Box(page, "eh-row");
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 12;
            UiKit.Small(row, Loc.T("BTN_RETURN_MENU"), GameSession.ReturnToMenu, "eh-btn-primary");
            GameInput.UpdateCursor();
        }

        void OnDestroy()
        {
            IsPlaying = false;
            Hud.Hidden = false;
            ModalState.Close(this);
        }
    }
}
