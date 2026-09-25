using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyRemap.Controls;
using KeyRemap.Core;
using Microsoft.Win32;

namespace KeyRemap.Views;

/// <summary>软件设置：开关快捷键、关闭行为、个性化外观与图标。</summary>
public partial class SettingsWindow : Window
{
    /// <summary>Below this the text is hard to read against the card background.</summary>
    private const double MinimumContrast = 3.5;

    private readonly AppConfig _config;

    public SettingsWindow()
    {
        InitializeComponent();
        WindowEffects.Attach(this);
        IconService.Attach(this);

        _config = App.Config;

        App.ConfigChanged += Refresh;
        Capture.Accepted += OnHotkeyCaptured;
        Closed += (_, _) =>
        {
            App.ConfigChanged -= Refresh;
            Capture.End();
        };

        Refresh();
    }

    private void Refresh()
    {
        RefreshHotkey();
        RefreshCloseBehavior();
        RefreshAppearance();
        RefreshIcons();
    }

    private void RefreshHotkey()
    {
        HotkeyText.Text = _config.HotkeyDisplay;
        HotkeyText.Foreground = (Brush)FindResource(_config.HasHotkey ? "TextPrimaryBrush" : "TextTertiaryBrush");
        ClearHotkeyButton.IsEnabled = _config.HasHotkey;
        HotkeyHint.Text = _config.HasHotkey
            ? "该快捷键全局生效，程序运行期间无论焦点在哪个窗口都有效。按下后切换按键覆盖总开关。"
            : "当前没有设置开关快捷键。点击「更改」录制一个，例如 Ctrl+Shift+O。";
    }

    private void RefreshCloseBehavior()
    {
        Select(CloseAskButton, _config.CloseBehavior == CloseBehavior.Ask);
        Select(CloseTrayButton, _config.CloseBehavior == CloseBehavior.MinimizeToTray);
        Select(CloseExitButton, _config.CloseBehavior == CloseBehavior.Exit);
    }

    private void RefreshAppearance()
    {
        var theme = _config.Theme;
        var palette = ThemeManager.Current;

        Select(AppearanceSolidButton, theme.Appearance == AppearanceMode.Solid);
        Select(AppearanceGlassButton, theme.Appearance == AppearanceMode.Acrylic);
        AppearanceGlassButton.IsEnabled = WindowEffects.BackdropSupported;
        AppearanceHint.Text = WindowEffects.BackdropSupported
            ? "毛玻璃使用 Windows 11 的系统亚克力材质（作用于标题栏）。窗口内容保持不透明，避免文字发灰影响阅读。"
            : "当前系统不支持系统毛玻璃材质（需要 Windows 11），已自动使用纯色背景。";

        BackgroundSwatch.Background = Swatch(theme.Background, AppTheme.DefaultBackground);
        BorderSwatch.Background = Swatch(theme.Border, AppTheme.DefaultBorder);
        AccentSwatch.Background = Swatch(theme.Accent, AppTheme.DefaultAccent);
        BackgroundHex.Text = theme.Background;
        BorderHex.Text = theme.Border;
        AccentHex.Text = theme.Accent;

        var customText = !string.IsNullOrWhiteSpace(theme.Text);
        TextSwatch.Background = new SolidColorBrush(palette.TextPrimary);
        TextHex.Text = customText ? theme.Text! : $"自动（{ColorUtil.ToHex(palette.TextPrimary)}）";

        var contrast = ColorUtil.ContrastRatio(palette.TextPrimary, palette.Surface);
        ThemeContrastHint.Text = contrast < MinimumContrast
            ? $"文字与卡片背景的对比度仅 {contrast:F1}:1，可能不易阅读。"
            : $"文字与卡片背景对比度 {contrast:F1}:1，{(palette.IsDark ? "深色" : "浅色")}方案。";
    }

    private void RefreshIcons()
    {
        IconPreview.Source = IconService.WindowIcon();
        IconStateText.Text = IconService.HasCustomIcon
            ? $"自定义（{IconService.CustomFileName}）"
            : "内置图标";
        ResetIconButton.IsEnabled = IconService.HasCustomIcon;

        var applied = FolderIcon.IsApplied;
        FolderIconButton.Content = applied ? "恢复默认图标" : "应用到当前文件夹";
        FolderIconHint.Text = applied
            ? "当前文件夹在资源管理器中已显示该图标。"
            : "让程序所在文件夹在资源管理器里显示该图标：会在文件夹内写入 desktop.ini 并加上系统属性，可随时还原。";
    }

    private static void Select(Button button, bool selected) =>
        button.Style = (Style)button.FindResource(selected ? "PillButtonSelected" : "PillButton");

    private static Brush Swatch(string hex, string fallback) =>
        new SolidColorBrush(ColorUtil.Parse(hex, ColorUtil.Parse(fallback, Colors.Gray)));

    // ==================== 快捷键 ====================

    private void OnChangeHotkey(object sender, RoutedEventArgs e) =>
        Capture.Begin(
            "设置开关快捷键",
            "直接按下要用的快捷键，例如 Ctrl+Shift+O。松开全部按键后自动确认，判定窗口 80ms。",
            combo => _config.ValidateHotkey(combo));

    private void OnHotkeyCaptured(KeyCombo combo)
    {
        _config.CoverageToggleHotkey = combo;
        App.Commit();
    }

    private void OnClearHotkey(object sender, RoutedEventArgs e)
    {
        _config.CoverageToggleHotkey = null;
        App.Commit();
    }

    // ==================== 关闭行为 ====================

    private void OnCloseAsk(object sender, RoutedEventArgs e) => SetCloseBehavior(CloseBehavior.Ask);

    private void OnCloseTray(object sender, RoutedEventArgs e) => SetCloseBehavior(CloseBehavior.MinimizeToTray);

    private void OnCloseExit(object sender, RoutedEventArgs e) => SetCloseBehavior(CloseBehavior.Exit);

    private void SetCloseBehavior(CloseBehavior behavior)
    {
        _config.CloseBehavior = behavior;
        App.Commit();
    }

    // ==================== 外观 ====================

    private void OnAppearanceSolid(object sender, RoutedEventArgs e) => SetAppearance(AppearanceMode.Solid);

    private void OnAppearanceGlass(object sender, RoutedEventArgs e)
    {
        if (!WindowEffects.BackdropSupported) return;
        SetAppearance(AppearanceMode.Acrylic);
    }

    private void SetAppearance(AppearanceMode mode)
    {
        _config.Theme.Appearance = mode;
        App.Commit();
    }

    private void OnPickBackground(object sender, RoutedEventArgs e) =>
        PickColor("选择背景色", _config.Theme.Background, AppTheme.DefaultBackground,
            value => _config.Theme.Background = value);

    private void OnPickBorder(object sender, RoutedEventArgs e) =>
        PickColor("选择边框色", _config.Theme.Border, AppTheme.DefaultBorder,
            value => _config.Theme.Border = value);

    private void OnPickAccent(object sender, RoutedEventArgs e) =>
        PickColor("选择主色调", _config.Theme.Accent, AppTheme.DefaultAccent,
            value => _config.Theme.Accent = value);

    private void PickColor(string title, string current, string fallback, Action<string> apply)
    {
        var initial = ColorUtil.Parse(current, ColorUtil.Parse(fallback, Colors.Gray));
        var picked = ColorPickerDialog.Pick(this, title, initial);
        if (picked is null) return;

        apply(ColorUtil.ToHex(picked.Value));
        App.Commit();
    }

    private void OnPickText(object sender, RoutedEventArgs e)
    {
        var picked = ColorPickerDialog.Pick(this, "选择文字颜色", ThemeManager.Current.TextPrimary);
        if (picked is null) return;

        _config.Theme.Text = ColorUtil.ToHex(picked.Value);
        App.Commit();
    }

    private void OnTextAuto(object sender, RoutedEventArgs e)
    {
        _config.Theme.Text = null;
        App.Commit();
    }

    private void OnResetTheme(object sender, RoutedEventArgs e)
    {
        _config.Theme = new AppTheme();
        App.Commit();
    }

    // ==================== 图标 ====================

    private void OnChangeIcon(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图标",
            Filter = "图片文件|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        if (IconService.Install(dialog.FileName) is { } error)
        {
            Warn($"无法使用该图标：{error}");
            return;
        }

        App.Tray?.RefreshIcon();
        SyncFolderIcon();
        App.Commit();
    }

    private void OnResetIcon(object sender, RoutedEventArgs e)
    {
        if (IconService.Remove() is { } error)
        {
            Warn($"无法恢复默认图标：{error}");
            return;
        }

        App.Tray?.RefreshIcon();
        SyncFolderIcon();
        App.Commit();
    }

    /// <summary>Keeps desktop.ini pointing at whichever icon is currently active.</summary>
    private void SyncFolderIcon()
    {
        if (FolderIcon.IsApplied) FolderIcon.Apply();
    }

    private void OnToggleFolderIcon(object sender, RoutedEventArgs e)
    {
        string? error;

        if (FolderIcon.IsApplied)
        {
            var confirmed = ConfirmDialog.Ask(
                this,
                "恢复文件夹图标",
                $"将删除 {FolderIcon.IniPath}，并清除该文件夹上的系统属性，资源管理器随即恢复默认的文件夹图标。",
                "恢复",
                destructive: true);
            if (!confirmed) return;

            error = FolderIcon.Restore();
            if (error is not null) Warn($"恢复失败：{error}");
        }
        else
        {
            var confirmed = ConfirmDialog.Ask(
                this,
                "更改文件夹图标",
                $"将在这个文件夹里写入 desktop.ini，把图标指向「{FolderIcon.IconTarget}」，" +
                "并给文件夹加上系统属性，资源管理器就会用它来显示这个文件夹。\n\n" +
                "不会改动注册表，点同一个按钮即可还原。",
                "应用");
            if (!confirmed) return;

            error = FolderIcon.Apply();
            if (error is not null) Warn($"应用失败：{error}");
        }

        Refresh();
    }

    private void Warn(string message) =>
        MessageBox.Show(message, "按键映射", MessageBoxButton.OK, MessageBoxImage.Warning);

    // ==================== 关于 ====================

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ConfigStore.BaseDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Warn($"无法打开目录：{ex.Message}");
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
