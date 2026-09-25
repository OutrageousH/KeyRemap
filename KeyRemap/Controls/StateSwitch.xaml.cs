using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyRemap.Core;

namespace KeyRemap.Controls;

/// <summary>
/// The pill switch used by profiles (four display states) and rules (three). 启用失效
/// and 禁用失效 deliberately share one appearance (§13.18).
/// </summary>
public partial class StateSwitch : UserControl
{
    // Segoe MDL2 Assets glyphs.
    private const string GlyphCheck = "";
    private const string GlyphWarning = "";

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(SwitchState),
        typeof(StateSwitch),
        new PropertyMetadata(SwitchState.Off, OnStateChanged));

    public StateSwitch()
    {
        InitializeComponent();
        Apply(State);
    }

    public SwitchState State
    {
        get => (SwitchState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Raised on click, but only while the switch is operable. The listener flips the
    /// underlying model; this control never mutates its own state.
    /// </summary>
    public event EventHandler? Toggled;

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StateSwitch)d).Apply((SwitchState)e.NewValue);

    private Brush Lookup(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;

    private void Apply(SwitchState state)
    {
        switch (state)
        {
            case SwitchState.On:
                Pill.Background = Lookup("SwitchOnBrush");
                MoveKnob(toRight: true);
                Glyph.Text = GlyphCheck;
                Glyph.Foreground = Lookup("AccentBrush");
                ToolTip = "已启用 · 点击禁用";
                Pill.Cursor = Cursors.Hand;
                System.Windows.Automation.AutomationProperties.SetName(Pill, "开关（已启用）");
                break;

            case SwitchState.Off:
                Pill.Background = Lookup("SwitchOffBrush");
                MoveKnob(toRight: false);
                Glyph.Text = string.Empty;
                ToolTip = "已禁用 · 点击启用";
                Pill.Cursor = Cursors.Hand;
                System.Windows.Automation.AutomationProperties.SetName(Pill, "开关（已禁用）");
                break;

            default:
                Pill.Background = Lookup("SwitchFailedBrush");
                MoveKnob(toRight: false);
                Glyph.Text = GlyphWarning;
                Glyph.Foreground = Lookup("DangerBrush");
                ToolTip = "失效 · 补全键位后自动恢复为禁用";
                Pill.Cursor = Cursors.Arrow;
                System.Windows.Automation.AutomationProperties.SetName(Pill, "开关（失效，不可切换）");
                break;
        }
    }

    private void MoveKnob(bool toRight)
    {
        Knob.HorizontalAlignment = toRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Knob.Margin = toRight ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (State == SwitchState.Failed) return;
        Toggled?.Invoke(this, EventArgs.Empty);
    }
}
