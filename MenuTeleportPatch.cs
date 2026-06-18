using System.Reflection;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.player;
using ProjectMage.player.menu;

namespace SaS2Tweaks;

[HarmonyPatch]
internal static class MenuTeleportPatch
{
    // When a player opens a modal menu (inventory, map, etc.), vanilla MoveMeToCoopPlayer
    // teleports them to their partner if they are far apart / on a different layer. Suppress
    // that when the option is enabled. This only affects the menu-open auto-teleport; the
    // dedicated teleport keybind below is independent of it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerMenu), "MoveMeToCoopPlayer")]
    private static bool MoveMeToCoopPlayer_Prefix() =>
        GlobalSettings.SuppressMenuTeleport?.Value != true;

    // CharAnim.SetAnim and CharZipline.SetFromOther are internal, so reach them via AccessTools.
    private static readonly MethodInfo SetAnimMethod =
        AccessTools.Method(typeof(CharAnim), "SetAnim", [typeof(string), typeof(bool), typeof(bool)]);

    private static readonly MethodInfo SetFromOtherMethod =
        AccessTools.Method(typeof(CharZipline), "SetFromOther", [typeof(Character)]);

    /// Dedicated teleport: warps the given player to their co-op partner unconditionally.
    /// Unlike vanilla MoveMeToCoopPlayer this ignores the distance/layer check and does not go
    /// through the menu path, so it always fires in gameplay when the keybind is pressed,
    /// regardless of the "Suppress Menu Teleport" setting. Mirrors vanilla's warp setup.
    internal static void RequestTeleport(Player player)
    {
        if (player == null) return;

        var partner = GetLocalPartner(player);
        if (partner == null) return;

        var chars = CharMgr.character;
        if (chars == null) return;
        if (player.charIdx < 0 || player.charIdx >= chars.Length) return;
        if (partner.charIdx < 0 || partner.charIdx >= chars.Length) return;

        var me = chars[player.charIdx];
        var other = chars[partner.charIdx];
        if (me == null || other == null) return;
        if (me.warp == null || me.anim == null || me.zipline == null) return;
        if (me.dyingFrame > 0f || other.dyingFrame > 0f) return;

        me.loc = other.loc;
        me.loc.Y = CharCols.GetGround(me.loc);

        if (me.state != 0)
        {
            me.state = 0;
            SetAnimMethod?.Invoke(me.anim, ["land", false, true]);
        }

        if (other.state == 5)
            SetFromOtherMethod?.Invoke(me.zipline, [other]);
        else
            me.zipline.active = false;

        me.warp.active = true;
        me.warp.warpDest = me.loc;
        me.warp.warpOrig = me.loc;
        me.warp.warpPoint = 0.5f;
        me.warp.warpDuration = 1f;
        me.warp.warpFrame = 0.5f;
    }

    // The other local, active player (the co-op partner).
    private static Player GetLocalPartner(Player me)
    {
        var players = PlayerMgr.player;
        if (players == null) return null;
        foreach (var p in players)
            if (p != null && p != me && p.isLocal && p.active)
                return p;
        return null;
    }
}
