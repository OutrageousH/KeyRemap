using System.Collections.Specialized;
using System.Windows;
using KeyRemap.Controls;
using KeyRemap.Core;

namespace KeyRemap.Views;

/// <summary>按键定义界面 (需求 §6–§8).</summary>
public partial class ProfileWindow : Window
{
    private readonly Profile _profile;

    public ProfileWindow(Profile profile)
    {
        InitializeComponent();
        WindowEffects.Attach(this);
        IconService.Attach(this);

        _profile = profile;
        RuleList.ItemsSource = profile.Rules;

        profile.Rules.CollectionChanged += OnRulesChanged;
        App.ConfigChanged += Refresh;
        Capture.Accepted += OnCaptured;

        Closed += (_, _) =>
        {
            profile.Rules.CollectionChanged -= OnRulesChanged;
            App.ConfigChanged -= Refresh;
            Capture.End();
        };

        Refresh();
    }

    private void Refresh()
    {
        Title = $"按键定义 — {_profile.Name}";
        ProfileNameText.Text = _profile.Name;
        ProfileSummaryText.Text = _profile.Summary;
        RulesEmptyHint.Visibility = _profile.Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    // ==================== 方案级操作 ====================

    private void OnBack(object sender, RoutedEventArgs e) => Close();

    private void OnAddRule(object sender, RoutedEventArgs e)
    {
        // 新建规则初始为禁用失效 (§13.15).
        _profile.Rules.Add(new Rule());
        App.Commit();
    }

    private void OnDeleteRule(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Rule rule }) return;

        var confirmed = ConfirmDialog.Ask(
            this,
            "删除覆盖",
            $"确定要删除覆盖「{rule.SourceDisplay} → {rule.TargetDisplay}」吗？此操作无法撤销。",
            "删除",
            destructive: true);

        if (!confirmed) return;

        _profile.Rules.Remove(rule);
        App.Commit();
    }

    private void OnRuleToggled(object sender, EventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Rule rule }) return;
        if (!rule.Valid) return;

        rule.Enabled = !rule.Enabled;
        App.Commit();
    }

    // ==================== 键位选择模式 (§8) ====================

    private void OnPickSource(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Rule rule }) return;

        _capturing = rule;
        _capturingSource = true;
        Capture.Begin("设置被覆盖键", "直接按下要覆盖的按键。松开全部按键后自动确认，判定窗口 80ms。", Validate);
    }

    private void OnPickTarget(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Rule rule }) return;

        _capturing = rule;
        _capturingSource = false;
        Capture.Begin("设置目标键", "直接按下要输出的按键。松开全部按键后自动确认，判定窗口 80ms。", Validate);
    }

    private Rule? _capturing;
    private bool _capturingSource;

    private string? Validate(KeyCombo combo)
    {
        if (_capturing is null) return null;

        var hotkey = App.Config.CoverageToggleHotkey;
        return _capturingSource
            ? Rule.CheckSource(combo, _profile.Rules, _capturing, hotkey)
            : Rule.CheckTarget(combo, _capturing, hotkey);
    }

    private void OnCaptured(KeyCombo combo)
    {
        var rule = _capturing;
        _capturing = null;
        if (rule is null) return;

        if (_capturingSource) rule.Source = combo;
        else rule.Target = combo;

        _profile.Revalidate(App.Config.CoverageToggleHotkey);
        App.Commit();
    }
}
