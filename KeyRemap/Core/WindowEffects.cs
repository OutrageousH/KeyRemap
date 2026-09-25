using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KeyRemap.Core;

/// <summary>
/// Applies the Windows 11 system backdrop (云母/亚克力) and keeps the title bar in step with
/// the theme. Silently does nothing where the OS does not support it.
/// </summary>
public static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModePre20H1 = 19;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaMicaEffect = 1029;

    private const int DwmsbtNone = 1;
    private const int DwmsbtTransientWindow = 3; // 亚克力

    private static readonly List<WeakReference<Window>> Tracked = new();

    /// <summary>Windows 11 (build 22000) and later can render a system backdrop.</summary>
    public static bool BackdropSupported { get; } = Environment.OSVersion.Version.Build >= 22000;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    /// <summary>Registers a window so the effect follows later theme changes.</summary>
    public static void Attach(Window window)
    {
        Tracked.Add(new WeakReference<Window>(window));
        window.SourceInitialized += (_, _) => Apply(window);
        window.Closed += (_, _) => Tracked.RemoveAll(r => !r.TryGetTarget(out var w) || ReferenceEquals(w, window));
    }

    /// <summary>Re-applies the current theme to every tracked window.</summary>
    public static void Refresh()
    {
        for (var i = Tracked.Count - 1; i >= 0; i--)
        {
            if (Tracked[i].TryGetTarget(out var window)) Apply(window);
            else Tracked.RemoveAt(i);
        }
    }

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var palette = ThemeManager.Current;

        var dark = palette.IsDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModePre20H1, ref dark, sizeof(int));

        var glass = ThemeManager.Current.Glass && BackdropSupported;

        var backdrop = glass ? DwmsbtTransientWindow : DwmsbtNone;
        if (DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) != 0 && glass)
        {
            // Windows 11 21H2 exposed 云母 through an undocumented flag instead.
            var enabled = 1;
            DwmSetWindowAttribute(hwnd, DwmwaMicaEffect, ref enabled, sizeof(int));
        }

        // The frame is intentionally NOT stretched over the client area. Doing so makes DWM
        // composite the whole window at a fixed opacity, which turns every panel muddy and
        // unreadable — WPF cannot supply the per-pixel alpha that full-window 云母 needs.
        // Leaving the frame alone keeps the system material on the title bar and the content
        // crisp and fully themed.
        var margins = new Margins();
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }
}
