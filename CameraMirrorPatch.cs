using Common;
using System;
using System.Reflection;
using HarmonyLib;
using ProjectMage.player;

namespace SaS2Tweaks;

/// In local co-op, only Player 1's camMgr drives the shared camera.
/// When camera effects (zoom, shake, focus) are applied to P2's camMgr, they are invisible because P2's state is never synced to the shared ScrollManager.
[HarmonyPatch]
internal static class CameraMirrorPatch
{
    private static readonly MethodInfo GetMainPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetMainPlayer");

    private static readonly MethodInfo GetLocalCoopPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetLocalCoopPlayer");

    private static readonly MethodInfo IsLocalCoopMethod =
        AccessTools.Method(typeof(PlayerMgr), "IsLocalCoopMode");

    private static Player GetMainPlayer() =>
        GetMainPlayerMethod != null ? (Player)GetMainPlayerMethod.Invoke(null, null) : null;

    private static Player GetLocalCoopPlayer() =>
        GetLocalCoopPlayerMethod != null ? (Player)GetLocalCoopPlayerMethod.Invoke(null, null) : null;

    private static bool IsLocalCoop() =>
        IsLocalCoopMethod != null && (bool)IsLocalCoopMethod.Invoke(null, null);

    private static Type _splitScreenType;
    private static FieldInfo _splitScreenEnabledField;
    private static bool _splitScreenChecked;

    private static bool IsSplitScreenActive()
    {
        try
        {
            if (_splitScreenChecked)
                return _splitScreenEnabledField != null && (bool)_splitScreenEnabledField.GetValue(null);
            _splitScreenChecked = true;

            _splitScreenType = Type.GetType("SaS2SplitScreen.GlobalSettings, amione.SaS2SplitScreen");
            if (_splitScreenType == null) return false;

            _splitScreenEnabledField = _splitScreenType.GetField("SplitscreenEnabled", BindingFlags.Public | BindingFlags.Static);
            if (_splitScreenEnabledField == null) return false;

            var value = _splitScreenEnabledField.GetValue(null);
            if (value == null) return false;

            var valueProp = value.GetType().GetProperty("Value");
            return valueProp != null && (bool)valueProp.GetValue(value);
        }
        catch
        {
            return false;
        }
    }

    /// Returns true only when we are in gameplay with an active local P2 and splitscreen is off.
    private static bool CanMirror()
    {
        try
        {
            if (ProjectMage.gamestate.GameState.state != 1) return false;
            if (!IsLocalCoop()) return false;
            if (IsSplitScreenActive()) return false;
            var coop = GetLocalCoopPlayer();
            return coop != null && coop.active && coop.charIdx > -1;
        }
        catch
        {
            return false;
        }
    }

    private static float _lastP2Zoom = float.NaN;

    // Player.Update frame sync (zoom fallback)
    // CamMgr.ZoomBump is tiny and may be JIT inlined, so the postfix above never fires.
    // This frame sync detects P2 zoom bumps by tracking the zoom value across frames.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Update")]
    private static void Player_Update_Postfix(Player __instance)
    {
        try
        {
            if (__instance.ID != 1) return;
            if (!CanMirror()) return;

            var main = GetMainPlayer();
            if (main?.camMgr == null) return;

            var p2Cam = __instance.camMgr;

            if (!float.IsNaN(_lastP2Zoom))
            {
                var delta = p2Cam.zoom - _lastP2Zoom;
                if (delta > 0.001f)
                    main.camMgr.zoom += delta;
            }
            _lastP2Zoom = p2Cam.zoom;
        }
        catch (Exception e)
        {
            SaS2Tweaks.Instance.Log.LogWarning($"[CameraMirrorPatch] FrameSync error: {e.Message}");
        }
    }

    /// PlayerQuake.SetQuake (shake)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerQuake), "SetQuake")]
    private static void PlayerQuake_SetQuake_Postfix(PlayerQuake __instance, float v, Vector2 loc)
    {
        try
        {
            if (GlobalSettings.P2HitEffects?.Value != true) return;
            if (!CanMirror()) return;

            var main = GetMainPlayer();
            if (main == null || __instance == main.camMgr.quake) return;

            var coop = GetLocalCoopPlayer();
            if (coop != null && __instance == coop.camMgr.quake)
                main.camMgr.quake.SetQuake(v, loc);
        }
        catch (Exception e)
        {
            SaS2Tweaks.Instance.Log.LogWarning($"[CameraMirrorPatch] SetQuakePostfix error: {e.Message}");
        }
    }

    /// CamMgr.ZoomBump (zoom direct)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CamMgr), "ZoomBump")]
    private static void CamMgr_ZoomBump_Postfix(CamMgr __instance, float zoom)
    {
        try
        {
            if (GlobalSettings.P2FinisherEffects?.Value != true) return;
            if (!CanMirror()) return;

            var main = GetMainPlayer();
            if (main == null || __instance == main.camMgr) return;

            var coop = GetLocalCoopPlayer();
            if (coop != null && __instance == coop.camMgr)
                main.camMgr.zoom += zoom;
        }
        catch (Exception e)
        {
            SaS2Tweaks.Instance.Log.LogWarning($"[CameraMirrorPatch] ZoomBumpPostfix error: {e.Message}");
        }
    }

    /// CamMgr.SetFocus (focus)
    /// Vanilla calls SetFocus on either P1 or P2 camMgr depending on context.
    /// We mirror in BOTH directions so:
    ///   - Shared camera (P1 camMgr) gets focus when P2 performs a finisher
    ///   - P2 camMgr gets focus when vanilla targets P1 (splitscreen support)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CamMgr), "SetFocus")]
    private static void CamMgr_SetFocus_Postfix(CamMgr __instance, Vector2 loc, float zoom, float duration)
    {
        try
        {
            if (!CanMirror()) return;

            var main = GetMainPlayer();
            var coop = GetLocalCoopPlayer();
            if (main?.camMgr == null || coop?.camMgr == null || coop == main) return;

            if (__instance == main.camMgr)
            {
                // Vanilla focused P1 -> also focus P2 (splitscreen support)
                if (GlobalSettings.P2DaggerFocus?.Value != true) return;
                coop.camMgr.focusPoint = duration;
                coop.camMgr.focusVec = loc;
                coop.camMgr.focusZoom = zoom;
            }
            else if (__instance == coop.camMgr)
            {
                // Vanilla focused P2 -> also focus P1 (shared camera support)
                if (GlobalSettings.P2FinisherEffects?.Value != true) return;
                main.camMgr.focusPoint = duration;
                main.camMgr.focusVec = loc;
                main.camMgr.focusZoom = zoom;
            }
        }
        catch (Exception e)
        {
            SaS2Tweaks.Instance.Log.LogWarning($"[CameraMirrorPatch] SetFocusPostfix error: {e.Message}");
        }
    }
}
