using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Timers;
using BepInEx;
using BepInEx.NET.Common;
using HarmonyLib;

namespace SaS2Tweaks;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
[BepInDependency("amione.SaS2ModOptions", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("amione.SaS2DevTools", BepInDependency.DependencyFlags.SoftDependency)]
// ReSharper disable once ClassNeverInstantiated.Global
public class SaS2Tweaks : BasePlugin
{
    internal static SaS2Tweaks Instance;
    private Harmony _harmony;
    private FileSystemWatcher _configWatcher;
    private Timer _debounceTimer;

    public override void Load()
    {
        Instance = this;

        GlobalSettings.Bind(Config);

        var modOptionsType = Type.GetType("SaS2ModOptions.SaS2ModOptions, amione.SaS2ModOptions");
        if (modOptionsType != null)
        {
            TryRegisterModOptions();
            Log.LogInfo("Registered configs with SaS2ModOptions.");
        }
        else
        {
            Log.LogInfo("SaS2ModOptions not present, config file only.");
        }

        var dir = Path.GetDirectoryName(Config.ConfigFilePath);
        var file = Path.GetFileName(Config.ConfigFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            _configWatcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(1000) { AutoReset = false };
            _debounceTimer.Elapsed += (_, _) =>
            {
                Config.Reload();
                Log.LogInfo("Configuration reloaded.");
            };
            _configWatcher.Changed += (_, _) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
        }

        _harmony = new Harmony(PluginInfo.PluginGuid);
        _harmony.PatchAll();
        Log.LogInfo($"{PluginInfo.PluginName} v{PluginInfo.PluginVersion} loaded.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TryRegisterModOptions()
    {
        var order = 0;
        string cat;

        // Stat Regen
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.HealthRegenRate,
            cat = "Tweaks - Regen", "HP Regen Rate", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.StaminaRegenRate,
            cat, "Stamina Regen Rate", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.ManaRegenRate,
            cat, "Mana Regen Rate", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.RageRegenRate,
            cat, "Rage Regen Rate", order += 1);

        // Consumable Regen
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.HealthPotionRegenEnabled,
            cat, "Health Potion Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.HealthPotionRegenDelay,
            cat, "  Health Potion Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.FocusPotionRegenEnabled,
            cat, "Focus Potion Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.FocusPotionRegenDelay,
            cat, "  Focus Potion Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.RangedAmmoRegenEnabled,
            cat, "Ammo Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.RangedAmmoRegenDelay,
            cat, "  Ammo Regen Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.GrayPearlRegenEnabled,
            cat, "Gray Pearl Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.GrayPearlRegenDelay,
            cat, "  Gray Pearl Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.GrayPearlRegenLimit,
            cat, "  Gray Pearl Limit", order += 1);

        // General / Damage / Combat
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.DoNotDropSaltOnDeath,
            cat = "Tweaks - Combat", "No Salt Drop on Death", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.MouseCursorInversionDisabled,
            cat, "Disable Cursor Inversion", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.PlayerDamageMultiplier,
            cat, "Player Damage Multiplier", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.PlayerDefenseMultiplier,
            cat, "Player Defense Multiplier", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.ParryWindowFrames,
            cat, "Parry Window (frames)", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.ParryCooldown,
            cat, "Parry Cooldown (sec)", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.BlockStaminaMultiplier,
            cat, "Block Stamina Multiplier", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.DropRateMultiplier,
            cat, "Drop Rate Multiplier", order += 1);

        // Debug
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.DebugInfoEnabled,
            "Tweaks - Debug", "Debug Overlay", order += 1);

        // Co-op
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.P2CanTriggerDoors,
            cat = "Tweaks - Coop", "P2 Can Trigger Doors", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.SuppressMenuTeleport,
            cat, "Suppress Menu Teleport", order += 1);

        // Camera
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.Player1AimsCamera,
            cat, "P1 Aims Camera", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.Player2AimsCamera,
            cat, "P2 Aims Camera", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.CameraPriority,
            cat, "Camera Priority", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.Player1MovesCameraWhenAiming,
            cat, "P1 Moves Camera When Aiming", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.Player2MovesCameraWhenAiming,
            cat, "P2 Moves Camera When Aiming", order += 1);

    }

    public override bool Unload()
    {
        _configWatcher?.Dispose();
        _debounceTimer?.Dispose();
        _harmony?.UnpatchSelf();
        return base.Unload();
    }
}
