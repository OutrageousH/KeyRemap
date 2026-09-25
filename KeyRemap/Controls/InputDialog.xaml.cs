using System.Windows;
using System.Windows.Controls;

namespace KeyRemap.Controls;

/// <summary>Rounded single-line input used for renaming a profile (§5).</summary>
public partial class InputDialog : Window
{
    private readonly Func<string, string?> _validate;

    public InputDialog(string title, string prompt, string initial, Func<string, string?> validate)
    {
        InitializeComponent();

        _validate = validate;
        Title = title;
        TitleText.Text = title;
        PromptText.Text = prompt;
        ValueBox.Text = initial;

        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    /// <summary>Returns the accepted value, or null when the user cancelled.</summary>
    public static string? Ask(Window owner, string title, string prompt, string initial,
        Func<string, string?> validate)
    {
        var dialog = new InputDialog(title, prompt, initial, validate) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.ValueBox.Text.Trim() : null;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        // Clear the error as soon as the user starts correcting it.
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        var value = ValueBox.Text.Trim();
        var error = _validate(value);

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            ValueBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
