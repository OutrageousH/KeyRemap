namespace KeyRemap.Core;

/// <summary>
/// The key state behind 键位选择模式 (§8), kept free of timers and UI so it can be tested.
/// </summary>
/// <remarks>
/// The recorded combination is <b>whatever was held at the instant of the newest press</b>,
/// not the union of everything pressed while the session ran. Otherwise a key that was
/// pressed and released earlier — even seconds earlier — silently joins the result, and the
/// value only changes once the user lets go.
/// </remarks>
public sealed class CaptureBuffer
{
    /// <summary>Raw virtual keys currently held (left/right variants kept apart).</summary>
    private readonly HashSet<int> _pressed = new();

    /// <summary>Normalised keys of the combination as it stands right now.</summary>
    private readonly HashSet<int> _chord = new();

    public bool EverPressed { get; private set; }

    public DateTime StartedUtc { get; private set; }

    public DateTime LastReleaseUtc { get; private set; }

    public bool AllReleased => _pressed.Count == 0;

    /// <summary>Currently held keys, normalised and sorted.</summary>
    public int[] PressedKeys =>
        _pressed.Select(KeyNames.Normalize).Distinct().OrderBy(k => k).ToArray();

    /// <summary>The combination that would be recorded if the user let go right now.</summary>
    public int[] Chord => _chord.OrderBy(k => k).ToArray();

    /// <summary>Returns true when the visible state changed.</summary>
    public bool Press(int vk)
    {
        if (!_pressed.Add(vk)) return false; // auto-repeat of a key we already track

        EverPressed = true;

        // A press that starts from nothing begins a fresh burst, which is what the session
        // timeout should be measured against.
        if (_pressed.Count == 1) StartedUtc = DateTime.UtcNow;

        // Snapshot the held set: anything released before this moment drops out.
        _chord.Clear();
        foreach (var held in _pressed) _chord.Add(KeyNames.Normalize(held));

        return true;
    }

    /// <summary>Returns true when the visible state changed.</summary>
    public bool Release(int vk)
    {
        if (!_pressed.Remove(vk)) return false;

        LastReleaseUtc = DateTime.UtcNow;
        return true;
    }
}
