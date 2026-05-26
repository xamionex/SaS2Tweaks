using System.Reflection;
using Common;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.player;
using ProjectMage.player.menu;

namespace SaS2Tweaks;

public static class CoopPatchHelpers
{
    private static readonly MethodInfo GetMainPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetMainPlayer");

    private static Player GetMainPlayer() =>
        GetMainPlayerMethod != null ? (Player)GetMainPlayerMethod.Invoke(null, null) : null;

    private static readonly MethodInfo GetLocalCoopPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetLocalCoopPlayer");

    private static Player GetLocalCoopPlayer() =>
        GetLocalCoopPlayerMethod != null ? (Player)GetLocalCoopPlayerMethod.Invoke(null, null) : null;

    private static readonly MethodInfo GetCharacterMethod =
        AccessTools.Method(typeof(Player), "GetCharacter");

    private static Character GetCharacter(object obj) =>
        GetCharacterMethod != null ? (Character)GetCharacterMethod.Invoke(obj, null) : null;

    private static readonly MethodInfo IsModalActiveMethod =
        AccessTools.Method(typeof(PlayerMenu), "IsModalActive");

    private static bool IsModalActive(object obj) =>
        IsModalActiveMethod != null && (bool)IsModalActiveMethod.Invoke(obj, null);

    private static readonly MethodInfo SetAnimMethod =
        AccessTools.Method(typeof(CharAnim), "SetAnim");

    private static void SetAnim(object obj, object[] args) =>
        SetAnimMethod.Invoke(obj, args);

    /// P2 door trigger.
    /// Replaces movedChar.loc = pLoc inside the P2 layer-change branch of CharMovement.MoveCoopPlayers.
    /// 
    /// Vanilla (P2CanTriggerDoors = false) snaps P2 back to P1's position.
    /// Mod     (P2CanTriggerDoors = true)  teleports P1 to P2's new position.
    public static void HandleP2LayerChange(Character movedChar, Vector2 pLoc)
    {
        if (!GlobalSettings.P2CanTriggerDoors.Value)
        {
            movedChar.loc = pLoc; // vanilla: snap P2 back to P1
            return;
        }

        // P2CanTriggerDoors: teleport P1 to follow P2 through the door.
        var mainPlayer = GetMainPlayer();
        if (mainPlayer == null) return;
        var mainChar = CharMgr.character[mainPlayer.charIdx];
        if (mainChar.dyingFrame > 0f || IsModalActive(mainPlayer.menu)) return;
        mainChar.loc = movedChar.loc;
        mainChar.loc.Y = CharCols.GetGround(mainChar.loc);
        if (mainChar.state == 0) return;
        mainChar.state = 0;
        SetAnim(mainChar.anim, ["land", false, true]);
    }

    /// Runtime multiplier for P1's look-stick camera pan.
    /// Vanilla = 500f; returns 0 when Player1AimsCamera is false,
    /// or when the player is aiming and Player1MovesCameraWhenAiming is false.
    public static float GetP1LookScale()
    {
        var mainChar = GetCharacter(GetMainPlayer());
        if (mainChar != null && mainChar.draw.aiming &&
            GlobalSettings.Player1MovesCameraWhenAiming?.Value == false)
            return 0f;

        return GlobalSettings.Player1AimsCamera?.Value != false ? 500f : 0f;
    }

    /// Adds Player 2's right-stick contribution to the camera-target vector.
    /// No-op unless Player2AimsCamera is true and char2 is alive.
    /// Also suppressed if Player2 is aiming and Player2MovesCameraWhenAiming is false.
    public static Vector2 AddP2LookVec(Vector2 vector, Character char2)
    {
        if (char2 != null && char2.draw.aiming &&
            GlobalSettings.Player2MovesCameraWhenAiming?.Value == false)
            return vector;

        if (GlobalSettings.Player2AimsCamera?.Value != true || char2 == null)
            return vector;

        var lv = char2.keys.lookVec;
        if (lv.X > -0.1f && lv.X < 0.1f) lv.X = 0f;
        if (lv.Y > -0.1f && lv.Y < 0.1f) lv.Y = 0f;
        return vector + lv * 500f;
    }

    /// Camera priority adjustment injected after <c>(P1.loc + P2.loc) / 2f</c>.
    /// The game's own <c>+ new Vector2(0, -100)</c> offset is applied afterwards.
    public static Vector2 AdjustCameraBase(Vector2 midpoint)
    {
        var mode = GlobalSettings.CameraPriority?.Value ?? CameraPriorityMode.Midpoint;

        if (mode == CameraPriorityMode.Player1)
        {
            var c = GetCharacter(GetMainPlayer());
            if (c != null) return c.loc;
        }
        else if (mode == CameraPriorityMode.Player2)
        {
            var c = GetCharacter(GetLocalCoopPlayer());
            if (c != null) return c.loc;
        }
        else if (mode != CameraPriorityMode.Midpoint)
        {
            SaS2Tweaks.Instance.Log.LogInfo($"[CoopDoorPatch] Unknown camera mode: {mode}");
        }

        return midpoint;
    }
}