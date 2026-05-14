using System;
using BepInEx.Configuration;

namespace SaS2Tweaks;

public static class GlobalSettings
{
    // Stat Regen
    public static ConfigEntry<float> HealthRegenRate;
    public static ConfigEntry<float> StaminaRegenRate;
    public static ConfigEntry<float> ManaRegenRate;
    public static ConfigEntry<float> RageRegenRate;

    // Consumable Regen
    public static ConfigEntry<bool> HealthPotionRegenEnabled;
    public static ConfigEntry<int> HealthPotionRegenDelay;

    public static ConfigEntry<bool> FocusPotionRegenEnabled;
    public static ConfigEntry<int> FocusPotionRegenDelay;

    public static ConfigEntry<bool> RangedAmmoRegenEnabled;
    public static ConfigEntry<int> RangedAmmoRegenDelay;

    public static ConfigEntry<bool> GrayPearlRegenEnabled;
    public static ConfigEntry<int> GrayPearlRegenDelay;
    public static ConfigEntry<int> GrayPearlRegenLimit;

    // General
    public static ConfigEntry<bool> MouseCursorInversionDisabled;
    public static ConfigEntry<bool> DoNotDropSaltOnDeath;

    // Damage
    public static ConfigEntry<float> PlayerDamageMultiplier;
    public static ConfigEntry<float> PlayerDefenseMultiplier;

    // Combat Tweaks
    /// Perfect-parry window, expressed in frames at 60 fps. Default 9 = 0.15 s (vanilla).
    public static ConfigEntry<int> ParryWindowFrames;

    /// Seconds before the player can parry again. Default 1.0 (vanilla).
    public static ConfigEntry<float> ParryCooldown;

    /// Multiplier applied to the block-stamina value returned by the equipped weapon.
    /// 1 = vanilla; 2 = absorbs twice as much poise damage while blocking.
    public static ConfigEntry<float> BlockStaminaMultiplier;

    /// Multiplier applied to every harvest drop-chance roll.
    /// 1 = vanilla; 2 = double chance; values above ~10 effectively guarantee drops.
    public static ConfigEntry<float> DropRateMultiplier;

    // Debug
    public static ConfigEntry<bool> DebugInfoEnabled;

    // Helpers used by transpilers at runtime

    /// <summary>
    /// Adjusts a vanilla cursor angle based on mouse cursor inversion settings.
    /// </summary>
    /// <param name="vanillaAngle">
    /// The original cursor angle in radians, typically provided by the game engine.
    /// </param>
    /// <returns>
    /// The adjusted cursor angle in radians. If inversion is disabled, the original angle is returned.
    /// When inversion is enabled, special handling is applied for the right-side angle (-π/4) and left-side subtractor (π/2):
    /// <list type="bullet">
    /// <item>If the input angle is approximately -π/4, returns -π/4 - π/2 = -3π/4.</item>
    /// <item>If the input angle is approximately π/2, returns 0.</item>
    /// <item>Otherwise, returns the original angle unchanged.</item>
    /// </list>
    /// </returns>
    public static float CursorAngle(float vanillaAngle)
    {
        if (MouseCursorInversionDisabled?.Value != true)
            return vanillaAngle;

        const float rightSideAngle = -0.7853982f;   // -π/4
        const float leftSideSubtract = 1.570796f;   //  π/2

        // If this is the right-side constant, return the fixed angle.
        if (Math.Abs(vanillaAngle - rightSideAngle) < 0.0001f)
            return rightSideAngle - leftSideSubtract;

        // If this is the left-side subtractor, return 0 to cancel subtraction.
        // Any other val gets returned normally
        return Math.Abs(vanillaAngle - leftSideSubtract) < 0.0001f ? 0f : vanillaAngle;
    }

    /// Returns the configured drop-rate multiplier (clamped ≥ 0).
    public static float GetDropMultiplier()
        => DropRateMultiplier?.Value is float v and > 0f ? v : 1f;

    public static void Bind(ConfigFile cfg)
    {
        // Stat Regen
        HealthRegenRate = cfg.Bind("Stat Regen", "Health Regen Rate", 0f,
            new ConfigDescription("HP/s regenerated while alive (0 = off).",
                new AcceptableValueRange<float>(0f, 20f)));
        StaminaRegenRate = cfg.Bind("Stat Regen", "Stamina Regen Rate", 2f,
            new ConfigDescription("Stamina/s added on top of the natural game regen (0 = off).",
                new AcceptableValueRange<float>(0f, 20f)));
        ManaRegenRate = cfg.Bind("Stat Regen", "Mana Regen Rate", 0f,
            new ConfigDescription("MP/s regenerated while alive (0 = off).",
                new AcceptableValueRange<float>(0f, 10f)));
        RageRegenRate = cfg.Bind("Stat Regen", "Rage Regen Rate", 0f,
            new ConfigDescription("Rage/s regenerated while alive (0 = off).",
                new AcceptableValueRange<float>(0f, 10f)));

        // Consumable Regen
        HealthPotionRegenEnabled = cfg.Bind("Health Potion", "Enable Regen", false,
            "Automatically refill one health potion every N seconds.");
        HealthPotionRegenDelay = cfg.Bind("Health Potion", "Regen Delay (sec)", 600,
            new ConfigDescription("Seconds between each auto-refilled health potion.",
                new AcceptableValueRange<int>(10, 36000)));

        FocusPotionRegenEnabled = cfg.Bind("Focus Potion", "Enable Regen", false,
            "Automatically refill one focus potion every N seconds.");
        FocusPotionRegenDelay = cfg.Bind("Focus Potion", "Regen Delay (sec)", 600,
            new ConfigDescription("Seconds between each auto-refilled focus potion.",
                new AcceptableValueRange<int>(10, 36000)));

        RangedAmmoRegenEnabled = cfg.Bind("Ranged Ammo", "Enable Regen", false,
            "Automatically add one arrow every N seconds.");
        RangedAmmoRegenDelay = cfg.Bind("Ranged Ammo", "Regen Delay (sec)", 120,
            new ConfigDescription("Seconds between each auto-refilled arrow.",
                new AcceptableValueRange<int>(10, 36000)));

        GrayPearlRegenEnabled = cfg.Bind("Gray Pearl", "Enable Regen", false,
            "Automatically add one gray pearl every N seconds (up to the limit).");
        GrayPearlRegenDelay = cfg.Bind("Gray Pearl", "Regen Delay (sec)", 300,
            new ConfigDescription("Seconds between each auto-refilled gray pearl.",
                new AcceptableValueRange<int>(10, 36000)));
        GrayPearlRegenLimit = cfg.Bind("Gray Pearl", "Regen Limit", 20,
            new ConfigDescription("Stop regenerating when you own this many gray pearls.",
                new AcceptableValueRange<int>(1, 100)));

        // General
        MouseCursorInversionDisabled = cfg.Bind("General", "Disable Mouse Cursor Inversion", true,
            "Prevents the cursor sprite from flipping when the cursor is on the left side of the screen.");
        DoNotDropSaltOnDeath = cfg.Bind("General", "No Salt Drop on Death", false,
            "Disables losing salt on death.");

        // Damage
        PlayerDamageMultiplier = cfg.Bind("Damage", "Player Damage Multiplier", 1f,
            new ConfigDescription("Multiplies all outgoing player damage. 2 = double damage.",
                new AcceptableValueRange<float>(0f, 50f)));
        PlayerDefenseMultiplier = cfg.Bind("Damage", "Player Defense Multiplier", 1f,
            new ConfigDescription("Multiplies all player defense values. 2 = double defence.",
                new AcceptableValueRange<float>(0f, 50f)));

        // Combat Tweaks
        ParryWindowFrames = cfg.Bind("Combat", "Parry Window (frames)", 9,
            new ConfigDescription(
                "Duration of the perfect-parry window in frames at 60 fps. Vanilla = 9 (0.15 s).",
                new AcceptableValueRange<int>(1, 60)));
        ParryCooldown = cfg.Bind("Combat", "Parry Cooldown (sec)", 1f,
            new ConfigDescription(
                "Seconds the player must wait before they can parry again. Vanilla = 1.0.",
                new AcceptableValueRange<float>(0f, 10f)));
        BlockStaminaMultiplier = cfg.Bind("Combat", "Block Stamina Multiplier", 1f,
            new ConfigDescription(
                "Multiplier on block-stamina (poise damage reduction while blocking). " +
                "1 = vanilla; 2 = blocks absorb twice as much poise damage.",
                new AcceptableValueRange<float>(0f, 5f)));
        DropRateMultiplier = cfg.Bind("Loot", "Drop Rate Multiplier", 1f,
            new ConfigDescription(
                "Multiplier applied to every harvest item drop-chance roll. " +
                "1 = vanilla; 2 = doubled chance; high values (~10+) near-guarantee drops.",
                new AcceptableValueRange<float>(0f, 20f)));

        // Debug
        DebugInfoEnabled = cfg.Bind("Debug", "Show Debug Info", false,
            "Draws regen timer values on screen.");
    }
}