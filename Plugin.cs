using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Timers;
using BepInEx;
using BepInEx.NET.Common;
using HarmonyLib;

namespace SaS2Tweaks;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
[BepInDependency("amione.SaS2ModOptions")]
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

        // Stat Regen
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.HealthRegenRate, PluginInfo.PluginName,
            "HP Regen Rate", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.StaminaRegenRate, PluginInfo.PluginName,
            "Stamina Regen Rate", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.ManaRegenRate, PluginInfo.PluginName,
            "Mana Regen Rate", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.RageRegenRate, PluginInfo.PluginName,
            "Rage Regen Rate", order += 1);

        // Consumable Regen
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.HealthPotionRegenEnabled, PluginInfo.PluginName,
            "Health Potion Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.HealthPotionRegenDelay, PluginInfo.PluginName,
            "  Health Potion Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.FocusPotionRegenEnabled, PluginInfo.PluginName,
            "Focus Potion Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.FocusPotionRegenDelay, PluginInfo.PluginName,
            "  Focus Potion Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.RangedAmmoRegenEnabled, PluginInfo.PluginName,
            "Ammo Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.RangedAmmoRegenDelay, PluginInfo.PluginName,
            "  Ammo Regen Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.GrayPearlRegenEnabled, PluginInfo.PluginName,
            "Gray Pearl Auto-Regen", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.GrayPearlRegenDelay, PluginInfo.PluginName,
            "  Gray Pearl Delay", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.GrayPearlRegenLimit, PluginInfo.PluginName,
            "  Gray Pearl Limit", order += 1);

        // General / Damage
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.DoNotDropSaltOnDeath, PluginInfo.PluginName,
            "No Salt Drop on Death", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.MouseCursorInversionDisabled, PluginInfo.PluginName,
            "Disable Cursor Inversion", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.PlayerDamageMultiplier, PluginInfo.PluginName,
            "Player Damage Multiplier", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.PlayerDefenseMultiplier, PluginInfo.PluginName,
            "Player Defense Multiplier", order += 1);

        // Combat Tweaks
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.ParryWindowFrames, PluginInfo.PluginName,
            "Parry Window (frames)", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.ParryCooldown, PluginInfo.PluginName,
            "Parry Cooldown (sec)", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.BlockStaminaMultiplier, PluginInfo.PluginName,
            "Block Stamina Multiplier", order += 1);
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.DropRateMultiplier, PluginInfo.PluginName,
            "Drop Rate Multiplier", order += 1);

        // Debug
        SaS2ModOptions.SaS2ModOptions.RegisterConfig(GlobalSettings.DebugInfoEnabled, PluginInfo.PluginName,
            "Debug Overlay", order += 1);
    }

    public override bool Unload()
    {
        _configWatcher?.Dispose();
        _debounceTimer?.Dispose();
        _harmony?.UnpatchSelf();
        return base.Unload();
    }
}