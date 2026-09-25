using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using KeyRemap.Core;

namespace KeyRemap.Controls;

/// <summary>RGB 色盘取色对话框：饱和度/明度方块 + 色相条 + RGB 数值 + 十六进制 + 预设。</summary>
public partial class ColorPickerDialog : Window
{
    private static readonly string[] Presets =
    {
        "#F3F3F3", "#FFFFFF", "#E1E1E1", "#9B9B9B", "#3B3B3B", "#1E1E1E", "#0F0F0F",
        "#0F6CBD", "#3A9BEA", "#107C10", "#B7791F", "#C42B1C", "#8764B8", "#00838F",
        "#FDE7E9", "#E8F1FA", "#FFF4CE", "#E6F2E6",
    };

    /// <summary>Keeps the round thumb fully inside the field instead of clipping at the edge.</summary>
    private const double SvInset = 8;
    private const double HueInset = 5;

    private double _hue;
    private double _saturation;
    private double _value;
    private bool _syncing;
    private bool _dragging;

    public ColorPickerDialog(string title, Color initial)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        Result = initial;
        OriginalSwatch.Background = new SolidColorBrush(initial);

        BuildPresets();
        SetFromColor(initial);

        Loaded += (_, _) => RepositionThumbs();
    }

    /// <summary>Shows the picker; returns null when the user cancels.</summary>
    public static Color? Pick(Window owner, string title, Color initial)
    {
        var dialog = new ColorPickerDialog(title, initial) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public Color Result { get; private set; }

    // ==================== 状态同步 ====================

    private void SetFromColor(Color color)
    {
        var (h, s, v) = ToHsv(color);
        _hue = h;
        _saturation = s;
        _value = v;
        Refresh(updateSliders: true);
    }

    /// <summary>Pushes the current HSV state out to every control.</summary>
    private void Refresh(bool updateSliders)
    {
        _syncing = true;
        try
        {
            Result = FromHsv(_hue, _saturation, _value);
            var swatch = new SolidColorBrush(Result);

            SvHueLayer.Fill = new SolidColorBrush(FromHsv(_hue, 1, 1));
            NewSwatch.Background = swatch;

            RValue.Text = Result.R.ToString();
            GValue.Text = Result.G.ToString();
            BValue.Text = Result.B.ToString();

            if (updateSliders)
            {
                RSlider.Value = Result.R;
                GSlider.Value = Result.G;
                BSlider.Value = Result.B;
            }

            HexBox.Text = ColorUtil.ToHex(Result);
        }
        finally
        {
            _syncing = false;
        }

        RepositionThumbs();
    }

    private void RepositionThumbs()
    {
        var w = SvBox.ActualWidth;
        var h = SvBox.ActualHeight;
        if (w > SvInset * 2 && h > SvInset * 2)
        {
            var x = SvInset + _saturation * (w - SvInset * 2);
            var y = SvInset + (1 - _value) * (h - SvInset * 2);
            Canvas.SetLeft(SvThumbOuter, x - SvThumbOuter.Width / 2);
            Canvas.SetTop(SvThumbOuter, y - SvThumbOuter.Height / 2);
            Canvas.SetLeft(SvThumb, x - SvThumb.Width / 2);
            Canvas.SetTop(SvThumb, y - SvThumb.Height / 2);
        }

        var hh = HueBox.ActualHeight;
        if (hh > HueInset * 2)
        {
            var y = HueInset + _hue / 360.0 * (hh - HueInset * 2);
            Canvas.SetTop(HueThumbOuter, y - HueThumbOuter.Height / 2);
            Canvas.SetTop(HueThumb, y - HueThumb.Height / 2);
            Canvas.SetLeft(HueThumbOuter, 0);
            Canvas.SetLeft(HueThumb, 0);
        }
    }

    // ==================== 鼠标交互 ====================

    private void OnSvDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        SvBox.CaptureMouse();
        ApplySv(e);
    }

    private void OnSvMove(object sender, MouseEventArgs e)
    {
        if (_dragging) ApplySv(e);
    }

    private void OnSvUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        SvBox.ReleaseMouseCapture();
    }

    private void ApplySv(MouseEventArgs e)
    {
        var w = SvBox.ActualWidth - SvInset * 2;
        var h = SvBox.ActualHeight - SvInset * 2;
        if (w <= 0 || h <= 0) return;

        var p = e.GetPosition(SvBox);
        _saturation = Math.Clamp((p.X - SvInset) / w, 0, 1);
        _value = 1 - Math.Clamp((p.Y - SvInset) / h, 0, 1);
        Refresh(updateSliders: true);
    }

    private void OnHueDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        HueBox.CaptureMouse();
        ApplyHue(e);
    }

    private void OnHueMove(object sender, MouseEventArgs e)
    {
        if (_dragging) ApplyHue(e);
    }

    private void OnHueUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        HueBox.ReleaseMouseCapture();
    }

    private void ApplyHue(MouseEventArgs e)
    {
        var h = HueBox.ActualHeight - HueInset * 2;
        if (h <= 0) return;

        var y = e.GetPosition(HueBox).Y;
        _hue = Math.Clamp((y - HueInset) / h, 0, 1) * 360.0;
        Refresh(updateSliders: true);
    }

    private void OnRgbSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing) return;

        var color = Color.FromRgb((byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value);
        var (h, s, v) = ToHsv(color);
        _hue = h;
        _saturation = s;
        _value = v;
        Refresh(updateSliders: false);
    }

    // ==================== 十六进制 ====================

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitHex();
        e.Handled = true;
    }

    private void OnHexCommit(object sender, RoutedEventArgs e) => CommitHex();

    private void CommitHex()
    {
        if (_syncing) return;

        var text = HexBox.Text.Trim().TrimStart('#');
        if (text.Length is not (3 or 6) || !text.All(Uri.IsHexDigit))
        {
            HexBox.Text = ColorUtil.ToHex(Result);
            return;
        }

        SetFromColor(ColorUtil.Parse(HexBox.Text, Result));
    }

    // ==================== 预设 ====================

    private void BuildPresets()
    {
        foreach (var hex in Presets)
        {
            var color = ColorUtil.Parse(hex, Colors.Gray);
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
            };
            swatch.SetResourceReference(Border.BorderBrushProperty, "BorderStrongBrush");

            var button = new Button
            {
                Content = swatch,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = hex,
                // Stock Button chrome would draw a panel behind every swatch.
                Template = (ControlTemplate)XamlReader.Parse(
                    "<ControlTemplate TargetType=\"Button\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
                    "<ContentPresenter /></ControlTemplate>"),
            };
            button.Click += (_, _) => SetFromColor(color);
            AutomationProperties.SetName(button, hex);

            PresetPanel.Children.Add(button);
        }
    }

    // ==================== 颜色换算 ====================

    private static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * (((g - b) / delta) % 6);
            else if (max == g) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0) hue += 360;
        var saturation = max <= 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - c;

        double r, g, b;
        if (hue < 60) (r, g, b) = (c, x, 0);
        else if (hue < 120) (r, g, b) = (x, c, 0);
        else if (hue < 180) (r, g, b) = (0, c, x);
        else if (hue < 240) (r, g, b) = (0, x, c);
        else if (hue < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
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
