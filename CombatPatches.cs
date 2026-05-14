using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ProjectMage.character;
using ProjectMage.character.script;
using ProjectMage.Monsters;

namespace SaS2Tweaks;

public static class CombatPatches
{
    // Block-stamina multiplier
    //
    // GameMonster.GetBlockStamina(Character) returns 0-1 representing the fraction
    // of poise damage absorbed while blocking.  We scale the result.
    [HarmonyPatch(typeof(GameMonster), "GetBlockStamina")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void GetBlockStaminaPatch(Character character, ref float __result)
    {
        var mult = GlobalSettings.BlockStaminaMultiplier.Value;
        if (Math.Abs(mult - 1f) < 0.001f) return;
        // Clamp to [0, 1]: absorbing more poise than was dealt is nonsensical.
        __result = Math.Min(__result * mult, 1f);
    }

    // Parry window + cooldown
    //
    // CharUpdate.SetParry() vanilla:
    //   parryFrame    = 0.15f   ->  9 frames at 60 fps  (perfect-parry window)
    //   parryCooldown = 1.0f   ->  seconds before next parry is allowed
    //
    // TargetMethod() lets Harmony resolve the type at runtime so we don't need a
    // compile-time reference to the internal CharUpdate class.
    private static readonly FieldInfo ParryFrameField =
        AccessTools.Field(AccessTools.TypeByName("ProjectMage.character.CharUpdate"), "parryFrame");

    private static readonly FieldInfo ParryCooldownField =
        AccessTools.Field(AccessTools.TypeByName("ProjectMage.character.CharUpdate"), "parryCooldown");

    [HarmonyPatch(typeof(CharUpdate), "SetParry")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    static void SetParryPatch(object __instance)
    {
        ParryFrameField?.SetValue(__instance, GlobalSettings.ParryWindowFrames.Value / 60f);
        ParryCooldownField?.SetValue(__instance, GlobalSettings.ParryCooldown.Value);
    }

    // Harvest drop-rate multiplier
    //
    // CharHarvest.DoHarvest calls Common.Rand.CoinToss(fData / 100f) for every potential drop. We transpile to multiply that probability by our factor, then clamp to 1.0.
    //
    // IL injected before every callvirt/call to CoinToss(float):
    //
    //   call  float32 GlobalSettings::GetDropMultiplier()
    //   mul
    //   ldc.r4 1.0
    //   call  float32 Math::Min(float32, float32)
    private static readonly MethodInfo CoinTossMethod =
        AccessTools.Method(AccessTools.TypeByName("Common.Rand"), "CoinToss", [typeof(float)]);

    private static readonly MethodInfo GetDropMultiplierMethod =
        AccessTools.Method(typeof(GlobalSettings), nameof(GlobalSettings.GetDropMultiplier));

    private static readonly MethodInfo MathMinFloat =
        AccessTools.Method(typeof(Math), "Min", [typeof(float), typeof(float)]);

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CharHarvest), "DoHarvest")]
    // ReSharper disable once UnusedMember.Local
    static IEnumerable<CodeInstruction> DoHarvestTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instr in instructions)
        {
            if ((instr.opcode == OpCodes.Callvirt || instr.opcode == OpCodes.Call)
                && instr.operand is MethodInfo mi && mi == CoinTossMethod)
            {
                // stack: [..., probability]
                yield return new CodeInstruction(OpCodes.Call, GetDropMultiplierMethod); // -> [..., prob, mult]
                yield return new CodeInstruction(OpCodes.Mul); // -> [..., prob*mult]
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1f); // -> [..., prob*mult, 1.0]
                yield return new CodeInstruction(OpCodes.Call, MathMinFloat); // -> [..., min(prob*mult,1)]
            }

            yield return instr;
        }
    }
}
