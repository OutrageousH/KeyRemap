using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace KeyRemap.Core;

/// <summary>A named set of rules. See 需求 §3.1 for the four display states.</summary>
public sealed class Profile : ObservableObject
{
    private string _name = "";
    private bool _enabled;
    private bool _valid;

    public Profile()
    {
        Rules.CollectionChanged += OnRulesChanged;
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>
    /// User intent. Deliberately left untouched while the profile is invalid —
    /// failure is a display-only overlay, so the intent survives recovery (§3.1.4).
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (Set(ref _enabled, value)) Raise(nameof(SwitchState));
        }
    }

    public ObservableCollection<Rule> Rules { get; } = new();

    /// <summary>Derived from <see cref="Rules"/>, never persisted.</summary>
    public bool Valid
    {
        get => _valid;
        private set
        {
            if (Set(ref _valid, value))
            {
                Raise(nameof(SwitchState));
                Raise(nameof(Summary));
                Raise(nameof(StatusText));
            }
        }
    }

    public SwitchState SwitchState => !Valid ? SwitchState.Failed : Enabled ? SwitchState.On : SwitchState.Off;

    public string Summary
    {
        get
        {
            if (Rules.Count == 0) return "空方案 · 无效";
            var active = Rules.Count(r => r.Enabled && r.Valid);
            var text = $"{Rules.Count} 条覆盖规则 · {active} 条生效";
            return Valid ? text : text + " · 无效";
        }
    }

    public string StatusText => !Valid
        ? "失效"
        : Enabled ? "已启用" : "已禁用";

    /// <summary>Recomputes every rule, then the profile itself (§13.16).</summary>
    public void Revalidate(KeyCombo? hotkey)
    {
        foreach (var rule in Rules) rule.Revalidate(Rules, hotkey);
        Valid = Rules.Count > 0 && Rules.All(r => r.Valid);
        Raise(nameof(Summary));
    }

    private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (Rule r in e.NewItems)
                r.PropertyChanged += OnRuleChanged;

        if (e.OldItems is not null)
            foreach (Rule r in e.OldItems)
                r.PropertyChanged -= OnRuleChanged;

        Raise(nameof(Summary));
    }

    private void OnRuleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Rule.Valid) or nameof(Rule.Enabled))
            Raise(nameof(Summary));
    }
}
