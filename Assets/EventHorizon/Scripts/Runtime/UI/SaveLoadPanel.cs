using System;
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
            UiKit.Text(header, saveMode ? "SAVE GAME" : "LOAD GAME", "eh-panel-title");
            UiKit.Box(header, "eh-spacer");
            UiKit.Small(header, "BACK", () => { UiAudio.Back(); _onBack?.Invoke(); });
            UiKit.Text(Element, saveMode ? "Choose a slot. Saving over an existing slot replaces it." : "Saved games are stored in your user data folder.", "eh-dim", "eh-small");
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
            UiKit.Text(text, $"SLOT {info.slot:00}", "eh-kicker");
            if (info.Exists)
            {
                var d = info.data;
                UiKit.Text(text, d.saveName, "eh-card-title");
                UiKit.Text(text, $"{d.characterName} \"{d.callsign}\"  //  {ModeLabel(d)}", "eh-small");
                UiKit.Text(text, $"POP {d.population}   DEV {d.colonyDevelopment:0}%   PLAYTIME {UiKit.Duration(d.playtimeSeconds)}", "eh-mono", "eh-small", "eh-dim");
                UiKit.Text(text, info.savedAt == default ? "" : info.savedAt.ToLocalTime().ToString("yyyy-MM-dd  HH:mm"), "eh-mono", "eh-small", "eh-dim");
            }
            else
            {
                UiKit.Text(text, "EMPTY SLOT", "eh-card-title", "eh-dim");
            }

            var actions = UiKit.Box(card, "eh-col");
            if (_saveMode)
            {
                UiKit.Small(actions, "SAVE HERE", () =>
                {
                    void DoSave() => UiKit.Prompt(_modal, "NAME THIS SAVE", info.Exists ? info.data.saveName : DefaultName(), n =>
                    {
                        bool ok = SaveSystem.Save(info.slot, n);
                        Hud.Toast(ok ? "Game saved" : "Save failed: see the log", !ok);
                        Refresh();
                    });
                    if (info.Exists) UiKit.Confirm(_modal, "OVERWRITE SAVE", $"Replace \"{info.data.saveName}\"?", "OVERWRITE", DoSave, danger: true);
                    else DoSave();
                }, "eh-btn-primary");
            }
            else
            {
                var load = UiKit.Small(actions, "LOAD", () => GameSession.LoadSave(info.data, info.slot), "eh-btn-primary");
                load.SetEnabled(info.Exists);
            }
            var rename = UiKit.Small(actions, "RENAME", () => UiKit.Prompt(_modal, "RENAME SAVE", info.data.saveName, n => { SaveSystem.Rename(info.slot, n); Refresh(); }));
            rename.SetEnabled(info.Exists);
            var del = UiKit.Small(actions, "DELETE", () => UiKit.Confirm(_modal, "DELETE SAVE",
                $"Permanently delete \"{info.data.saveName}\"? This cannot be undone.", "DELETE", () => { SaveSystem.Delete(info.slot); Refresh(); }, danger: true), "eh-btn-danger");
            del.SetEnabled(info.Exists);
        }

        static string DefaultName()
        {
            var s = GameSession.State;
            var env = Core.MarsEnvironment.Instance;
            return $"Sol {(env != null ? env.Sol : 0)} - {MissionTracker.CurrentTitle}";
        }

        static string ModeLabel(SaveData d) => d.mode switch
        {
            GameMode.Sandbox => "FREE EXPLORATION",
            GameMode.Science => "SCIENCE LAB",
            _ => $"CH {d.chapter:00} {Campaign.Get(d.chapter).title}",
        };
    }
}
