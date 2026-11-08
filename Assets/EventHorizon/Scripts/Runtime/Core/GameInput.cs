using System.Collections.Generic;
using EventHorizon.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EventHorizon.Core
{
    /// <summary>
    /// The game's single set of input actions. Every system reads from here, so a key rebound in Settings
    /// changes it everywhere. Rebinds are saved as JSON overrides inside <see cref="GameSettings"/>.
    /// </summary>
    public static class GameInput
    {
        static InputActionAsset s_asset;
        static readonly Stack<object> s_blockers = new();

        public static InputActionAsset Asset { get { Init(); return s_asset; } }
        public static InputActionMap Player { get { Init(); return s_asset.FindActionMap("Player"); } }

        public static InputAction Move => Player.FindAction("Move");
        public static InputAction Look => Player.FindAction("Look");
        public static InputAction Jump => Player.FindAction("Jump");
        public static InputAction Run => Player.FindAction("Run");
        public static InputAction Crouch => Player.FindAction("Crouch");
        public static InputAction Interact => Player.FindAction("Interact");
        public static InputAction Inventory => Player.FindAction("Inventory");
        public static InputAction Missions => Player.FindAction("Missions");
        public static InputAction Map => Player.FindAction("Map");
        public static InputAction ToggleView => Player.FindAction("ToggleView");
        /// <summary>Esc. Lives in its own map so it keeps working while gameplay input is blocked by a menu.</summary>
        public static InputAction Pause { get { Init(); return s_asset.FindActionMap("System").FindAction("Pause"); } }

        /// <summary>Actions shown on the Controls settings page, in display order.</summary>
        public static readonly string[] Rebindable =
            { "Move", "Jump", "Run", "Crouch", "Interact", "Inventory", "Missions", "Map", "ToggleView" };

        /// <summary>Gameplay input is off while any menu, terminal or dialog holds a block.</summary>
        public static bool GameplayBlocked => s_blockers.Count > 0;

        public static void Block(object owner)
        {
            if (!s_blockers.Contains(owner)) s_blockers.Push(owner);
            Refresh();
        }

        public static void Unblock(object owner)
        {
            if (s_blockers.Count == 0) return;
            var keep = new List<object>(s_blockers);
            keep.Remove(owner);
            s_blockers.Clear();
            for (int i = keep.Count - 1; i >= 0; i--) s_blockers.Push(keep[i]);
            Refresh();
        }

        public static void ClearBlocks()
        {
            s_blockers.Clear();
            Refresh();
        }

        static void Refresh()
        {
            Init();
            if (GameplayBlocked) Player.Disable(); else Player.Enable();
            UpdateCursor();
        }

        /// <summary>Cursor is free in menus, locked during play.</summary>
        public static void UpdateCursor()
        {
            bool free = GameplayBlocked || !GameSession.InGameplay;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        static void Init()
        {
            if (s_asset != null) return;
            s_asset = ScriptableObject.CreateInstance<InputActionAsset>();
            s_asset.name = "EventHorizonInput";

            var player = s_asset.AddActionMap("Player");
            var move = player.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            var look = player.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");
            Button(player, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Button(player, "Run", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Button(player, "Crouch", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
            Button(player, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Button(player, "Inventory", "<Keyboard>/tab", "<Gamepad>/buttonNorth").AddBinding("<Keyboard>/i");
            Button(player, "Missions", "<Keyboard>/j", "<Gamepad>/dpad/up");
            Button(player, "Map", "<Keyboard>/m", "<Gamepad>/dpad/down");
            Button(player, "ToggleView", "<Keyboard>/v", "<Gamepad>/select");

            var system = s_asset.AddActionMap("System");
            Button(system, "Pause", "<Keyboard>/escape", "<Gamepad>/start");

            var overrides = SettingsService.Current.controls.bindingOverridesJson;
            if (!string.IsNullOrEmpty(overrides)) s_asset.LoadBindingOverridesFromJson(overrides);
            s_asset.Enable();
        }

        static InputAction Button(InputActionMap map, string name, string key, string pad)
        {
            var a = map.AddAction(name, InputActionType.Button, key);
            a.AddBinding(pad);
            return a;
        }

        /// <summary>Human-readable keyboard/mouse binding, e.g. "W/A/S/D" or "E".</summary>
        public static string DisplayBinding(InputAction action)
        {
            int i = KeyboardBindingIndex(action);
            if (i < 0) return "-";
            if (action.bindings[i].isComposite)
            {
                var parts = new List<string>();
                for (int p = i + 1; p < action.bindings.Count && action.bindings[p].isPartOfComposite; p++)
                    parts.Add(action.GetBindingDisplayString(p));
                return string.Join("/", parts);
            }
            return action.GetBindingDisplayString(i);
        }

        /// <summary>Index of the first keyboard/mouse binding (or composite) of an action.</summary>
        public static int KeyboardBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite) return i;
                if (!b.isPartOfComposite && (b.effectivePath.StartsWith("<Keyboard>") || b.effectivePath.StartsWith("<Mouse>"))) return i;
            }
            return -1;
        }

        public static void SaveOverrides()
        {
            SettingsService.Current.controls.bindingOverridesJson = Asset.SaveBindingOverridesAsJson();
            SettingsService.Save();
        }

        public static void ResetOverrides()
        {
            Asset.RemoveAllBindingOverrides();
            SaveOverrides();
        }
    }
}
