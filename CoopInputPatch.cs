using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Common;
using HarmonyLib;
using Menumancer.hud;
using ProjectMage.director;
using ProjectMage.gamestate;
using ProjectMage.player;

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

    // Per-player rising-edge state for the dedicated teleport combo.
    // Indexed by PlayerMgr.player slot so each local player triggers once per press.
    private static readonly bool[] TeleportComboHeld = new bool[8];

    // Rising-edge state for the three camera-toggle binds, per input source.
    // [bind 0=aim,1=priority,2=cycle][source 0=keyboard,1=pad0,2=pad1]
    private static readonly bool[,] CamBindHeld = new bool[3, 3];

    // Dedicated teleport to the co-op partner, using the rebindable Teleport combo.
    // The keyboard part drives the ID 0 player; each gamepad player reads their own
    // assigned controller. Fires once on the rising edge of the combo.
    private static void HandleTeleportInput(KeyboardState ks)
    {
        var bind = GlobalSettings.TeleportBind;
        var players = PlayerMgr.player;
        if (bind == null || players == null) return;

        for (var i = 0; i < players.Length && i < TeleportComboHeld.Length; i++)
        {
            var p = players[i];
            if (p == null || !p.isLocal || !p.active)
            {
                TeleportComboHeld[i] = false;
                continue;
            }

            var combo = false;

            // Keyboard input only feeds the ID 0 player (see InputProfile.Update).
            if (p.ID == 0 && bind.HeldKeyboard(ks)) combo = true;

            // Gamepad players read their own assigned controller.
            var padIdx = p.inputProfile?.gamepadIdx ?? -1;
            if (padIdx >= 0)
            {
                var gp = GetGamePadState(padIdx);
                if (gp.IsConnected && bind.HeldGamepad(gp)) combo = true;
            }

            if (combo && !TeleportComboHeld[i])
                MenuTeleportPatch.RequestTeleport(p);

            TeleportComboHeld[i] = combo;
        }
    }

    // Evaluate one camera-toggle bind across the keyboard (-> player 0) and pads 0/1
    // (-> the matching player), firing the action once per rising edge of each source.
    private static void CheckCameraBind(int bindIdx, Keybind bind, KeyboardState ks, Action<int> action)
    {
        if (bind == null) return;

        var kb = bind.HeldKeyboard(ks);
        if (kb && !CamBindHeld[bindIdx, 0]) action(0);
        CamBindHeld[bindIdx, 0] = kb;

        for (var pad = 0; pad <= 1; pad++)
        {
            var gp = GetGamePadState(pad);
            var held = gp.IsConnected && bind.HeldGamepad(gp);
            if (held && !CamBindHeld[bindIdx, pad + 1]) action(pad);
            CamBindHeld[bindIdx, pad + 1] = held;
        }
    }

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

        // Dedicated co-op teleport (rebindable combo: default KB Shift+T, pad LS+Y).
        HandleTeleportInput(ks);

        // Camera toggles (rebindable combos). Aim/priority apply to the pressing player;
        // cycle is global. Keyboard -> player 0; pad N -> player N.
        CheckCameraBind(0, GlobalSettings.P1AimsCameraBind, ks, TogglePlayerAimsCamera);
        CheckCameraBind(1, GlobalSettings.TogglePriorityBind, ks, TogglePriorityForPlayer);
        CheckCameraBind(2, GlobalSettings.CyclePriorityBind, ks, _ => CycleCameraPriority());

        // Draw on-screen feedback
        if (!(_messageEndTime > now) || string.IsNullOrEmpty(_statusMessage)) return;
        SpriteTools.BeginAlpha();
        Text.DrawText(new StringBuilder(_statusMessage), new Vector2(100, 260), Color.Yellow, 0.45f, 0);
        SpriteTools.End();
    }

    // Toggle for a specific player.
    // If the camera already follows that player, switch to Midpoint.
    // Otherwise, switch to that player.
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

    // Cycle order: Midpoint, then Player1, then Player2, then back to Midpoint.
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