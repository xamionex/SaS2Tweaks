using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Common;
using HarmonyLib;
using Menumancer.hud;
using ProjectMage.director;
using ProjectMage.gamestate;

namespace SaS2Tweaks;

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

    // Separate throttles for aim toggles
    private static float _lastToggleP1AimTime;
    private static float _lastToggleP2AimTime;
    private static string _statusMessage = "";
    private static float _messageEndTime;

    // DevTools detection
    private static bool? _devToolsPresent;
    private static object _devToolsGlobalInstance;
    private static FieldInfo _freeCamActiveField;

    private static bool IsDevToolsFreeCamActive()
    {
        if (_devToolsPresent == null)
        {
            // Look for the DevTools plugin type
            var devToolsType = Type.GetType("SaS2DevTools.SaS2DevTools, amione.SaS2DevTools");
            if (devToolsType != null)
            {
                _devToolsPresent = true;
                // Get the static Instance field
                var instanceField = devToolsType.GetField("Instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var pluginInstance = instanceField?.GetValue(null);
                if (pluginInstance != null)
                {
                    // Get the Global property (returns GlobalSettings object)
                    var globalProp =
                        devToolsType.GetProperty("Global", BindingFlags.Public | BindingFlags.Instance);
                    _devToolsGlobalInstance = globalProp?.GetValue(pluginInstance);
                    if (_devToolsGlobalInstance != null)
                    {
                        _freeCamActiveField = _devToolsGlobalInstance.GetType().GetField("FreeCamActive",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }
            else
            {
                _devToolsPresent = false;
            }
        }

        if (_devToolsPresent != true || _freeCamActiveField == null || _devToolsGlobalInstance == null)
            return false;
        return (bool)_freeCamActiveField.GetValue(_devToolsGlobalInstance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameDraw), "DrawGame")]
    private static void DrawGamePostfix()
    {
        if (GameState.state != 1) return;

        // If DevTools free-cam is active, skip ALL SaS2Tweaks input processing
        if (IsDevToolsFreeCamActive()) return;
        var now = Environment.TickCount;
        var ks = GlobalInputMgr.ks;

        // F1 toggles Player1AimsCamera
        if (KSingle(ks, Keys.F1) && now - _lastToggleP1AimTime >= ThrottleMs)
        {
            _lastToggleP1AimTime = now;
            TogglePlayerAimsCamera(0);
        }

        // KB: F3 toggles priority for Player 1
        if (KSingle(ks, Keys.F3) && now - _lastToggleTime >= ThrottleMs)
        {
            _lastToggleTime = now;
            TogglePriorityForPlayer(0);
        }

        // KB: F4 cycles camera priority
        if (KSingle(ks, Keys.F4) && now - _lastToggleTime >= ThrottleMs)
        {
            _lastToggleTime = now;
            CycleCameraPriority();
        }

        // Controller handling
        for (var playerIdx = 0; playerIdx <= 1; playerIdx++)
        {
            var gp = GetGamePadState(playerIdx);
            if (!gp.IsConnected) continue;

            var rightStickPressed = gp.Buttons.RightStick == ButtonState.Pressed;
            var leftTriggerPressed = gp.Triggers.Left > 0.5f;
            var rightTriggerPressed = gp.Triggers.Right > 0.5f;
            var leftShoulderPressed = gp.Buttons.LeftShoulder == ButtonState.Pressed;
            var rightShoulderPressed = gp.Buttons.RightShoulder == ButtonState.Pressed;

            // RSC + LT = toggle priority for this player
            if (rightStickPressed && leftTriggerPressed && now - _lastToggleTime >= ThrottleMs)
            {
                _lastToggleTime = now;
                TogglePriorityForPlayer(playerIdx);
            }

            // RSC + RT = cycle camera priority
            if (rightStickPressed && rightTriggerPressed && now - _lastToggleTime >= ThrottleMs)
            {
                _lastToggleTime = now;
                CycleCameraPriority();
            }

            // RSC + Left Shoulder = toggle current players' setting
            if (rightStickPressed && leftShoulderPressed && now - _lastToggleP1AimTime >= ThrottleMs)
            {
                _lastToggleP1AimTime = now;
                TogglePlayerAimsCamera(playerIdx);
            }
        }

        // Draw on-screen feedback
        if (!(_messageEndTime > now) || string.IsNullOrEmpty(_statusMessage)) return;
        SpriteTools.BeginAlpha();
        Text.DrawText(new StringBuilder(_statusMessage), new Vector2(100, 260), Color.Yellow, 0.45f, 0);
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

    // Toggle the "AimsCamera" config entry for a given player
    private static void TogglePlayerAimsCamera(int playerIdx)
    {
        var setting = playerIdx == 0 ? GlobalSettings.Player1AimsCamera : GlobalSettings.Player2AimsCamera;
        if (setting == null) return;
        setting.Value = !setting.Value;
        ShowAimMessage(playerIdx, setting.Value);
    }

    private static void ShowAimMessage(int playerIdx, bool enabled)
    {
        _statusMessage = enabled
            ? $"Player {playerIdx + 1} aims camera -> ON"
            : $"Player {playerIdx + 1} aims camera -> OFF";
        _messageEndTime = Environment.TickCount + 1500;
    }
}