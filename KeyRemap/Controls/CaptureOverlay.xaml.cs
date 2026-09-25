using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyRemap.Core;

namespace KeyRemap.Controls;

/// <summary>
/// 键位选择模式的共用遮罩 (§8). Owns the capture session, shows the live key set,
/// and hands a validated combination back through <see cref="Accepted"/>.
/// </summary>
public partial class CaptureOverlay : UserControl
{
    private CaptureSession? _session;
    private Func<KeyCombo, string?>? _validator;
    private string _title = "";
    private string _hint = "";

    public CaptureOverlay()
    {
        InitializeComponent();
    }

    /// <summary>Raised after a valid combination was captured and the overlay has closed.</summary>
    public event Action<KeyCombo>? Accepted;

    public bool IsCapturing => _session is not null;

    /// <summary>
    /// Enters capture mode. <paramref name="validator"/> returns null when the combination is
    /// acceptable, otherwise the reason shown to the user.
    /// </summary>
    public void Begin(string title, string hint, Func<KeyCombo, string?> validator)
    {
        End();

        _title = title;
        _hint = hint;
        _validator = validator;

        TitleLabel.Text = title;
        HintLabel.Text = hint;
        KeysLabel.Text = "等待按键…";
        KeysLabel.Foreground = (Brush)FindResource("TextTertiaryBrush");
        StatusLabel.Text = string.Empty;
        ReasonLabel.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        Visibility = Visibility.Visible;

        var session = App.Engine.BeginCapture(Dispatcher);
        session.Updated += OnUpdated;
        session.Committed += OnCommitted;
        session.Abandoned += OnAbandoned;
        _session = session;

        Window.GetWindow(this)?.Activate();
    }

    /// <summary>Leaves capture mode and hides. Safe to call when not capturing.</summary>
    public void End()
    {
        StopSession();
        Visibility = Visibility.Collapsed;
    }

    private void StopSession()
    {
        var session = _session;
        _session = null;

        if (session is not null)
        {
            session.Updated -= OnUpdated;
            session.Committed -= OnCommitted;
            session.Abandoned -= OnAbandoned;
        }

        App.Engine.EndCapture();
    }

    private void OnUpdated(CaptureSnapshot snapshot)
    {
        if (_session is null) return;

        // Show what would be recorded right now, never a value that only appears on release.
        if (snapshot.Captured.Length == 0)
        {
            ShowKeys("等待按键…", dimmed: true);
            StatusLabel.Text = string.Empty;
        }
        else
        {
            ShowKeys(new KeyCombo(snapshot.Captured).Display, dimmed: false);

            StatusLabel.Text = snapshot.InReleaseWindow
                ? "判定中…"
                : snapshot.CapturedDiffersFromPressed
                    ? $"当前按住 {new KeyCombo(snapshot.Pressed).Display}，已松开的键也算作本次组合。"
                    : "松开全部按键后自动确认。";
        }

        // Unsupported keys still show their name, plus why they cannot be used (§13.13).
        ShowReason(snapshot.Captured.Length > 0 ? Validate(new KeyCombo(snapshot.Captured)) : null);
    }

    private void OnCommitted(int[] keys)
    {
        var combo = new KeyCombo(keys);
        var error = Validate(combo);

        if (error is not null)
        {
            // 非法：提示原因并保持未设置，等待用户重试或取消 (§8.6, §13.20).
            ShowKeys(combo.Display, dimmed: false);
            StatusLabel.Text = "未能设置，请重试或取消。";
            ShowReason(error);
            RetryButton.Visibility = Visibility.Visible;
            StopSession();
            return;
        }

        End();
        Accepted?.Invoke(combo);
    }

    private void OnAbandoned()
    {
        ShowKeys("已超时", dimmed: false);
        StatusLabel.Text = "长时间没有检测到松开动作，已自动取消。";
        RetryButton.Visibility = Visibility.Visible;
        StopSession();
    }

    private void OnRetry(object sender, RoutedEventArgs e) => Begin(_title, _hint, _validator!);

    private void OnCancel(object sender, RoutedEventArgs e) => End();

    private string? Validate(KeyCombo combo) => _validator?.Invoke(combo);

    private void ShowKeys(string text, bool dimmed)
    {
        KeysLabel.Text = text;
        KeysLabel.Foreground = (Brush)FindResource(dimmed ? "TextTertiaryBrush" : "TextPrimaryBrush");
    }

    private void ShowReason(string? reason)
    {
        if (reason is null)
        {
            ReasonLabel.Visibility = Visibility.Collapsed;
            return;
        }

        ReasonLabel.Text = reason;
        ReasonLabel.Visibility = Visibility.Visible;
    }
}
