using System;

namespace PicoStackables;

/// <summary>
/// Single source of truth for turning a vanilla stack size into the managed
/// one. Used by the server (authoritative apply), the client (local apply so
/// multiplayer clients enforce the same limits) and the dialog preview, so all
/// three can never disagree.
/// </summary>
public static class StackSizeCalc
{
    /// <summary>
    /// Base value from the global settings alone (multiplier or flat size),
    /// ignoring any per-item override. <paramref name="preventShrinking"/>
    /// clamps the result to at least the vanilla original.
    /// </summary>
    public static int Base(int original, bool useFlat, int flatSize, float multiplier, bool preventShrinking)
    {
        int value = useFlat
            ? flatSize
            : (int)Math.Round(original * multiplier);

        if (preventShrinking && value < original)
            value = original;

        return Math.Max(1, value);
    }

    /// <summary>
    /// Final managed stack size including a per-item override. Overrides are an
    /// explicit user choice and bypass the shrink guard.
    /// </summary>
    public static int Final(
        int original, bool hasOverride, int overrideValue,
        bool useFlat, int flatSize, float multiplier, bool preventShrinking)
        => hasOverride
            ? Math.Max(1, overrideValue)
            : Base(original, useFlat, flatSize, multiplier, preventShrinking);
}
