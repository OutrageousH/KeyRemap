using System.Windows;
using KeyRemap.Core;

namespace KeyRemap.Controls;

/// <summary>首次关闭窗口时询问去向 (需求新增)，并可记住选择。</summary>
public partial class CloseChoiceDialog : Window
{
    private CloseChoiceDialog()
    {
        InitializeComponent();
    }

    /// <summary>用户在关闭询问里做出的选择。</summary>
    public enum Choice
    {
        /// <summary>对话框被关掉了，什么都不做。</summary>
        Cancelled,

        MinimizeToTray,
        Exit,
    }

    /// <summary>是否把本次选择写入设置。</summary>
    public bool Remember { get; private set; }

    private Choice _choice = Choice.Cancelled;

    /// <summary>
    /// Shows the prompt. <paramref name="result"/> is the chosen behaviour, or null when the
    /// user dismissed the dialog and the window should simply stay open.
    /// </summary>
    public static CloseBehavior? Ask(Window owner, out bool remember)
    {
        var dialog = new CloseChoiceDialog { Owner = owner };
        dialog.ShowDialog();

        remember = dialog.Remember;
        return dialog._choice switch
        {
            Choice.MinimizeToTray => CloseBehavior.MinimizeToTray,
            Choice.Exit => CloseBehavior.Exit,
            _ => null,
        };
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        Remember = RememberBox.IsChecked == true;
        _choice = Choice.MinimizeToTray;
        DialogResult = true;
        Close();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Remember = RememberBox.IsChecked == true;
        _choice = Choice.Exit;
        DialogResult = true;
        Close();
    }
}
