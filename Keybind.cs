using System;
using BepInEx.Configuration;
using Common;

namespace SaS2Tweaks;

/// <summary>
/// Read-only view of a rebindable key/button combo stored in a string config entry
/// (format "KbMod|KbKey|PadMod|PadButton"). Tweaks only evaluates binds; rebinding /
/// capture lives in the Mod Options menu (which writes the same string), so this copy
/// deliberately mirrors only the evaluation half of SaS2ModOptions.Keybind.
///
/// Kept local so Tweaks does not hard-depend on the (soft-dependency) SaS2ModOptions assembly.
/// </summary>
public sealed class Keybind
{
    // Gamepad button codes (match ProjectMage InputProfile conventions).
    public const int PadA = -14, PadB = -15, PadX = -16, PadY = -17;
    public const int PadLb = -20, PadRb = -18, PadLt = -21, PadRt = -19;
    public const int PadLs = -28, PadRs = -29, PadStart = -22, PadBack = -23;
    public const int PadDLeft = -10, PadDRight = -11, PadDUp = -12, PadDDown = -13;

    private const Keys KeyNone = (Keys)0;

    private Keys _kbMod, _kbKey;
    private int _padMod, _padButton;

    private readonly ConfigEntry<string> _cfg;
    private string _lastRaw;

    public Keybind(ConfigEntry<string> cfg)
    {
        _cfg = cfg;
        SyncFromConfig();
    }

    /// The backing string config entry, to register with the Mod Options menu.
    public ConfigEntry<string> Config => _cfg;

    private void SyncFromConfig()
    {
        if (_cfg == null) return;
        var raw = _cfg.Value;
        if (raw == _lastRaw) return;
        Parse(raw);
        _lastRaw = raw;
    }

    private void Parse(string s)
    {
        _kbMod = KeyNone;
        _kbKey = KeyNone;
        _padMod = 0;
        _padButton = 0;
        if (string.IsNullOrEmpty(s)) return;

        var parts = s.Split('|');
        if (parts.Length >= 1) _kbMod = ParseKey(parts[0]);
        if (parts.Length >= 2) _kbKey = ParseKey(parts[1]);
        if (parts.Length >= 3) int.TryParse(parts[2], out _padMod);
        if (parts.Length >= 4) int.TryParse(parts[3], out _padButton);
    }

    private static Keys ParseKey(string s)
    {
        if (string.IsNullOrEmpty(s) || s == "None" || s == "0") return KeyNone;
        try { return (Keys)Enum.Parse(typeof(Keys), s); }
        catch { return KeyNone; }
    }

    public bool HeldKeyboard(KeyboardState ks)
    {
        SyncFromConfig();
        if (_kbKey == KeyNone || !ks.IsKeyDown(_kbKey)) return false;
        return _kbMod == KeyNone || IsModDown(ks, _kbMod);
    }

    public bool HeldGamepad(GamePadState gp)
    {
        SyncFromConfig();
        if (_padButton == 0 || !IsPadDown(gp, _padButton)) return false;
        return _padMod == 0 || IsPadDown(gp, _padMod);
    }

    public bool Held(KeyboardState ks, GamePadState gp) => HeldKeyboard(ks) || HeldGamepad(gp);

    private static bool IsModDown(KeyboardState ks, Keys mod)
    {
        switch (mod)
        {
            case Keys.LeftControl:
            case Keys.RightControl:
                return ks.IsKeyDown(Keys.LeftControl) || ks.IsKeyDown(Keys.RightControl);
            case Keys.LeftShift:
            case Keys.RightShift:
                return ks.IsKeyDown(Keys.LeftShift) || ks.IsKeyDown(Keys.RightShift);
            case Keys.LeftAlt:
            case Keys.RightAlt:
                return ks.IsKeyDown(Keys.LeftAlt) || ks.IsKeyDown(Keys.RightAlt);
            default:
                return ks.IsKeyDown(mod);
        }
    }

    private static bool IsPadDown(GamePadState gp, int code)
    {
        switch (code)
        {
            case PadA: return gp.Buttons.A == ButtonState.Pressed;
            case PadB: return gp.Buttons.B == ButtonState.Pressed;
            case PadX: return gp.Buttons.X == ButtonState.Pressed;
            case PadY: return gp.Buttons.Y == ButtonState.Pressed;
            case PadLb: return gp.Buttons.LeftShoulder == ButtonState.Pressed;
            case PadRb: return gp.Buttons.RightShoulder == ButtonState.Pressed;
            case PadLt: return gp.Triggers.Left > 0.5f;
            case PadRt: return gp.Triggers.Right > 0.5f;
            case PadLs: return gp.Buttons.LeftStick == ButtonState.Pressed;
            case PadRs: return gp.Buttons.RightStick == ButtonState.Pressed;
            case PadStart: return gp.Buttons.Start == ButtonState.Pressed;
            case PadBack: return gp.Buttons.Back == ButtonState.Pressed;
            case PadDLeft: return gp.DPad.Left == ButtonState.Pressed;
            case PadDRight: return gp.DPad.Right == ButtonState.Pressed;
            case PadDUp: return gp.DPad.Up == ButtonState.Pressed;
            case PadDDown: return gp.DPad.Down == ButtonState.Pressed;
            default: return false;
        }
    }
}
