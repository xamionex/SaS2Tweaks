using System.Reflection;
using Common;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.player;
using ProjectMage.player.menu;

namespace SaS2Tweaks;

public static class CoopPatchHelpers
{
    private static readonly MethodInfo GetMainPlayerMethod = AccessTools.Method(typeof(PlayerMgr), "GetMainPlayer");

    private static Player GetMainPlayer() =>
        GetMainPlayerMethod != null ? (Player)GetMainPlayerMethod.Invoke(null, null) : null;

    private static readonly MethodInfo GetLocalCoopPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetLocalCoopPlayer");

    private static Player GetLocalCoopPlayer() =>
        GetLocalCoopPlayerMethod != null ? (Player)GetLocalCoopPlayerMethod.Invoke(null, null) : null;

    private static readonly MethodInfo GetCharacterMethod = AccessTools.Method(typeof(Player), "GetCharacter");

    private static Character GetCharacter(object obj) =>
        GetCharacterMethod != null ? (Character)GetCharacterMethod.Invoke(obj, null) : null;

    private static readonly MethodInfo IsModalActiveMethod = AccessTools.Method(typeof(PlayerMenu), "IsModalActive");

    private static bool IsModalActive(object obj) =>
        IsModalActiveMethod != null && (bool)IsModalActiveMethod.Invoke(obj, null);

    private static readonly MethodInfo SetAnimMethod = AccessTools.Method(typeof(CharAnim), "SetAnim");
    private static void SetAnim(object obj, object[] args) => SetAnimMethod.Invoke(obj, args);

    /// P2 door trigger<br />
    /// Replaces <c>movedChar.loc = pLoc</c> inside the P2 layer-change branch<br />
    /// of <c>CharMovement.MoveCoopPlayers</c>.<br />
    /// <br />
    /// Vanilla (P2CanTriggerDoors = false) -> snaps P2 back to P1's position.<br />
    /// Mod     (P2CanTriggerDoors = true)  -> teleports P1 to P2's new position.<br />
    public static void HandleP2LayerChange(Character movedChar, Vector2 pLoc)
    {
        if (!GlobalSettings.P2CanTriggerDoors.Value)
        {
            movedChar.loc = pLoc; // vanilla: snap P2 back
            return;
        }

        // True co-op: P2 walked through a door -> bring P1 along.
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

    // Camera aim scale
    /// Runtime multiplier for P1's look-stick camera pan.<br />
    /// Vanilla = 500 f; returns 0 when Player1AimsCamera is false.<br />
    public static float GetP1LookScale() => GlobalSettings.Player1AimsCamera?.Value != false ? 500f : 0f;

    /// Adds Player 2's right-stick contribution to the camera-target vector.<br />
    /// No-op unless Player2AimsCamera is true and <paramref name="char2"/> is alive.<br />
    /// Uses the same dead-zone clamp and 500 f scale as P1.<br />
    public static Vector2 AddP2LookVec(Vector2 vector, Character char2)
    {
        if (GlobalSettings.Player2AimsCamera?.Value != true || char2 == null) return vector;
        var lv = char2.keys.lookVec;
        if (lv.X is > -0.1f and < 0.1f) lv.X = 0f;
        if (lv.Y is > -0.1f and < 0.1f) lv.Y = 0f;
        return vector + lv * 500f;
    }

    /// Camera prio: Injected immediately after the vanilla <c>(P1.loc + P2.loc) / 2f</c>.<br />
    /// Returns the priority-adjusted camera base; the game's own <c>+ new Vector2(0, -100)</c> offset is still applied by the original IL.
    public static Vector2 AdjustCameraBase(Vector2 midpoint)
    {
        var mode = GlobalSettings.CameraPriority?.Value ?? CameraPriorityMode.Midpoint;
        switch (mode)
        {
            case CameraPriorityMode.Player1:
            {
                var c = GetCharacter(GetMainPlayer());
                if (c != null) return c.loc;
                break;
            }
            case CameraPriorityMode.Player2:
            {
                var c = GetCharacter(GetLocalCoopPlayer());
                if (c != null) return c.loc;
                break;
            }
            case CameraPriorityMode.Midpoint:
                break;
            default:
                SaS2Tweaks.Instance.Log.LogInfo($"[CoopDoorPatch] Unknown camera mode: {mode}");
                break;
        }

        return midpoint;
    }
}