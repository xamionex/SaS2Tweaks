using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using HarmonyLib;
using Menumancer.hud;
using ProjectMage.director;
using ProjectMage.gamestate;

namespace SaS2Tweaks
{
    [HarmonyPatch]
    internal static class CoopInputPatch
    {
        private static readonly MethodInfo GetGamePadStateMethod =
            AccessTools.Method(typeof(GlobalInputMgr), "GetGamePadState", [typeof(int)]);

        private static GamePadState GetGamePadState(int playerIdx)
        {
            if (GetGamePadStateMethod == null) return default;
            if (playerIdx is < 0 or > 3) return default;
            try
            {
                return (GamePadState)GetGamePadStateMethod.Invoke(null, [playerIdx]);
            }
            catch
            {
                return default;
            }
        }

        // Keyboard state tracking for KSingle
        private static readonly HashSet<Keys> KHeld = [];
        private static bool KSingle(KeyboardState ks, Keys k)
        {
            if (ks.IsKeyDown(k)) return KHeld.Add(k);
            KHeld.Remove(k);
            return false;
        }

        private static float _lastToggleTime;
        private const float ThrottleMs = 500f;

        private static string _statusMessage = "";
        private static float _messageEndTime;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameDraw), "DrawGame")]
        private static void DrawGamePostfix()
        {
            if (GameState.state != 1) return;

            var now = Environment.TickCount;
            var ks = GlobalInputMgr.ks;

            // KB: F3 toggles priority for the current player (Player 1)
            if (KSingle(ks, Keys.F3) && now - _lastToggleTime >= ThrottleMs)
            {
                _lastToggleTime = now;
                TogglePriorityForPlayer(0);
            }

            // KB: F4 cycles through all camera priority modes
            if (KSingle(ks, Keys.F4) && now - _lastToggleTime >= ThrottleMs)
            {
                _lastToggleTime = now;
                CycleCameraPriority();
            }

            // Controller
            for (var playerIdx = 0; playerIdx <= 1; playerIdx++)
            {
                var gp = GetGamePadState(playerIdx);
                if (!gp.IsConnected) continue;

                var rightStickPressed = gp.Buttons.RightStick == ButtonState.Pressed;
                var leftTriggerPressed = gp.Triggers.Left > 0.5f;
                var rightTriggerPressed = gp.Triggers.Right > 0.5f;

                // RSC + LT = toggle priority for this specific player
                if (rightStickPressed && leftTriggerPressed && now - _lastToggleTime >= ThrottleMs)
                {
                    _lastToggleTime = now;
                    TogglePriorityForPlayer(playerIdx);
                }

                // RSC + RT = cycle all camera modes (global, like F4)
                if (rightStickPressed && rightTriggerPressed && now - _lastToggleTime >= ThrottleMs)
                {
                    _lastToggleTime = now;
                    CycleCameraPriority();
                }
            }

            // Draw on‑screen feedback
            if (!(_messageEndTime > now) || string.IsNullOrEmpty(_statusMessage)) return;
            SpriteTools.BeginAlpha();
            Text.DrawText(new System.Text.StringBuilder(_statusMessage),
                new Vector2(100, 260), Color.Yellow, 0.45f, 0);
            SpriteTools.End();
        }

        // Toggle for a specific player: if camera already follows that player -> Midpoint, else -> that player.
        private static void TogglePriorityForPlayer(int playerIdx)
        {
            var current = GlobalSettings.CameraPriority.Value;
            CameraPriorityMode newMode;

            switch (playerIdx)
            {
                case 0:
                    newMode = current == CameraPriorityMode.Player1
                        ? CameraPriorityMode.Midpoint
                        : CameraPriorityMode.Player1;
                    break;
                case 1:
                    newMode = current == CameraPriorityMode.Player2
                        ? CameraPriorityMode.Midpoint
                        : CameraPriorityMode.Player2;
                    break;
                default:
                    return;
            }

            GlobalSettings.CameraPriority.Value = newMode;
            ShowMessage(newMode);
        }

        // Cycle: Midpoint -> Player1 -> Player2 -> Midpoint
        private static void CycleCameraPriority()
        {
            var current = GlobalSettings.CameraPriority.Value;
            var newMode = current switch
            {
                CameraPriorityMode.Midpoint => CameraPriorityMode.Player1,
                CameraPriorityMode.Player1 => CameraPriorityMode.Player2,
                _ => CameraPriorityMode.Midpoint
            };

            GlobalSettings.CameraPriority.Value = newMode;
            ShowMessage(newMode);
        }

        private static void ShowMessage(CameraPriorityMode mode)
        {
            _statusMessage = mode switch
            {
                CameraPriorityMode.Player1 => "Camera -> Player 1",
                CameraPriorityMode.Player2 => "Camera -> Player 2",
                _ => "Camera -> Midpoint"
            };
            _messageEndTime = Environment.TickCount + 1500;
        }
    }
}