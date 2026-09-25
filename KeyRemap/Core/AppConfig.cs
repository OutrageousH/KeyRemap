using System.Collections.ObjectModel;

namespace KeyRemap.Core;

/// <summary>关闭主窗口时的行为。</summary>
public enum CloseBehavior
{
    /// <summary>每次询问。</summary>
    Ask = 0,

    /// <summary>最小化到系统托盘。</summary>
    MinimizeToTray = 1,

    /// <summary>退出程序。</summary>
    Exit = 2,
}

/// <summary>Root of the persisted configuration (需求 §4).</summary>
public sealed class AppConfig : ObservableObject
{
    private bool _coverageEnabled = true;
    private KeyCombo? _coverageToggleHotkey = Hotkey.Default;
    private CloseBehavior _closeBehavior = CloseBehavior.Ask;
    private AppTheme _theme = new();

    public ObservableCollection<Profile> Profiles { get; } = new();

    /// <summary>按键覆盖总开关. Persisted (§13.9).</summary>
    public bool CoverageEnabled
    {
        get => _coverageEnabled;
        set
        {
            if (Set(ref _coverageEnabled, value)) Raise(nameof(CoverageStatusText));
        }
    }

    /// <summary>开关快捷键。null 表示已关闭该快捷键。默认 Ctrl+Shift+O。</summary>
    public KeyCombo? CoverageToggleHotkey
    {
        get => _coverageToggleHotkey;
        set
        {
            if (Set(ref _coverageToggleHotkey, value))
            {
                Raise(nameof(HotkeyDisplay));
                Raise(nameof(HasHotkey));
            }
        }
    }

    public bool HasHotkey => CoverageToggleHotkey is { IsEmpty: false };

    public string HotkeyDisplay => CoverageToggleHotkey is { IsEmpty: false } combo ? combo.Display : "未设置";

    public CloseBehavior CloseBehavior
    {
        get => _closeBehavior;
        set => Set(ref _closeBehavior, value);
    }

    public AppTheme Theme
    {
        get => _theme;
        set => Set(ref _theme, value);
    }

    public string CoverageStatusText => CoverageEnabled ? "按键覆盖已开启" : "按键覆盖已暂停";

    /// <summary>Creates a profile named 按键方案N with the smallest unused N (§5).</summary>
    public Profile AddProfile()
    {
        var profile = new Profile { Name = NextProfileName(), Enabled = false };
        Profiles.Add(profile);
        return profile;
    }

    public string NextProfileName()
    {
        var used = Profiles.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        for (var n = 1; ; n++)
        {
            var candidate = $"按键方案{n}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

    /// <summary>True when another profile already carries this name (§5).</summary>
    public bool IsNameTaken(string name, Profile? except = null) =>
        Profiles.Any(p => !ReferenceEquals(p, except) &&
                          string.Equals(p.Name, name, StringComparison.Ordinal));

    public void RevalidateAll()
    {
        foreach (var profile in Profiles) profile.Revalidate(CoverageToggleHotkey);
    }

    /// <summary>
    /// Null when the combination is usable as the coverage toggle. Empty/null means
    /// "no hotkey", which is allowed.
    /// </summary>
    public string? ValidateHotkey(KeyCombo? combo)
    {
        if (combo is null || combo.IsEmpty) return null;

        foreach (var key in combo.Keys)
        {
            if (!KeyNames.IsRecordable(key)) return $"{KeyNames.Name(key)} 不是可用的按键。";
        }

        // A lone modifier would fire on every Ctrl/Shift/Alt press.
        if (combo.Keys.All(KeyNames.IsModifier)) return "快捷键至少要包含一个非修饰键，例如 Ctrl+Shift+O。";

        foreach (var profile in Profiles)
        {
            foreach (var rule in profile.Rules)
            {
                if (rule.Source is not null && rule.Source.StableKey == combo.StableKey)
                    return $"「{combo.Display}」已被方案「{profile.Name}」用作被覆盖键。";

                if (Hotkey.IsInside(rule.Target, combo))
                    return $"「{combo.Display}」已被方案「{profile.Name}」用作目标键的一部分。";
            }
        }

        return null;
    }
}
