using System;
using BepInEx.Configuration;

namespace SaS2Tweaks;

/// Which player the camera focuses on in local co-op.
public enum CameraPriorityMode
{
    /// Camera centers between both players (vanilla behavior).
    Midpoint,

    /// Camera follows Player 1.
    Player1,

    /// Camera follows Player 2.
    Player2
}

public static class GlobalSettings
{
    /// When true, aiming with a ranged weapon moves the camera toward the aim direction (vanilla).
    /// When false, the camera stays fixed on the player.
    public static ConfigEntry<bool> Player1MovesCameraWhenAiming;

    /// Same for Player 2 in local co-op.
    public static ConfigEntry<bool> Player2MovesCameraWhenAiming;

    /// When true, Player 2 can walk through layer-change doors independently.
    /// Doing so teleports Player 1 to Player 2's new position instead of resetting Player 2 back to Player 1.
    public static ConfigEntry<bool> P2CanTriggerDoors;

    /// When true, opening a menu (inventory, map, etc.) will not teleport the menu-opener to their coop partner.
    public static ConfigEntry<bool> SuppressMenuTeleport;

    /// When true, Player 1's right-stick aim offsets the camera (vanilla).
    /// Set false to stop P1's aim from panning the view.
    public static ConfigEntry<bool> Player1AimsCamera;

    /// When true, Player 2's right-stick aim also offsets the camera.
    /// Has no effect outside local co-op, or when Player 2 is dead.
    public static ConfigEntry<bool> Player2AimsCamera;

    /// Controls which player the camera centres on in local co-op.
    /// Midpoint = vanilla (between both); Player1 = follow P1; Player2 = follow P2.
    public static ConfigEntry<CameraPriorityMode> CameraPriority;

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

    // Combat
    /// Perfect-parry window, expressed in frames at 60 fps.
    /// Default 9 = 0.15 s (vanilla).
    public static ConfigEntry<int> ParryWindowFrames;

    /// Seconds before the player can parry again.
    /// Default 1.0 (vanilla).
    public static ConfigEntry<float> ParryCooldown;

    /// Multiplier applied to the block-stamina value returned by the equipped weapon.
    /// 1 = vanilla; 2 = absorbs twice as much poise damage while blocking.
    public static ConfigEntry<float> BlockStaminaMultiplier;

    /// Multiplier applied to every harvest drop-chance roll.
    /// 1 = vanilla; 2 = double chance; values above ~10 effectively guarantee drops.
    public static ConfigEntry<float> DropRateMultiplier;

    // Debug
    public static ConfigEntry<bool> DebugInfoEnabled;

    // -- Transpiler helpers ----------------------------------------------------

    /// Adjusts a vanilla cursor angle based on mouse cursor inversion settings.
    /// When inversion is disabled, replaces the right-side constant with the left-side one,
    /// so the cursor sprite points the same direction on both halves of the screen.
    public static float CursorAngle(float vanillaAngle)
    {
        if (MouseCursorInversionDisabled?.Value != true)
            return vanillaAngle;

        const float rightSideAngle = -0.7853982f; // -π/4
        const float leftSideSubtract = 1.570796f; //  π/2

        // Right-side constant: return the combined left-side angle.
        if (Math.Abs(vanillaAngle - rightSideAngle) < 0.0001f)
            return rightSideAngle - leftSideSubtract;

        // Left-side subtractor: return 0 to cancel the subtraction.
        // Any other value passes through unchanged.
        return Math.Abs(vanillaAngle - leftSideSubtract) < 0.0001f ? 0f : vanillaAngle;
    }

    /// Returns the configured drop-rate multiplier (clamped to at least 0).
    public static float GetDropMultiplier() =>
        DropRateMultiplier?.Value is { } v and > 0f ? v : 1f;

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

        // Combat
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

        // Co-op
        P2CanTriggerDoors = cfg.Bind("Co-op", "P2 Can Trigger Doors", true,
            "When enabled, Player 2 can walk through layer-change doors independently. " +
            "Player 1 is teleported to Player 2's destination instead of P2 being snapped back.");
        SuppressMenuTeleport = cfg.Bind("Co-op", "Suppress Menu Teleport", true,
            "When enabled, opening a menu (inventory, map, etc.) will not teleport " +
            "the menu-opener to their coop partner.");

        // Camera
        Player1AimsCamera = cfg.Bind("Camera", "Player1 Aims Camera", true,
            "When true (vanilla), Player 1's right stick pans the camera while aiming. " +
            "Set false to prevent P1's aim from moving the view.");
        Player2AimsCamera = cfg.Bind("Camera", "Player2 Aims Camera", false,
            "When true, Player 2's right stick also pans the shared camera while aiming.");
        CameraPriority = cfg.Bind("Camera", "Camera Priority", CameraPriorityMode.Midpoint,
            "Controls which player the co-op camera centres on.\n" +
            " Midpoint, vanilla: halfway between both players.\n" +
            " Player1, camera follows Player 1.\n Player2, camera follows Player 2.");
        Player1MovesCameraWhenAiming = cfg.Bind("Camera", "P1 Moves Camera When Aiming", false,
            "When true, aiming a bow moves the camera toward the aim direction.\n" +
            "False keeps the camera fixed on the player.");
        Player2MovesCameraWhenAiming = cfg.Bind("Camera", "P2 Moves Camera When Aiming", false,
            "When true, aiming a bow moves the camera toward the aim direction.\n" +
            "False keeps the camera fixed on the player.");

    }
}