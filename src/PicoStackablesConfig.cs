using System.Collections.Generic;

namespace PicoStackables;

public class PicoStackablesConfig
{
    /// <summary>
    /// Multiplier applied to every item/block stack size.
    /// 1.0 = no change, 2.0 = double, 0.5 = half.
    /// Does NOT apply to items listed in ItemOverrides or BlockOverrides.
    /// </summary>
    public float GlobalMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// Per-item absolute stack size overrides.
    /// Key  = full item code (e.g. "game:stick", "game:stone-granite").
    /// Value = desired max stack size.
    /// Items listed here ignore GlobalMultiplier entirely.
    /// </summary>
    public Dictionary<string, int> ItemOverrides { get; set; } = new();

    /// <summary>
    /// Same as ItemOverrides but for placeable blocks (block codes).
    /// </summary>
    public Dictionary<string, int> BlockOverrides { get; set; } = new();
}
