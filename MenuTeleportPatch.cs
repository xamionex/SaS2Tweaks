using HarmonyLib;
using ProjectMage.player.menu;

namespace SaS2Tweaks;

[HarmonyPatch]
internal static class MenuTeleportPatch
{
    // When a player opens a modal menu (inventory, map, etc.),
    // the vanilla MoveMeToCoopPlayer teleports them to their partner
    // if they are >1000 units apart or on different layers.
    // Skip it when the option is enabled.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerMenu), "MoveMeToCoopPlayer")]
    private static bool MoveMeToCoopPlayer_Prefix()
    {
        if (GlobalSettings.SuppressMenuTeleport?.Value != true) return true;
        return false;
    }
}
