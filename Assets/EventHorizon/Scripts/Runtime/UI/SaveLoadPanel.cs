using System;
using EventHorizon.Core;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace EventHorizon.UI
{
    /// <summary>
    /// Save slots with thumbnail, character, mission, population, development, playtime and date.
    /// Load mode: Load / Rename / Delete. Save mode (pause menu): Save here / Rename / Delete.
    /// </summary>
    public class SaveLoadPanel
    {
        readonly VisualElement _modal;
        readonly bool _saveMode;
        readonly ScrollView _list;
        readonly Action _onBack;

        public VisualElement Element { get; }

        public SaveLoadPanel(VisualElement modalRoot, bool saveMode, Action onBack)
        {
            _modal = modalRoot;
            _saveMode = saveMode;
            _onBack = onBack;
            Element = new VisualElement();
            Element.AddToClassList("eh-panel");
            Element.style.flexGrow = 1;
            var header = UiKit.Box(Element, "eh-row");
            UiKit.Text(header, Loc.T(saveMode ? "PAUSE_SAVE" : "PAUSE_LOAD"), "eh-panel-title");
            UiKit.Box(header, "eh-spacer");
            UiKit.Small(header, Loc.T("BTN_BACK"), () => { UiAudio.Back(); _onBack?.Invoke(); });
            UiKit.Text(Element, Loc.T(saveMode ? "SAVE_HINT_SAVE" : "SAVE_HINT_LOAD"), "eh-dim", "eh-small");
            _list = UiKit.Scroll(Element);
            _list.style.flexGrow = 1;
            _list.style.marginTop = 8;
            Refresh();
        }

        public void Refresh()
        {
            _list.Clear();
            foreach (var info in SaveSystem.ListCampaignSlots()) AddSlot(info);
        }

        void AddSlot(SaveSlotInfo info)
        {
            var card = UiKit.Box(_list, "eh-card", "eh-row");
            var thumb = UiKit.Box(card, "eh-thumb");
            var tex = info.Exists ? SaveSystem.LoadThumbnail(info) : null;
            if (tex != null) thumb.style.backgroundImage = new StyleBackground(tex);

            var text = UiKit.Box(card, "eh-col", "eh-grow");
            UiKit.Text(text, Loc.T("SAVE_SLOT", info.slot.ToString("00")), "eh-kicker");
            if (info.Exists)
            {
                var d = info.data;
                UiKit.Text(text, d.saveName, "eh-card-title");
                UiKit.Text(text, $"{d.characterName} \"{d.callsign}\"  //  {ModeLabel(d)}", "eh-small");
                UiKit.Text(text, Loc.T("SAVE_STATS", d.population, d.colonyDevelopment.ToString("0"), UiKit.Duration(d.playtimeSeconds)), "eh-mono", "eh-small", "eh-dim");
                UiKit.Text(text, info.savedAt == default ? "" : info.savedAt.ToLocalTime().ToString("yyyy-MM-dd  HH:mm"), "eh-mono", "eh-small", "eh-dim");
            }
            else
            {
                UiKit.Text(text, Loc.T("SAVE_EMPTY"), "eh-card-title", "eh-dim");
            }

            var actions = UiKit.Box(card, "eh-col");
            if (_saveMode)
            {
                UiKit.Small(actions, Loc.T("SAVE_HERE"), () =>
                {
                    void DoSave() => UiKit.Prompt(_modal, Loc.T("SAVE_NAME_PROMPT"), info.Exists ? info.data.saveName : DefaultName(), n =>
                    {
                        bool ok = SaveSystem.Save(info.slot, n);
                        Hud.Toast(Loc.T(ok ? "TOAST_SAVED" : "TOAST_SAVE_FAILED"), !ok);
                        Refresh();
                    });
                    if (info.Exists) UiKit.Confirm(_modal, Loc.T("SAVE_OVERWRITE"), Loc.T("SAVE_OVERWRITE_Q", info.data.saveName), Loc.T("BTN_OVERWRITE"), DoSave, danger: true);
                    else DoSave();
                }, "eh-btn-primary");
            }
            else
            {
                var load = UiKit.Small(actions, Loc.T("BTN_LOAD"), () => GameSession.LoadSave(info.data, info.slot), "eh-btn-primary");
                load.SetEnabled(info.Exists);
            }
            var rename = UiKit.Small(actions, Loc.T("BTN_RENAME"), () => UiKit.Prompt(_modal, Loc.T("SAVE_RENAME"), info.data.saveName, n => { SaveSystem.Rename(info.slot, n); Refresh(); }));
            rename.SetEnabled(info.Exists);
            var del = UiKit.Small(actions, Loc.T("BTN_DELETE"), () => UiKit.Confirm(_modal, Loc.T("SAVE_DELETE"),
                Loc.T("SAVE_DELETE_Q", info.data.saveName), Loc.T("BTN_DELETE"), () => { SaveSystem.Delete(info.slot); Refresh(); }, danger: true), "eh-btn-danger");
            del.SetEnabled(info.Exists);
        }

        static string DefaultName()
        {
            var s = GameSession.State;
            var env = Core.MarsEnvironment.Instance;
            return Loc.T("SAVE_DEFAULT_NAME", env != null ? env.Sol : 0, MissionTracker.CurrentTitle);
        }

        static string ModeLabel(SaveData d) => d.mode switch
        {
            GameMode.Sandbox => Loc.T("MODE_SANDBOX"),
            GameMode.Science => Loc.T("MENU_SCIENCE_LAB"),
            _ => $"{MissionTracker.ChapterLabel(d.chapter)} // {Loc.T(Story.Story.MainQuest(d.chapter).TitleKey)}",
        };
    }
}
