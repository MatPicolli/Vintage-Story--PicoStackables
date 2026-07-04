using System.Collections.Generic;

namespace PicoStackables;

public class PicoStackablesConfig
{
    /// <summary>
    /// Multiplier applied to every item/block stack size.
    /// 1.0 = no change, 2.0 = double, 0.5 = half.
    /// Does NOT apply to items listed in ItemOverrides or BlockOverrides.
    /// Ignored when <see cref="UseFlatSize"/> is true.
    /// </summary>
    public float GlobalMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// When true, every managed item/block is set to <see cref="FlatStackSize"/>
    /// instead of being scaled by <see cref="GlobalMultiplier"/>.
    /// Per-item/block overrides still take precedence.
    /// </summary>
    public bool UseFlatSize { get; set; } = false;

    /// <summary>
    /// The absolute stack size applied to every managed item/block when
    /// <see cref="UseFlatSize"/> is true (e.g. 500 or 1000).
    /// </summary>
    public int FlatStackSize { get; set; } = 100;

    /// <summary>
    /// Safety net: when true the mod never sets a stack size below the item's
    /// vanilla value, so lowering the multiplier / a small flat size can't
    /// truncate and destroy existing oversized stacks. Overrides are explicit
    /// and bypass this guard.
    /// </summary>
    public bool PreventShrinking { get; set; } = true;

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
