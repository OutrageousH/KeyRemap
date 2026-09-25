using System.Windows;

namespace KeyRemap.Controls;

/// <summary>Rounded replacement for MessageBox so destructive actions can ask twice (§13.10).</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText, bool destructive)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = confirmText;

        if (destructive)
            OkButton.Style = (Style)FindResource("DangerButton");
    }

    /// <summary>Shows the dialog and reports whether the user confirmed.</summary>
    public static bool Ask(Window owner, string title, string message,
        string confirmText = "确定", bool destructive = false)
    {
        var dialog = new ConfirmDialog(title, message, confirmText, destructive) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
