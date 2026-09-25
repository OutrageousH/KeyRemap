using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace KeyRemap.Core;

/// <summary>背景呈现方式。</summary>
public enum AppearanceMode
{
    /// <summary>纯色背景。</summary>
    Solid = 0,

    /// <summary>Windows 系统亚克力/云母材质。</summary>
    Acrylic = 1,
}

/// <summary>用户可自定义的窗口外观 (个性化)。颜色以 #RRGGBB 持久化，便于直接看懂与手改。</summary>
public sealed class AppTheme
{
    public const string DefaultBackground = "#F3F3F3";
    public const string DefaultBorder = "#E1E1E1";
    public const string DefaultAccent = "#0F6CBD";

    public string Background { get; set; } = DefaultBackground;

    public string Border { get; set; } = DefaultBorder;

    public string Accent { get; set; } = DefaultAccent;

    /// <summary>自定义文字色；null 表示按背景亮度自动推导。</summary>
    public string? Text { get; set; }

    public AppearanceMode Appearance { get; set; } = AppearanceMode.Solid;

    public AppTheme Clone() => new()
    {
        Background = Background,
        Border = Border,
        Accent = Accent,
        Text = Text,
        Appearance = Appearance,
    };

    public bool IsDefault =>
        string.Equals(Background, DefaultBackground, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Border, DefaultBorder, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Accent, DefaultAccent, StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrWhiteSpace(Text) &&
        Appearance == AppearanceMode.Solid;
}

public static class ColorUtil
{
    public static Color Parse(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;

        var text = hex.Trim().TrimStart('#');
        if (text.Length == 3)
            text = string.Concat(text.Select(c => new string(c, 2)));

        if (text.Length != 6) return fallback;
        if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return fallback;

        return Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>Mixes two colours; t=0 returns a, t=1 returns b.</summary>
    public static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>Perceptual luminance, 0 (black) to 1 (white).</summary>
    public static double Luminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;

    /// <summary>WCAG contrast ratio, 1 (identical) to 21 (black on white).</summary>
    public static double ContrastRatio(Color a, Color b)
    {
        static double Linearize(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Relative(Color c) =>
            0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);

        var la = Relative(a);
        var lb = Relative(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}

/// <summary>Every colour the UI paints with, derived from <see cref="AppTheme"/>.</summary>
public sealed record Palette(
    bool IsDark,
    /// <summary>True when the window is expected to show the system backdrop behind it.</summary>
    bool Glass,
    Color Background,
    Color Surface,
    Color SurfaceMuted,
    Color SurfaceSunken,
    Color BorderSoft,
    Color BorderStrong,
    Color TextPrimary,
    Color TextSecondary,
    Color TextTertiary,
    Color Accent,
    Color AccentHover,
    Color AccentPressed,
    Color AccentSoft,
    Color Danger,
    Color DangerHover,
    Color DangerSoft,
    Color Success,
    Color Hover,
    Color Pressed,
    Color SwitchOn,
    Color SwitchOff,
    Color SwitchFailed,
    Color SwitchKnob,
    /// <summary>Text drawn on top of <see cref="Accent"/>; flips for pale accents.</summary>
    Color OnAccent)
{
    private const double DarkThreshold = 0.42;

    public static Palette From(AppTheme theme)
    {
        var background = ColorUtil.Parse(theme.Background, ColorUtil.Parse(AppTheme.DefaultBackground, Colors.WhiteSmoke));
        var border = ColorUtil.Parse(theme.Border, ColorUtil.Parse(AppTheme.DefaultBorder, Colors.Gainsboro));
        var accent = ColorUtil.Parse(theme.Accent, ColorUtil.Parse(AppTheme.DefaultAccent, Colors.SteelBlue));

        var isDark = ColorUtil.Luminance(background) < DarkThreshold;
        var white = Colors.White;
        var black = Colors.Black;

        // Cards sit above the app background, so surfaces shift toward white on dark themes
        // and stay near-white on light ones.
        var surface = isDark
            ? ColorUtil.Blend(background, white, 0.07)
            : ColorUtil.Blend(background, white, 0.82);

        var surfaceMuted = ColorUtil.Blend(background, surface, 0.55);
        var surfaceSunken = isDark
            ? ColorUtil.Blend(background, black, 0.22)
            : ColorUtil.Blend(background, black, 0.04);

        var borderStrong = isDark
            ? ColorUtil.Blend(border, white, 0.12)
            : ColorUtil.Blend(border, black, 0.08);

        // Text follows the background rather than a fixed scheme, so a dark custom
        // background automatically flips to light text — unless the user pinned a colour.
        var autoText = isDark ? Color.FromRgb(0xF2, 0xF2, 0xF2) : Color.FromRgb(0x1B, 0x1B, 0x1B);
        var textPrimary = string.IsNullOrWhiteSpace(theme.Text)
            ? autoText
            : ColorUtil.Parse(theme.Text, autoText);
        var anchor = isDark ? ColorUtil.Blend(background, white, 0.02) : background;
        var textSecondary = ColorUtil.Blend(textPrimary, anchor, 0.32);
        var textTertiary = ColorUtil.Blend(textPrimary, anchor, 0.62);

        var accentHover = ColorUtil.Blend(accent, white, isDark ? 0.16 : 0.10);
        var accentPressed = ColorUtil.Blend(accent, black, 0.20);
        var accentSoft = isDark
            ? ColorUtil.Blend(accent, surface, 0.80)
            : ColorUtil.Blend(accent, surface, 0.88);

        var danger = isDark ? ColorUtil.Blend(Color.FromRgb(0xC4, 0x2B, 0x1C), white, 0.20) : Color.FromRgb(0xC4, 0x2B, 0x1C);
        var dangerHover = isDark ? ColorUtil.Blend(danger, white, 0.12) : ColorUtil.Blend(danger, black, 0.12);
        var dangerSoft = ColorUtil.Blend(danger, surface, isDark ? 0.78 : 0.88);

        var success = isDark ? Color.FromRgb(0x4C, 0xC2, 0x4C) : Color.FromRgb(0x0F, 0x7B, 0x0F);

        var hover = isDark ? ColorUtil.Blend(background, white, 0.10) : ColorUtil.Blend(background, black, 0.05);
        var pressed = isDark ? ColorUtil.Blend(background, white, 0.16) : ColorUtil.Blend(background, black, 0.10);

        return new Palette(
            isDark,
            theme.Appearance == AppearanceMode.Acrylic,
            background,
            surface,
            surfaceMuted,
            surfaceSunken,
            border,
            borderStrong,
            textPrimary,
            textSecondary,
            textTertiary,
            accent,
            accentHover,
            accentPressed,
            accentSoft,
            danger,
            dangerHover,
            dangerSoft,
            success,
            hover,
            pressed,
            SwitchOn: accent,
            SwitchOff: isDark ? ColorUtil.Blend(background, white, 0.30) : ColorUtil.Blend(background, black, 0.24),
            SwitchFailed: ColorUtil.Blend(danger, surface, 0.55),
            SwitchKnob: isDark ? Color.FromRgb(0xEC, 0xEC, 0xEC) : Colors.White,
            OnAccent: ColorUtil.Luminance(accent) > 0.62 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White);
    }
}

/// <summary>
/// Publishes the palette as brush resources. Styles reference them with DynamicResource, so
/// replacing the entries is enough to reskin every open window live.
/// </summary>
/// <remarks>
/// Entries are replaced rather than mutated: a ResourceDictionary seals and freezes the
/// Freezables it holds, so an existing brush can no longer be recoloured.
/// </remarks>
public static class ThemeManager
{
    public static Palette Current { get; private set; } = Palette.From(new AppTheme());

    public static void Apply(AppTheme theme)
    {
        var palette = Palette.From(theme);
        Current = palette;

        var application = Application.Current;
        if (application is null) return;

        var resources = application.Resources;
        Publish(resources, "AppBackgroundBrush", palette.Background);
        Publish(resources, "SurfaceBrush", palette.Surface);
        Publish(resources, "SurfaceMutedBrush", palette.SurfaceMuted);
        Publish(resources, "SurfaceSunkenBrush", palette.SurfaceSunken);
        Publish(resources, "BorderSoftBrush", palette.BorderSoft);
        Publish(resources, "BorderStrongBrush", palette.BorderStrong);
        Publish(resources, "TextPrimaryBrush", palette.TextPrimary);
        Publish(resources, "TextSecondaryBrush", palette.TextSecondary);
        Publish(resources, "TextTertiaryBrush", palette.TextTertiary);
        Publish(resources, "AccentBrush", palette.Accent);
        Publish(resources, "AccentHoverBrush", palette.AccentHover);
        Publish(resources, "AccentPressedBrush", palette.AccentPressed);
        Publish(resources, "AccentSoftBrush", palette.AccentSoft);
        Publish(resources, "DangerBrush", palette.Danger);
        Publish(resources, "DangerHoverBrush", palette.DangerHover);
        Publish(resources, "DangerSoftBrush", palette.DangerSoft);
        Publish(resources, "SuccessBrush", palette.Success);
        Publish(resources, "HoverBrush", palette.Hover);
        Publish(resources, "PressedBrush", palette.Pressed);
        Publish(resources, "SwitchOnBrush", palette.SwitchOn);
        Publish(resources, "SwitchOffBrush", palette.SwitchOff);
        Publish(resources, "SwitchFailedBrush", palette.SwitchFailed);
        Publish(resources, "SwitchKnobBrush", palette.SwitchKnob);
        Publish(resources, "OnAccentBrush", palette.OnAccent);
    }

    private static void Publish(ResourceDictionary resources, string key, Color color) =>
        resources[key] = new SolidColorBrush(color);
}
