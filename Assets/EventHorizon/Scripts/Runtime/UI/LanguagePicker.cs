using System;
using EventHorizon.Core;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Language cards showing each language in its own name (English, Монгол хэл). Used on first start, in the
    /// main menu, Settings and the pause menu. Picking one switches the whole interface immediately.
    /// </summary>
    public static class LanguagePicker
    {
        public static VisualElement Cards(VisualElement parent, Action picked = null)
        {
            var row = UiKit.Box(parent, "eh-lang-row");
            foreach (var lang in Loc.Languages)
            {
                var l = lang;
                var card = UiKit.Box(row, "eh-lang-card");
                card.EnableInClassList("eh-lang-card--selected", l.code == Loc.Current);
                UiKit.Text(card, l.nativeName, "eh-lang-native");
                UiKit.Text(card, l.englishName.ToUpperInvariant(), "eh-lang-english");
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    UiAudio.Confirm();
                    Loc.SetLanguage(l.code);
                    picked?.Invoke();
                });
                card.RegisterCallback<PointerEnterEvent>(_ => UiAudio.Hover());
            }
            return row;
        }

        /// <summary>Compact switch (EN | МН) for corners of the screen.</summary>
        public static VisualElement Chip(VisualElement parent)
        {
            var chip = UiKit.Box(parent, "eh-lang-chip");
            foreach (var lang in Loc.Languages)
            {
                var l = lang;
                var b = UiKit.Small(chip, l.nativeName, () => Loc.SetLanguage(l.code));
                b.EnableInClassList("eh-btn-primary", l.code == Loc.Current);
                b.style.marginLeft = 6;
            }
            return chip;
        }

        /// <summary>Full-screen first-start picker. Calls <paramref name="done"/> after a choice.</summary>
        public static VisualElement FirstStart(VisualElement root, Action done)
        {
            var bg = UiKit.Box(root, "eh-modal-bg");
            bg.pickingMode = PickingMode.Position;
            var panel = UiKit.Box(bg, "eh-panel");
            panel.style.width = 760;
            // Shown before any language is chosen, so the heading is in every language at once.
            UiKit.Text(panel, "EVENT HORIZON: MARS", "eh-kicker");
            foreach (var lang in Loc.Languages)
                UiKit.Text(panel, Loc.In(lang.code, "LANG_CHOOSE"), "eh-panel-title");
            Cards(panel, () => { bg.RemoveFromHierarchy(); done?.Invoke(); });
            foreach (var lang in Loc.Languages)
                UiKit.Text(panel, Loc.In(lang.code, "LANG_CHANGE_LATER"), "eh-small", "eh-dim");
            return bg;
        }
    }
}
