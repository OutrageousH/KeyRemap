namespace KeyRemap.Core;

/// <summary>
/// The decisions behind a remap, kept free of the hook so they can be reasoned about and
/// tested without a live keyboard. The engine only executes what these return.
/// </summary>
public static class RemapPlan
{
    /// <summary>
    /// Which keys the foreground application currently believes are held.
    /// The trigger key's key-down was swallowed and never reached the app, and source
    /// modifiers that the target does not want were explicitly released, so neither counts
    /// as held even though the hardware says otherwise.
    /// </summary>
    public static HashSet<int> LogicalDown(
        IEnumerable<int> physicalDown,
        int triggerVk,
        IEnumerable<int> neutralisedModifiers)
    {
        var held = physicalDown.ToHashSet();

        held.Remove(KeyNames.Normalize(triggerVk));
        foreach (var vk in neutralisedModifiers) held.Remove(KeyNames.Normalize(vk));

        return held;
    }

    /// <summary>
    /// The combination to try when the full held set matched nothing: the modifiers that were
    /// down at the instant this key went down, plus the key itself (§8). Without this fallback a
    /// single unrelated key held elsewhere on the keyboard silently disables every rule.
    /// </summary>
    /// <returns>Null when the trigger is itself a modifier, where only exact matching applies.</returns>
    public static int[]? AnchorCombo(IEnumerable<int> held, int triggerVk)
    {
        var trigger = KeyNames.Normalize(triggerVk);
        if (KeyNames.IsModifier(trigger)) return null;

        var anchor = held.Where(vk => vk == trigger || KeyNames.IsModifier(vk)).ToArray();
        return Array.IndexOf(anchor, trigger) < 0 ? null : anchor;
    }

    /// <summary>Target keys that still need a synthesised key-down.</summary>
    public static List<int> KeysToPress(IEnumerable<int> target, HashSet<int> logicalDown) =>
        target.Where(vk => !logicalDown.Contains(vk)).ToList();

    /// <summary>
    /// Source modifiers the target does not contain. They are held for real but must not
    /// colour the injected keys, so they get released for the duration of the press.
    /// </summary>
    public static List<int> ModifiersToNeutralise(IEnumerable<int> source, IEnumerable<int> target)
    {
        var wanted = target.ToHashSet();
        return source.Where(vk => KeyNames.IsModifier(vk) && !wanted.Contains(vk)).ToList();
    }

    /// <summary>
    /// Non-modifier keys of a press, used to mirror auto-repeat while the source is held.
    /// A modifier-only target has nothing to repeat.
    /// </summary>
    public static List<int> RepeatableKeys(IEnumerable<int> target) =>
        target.Where(vk => !KeyNames.IsModifier(vk)).ToList();
}
