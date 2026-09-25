namespace KeyRemap.Core;

/// <summary>
/// An unordered set of virtual keys. Stored normalised and sorted so two combos
/// with the same keys compare equal regardless of press order.
/// </summary>
public sealed class KeyCombo : IEquatable<KeyCombo>
{
    public int[] Keys { get; }

    public string Display { get; }

    public KeyCombo(IEnumerable<int> keys)
    {
        Keys = keys.Select(KeyNames.Normalize).Distinct().OrderBy(k => k).ToArray();
        Display = string.Join("+", Keys
            .OrderBy(KeyNames.DisplayOrder)
            .ThenBy(k => k)
            .Select(KeyNames.Name));
    }

    public bool IsEmpty => Keys.Length == 0;

    /// <summary>Canonical dictionary key for set matching.</summary>
    public string StableKey => string.Join(",", Keys);

    public bool Contains(int vk) => Array.IndexOf(Keys, KeyNames.Normalize(vk)) >= 0;

    /// <summary>True when every key of <paramref name="other"/> is present here.</summary>
    public bool ContainsAll(IEnumerable<int> other) => other.All(Contains);

    public bool Equals(KeyCombo? other) =>
        other is not null && Keys.AsSpan().SequenceEqual(other.Keys);

    public override bool Equals(object? obj) => Equals(obj as KeyCombo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var k in Keys) hash.Add(k);
        return hash.ToHashCode();
    }

    public override string ToString() => Display;

    public static string StableKeyOf(IEnumerable<int> keys) =>
        string.Join(",", keys.Select(KeyNames.Normalize).Distinct().OrderBy(k => k));
}
