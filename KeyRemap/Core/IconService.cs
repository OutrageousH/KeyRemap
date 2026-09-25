using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
// System.Drawing and System.Windows.Media both define Color/Size/ImageSource, so the
// drawing types are reached through an alias instead of an import.
using Drawing = System.Drawing;

namespace KeyRemap.Core;

/// <summary>
/// 应用图标入口：未自定义时用内置图标，自定义后统一从程序目录的 app-icon.ico 读取，
/// 标题栏、任务栏、托盘、以及文件夹图标都走这里。
/// </summary>
public static class IconService
{
    /// <summary>自定义图标固定存成这个文件名，放在程序目录里，保持绿色便携。</summary>
    public const string CustomFileName = "app-icon.ico";

    /// <summary>Manifest resource name of the built-in icon (see KeyRemap.csproj).</summary>
    private const string EmbeddedIconName = "KeyRemap.app.ico";

    /// <summary>写进 .ico 的尺寸集合；256 用 PNG 压缩，其余同样是 PNG 帧。</summary>
    private static readonly int[] Frames = { 256, 128, 64, 48, 32, 24, 16 };

    private static readonly List<WeakReference<Window>> Windows = new();

    public static string CustomIconPath => Path.Combine(ConfigStore.BaseDirectory, CustomFileName);

    public static bool HasCustomIcon
    {
        get
        {
            try { return File.Exists(CustomIconPath); }
            catch { return false; }
        }
    }

    /// <summary>Registers a window so it follows later icon changes.</summary>
    public static void Attach(Window window)
    {
        Windows.Add(new WeakReference<Window>(window));
        Apply(window);
        window.Closed += (_, _) => Windows.RemoveAll(r => !r.TryGetTarget(out var w) || ReferenceEquals(w, window));
    }

    public static void RefreshAll()
    {
        for (var i = Windows.Count - 1; i >= 0; i--)
        {
            if (Windows[i].TryGetTarget(out var window)) Apply(window);
            else Windows.RemoveAt(i);
        }
    }

    private static void Apply(Window window)
    {
        try
        {
            var icon = WindowIcon();
            if (icon is not null) window.Icon = icon;
        }
        catch
        {
            // Keep whatever icon the window already had.
        }
    }

    /// <summary>Largest frame of the active icon, for WPF windows.</summary>
    public static System.Windows.Media.ImageSource? WindowIcon()
    {
        try
        {
            using var stream = OpenIconStream();
            if (stream is null) return null;

            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A fresh icon instance for the tray; the caller owns it.</summary>
    public static Drawing.Icon? TrayIcon()
    {
        try
        {
            using var stream = OpenIconStream();
            if (stream is null) return null;

            using var icon = new Drawing.Icon(stream, new Drawing.Size(16, 16));
            return (Drawing.Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static Stream? OpenIconStream()
    {
        if (HasCustomIcon)
            return new MemoryStream(File.ReadAllBytes(CustomIconPath), writable: false);

        return typeof(IconService).Assembly.GetManifestResourceStream(EmbeddedIconName);
    }

    /// <summary>
    /// Turns any image file into a multi-size ICO in the program directory.
    /// Returns null on success, otherwise a message to show the user.
    /// </summary>
    public static string? Install(string sourcePath)
    {
        try
        {
            if (string.Equals(Path.GetExtension(sourcePath), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                // Validate first so a bogus file cannot leave the tray without an icon.
                using var probe = new Drawing.Icon(sourcePath);
                File.Copy(sourcePath, CustomIconPath, overwrite: true);
            }
            else
            {
                using var square = LoadSquare(sourcePath);
                File.WriteAllBytes(CustomIconPath, EncodeIco(square));
            }

            RefreshAll();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Drops the custom icon and falls back to the built-in one.</summary>
    public static string? Remove()
    {
        try
        {
            if (HasCustomIcon) File.Delete(CustomIconPath);

            RefreshAll();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static Drawing.Bitmap LoadSquare(string path)
    {
        using var raw = new Drawing.Bitmap(path);

        // Cap the working size so a huge photo does not blow up memory.
        const int cap = 1024;
        var side = Math.Max(raw.Width, raw.Height);
        if (side > cap) side = cap;

        var square = new Drawing.Bitmap(side, side, PixelFormat.Format32bppArgb);
        var inner = side;
        if (raw.Width > raw.Height) inner = Math.Max(1, (int)Math.Round((double)raw.Height / raw.Width * side));
        else if (raw.Height > raw.Width) inner = Math.Max(1, (int)Math.Round((double)raw.Width / raw.Height * side));

        using (var g = Drawing.Graphics.FromImage(square))
        {
            g.Clear(Drawing.Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;

            if (raw.Width >= raw.Height) g.DrawImage(raw, 0, (side - inner) / 2, side, inner);
            else g.DrawImage(raw, (side - inner) / 2, 0, inner, side);
        }

        return square;
    }

    /// <summary>
    /// Writes an ICO container whose entries are PNG payloads. Windows Vista and later read
    /// PNG frames, which keeps the 256px entry small and preserves alpha.
    /// </summary>
    private static byte[] EncodeIco(Drawing.Bitmap source)
    {
        var payloads = new List<(int Size, byte[] Data)>();

        foreach (var size in Frames)
        {
            using var frame = new Drawing.Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Drawing.Graphics.FromImage(frame))
            {
                g.Clear(Drawing.Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, 0, 0, size, size);
            }

            using var buffer = new MemoryStream();
            frame.Save(buffer, ImageFormat.Png);
            payloads.Add((size, buffer.ToArray()));
        }

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);

        writer.Write((ushort)0);              // reserved
        writer.Write((ushort)1);              // type: icon
        writer.Write((ushort)payloads.Count); // image count

        var offset = 6 + 16 * payloads.Count;
        foreach (var (size, data) in payloads)
        {
            writer.Write((byte)(size >= 256 ? 0 : size)); // 0 means 256
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);   // palette entries
            writer.Write((byte)0);   // reserved
            writer.Write((ushort)1); // colour planes
            writer.Write((ushort)32);
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var (_, data) in payloads) writer.Write(data);

        writer.Flush();
        return output.ToArray();
    }
}
