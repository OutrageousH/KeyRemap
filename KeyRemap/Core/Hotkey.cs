namespace KeyRemap.Core;

/// <summary>
/// The global toggle that switches 按键覆盖总开关. Configurable since this revision;
/// Ctrl+Shift+O remains the default.
/// </summary>
public static class Hotkey
{
    public static KeyCombo Default => new(new[] { Vk.Ctrl, Vk.Shift, Vk.O });

    public static string DefaultDisplay => "Ctrl+Shift+O";

    /// <summary>Exact match against the configured toggle.</summary>
    public static bool Matches(KeyCombo combo, KeyCombo? hotkey) =>
        hotkey is { IsEmpty: false } && combo.StableKey == hotkey.StableKey;

    /// <summary>The toggle appears somewhere inside <paramref name="combo"/>.</summary>
    public static bool IsInside(KeyCombo? combo, KeyCombo? hotkey) =>
        combo is not null && hotkey is { IsEmpty: false } && combo.ContainsAll(hotkey.Keys);
}
