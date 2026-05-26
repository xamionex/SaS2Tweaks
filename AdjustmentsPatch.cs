using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Common;
using HarmonyLib;
using LootHero.loot;
using Menumancer.hud;
using ProjectMage.character;
using ProjectMage.config;
using ProjectMage.gamestate;
using ProjectMage.player;

namespace SaS2Tweaks;

[HarmonyPatch]
internal static class AdjustmentsPatch
{
    private static readonly MethodInfo GetMainPlayerMethod = AccessTools.Method(typeof(PlayerMgr), "GetMainPlayer");
    private static readonly MethodInfo IsLocalCoopMethod = AccessTools.Method(typeof(PlayerMgr), "IsLocalCoopMode");

    private static readonly MethodInfo GetLocalCoopPlayerMethod =
        AccessTools.Method(typeof(PlayerMgr), "GetLocalCoopPlayer");

    private static readonly MethodInfo GetMaxStaminaMethod = AccessTools.Method(typeof(PlayerStats), "GetMaxStamina",
        [typeof(bool)]);

    private static readonly MethodInfo GetMaxHpMethod = AccessTools.Method(typeof(PlayerStats), "GetMaxHP",
        [typeof(bool)]);

    private static readonly MethodInfo GetMaxMpMethod = AccessTools.Method(typeof(PlayerStats), "GetMaxMP",
        [typeof(bool)]);

    private static readonly MethodInfo GetMaxRageMethod = AccessTools.Method(typeof(PlayerStats), "GetMaxRage");

    private static readonly MethodInfo AddItemMethod = AccessTools.Method(typeof(Player), "AddItem",
        [typeof(string), typeof(int), typeof(bool)]);

    // SaS2Pauser interop, null when the mod is not installed
    private static readonly MethodInfo SaS2PauserIsPausedMethod =
        AccessTools.Method(AccessTools.TypeByName("SaS2Pauser.PausePatch"), "IsPaused");

    private static readonly object[] TrueArg = [true];
    private static readonly object[] EmptyArgs = [];

    // Regen delay timers (per-tick countdown, reset on replenish)
    private static float _healthPotionTimer;
    private static float _focusPotionTimer;
    private static float _rangedAmmoTimer;
    private static float _grayPearlTimer;

    private static void ResetTimers()
    {
        _healthPotionTimer = GlobalSettings.HealthPotionRegenDelay.Value;
        _focusPotionTimer = GlobalSettings.FocusPotionRegenDelay.Value;
        _rangedAmmoTimer = GlobalSettings.RangedAmmoRegenDelay.Value;
        _grayPearlTimer = GlobalSettings.GrayPearlRegenDelay.Value;
    }

    private static Player GetMainPlayer() =>
        GetMainPlayerMethod != null ? (Player)GetMainPlayerMethod.Invoke(null, null) : null;

    private static bool IsLocalCoop() =>
        IsLocalCoopMethod != null && (bool)IsLocalCoopMethod.Invoke(null, null);

    private static Player GetLocalCoopPlayer() =>
        GetLocalCoopPlayerMethod != null ? (Player)GetLocalCoopPlayerMethod.Invoke(null, null) : null;

    private static float GetMaxStamina(PlayerStats stats) =>
        GetMaxStaminaMethod != null ? (float)GetMaxStaminaMethod.Invoke(stats, TrueArg) : 0f;

    private static float GetMaxHp(PlayerStats stats) =>
        GetMaxHpMethod != null ? (float)GetMaxHpMethod.Invoke(stats, TrueArg) : 0f;

    private static float GetMaxMp(PlayerStats stats) =>
        GetMaxMpMethod != null ? (float)GetMaxMpMethod.Invoke(stats, TrueArg) : 0f;

    private static float GetMaxRage(PlayerStats stats) =>
        GetMaxRageMethod != null ? (float)GetMaxRageMethod.Invoke(stats, EmptyArgs) : 0f;

    private static void AddItem(Player player, string loot, int count, bool dontList = false) =>
        AddItemMethod?.Invoke(player, [loot, count, dontList]);

    /// Returns true when SaS2Pauser is installed and currently has the game paused.
    private static bool IsGamePaused() =>
        SaS2PauserIsPausedMethod != null && (bool)SaS2PauserIsPausedMethod.Invoke(null, null);

    private static int CountGrayPearls(PlayerEquipment equipment)
    {
        return (from item in equipment.invItem
            let def = LootCatalog.lootDef[item.lootIdx]
            where def.type == 4 && def.flags != null && def.flags.Contains(5) && item.count > 0
            select item.count).Sum();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerEquipment), "ReplenishConsumables")]
    public static void AfterReplenishConsumables(bool useStockpile) => ResetTimers();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerMgr), "Init")]
    public static void AfterPlayerMgrInit() => ResetTimers();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerMgr), "Update")]
    public static void PlayerMgrUpdate_Postfix(float frameTime, float realTime)
    {
        if (GameState.state != 1) return;

        // Skip all regen while SaS2Pauser has the game paused.
        if (IsGamePaused()) return;

        var main = GetMainPlayer();
        if (main != null) RegeneratePlayer(main, frameTime);

        if (IsLocalCoop())
        {
            var coop = GetLocalCoopPlayer();
            if (coop != null) RegeneratePlayer(coop, frameTime);
        }
    }

    private static void RegeneratePlayer(Player player, float frameTime)
    {
        if (player.charIdx < 0 || !player.active) return;

        var character = CharMgr.character[player.charIdx];
        if (character.hp <= 0f) return;

        // Stat Regen
        var hpRate = GlobalSettings.HealthRegenRate.Value;
        if (hpRate > 0f)
            character.hp = Math.Min(character.hp + hpRate * frameTime, GetMaxHp(player.stats));

        var stamRate = GlobalSettings.StaminaRegenRate.Value;
        if (stamRate > 0f)
            character.stamina = Math.Min(character.stamina + stamRate * frameTime, GetMaxStamina(player.stats));

        var mpRate = GlobalSettings.ManaRegenRate.Value;
        if (mpRate > 0f)
            character.mp = Math.Min(character.mp + mpRate * frameTime, GetMaxMp(player.stats));

        var rageRate = GlobalSettings.RageRegenRate.Value;
        if (rageRate > 0f)
            character.rage = Math.Min(character.rage + rageRate * frameTime, GetMaxRage(player.stats));

        // Consumable Regen
        if (GlobalSettings.HealthPotionRegenEnabled.Value)
        {
            _healthPotionTimer -= frameTime;
            if (_healthPotionTimer <= 0f)
            {
                _healthPotionTimer = GlobalSettings.HealthPotionRegenDelay.Value;
                AddItem(player, "health_potion", 1);
            }
        }

        if (GlobalSettings.FocusPotionRegenEnabled.Value)
        {
            _focusPotionTimer -= frameTime;
            if (_focusPotionTimer <= 0f)
            {
                _focusPotionTimer = GlobalSettings.FocusPotionRegenDelay.Value;
                AddItem(player, "focus_potion", 1);
            }
        }

        if (GlobalSettings.RangedAmmoRegenEnabled.Value)
        {
            _rangedAmmoTimer -= frameTime;
            if (_rangedAmmoTimer <= 0f)
            {
                _rangedAmmoTimer = GlobalSettings.RangedAmmoRegenDelay.Value;
                AddItem(player, "arrow", 1, dontList: true);
            }
        }

        if (GlobalSettings.GrayPearlRegenEnabled.Value)
        {
            _grayPearlTimer -= frameTime;
            if (_grayPearlTimer <= 0f)
            {
                _grayPearlTimer = GlobalSettings.GrayPearlRegenDelay.Value;
                if (CountGrayPearls(player.equipment) < GlobalSettings.GrayPearlRegenLimit.Value)
                    AddItem(player, "gray_pearl", 1, dontList: true);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerEquipment), "PopulateHVals")]
    public static void PopulateHVals_Postfix(float[] hVals, int equipType, bool runicArt = false, bool scaled = true)
    {
        var mult = GlobalSettings.PlayerDamageMultiplier.Value;
        if (Math.Abs(mult - 1f) < 0.001f) return;
        if (hVals.Length != 6) return;
        if (equipType != 4 && equipType != 5 && equipType != 6) return;
        for (var i = 0; i < 6; i++) hVals[i] *= mult;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerEquipment), "GetDefense")]
    // ReSharper disable once InconsistentNaming
    public static void GetDefense_Postfix(int damageType, bool scaled, ref float __result)
    {
        __result *= GlobalSettings.PlayerDefenseMultiplier.Value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStats), "CreateXPPile")]
    public static bool CreateXPPile_Prefix() => !GlobalSettings.DoNotDropSaltOnDeath.Value;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerMgr), "Draw")]
    public static void PlayerMgrDraw_Postfix()
    {
        if (!GlobalSettings.DebugInfoEnabled.Value) return;
        var sb = new StringBuilder();
        sb.AppendFormat(
            $"HPPot({(GlobalSettings.HealthPotionRegenEnabled.Value ? "On" : "Off")}): {(int)_healthPotionTimer}s | Focus({(GlobalSettings.FocusPotionRegenEnabled.Value ? "On" : "Off")}): {(int)_focusPotionTimer}s | Ammo({(GlobalSettings.RangedAmmoRegenEnabled.Value ? "On" : "Off")}): {(int)_rangedAmmoTimer}s | GrayPearl({(GlobalSettings.GrayPearlRegenEnabled.Value ? "On" : "Off")}): {(int)_grayPearlTimer}s | GameState: {GameState.state}"
        );
        Text.DrawText(sb, new Vector2(100f, 220f), Color.White, 0.3f, 0);
    }

    // Mouse cursor inversion disable.
    //
    // MouseMgr.Draw uses two rotation constants for the left-half-of-screen cursor inversion:
    //   -0.7853982 (-π/4)  -- the inversion angle applied on the left side.
    //    1.570796  ( π/2)  -- the normal "pointing up" rotation.
    //
    // We intercept every occurrence of either constant,
    // and pass it through GlobalSettings.CursorAngle(float),
    // which replaces -π/4 with π/2 when inversion is disabled,
    // so both sides of the screen use the same angle.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MouseMgr), "Draw")]
    private static IEnumerable<CodeInstruction> MouseMgrDraw_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var cursorAngleMethod = AccessTools.Method(typeof(GlobalSettings), nameof(GlobalSettings.CursorAngle));

        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f)
            {
                if (Math.Abs(f - -0.7853982f) < 0.001f || Math.Abs(f - 1.570796f) < 0.001f)
                {
                    // push the vanilla float, then call CursorAngle(float),
                    // which returns either the same value (inversion on) or the corrected angle (off)
                    yield return instr;
                    yield return new CodeInstruction(OpCodes.Call, cursorAngleMethod);
                    continue;
                }
            }

            yield return instr;
        }
    }
}