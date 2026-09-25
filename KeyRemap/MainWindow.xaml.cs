using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyRemap.Controls;
using KeyRemap.Core;

namespace KeyRemap;

/// <summary>方案列表 (需求 §5), plus the close-to-tray behaviour.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowEffects.Attach(this);
        IconService.Attach(this);

        ProfileList.ItemsSource = App.Config.Profiles;
        App.Config.Profiles.CollectionChanged += OnProfilesChanged;
        App.ConfigChanged += OnConfigChanged;
        Closing += OnClosing;

        Closed += (_, _) =>
        {
            App.Config.Profiles.CollectionChanged -= OnProfilesChanged;
            App.ConfigChanged -= OnConfigChanged;
        };

        RefreshFooter();
        UpdateCoverageChip();
        UpdateEmptyState();
    }

    private void OnConfigChanged()
    {
        UpdateCoverageChip();
        UpdateEmptyState();
        RefreshFooter();
    }

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void UpdateEmptyState() =>
        EmptyState.Visibility = App.Config.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void RefreshFooter() =>
        FooterText.Text = $"配置文件：{ConfigStore.FilePath}　·　删除整个文件夹即可干净卸载";

    private void UpdateCoverageChip()
    {
        var config = App.Config;
        CoverageDot.Fill = (Brush)FindResource(config.CoverageEnabled ? "SuccessBrush" : "TextTertiaryBrush");
        CoverageLabel.Text = config.CoverageStatusText;
        CoverageLabel.Foreground = (Brush)FindResource(config.CoverageEnabled ? "TextPrimaryBrush" : "TextSecondaryBrush");
        CoverageHotkeyLabel.Text = config.HasHotkey ? config.HotkeyDisplay : "未设快捷键";
    }

    // ==================== 关闭行为 ====================

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (App.IsShuttingDown) return;

        var behavior = App.Config.CloseBehavior;

        if (behavior == CloseBehavior.Ask)
        {
            var choice = CloseChoiceDialog.Ask(this, out var remember);
            if (choice is null)
            {
                e.Cancel = true; // 用户关掉了询问框：保持窗口打开
                return;
            }

            behavior = choice.Value;
            if (remember)
            {
                App.Config.CloseBehavior = behavior;
                App.Commit();
            }
        }

        if (behavior != CloseBehavior.MinimizeToTray) return;

        e.Cancel = true;
        Hide();
        App.Tray?.ShowBalloon("按键映射", "已最小化到系统托盘，按键覆盖继续生效。双击托盘图标可重新打开。");
    }

    /// <summary>Brings the window back from the tray.</summary>
    public void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    // ==================== 方案操作 ====================

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var window = new Views.SettingsWindow { Owner = this };
        window.ShowDialog();
    }

    private void OnAddProfile(object sender, RoutedEventArgs e)
    {
        App.Config.AddProfile();
        App.Commit();
    }

    private void OnProfileRowClick(object sender, MouseButtonEventArgs e)
    {
        // 名称旁的按钮已自行处理点击，这里只响应空白区域的点击。
        if (e.OriginalSource is DependencyObject source && FindAncestorButton(source) is not null) return;

        if (sender is FrameworkElement { DataContext: Profile profile })
            OpenProfileWindow(profile);
    }

    private static Button? FindAncestorButton(DependencyObject node)
    {
        for (var current = node; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Button button) return button;
        }

        return null;
    }

    private void OnEditProfile(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Profile profile })
            OpenProfileWindow(profile);
    }

    private void OpenProfileWindow(Profile profile)
    {
        var window = new Views.ProfileWindow(profile) { Owner = this };
        window.ShowDialog();
        UpdateEmptyState();
    }

    private void OnRenameProfile(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Profile profile }) return;

        var result = InputDialog.Ask(
            this,
            "重命名方案",
            "名称不能与现有方案重复。",
            profile.Name,
            value =>
            {
                if (string.IsNullOrWhiteSpace(value)) return "名称不能为空。";
                if (App.Config.IsNameTaken(value, profile)) return $"已存在名为「{value}」的方案，请换一个名称。";
                return null;
            });

        // 名称重复或取消时保持原名不变。
        if (result is null || result == profile.Name) return;

        profile.Name = result;
        App.Commit();
    }

    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Profile profile }) return;

        var confirmed = ConfirmDialog.Ask(
            this,
            "删除方案",
            $"确定要删除「{profile.Name}」吗？该方案下的 {profile.Rules.Count} 条覆盖规则会一并删除，此操作无法撤销。",
            "删除",
            destructive: true);

        if (!confirmed) return;

        App.Config.Profiles.Remove(profile);
        App.Commit();
    }

    private void OnProfileToggled(object sender, EventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Profile profile }) return;

        profile.Enabled = !profile.Enabled;
        App.Commit();
    }
}
