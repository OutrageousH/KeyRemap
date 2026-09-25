using System.Windows.Threading;

namespace KeyRemap.Core;

/// <summary>
/// Turns physical key events into remapped output. Everything here runs on the hook
/// thread, so nothing may block (§9, §12).
/// </summary>
public sealed class RemapEngine : IDisposable
{
    private readonly KeyboardHook _hook = new();
    private readonly Dictionary<int, ActiveMapping> _active = new();

    /// <summary>Guards the transient input state, which the UI thread resets around capture mode.</summary>
    private readonly object _stateGate = new();

    /// <summary>How key events are synthesised. Replaced in tests so no real key is ever sent.</summary>
    internal delegate void KeySynthesiser(int vk, bool keyUp);

    internal KeySynthesiser Synthesise { get; set; } = KeyboardHook.InjectKey;

    /// <summary>Normalised virtual keys currently held down by hardware.</summary>
    private readonly HashSet<int> _down = new();

    /// <summary>Keys belonging to an in-flight Ctrl+Shift+O whose release must be swallowed.</summary>
    private readonly HashSet<int> _hotkeySwallow = new();

    private readonly uint _selfPid = (uint)Environment.ProcessId;

    private RuleTable _table = RuleTable.Empty;
    private volatile KeyCombo? _hotkey = Hotkey.Default;
    private volatile bool _coverageEnabled = true;
    private volatile CaptureSession? _capture;

    /// <summary>Raised on the hook thread; subscribers must marshal to the UI thread.</summary>
    public event Action<bool>? CoverageToggled;

    public bool CoverageEnabled => _coverageEnabled;

    public CaptureSession? Capture => _capture;

    /// <summary>True once the hook is installed and the engine is intercepting keys.</summary>
    public bool IsRunning => _hook.IsRunning;

    public void Start()
    {
        _hook.Handler = OnKey;
        _hook.Start();
    }

    /// <summary>Swaps in a freshly compiled rule table. Safe to call from the UI thread.</summary>
    public void Reload(AppConfig config)
    {
        _coverageEnabled = config.CoverageEnabled;
        _hotkey = config.CoverageToggleHotkey;
        _table = RuleTable.Build(config);
    }

    public void SetCoverageEnabled(bool enabled) => _coverageEnabled = enabled;

    /// <summary>Suspends remapping while the user records a key combination (§8).</summary>
    public CaptureSession BeginCapture(Dispatcher dispatcher)
    {
        var session = new CaptureSession(dispatcher);
        _capture = session; // must be set first: the hook thread stops touching the state below
        ResetInputState();
        return session;
    }

    public void EndCapture()
    {
        var session = _capture;
        if (session is null) return;

        _capture = null;
        session.Dispose();
        ResetInputState();
    }

    /// <summary>
    /// Releases everything we believe is held and forgets it. Capture mode swallows keys
    /// outright, so without this a key held across the boundary would stay logically down
    /// forever — and any mapping already pressed would never receive its key-up.
    /// </summary>
    private void ResetInputState()
    {
        lock (_stateGate)
        {
            foreach (var mapping in _active.Values)
                for (var i = mapping.InjectedDown.Count - 1; i >= 0; i--)
                    Synthesise(mapping.InjectedDown[i], keyUp: true);

            _active.Clear();
            _down.Clear();
            _hotkeySwallow.Clear();
        }
    }

    /// <summary>
    /// Feeds a physical key through exactly the path the hook uses. Exists so the press and
    /// release state machine can be exercised without a real keyboard.
    /// </summary>
    internal bool HandlePhysicalKey(HookKey key)
    {
        if (key.Injected) return false;
        lock (_stateGate) return ProcessPhysicalKey(key);
    }

    private bool OnKey(HookKey key)
    {
        // Injected events never participate in matching, so P→A with A→B does not chain (§9).
        if (key.Injected) return false;

        var capture = _capture;
        if (capture is not null)
        {
            capture.Feed(key);
            return true; // swallow everything so the user can press any key safely
        }

        lock (_stateGate) return ProcessPhysicalKey(key);
    }

    private bool ProcessPhysicalKey(HookKey key)
    {
        var isRepeat = key.IsDown && _down.Contains(KeyNames.Normalize(key.Vk));

        if (key.IsDown) _down.Add(KeyNames.Normalize(key.Vk));
        else _down.Remove(KeyNames.Normalize(key.Vk));

        if (HandleHotkey(key.Vk, key.IsDown, isRepeat)) return true;

        // Our own windows need normal typing, so coverage pauses while they are focused. A
        // release still has to be processed though: a press that started elsewhere must be
        // undone here, or its synthesised keys would stay logically down forever.
        if (IsOwnWindowFocused())
        {
            if (!key.IsDown) OnKeyUp(key.Vk);
            return false;
        }

        return key.IsDown ? OnKeyDown(key.Vk, isRepeat) : OnKeyUp(key.Vk);
    }

    private bool OnKeyDown(int rawVk, bool isRepeat)
    {
        if (isRepeat)
        {
            // A covered source key must never auto-repeat into the foreground app, whether or
            // not the target happens to have a key that can repeat. Anything else leaks the
            // source key straight through while the user keeps holding it.
            if (!_active.TryGetValue(rawVk, out var repeating)) return false;

            foreach (var vk in RemapPlan.RepeatableKeys(repeating.InjectedDown))
            {
                Synthesise(vk, keyUp: false);
                Synthesise(vk, keyUp: true);
            }

            return true;
        }

        if (!_coverageEnabled) return false;

        var table = _table;
        if (table.IsEmpty) return false;

        var pressed = new int[_down.Count];
        _down.CopyTo(pressed);
        Array.Sort(pressed);

        // Everything held matches exactly — most specific wins. If nothing matches that, fall
        // back to the modifier state as it was when this key went down (§8): otherwise a single
        // unrelated key held anywhere on the keyboard would silently disable every rule.
        var target = table.Lookup(KeyCombo.StableKeyOf(pressed));
        if (target is null && RemapPlan.AnchorCombo(pressed, rawVk) is { } anchor)
            target = table.Lookup(KeyCombo.StableKeyOf(anchor));

        if (target is null) return false;

        var mapping = new ActiveMapping();

        // A source modifier the target does not want must not colour the injected keys,
        // so neutralise it for the duration of the press and restore it on release.
        foreach (var vk in RemapPlan.ModifiersToNeutralise(pressed, target))
        {
            Synthesise(vk, keyUp: true);
            mapping.PendingRestore.Add(vk);
        }

        // The app never saw the trigger key go down, so a target that contains it still needs
        // an explicit press — that is what makes A → Ctrl+A work.
        var logicalDown = RemapPlan.LogicalDown(_down, rawVk, mapping.PendingRestore);
        foreach (var vk in RemapPlan.KeysToPress(target, logicalDown))
        {
            Synthesise(vk, keyUp: false);
            mapping.InjectedDown.Add(vk);
        }

        _active[rawVk] = mapping;
        return true;
    }

    private bool OnKeyUp(int rawVk)
    {
        foreach (var mapping in _active.Values) mapping.PendingRestore.Remove(KeyNames.Normalize(rawVk));

        if (!_active.TryGetValue(rawVk, out var active)) return false;

        for (var i = active.InjectedDown.Count - 1; i >= 0; i--)
            Synthesise(active.InjectedDown[i], keyUp: true);

        // Re-press the neutralised modifiers that the user is still physically holding.
        foreach (var vk in active.PendingRestore)
            Synthesise(vk, keyUp: false);

        _active.Remove(rawVk);
        return true;
    }

    /// <summary>
    /// The configured coverage toggle. Works regardless of focus, including while one of
    /// our own windows is up.
    /// </summary>
    private bool HandleHotkey(int rawVk, bool isDown, bool isRepeat)
    {
        var normalised = KeyNames.Normalize(rawVk);

        if (!isDown)
            return _hotkeySwallow.Remove(normalised);

        if (isRepeat) return _hotkeySwallow.Contains(normalised);

        var hotkey = _hotkey;
        if (hotkey is null || hotkey.IsEmpty) return false;
        if (!hotkey.Contains(normalised)) return false;

        // Exact match against the currently held set — no extra keys allowed.
        if (_down.Count != hotkey.Keys.Length) return false;
        if (!hotkey.Keys.All(_down.Contains)) return false;

        // Every other key of the toggle already reached the foreground app on its way down;
        // undo that so the toggle itself stays invisible to whatever had focus.
        foreach (var key in hotkey.Keys)
        {
            if (key == normalised) continue;
            Synthesise(key, keyUp: true);
        }

        foreach (var key in hotkey.Keys) _hotkeySwallow.Add(key);

        _coverageEnabled = !_coverageEnabled;
        CoverageToggled?.Invoke(_coverageEnabled);
        return true;
    }

    private bool IsOwnWindowFocused()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(foreground, out var pid);
        return pid == _selfPid;
    }

    private sealed class ActiveMapping
    {
        /// <summary>Target keys we synthesised down, released in reverse order.</summary>
        public List<int> InjectedDown { get; } = new();

        /// <summary>Source modifiers neutralised on press and re-pressed on release.</summary>
        public List<int> PendingRestore { get; } = new();
    }

    /// <summary>Immutable source-set → target-set lookup, rebuilt whenever config changes.</summary>
    private sealed class RuleTable
    {
        public static readonly RuleTable Empty = new(new Dictionary<string, int[]>());

        private readonly Dictionary<string, int[]> _map;

        private RuleTable(Dictionary<string, int[]> map) => _map = map;

        public bool IsEmpty => _map.Count == 0;

        public int[]? Lookup(string stableKey) => _map.TryGetValue(stableKey, out var v) ? v : null;

        public static RuleTable Build(AppConfig config)
        {
            var map = new Dictionary<string, int[]>(StringComparer.Ordinal);

            foreach (var profile in config.Profiles)
            {
                // §9: the profile must itself be enabled and valid for any of its rules to fire.
                if (!profile.Enabled || !profile.Valid) continue;

                foreach (var rule in profile.Rules)
                {
                    if (!rule.Enabled || !rule.Valid) continue;
                    if (rule.Source is null || rule.Target is null) continue;

                    // Duplicate sources are only possible across profiles; first profile wins.
                    map.TryAdd(rule.Source.StableKey, rule.Target.Keys);
                }
            }

            return new RuleTable(map);
        }
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _capture = null;
        _hook.Dispose();
    }
}
