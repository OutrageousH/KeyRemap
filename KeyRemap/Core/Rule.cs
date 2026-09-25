using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KeyRemap.Core;

/// <summary>Three-state switch shown for a rule, and four-state for a profile.</summary>
public enum SwitchState
{
    /// <summary>启用</summary>
    On,

    /// <summary>禁用</summary>
    Off,

    /// <summary>失效 — 启用失效 and 禁用失效 share this appearance.</summary>
    Failed,
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>One “被覆盖键 → 目标键” mapping.</summary>
public sealed class Rule : ObservableObject
{
    private bool _enabled;
    private KeyCombo? _source;
    private KeyCombo? _target;
    private bool _valid;
    private string? _reason;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (Set(ref _enabled, value)) Raise(nameof(SwitchState));
        }
    }

    public KeyCombo? Source
    {
        get => _source;
        set
        {
            if (Set(ref _source, value)) Raise(nameof(SourceDisplay));
        }
    }

    public KeyCombo? Target
    {
        get => _target;
        set
        {
            if (Set(ref _target, value)) Raise(nameof(TargetDisplay));
        }
    }

    /// <summary>Derived, never persisted. See 需求 §3.2.</summary>
    public bool Valid
    {
        get => _valid;
        private set
        {
            if (Set(ref _valid, value)) Raise(nameof(SwitchState));
        }
    }

    /// <summary>Human-readable cause when <see cref="Valid"/> is false.</summary>
    public string? Reason
    {
        get => _reason;
        private set => Set(ref _reason, value);
    }

    public SwitchState SwitchState => !Valid ? SwitchState.Failed : Enabled ? SwitchState.On : SwitchState.Off;

    public string SourceDisplay => Source?.Display ?? "点击设置";

    public string TargetDisplay => Target?.Display ?? "点击设置";

    public bool HasSource => Source is { IsEmpty: false };

    public bool HasTarget => Target is { IsEmpty: false };

    /// <summary>
    /// Recomputes validity against the sibling rules of the owning profile.
    /// A rule that turns invalid is forced to 禁用失效 (§3.2.2); recovering from
    /// failure lands on 禁用, never back on 启用 (§3.2.3).
    /// </summary>
    public void Revalidate(IReadOnlyList<Rule> siblings, KeyCombo? hotkey)
    {
        var error = CheckSource(Source, siblings, this, hotkey) ?? CheckTarget(Target, this, hotkey);
        Reason = error;
        Valid = error is null;
        if (!Valid && Enabled) Enabled = false;
    }

    /// <summary>Null when the combination is an acceptable 被覆盖键, otherwise the reason.</summary>
    public static string? CheckSource(KeyCombo? combo, IReadOnlyList<Rule> siblings, Rule self, KeyCombo? hotkey)
    {
        if (combo is null || combo.IsEmpty) return "尚未设置被覆盖键";

        foreach (var key in combo.Keys)
        {
            if (!KeyNames.IsRecordable(key)) return $"{KeyNames.Name(key)} 不是可用的按键";
            if (key == Vk.Esc) return "Esc 不能作为被覆盖键";
            if (key == Vk.CapsLock) return "Caps Lock 不能作为被覆盖键";
        }

        if (Hotkey.Matches(combo, hotkey)) return $"{hotkey!.Display} 是开关快捷键，不能作为被覆盖键";

        foreach (var sibling in siblings)
        {
            if (ReferenceEquals(sibling, self)) continue;
            if (sibling.Source is not null && sibling.Source.StableKey == combo.StableKey)
                return "该组合键在本方案中已被其他规则用作被覆盖键";
        }

        return null;
    }

    /// <summary>Null when the combination is an acceptable 目标键, otherwise the reason.</summary>
    public static string? CheckTarget(KeyCombo? combo, Rule self, KeyCombo? hotkey)
    {
        if (combo is null || combo.IsEmpty) return "尚未设置目标键";

        foreach (var key in combo.Keys)
        {
            if (!KeyNames.IsRecordable(key)) return $"{KeyNames.Name(key)} 不是可用的按键";
        }

        if (Hotkey.IsInside(combo, hotkey)) return $"{hotkey!.Display} 不能作为目标键的一部分";
        if (self.Source is not null && !self.Source.IsEmpty && self.Source.StableKey == combo.StableKey)
            return "目标键不能与被覆盖键相同";
        return null;
    }
}
