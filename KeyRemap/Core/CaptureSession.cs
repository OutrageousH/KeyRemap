using System.Windows.Threading;

namespace KeyRemap.Core;

/// <summary>Immutable view of a capture session, safe to hand to the UI thread.</summary>
public sealed class CaptureSnapshot
{
    /// <summary>Keys held right now.</summary>
    public int[] Pressed { get; init; } = Array.Empty<int>();

    /// <summary>What would be recorded if the user let go right now.</summary>
    public int[] Captured { get; init; } = Array.Empty<int>();

    /// <summary>All keys are up and the 80 ms window is still running.</summary>
    public bool InReleaseWindow { get; init; }

    /// <summary>True when the pending result has keys the user is no longer holding.</summary>
    public bool CapturedDiffersFromPressed => !Captured.AsSpan().SequenceEqual(Pressed);
}

/// <summary>
/// 键位选择模式 (需求 §8). Collects physical key presses until every key has been
/// released and an 80 ms grace window has elapsed, then reports the resulting set.
/// </summary>
public sealed class CaptureSession : IDisposable
{
    /// <summary>松开延迟 (§13.3).</summary>
    public const int ReleaseWindowMs = 80;

    /// <summary>Safety valve so a stuck key cannot trap the user in capture mode.</summary>
    private const int MaxSessionMs = 15000;

    private readonly object _gate = new();
    private readonly DispatcherTimer _timer;
    private readonly CaptureBuffer _buffer = new();

    private bool _finished;
    private int _notifyQueued;

    public CaptureSession(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Input, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(10),
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>Raised on the UI thread whenever the pressed set changes.</summary>
    public event Action<CaptureSnapshot>? Updated;

    /// <summary>Raised on the UI thread with the captured key set, normalised and sorted.</summary>
    public event Action<int[]>? Committed;

    /// <summary>Raised on the UI thread when the safety valve trips.</summary>
    public event Action? Abandoned;

    public bool IsFinished { get; private set; }

    /// <summary>Called from the hook thread for every physical key event.</summary>
    public void Feed(HookKey key)
    {
        bool changed;

        lock (_gate)
        {
            if (_finished) return;
            changed = key.IsDown ? _buffer.Press(key.Vk) : _buffer.Release(key.Vk);
        }

        if (changed) Notify();
    }

    public CaptureSnapshot Snapshot()
    {
        lock (_gate) return SnapshotLocked();
    }

    private CaptureSnapshot SnapshotLocked() => new()
    {
        Pressed = _buffer.PressedKeys,
        Captured = _buffer.Chord,
        InReleaseWindow = _buffer.EverPressed && _buffer.AllReleased,
    };

    private void Notify()
    {
        if (Interlocked.Exchange(ref _notifyQueued, 1) == 1) return;

        _timer.Dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _notifyQueued, 0);
            if (_finished) return;
            Updated?.Invoke(Snapshot());
        }, DispatcherPriority.Input);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        int[]? result = null;
        var abandoned = false;
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            if (_finished || !_buffer.EverPressed) return;

            if ((now - _buffer.StartedUtc).TotalMilliseconds > MaxSessionMs)
            {
                _finished = true;
                abandoned = true;
            }
            else if (_buffer.AllReleased &&
                     (now - _buffer.LastReleaseUtc).TotalMilliseconds >= ReleaseWindowMs)
            {
                _finished = true;
                result = _buffer.Chord;
            }
        }

        if (abandoned)
        {
            IsFinished = true;
            _timer.Stop();
            Abandoned?.Invoke();
            return;
        }

        if (result is not null)
        {
            IsFinished = true;
            _timer.Stop();
            Committed?.Invoke(result);
        }
    }

    public void Dispose()
    {
        lock (_gate) _finished = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
