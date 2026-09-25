namespace KeyRemap.Core;

/// <summary>Windows virtual-key codes used by this app.</summary>
public static class Vk
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Shift = 0x10;
    public const int Ctrl = 0x11;
    public const int Alt = 0x12;
    public const int Pause = 0x13;
    public const int CapsLock = 0x14;
    public const int Esc = 0x1B;
    public const int Space = 0x20;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int PrintScreen = 0x2C;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int O = 0x4F;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int Apps = 0x5D;
    public const int Numpad0 = 0x60;
    public const int Numpad9 = 0x69;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;

    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LCtrl = 0xA2;
    public const int RCtrl = 0xA3;
    public const int LAlt = 0xA4;
    public const int RAlt = 0xA5;
}

/// <summary>Virtual-key normalisation and Chinese display names.</summary>
public static class KeyNames
{
    private static readonly Dictionary<int, string> Named = new()
    {
        [Vk.Back] = "退格",
        [Vk.Tab] = "制表符",
        [Vk.Enter] = "回车",
        [Vk.Shift] = "Shift",
        [Vk.Ctrl] = "Ctrl",
        [Vk.Alt] = "Alt",
        [Vk.Pause] = "暂停",
        [Vk.CapsLock] = "大写锁定",
        [Vk.Esc] = "Esc",
        [Vk.Space] = "空格",
        [Vk.PageUp] = "上翻页",
        [Vk.PageDown] = "下翻页",
        [Vk.End] = "End",
        [Vk.Home] = "Home",
        [Vk.Left] = "左箭头",
        [Vk.Up] = "上箭头",
        [Vk.Right] = "右箭头",
        [Vk.Down] = "下箭头",
        [Vk.PrintScreen] = "截屏",
        [Vk.Insert] = "插入",
        [Vk.Delete] = "删除",
        [Vk.LWin] = "Win",
        [Vk.Apps] = "菜单键",
        [Vk.NumLock] = "数字锁定",
        [Vk.ScrollLock] = "滚动锁定",
        [0x6A] = "小键盘*",
        [0x6B] = "小键盘+",
        [0x6C] = "小键盘回车",
        [0x6D] = "小键盘-",
        [0x6E] = "小键盘.",
        [0x6F] = "小键盘/",
        [0xBA] = "分号",
        [0xBB] = "等号",
        [0xBC] = "逗号",
        [0xBD] = "减号",
        [0xBE] = "句号",
        [0xBF] = "斜杠",
        [0xC0] = "反引号",
        [0xDB] = "左方括号",
        [0xDC] = "反斜杠",
        [0xDD] = "右方括号",
        [0xDE] = "引号",
        [0xB0] = "下一曲",
        [0xB1] = "上一曲",
        [0xB2] = "停止",
        [0xB3] = "播放/暂停",
        [0xAD] = "静音",
        [0xAE] = "音量减",
        [0xAF] = "音量加",
        [0xE5] = "输入法",
        [0x15] = "假名",
        [0x19] = "转换",
    };

    /// <summary>Collapses left/right variants so Ctrl+A matches either Ctrl key.</summary>
    public static int Normalize(int vk) => vk switch
    {
        Vk.LShift or Vk.RShift => Vk.Shift,
        Vk.LCtrl or Vk.RCtrl => Vk.Ctrl,
        Vk.LAlt or Vk.RAlt => Vk.Alt,
        Vk.RWin => Vk.LWin,
        _ => vk,
    };

    /// <summary>
    /// True for virtual-key codes that stand for a key the user can actually press.
    /// The low-level hook also delivers placeholders — 0xFF for the Fn key on some
    /// keyboards, VK_PACKET for Unicode input, mouse buttons — none of which can ever
    /// make a usable mapping, so they must not be recorded as one.
    /// </summary>
    public static bool IsRecordable(int vk) => vk switch
    {
        0x00 or 0xFF => false,                          // 占位 / 无效码
        0x01 or 0x02 or 0x04 or 0x05 or 0x06 => false,  // 鼠标键，键盘钩子不应上报
        0xE7 => false,                                  // VK_PACKET，Unicode 输入包
        _ => true,
    };

    public static bool IsModifier(int vk) =>
        vk is Vk.Ctrl or Vk.Shift or Vk.Alt or Vk.LWin;

    /// <summary>Display ordering weight: modifiers lead, everything else follows sorted by code.</summary>
    public static int DisplayOrder(int vk) => vk switch
    {
        Vk.Ctrl => 0,
        Vk.Shift => 1,
        Vk.Alt => 2,
        Vk.LWin => 3,
        _ => 100,
    };

    public static string Name(int vk)
    {
        if (Named.TryGetValue(vk, out var named)) return named;
        if (vk is >= 0x41 and <= 0x5A) return ((char)vk).ToString();
        if (vk is >= 0x30 and <= 0x39) return ((char)vk).ToString();
        if (vk is >= 0x70 and <= 0x87) return "F" + (vk - 0x70 + 1);
        if (vk is >= 0x60 and <= 0x69) return "小键盘" + (vk - 0x60);
        return $"VK 0x{vk:X2}";
    }

    /// <summary>Keys that carry the extended scan-code bit; matters when injecting.</summary>
    public static bool IsExtended(int vk) => vk switch
    {
        Vk.PageUp or Vk.PageDown or Vk.End or Vk.Home => true,
        Vk.Left or Vk.Up or Vk.Right or Vk.Down => true,
        Vk.Insert or Vk.Delete or Vk.PrintScreen => true,
        Vk.LWin or Vk.RWin or Vk.Apps => true,
        Vk.RCtrl or Vk.RAlt => true,
        0x6F or Vk.NumLock => true,
        _ => false,
    };
}
