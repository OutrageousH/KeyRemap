using System.Runtime.InteropServices;

namespace KeyRemap.Core;

/// <summary>A single physical keyboard transition as reported by the hook.</summary>
public readonly struct HookKey
{
    /// <summary>Raw virtual-key code, unnormalised (left/right variants preserved).</summary>
    public int Vk { get; init; }

    public bool IsDown { get; init; }

    /// <summary>True when the event was synthesised rather than produced by hardware (§9).</summary>
    public bool Injected { get; init; }

    /// <summary>Auto-repeat of a key that is already held.</summary>
    public bool IsRepeat { get; init; }
}

/// <summary>
/// WH_KEYBOARD_LL hook running on its own thread with its own message loop, so a busy
/// UI thread can never stall the callback past the low-level-hook timeout.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly Native.LowLevelKeyboardProc _callback;
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private volatile IntPtr _hook;
    private volatile uint _threadId;
    private volatile Exception? _installError;
    private volatile bool _disposed;

    public KeyboardHook()
    {
        // Held in a field so the GC cannot collect the delegate out from under Win32.
        _callback = HookProc;
    }

    /// <summary>
    /// Invoked synchronously on the hook thread. Returning true drops the key event
    /// before it reaches the foreground application. Must be fast and never block.
    /// </summary>
    public Func<HookKey, bool>? Handler { get; set; }

    public bool IsRunning => _hook != IntPtr.Zero;

    /// <summary>Starts the hook and waits until it is installed, or throws.</summary>
    public void Start()
    {
        if (_thread is not null) return;

        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = "KeyRemap.Hook",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _ready.Wait(TimeSpan.FromSeconds(5));

        if (_installError is not null)
            throw new InvalidOperationException("无法安装键盘钩子。", _installError);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("安装键盘钩子超时。");
    }

    private void ThreadProc()
    {
        try
        {
            var module = Native.GetModuleHandle(null);
            _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _callback, module, 0);

            if (_hook == IntPtr.Zero)
                _installError = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            _threadId = Native.GetCurrentThreadId();
        }
        catch (Exception ex)
        {
            _installError = ex;
        }
        finally
        {
            _ready.Set();
        }

        if (_hook == IntPtr.Zero) return;

        while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessage(ref msg);
        }

        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || _disposed)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        var message = (int)wParam;
        var isDown = message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
        var isUp = message is Native.WM_KEYUP or Native.WM_SYSKEYUP;

        if (!isDown && !isUp)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
        var injected = (data.flags & Native.LLKHF_INJECTED) != 0 || data.dwExtraInfo == Native.InjectTag;

        var key = new HookKey
        {
            Vk = (int)data.vkCode,
            IsDown = isDown,
            Injected = injected,
        };

        try
        {
            var handler = Handler;
            if (handler is not null && handler(key))
                return 1; // swallow: neither the app nor the rest of the hook chain sees it
        }
        catch
        {
            // A throwing handler must never wedge the user's keyboard.
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public static void InjectKey(int vk, bool keyUp)
    {
        var flags = 0u;
        if (keyUp) flags |= Native.KEYEVENTF_KEYUP;
        if (KeyNames.IsExtended(vk)) flags |= Native.KEYEVENTF_EXTENDEDKEY;

        var input = new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion
            {
                ki = new Native.KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = Native.InjectTag,
                },
            },
        };

        Native.SendInput(1, new[] { input }, Marshal.SizeOf<Native.INPUT>());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_threadId != 0)
            Native.PostThreadMessage(_threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);

        _thread?.Join(TimeSpan.FromSeconds(2));

        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _ready.Dispose();
    }
}
