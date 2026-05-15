using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.player;

namespace SaS2Tweaks;

/// Camera aim + priority<br />
/// <br />
/// Only Player 0 calls camMgr.Update (Player.cs l.373). Inside Update:<br />
/// character  = P1's Character (first GetCharacter() result)<br />
/// character2 = P2's Character (assigned as null, then set in coop mode)<br />
/// <br />
/// P1 look-scale:<br />
/// Pattern: ldfld CharKeys.lookVec -> ldc.r4 500f<br />
/// Action:  replace 500f -> call GetP1LookScale()  (returns 500 or 0)<br />
/// <br />
/// P2 look injection:<br />
/// After the stloc that stores "vector += lookVec * scale", inject:<br />
/// vector = AddP2LookVec(vector, character2)<br />
/// The local-variable slots for 'vector' and 'character2' are found at runtime in a pre-scan pass.<br />
/// <br />
/// Camera priority:<br />
/// Pattern: ldc.r4 2f -> call Vector2.op_Division (the /2f in midpoint)<br />
/// Action:  emit op_Division as normal, then inject: call AdjustCameraBase(Vector2) -> Vector2<br />
/// The game's own "+ new Vector2(0, -100f)" continues unmodified.<br />
[HarmonyPatch]
internal static class CoopCameraPatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CamMgr), "Update")]
    private static IEnumerable<CodeInstruction> CamMgrUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var lookVecField = AccessTools.Field(typeof(CharKeys), "lookVec");
        var getCharacter = AccessTools.Method(typeof(Player), "GetCharacter");
        var getP1Scale = AccessTools.Method(typeof(CoopPatchHelpers), nameof(CoopPatchHelpers.GetP1LookScale));
        var addP2Look = AccessTools.Method(typeof(CoopPatchHelpers), nameof(CoopPatchHelpers.AddP2LookVec));
        var adjustBase = AccessTools.Method(typeof(CoopPatchHelpers), nameof(CoopPatchHelpers.AdjustCameraBase));

        // Pre-scan: find stloc for 'character' and 'character2'
        var codes = new List<CodeInstruction>(instructions);
        var charFound = false;
        CodeInstruction char2Stloc = null;
        for (var i = 0; i < codes.Count - 1; i++)
        {
            if (!charFound)
            {
                // Match: call or callvirt to GetCharacter()
                if ((codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt) &&
                    codes[i].operand is MethodInfo mi && mi == getCharacter && IsStloc(codes[i + 1]))
                {
                    charFound = true;
                }
            }
            else
            {
                // The next instruction after charFound is the stloc for 'character'.
                // The line after that is "character2 = null;"
                // We want the stloc that stores null into character2.
                if (codes[i].opcode != OpCodes.Ldnull || !IsStloc(codes[i + 1])) continue;
                char2Stloc = codes[i + 1];
                break;
            }
        }

        if (char2Stloc == null)
        {
            SaS2Tweaks.Instance.Log.LogInfo(
                "[CoopCameraPatch] character2 local not found, P2 aim-camera and CameraPriority features inactive.");
            foreach (var c in codes) yield return c;
            yield break;
        }

        // Main pass
        var lookVecSeen = false;
        var lookScaleDone = false;
        var waitVecStloc = false;
        var prevLdc2F = false;
        var camPrioDone = false;
        foreach (var instr in codes)
        {
            // P1 Look scale: replace 500f with GetP1LookScale()
            if (!lookScaleDone)
            {
                if (instr.LoadsField(lookVecField))
                {
                    lookVecSeen = true;
                    yield return instr;
                    continue;
                }

                if (lookVecSeen && instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f &&
                    Math.Abs(f - 500f) < 0.01f)
                {
                    yield return new CodeInstruction(OpCodes.Call, getP1Scale);
                    lookVecSeen = false;
                    lookScaleDone = true;
                    waitVecStloc = true;
                    continue;
                }

                lookVecSeen = false;
            }

            // P2 Look aim: inject AddP2LookVec after the vector stloc
            if (waitVecStloc && IsStloc(instr))
            {
                waitVecStloc = false;
                yield return instr; // stloc vector (P1 part)
                yield return LdlocFrom(instr); // load vector
                yield return LdlocFrom(char2Stloc); // load character2
                yield return new CodeInstruction(OpCodes.Call, addP2Look);
                yield return StlocFrom(instr); // store modified vector
                continue;
            }

            // Camera Prio: inject AdjustCameraBase after the midpoint /2f
            if (!camPrioDone)
            {
                if (prevLdc2F && instr.opcode == OpCodes.Call && instr.operand is MethodInfo divMi &&
                    divMi.DeclaringType == typeof(Vector2) &&
                    (divMi.Name.Contains("Division") || divMi.Name.Contains("Multiply")))
                {
                    yield return instr; // op_Division -> midpoint on stack
                    yield return new CodeInstruction(OpCodes.Call, adjustBase);
                    camPrioDone = true;
                    prevLdc2F = false;
                    continue;
                }

                prevLdc2F = instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f2 && Math.Abs(f2 - 2f) < 0.001f;
            }

            yield return instr;
        }

        if (!lookScaleDone)
            SaS2Tweaks.Instance.Log.LogInfo("[CoopCameraPatch] lookVec 500f not found, camera aim toggles inactive.");
        if (!camPrioDone)
            SaS2Tweaks.Instance.Log.LogInfo("[CoopCameraPatch] Midpoint /2f not found, CameraPriority inactive.");
    }

    // IL helpers
    private static bool IsStloc(CodeInstruction i) =>
        i.opcode == OpCodes.Stloc || i.opcode == OpCodes.Stloc_S || i.opcode == OpCodes.Stloc_0 ||
        i.opcode == OpCodes.Stloc_1 || i.opcode == OpCodes.Stloc_2 || i.opcode == OpCodes.Stloc_3;

    /// Creates a ldloc instruction that loads the same local as the given stloc.
    private static CodeInstruction LdlocFrom(CodeInstruction stloc)
    {
        if (stloc.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
        if (stloc.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
        if (stloc.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
        if (stloc.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
        var op = stloc.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc;
        return new CodeInstruction(op, stloc.operand);
    }

    /// Clones a stloc instruction (same opcode + operand).
    private static CodeInstruction StlocFrom(CodeInstruction stloc) => new CodeInstruction(stloc.opcode, stloc.operand);
}