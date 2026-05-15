using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.player;

namespace SaS2Tweaks;

/// P2 triggering layer-change doors<br />
/// <br />
/// CharMovement.MoveCoopPlayers(int pLayer, Vector2 pLoc) is private.<br />
/// [HarmonyTargetMethod] is used to point at it without a string literal.<br />
/// <br />
/// Inside the method, "this.c.loc = pLoc" appears in three places:<br />
///   1. Screen-edge constraint (less than 5 % or greater than 95 % of scroll width).<br />
///   2. PVP-mode reset.<br />
///   3. TARGET: "else if (GetLocalCoopPlayer().ID == playerIdx)" branch.<br />
///<br />
/// We count calls to GetLocalCoopPlayer(). The 3rd call is the condition-check<br />
/// for that else-if branch. The very next "stfld Character::loc" after that<br />
/// 3rd call is the assignment we want to intercept.<br />
///<br />
/// At that point the evaluation stack holds exactly:<br />
/// [Character (this.c), Vector2 (pLoc)]<br />
/// HandleP2LayerChange(Character, Vector2) -> void consumes the same two values,<br />
/// so we can do a straight one-for-one replacement of stfld -> call.<br />
[HarmonyPatch]
internal static class CoopDoorPatch
{
    [HarmonyPatch(typeof(CharMovement), "MoveCoopPlayers", typeof(int), typeof(Vector2))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getLocalCoopPlayer = AccessTools.Method(typeof(PlayerMgr), "GetLocalCoopPlayer");
        var locField = AccessTools.Field(typeof(Character), "loc");
        var handler = AccessTools.Method(typeof(CoopPatchHelpers), nameof(CoopPatchHelpers.HandleP2LayerChange));
        var coopCalls = 0;
        var waiting = false;
        var patched = false;
        foreach (var instr in instructions)
        {
            if (!patched && instr.opcode == OpCodes.Call && instr.operand is MethodInfo mi && mi == getLocalCoopPlayer)
            {
                if (++coopCalls == 3) waiting = true;
            }

            if (waiting && !patched && instr.opcode == OpCodes.Stfld && instr.operand is FieldInfo fi && fi == locField)
            {
                // Stack: [Character, Vector2], matches HandleP2LayerChange signature.
                yield return new CodeInstruction(OpCodes.Call, handler);
                patched = true;
                continue; // drop the original stfld
            }

            yield return instr;
        }

        if (!patched)
            SaS2Tweaks.Instance.Log.LogInfo("[CoopDoorPatch] Target stfld not found, P2 door feature inactive.");
    }
}