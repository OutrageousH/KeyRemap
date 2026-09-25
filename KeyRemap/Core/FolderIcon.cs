using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyRemap.Core;

/// <summary>
/// 让程序所在文件夹在资源管理器里显示指定图标。
/// 做法是在文件夹里写一个 desktop.ini 并给文件夹加上系统属性 —— 这是 Windows 自己的
/// 「自定义文件夹」用的同一套机制，不碰注册表，删掉 desktop.ini 即可还原。
/// </summary>
public static class FolderIcon
{
    private const string IniName = "desktop.ini";
    private const string Marker = "[.ShellClassInfo]";

    private const int ShcneUpdateDir = 0x00001000;
    private const uint ShcnfPathW = 0x0005;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string item1, string? item2);

    public static string Folder { get; } =
        ConfigStore.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string IniPath => Path.Combine(ConfigStore.BaseDirectory, IniName);

    /// <summary>True when our desktop.ini is in place.</summary>
    public static bool IsApplied
    {
        get
        {
            try
            {
                return File.Exists(IniPath) && File.ReadAllText(IniPath).Contains(Marker, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>自定义图标优先；没有就用程序自身的 exe 图标。</summary>
    public static string IconTarget
    {
        get
        {
            if (IconService.HasCustomIcon) return IconService.CustomFileName;

            var exe = Path.GetFileName(Environment.ProcessPath);
            return string.IsNullOrEmpty(exe) ? "按键映射.exe" : exe;
        }
    }

    /// <summary>Returns null on success, otherwise a message to show the user.</summary>
    public static string? Apply()
    {
        try
        {
            File.WriteAllText(IniPath, $"{Marker}\r\nIconResource={IconTarget},0\r\n", Encoding.Unicode);
            File.SetAttributes(IniPath, FileAttributes.Hidden | FileAttributes.System);
            AddFolderAttributes();
            Notify();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Returns null on success, otherwise a message to show the user.</summary>
    public static string? Restore()
    {
        try
        {
            if (File.Exists(IniPath))
            {
                File.SetAttributes(IniPath, FileAttributes.Normal);
                File.Delete(IniPath);
            }

            ClearFolderAttributes();
            Notify();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static void AddFolderAttributes()
    {
        if (!Directory.Exists(Folder)) return;
        File.SetAttributes(Folder, File.GetAttributes(Folder) | FileAttributes.System | FileAttributes.ReadOnly);
    }

    private static void ClearFolderAttributes()
    {
        if (!Directory.Exists(Folder)) return;
        File.SetAttributes(Folder, File.GetAttributes(Folder) & ~(FileAttributes.System | FileAttributes.ReadOnly));
    }

    /// <summary>Tells Explorer to re-read the folder so the new icon shows up right away.</summary>
    private static void Notify()
    {
        try
        {
            SHChangeNotify(ShcneUpdateDir, ShcnfPathW, BasePathForShell(), null);
        }
        catch
        {
            // Explorer also picks it up on its own; nothing to do.
        }
    }

    private static string BasePathForShell() =>
        Folder.EndsWith(Path.DirectorySeparatorChar) ? Folder : Folder + Path.DirectorySeparatorChar;
}
