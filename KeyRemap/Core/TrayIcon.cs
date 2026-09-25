using System.Windows;

namespace KeyRemap.Core;

/// <summary>托盘图标与右键菜单。WinForms 仅用于 NotifyIcon，其余交互回调到 WPF 侧。</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ToolStripMenuItem _coverageItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _hotkeyItem;
    private System.Drawing.Icon? _icon;

    public TrayIcon()
    {
        _icon = IconService.TrayIcon();

        _coverageItem = new System.Windows.Forms.ToolStripMenuItem("暂停按键覆盖", null, (_, _) => ToggleRequested?.Invoke());
        _hotkeyItem = new System.Windows.Forms.ToolStripMenuItem { Enabled = false };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("显示主界面", null, (_, _) => ShowRequested?.Invoke()));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(_coverageItem);
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("退出", null, (_, _) => ExitRequested?.Invoke()));

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "按键映射",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }

    /// <summary>用户点了「显示主界面」或双击了托盘图标。</summary>
    public event Action? ShowRequested;

    /// <summary>用户点了「退出」。</summary>
    public event Action? ExitRequested;

    /// <summary>用户点了「暂停/恢复按键覆盖」。</summary>
    public event Action? ToggleRequested;

    public void Update(bool coverageEnabled, string hotkeyDisplay)
    {
        _coverageItem.Text = coverageEnabled ? "暂停按键覆盖" : "恢复按键覆盖";
        _coverageItem.Checked = coverageEnabled;
        _coverageItem.CheckOnClick = false;
        _hotkeyItem.Text = $"总开关快捷键：{hotkeyDisplay}";
    }

    public void ShowBalloon(string title, string message)
    {
        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(2500);
        }
        catch
        {
            // Notifications are best-effort.
        }
    }

    /// <summary>Re-reads the icon so a newly chosen custom icon shows up immediately.</summary>
    public void RefreshIcon()
    {
        var replacement = IconService.TrayIcon();
        if (replacement is null) return;

        var previous = _icon;
        _icon = replacement;
        _notifyIcon.Icon = replacement;
        previous?.Dispose();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
