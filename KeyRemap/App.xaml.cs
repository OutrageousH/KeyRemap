using System.Windows;
using System.Windows.Threading;
using KeyRemap.Core;

namespace KeyRemap;

public partial class App : Application
{
    private Mutex? _instanceMutex;

    public static AppConfig Config { get; private set; } = new();

    public static RemapEngine Engine { get; private set; } = null!;

    public static TrayIcon? Tray { get; private set; }

    /// <summary>Set once the user really wants to quit, so the close handler stops intercepting.</summary>
    public static bool IsShuttingDown { get; private set; }

    /// <summary>Raised after the configuration has been revalidated, saved and reloaded.</summary>
    public static event Action? ConfigChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例：多开会让两个钩子互相打架 (§12)。
        _instanceMutex = new Mutex(initiallyOwned: true, @"Local\KeyRemap_SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("按键映射已经在运行了，请在任务栏或系统托盘里找到已打开的窗口。",
                "按键映射", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 让托盘的 WinForms 菜单用上系统视觉样式。
        System.Windows.Forms.Application.EnableVisualStyles();

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"发生了未处理的错误：\n{args.Exception.Message}",
                "按键映射", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        Config = ConfigStore.Load();

        // 画刷必须在任何窗口创建之前注入，DynamicResource 才能在第一帧就取到。
        ThemeManager.Apply(Config.Theme);

        Engine = new RemapEngine();
        Engine.CoverageToggled += OnCoverageToggled;

        try
        {
            Engine.Start();
            Engine.Reload(Config);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"键盘钩子启动失败，按键覆盖无法工作。\n\n{ex.Message}",
                "按键映射", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        Tray = new TrayIcon();
        Tray.ShowRequested += () => Dispatcher.BeginInvoke(window.RestoreFromTray);
        Tray.ToggleRequested += () => Dispatcher.BeginInvoke(ToggleCoverageFromTray);
        Tray.ExitRequested += () => Dispatcher.BeginInvoke(RequestExit);
        Tray.Update(Config.CoverageEnabled, Config.HotkeyDisplay);

        if (ConfigStore.LastLoadWarning is { } warning)
            MessageBox.Show(warning, "按键映射", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Called from the hook thread when the coverage toggle fires.</summary>
    private void OnCoverageToggled(bool enabled)
    {
        Dispatcher.BeginInvoke(() =>
        {
            Config.CoverageEnabled = enabled;
            // The engine already flipped its own flag; only the persisted copy needs catching up.
            Commit(save: true, reloadEngine: false);
        }, DispatcherPriority.Normal);
    }

    private static void ToggleCoverageFromTray()
    {
        var enabled = !Engine.CoverageEnabled;
        Engine.SetCoverageEnabled(enabled);
        Config.CoverageEnabled = enabled;
        Commit(save: true, reloadEngine: false);
    }

    /// <summary>Quits for real, bypassing the close-to-tray prompt.</summary>
    public static void RequestExit()
    {
        IsShuttingDown = true;
        Current.Shutdown();
    }

    /// <summary>
    /// Single funnel for every configuration mutation: revalidate, push to the engine, persist,
    /// and refresh anything that renders the theme.
    /// </summary>
    public static void Commit(bool save = true, bool reloadEngine = true)
    {
        Config.RevalidateAll();
        if (reloadEngine) Engine.Reload(Config);
        if (save) ConfigStore.Save(Config);

        ThemeManager.Apply(Config.Theme);
        WindowEffects.Refresh();
        Tray?.Update(Config.CoverageEnabled, Config.HotkeyDisplay);

        ConfigChanged?.Invoke();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsShuttingDown = true;
        Tray?.Dispose();
        Engine?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
