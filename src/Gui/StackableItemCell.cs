using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PicoStackables.Gui;

/// <summary>
/// Renders one item row in the config dialog:
///   [icon]  Display Name
///           16 → 32
/// The "new stack" number is green (or yellow for overrides) and re-bakes
/// whenever the multiplier changes, giving live preview with no full recompose.
/// </summary>
public class StackableItemCell : IGuiElementCell
{
    public readonly string Code;
    public readonly bool   IsBlock;
    public readonly int    OriginalStack;

    // Set by the dialog when the user types an override value for this item
    public bool HasOverride;
    public int  OverrideValue;

    private readonly ICoreClientAPI capi;
    internal readonly ItemStack      stack;
    private readonly Func<float>     getMultiplier;
    private readonly Action<StackableItemCell>? onRightClick;

    private LoadedTexture? textTex;
    private float lastBakedMult   = float.MinValue;
    private bool  lastBakedHasOvr;
    private int   lastBakedOvr;

    // Row height in unscaled pixels (used by the cell list to lay rows out)
    public const int CellH    = 54;
    private const int IconSize = 40;
    private const int IconPad  = 7;
    private const int TextOffX = IconPad + IconSize + 6;

    // Set by the cell list (via the factory) – never construct our own here.
    public ElementBounds Bounds { get; set; } = null!;

    public StackableItemCell(
        ICoreClientAPI capi,
        ItemStack      stack,
        string         code,
        bool           isBlock,
        int            originalStack,
        Func<float>    getMultiplier,
        Action<StackableItemCell>? onRightClick = null)
    {
        this.capi          = capi;
        this.stack         = stack;
        this.Code          = code;
        this.IsBlock       = isBlock;
        this.OriginalStack = originalStack;
        this.getMultiplier = getMultiplier;
        this.onRightClick  = onRightClick;
    }

    // IGuiElementCell – required interface members
    public ElementBounds InsideClipBounds { get; set; } = null!;
    public string MouseOverCursor => null!;

    public void Compose() { }
    public void UpdateCellEdit(ICoreClientAPI api, bool editing, int cellIndex) { }

    public void UpdateCellHeight()
    {
        Bounds.fixedHeight = CellH;
        Bounds.CalcWorldBounds();
    }
    public void OnMouseDownOnElement(MouseEvent e, int elementIndex) { }
    public void OnMouseUpOnElement(MouseEvent e, int elementIndex)
    {
        if (e.Button == EnumMouseButton.Right)
            onRightClick?.Invoke(this);
    }
    public void OnMouseMoveOnElement(MouseEvent e, int elementIndex) { }

    public void OnRenderInteractiveElements(ICoreClientAPI api, float dt)
    {
        float mult = getMultiplier();

        // Re-bake Cairo texture only when something changed
        if (!NeedsRebake(mult))
        {
            DrawCell(api);
            return;
        }

        lastBakedMult   = mult;
        lastBakedHasOvr = HasOverride;
        lastBakedOvr    = OverrideValue;

        BakeText(mult);
        DrawCell(api);
    }

    private bool NeedsRebake(float mult)
    {
        if (textTex == null) return true;
        if (Math.Abs(mult - lastBakedMult) > 0.0001f) return true;
        if (lastBakedHasOvr != HasOverride) return true;
        if (HasOverride && lastBakedOvr != OverrideValue) return true;
        return false;
    }

    private void BakeText(float mult)
    {
        int texW = Math.Max(1, (int)(Bounds.OuterWidth  - TextOffX));
        int texH = Math.Max(1, (int)(Bounds.OuterHeight));

        int newStack = HasOverride
            ? Math.Max(1, OverrideValue)
            : Math.Max(1, (int)Math.Round(OriginalStack * mult));

        using var surface = new ImageSurface(Format.Argb32, texW, texH);
        using var ctx     = new Context(surface);

        // ── Item display name (white) ────────────────────────────────────────
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(13 * RuntimeEnv.GUIScale);
        ctx.SetSourceRGBA(1, 1, 1, 0.95);
        ctx.MoveTo(0, 18 * RuntimeEnv.GUIScale);
        ctx.ShowText(TruncateName(stack.GetName(), ctx, texW));

        // ── Stack size line ──────────────────────────────────────────────────
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(12 * RuntimeEnv.GUIScale);

        string baseStr = $"{OriginalStack}";
        string arrowStr = " → ";
        string newStr  = $"{newStack}";
        string ovLabel = HasOverride ? " (override)" : "";

        double y2 = 38 * RuntimeEnv.GUIScale;

        // Base size in light grey
        ctx.SetSourceRGBA(0.75, 0.75, 0.75, 1);
        ctx.MoveTo(0, y2);
        ctx.ShowText(baseStr);

        double x = ctx.TextExtents(baseStr).XAdvance;

        // Arrow in dim grey
        ctx.SetSourceRGBA(0.55, 0.55, 0.55, 1);
        ctx.MoveTo(x, y2);
        ctx.ShowText(arrowStr);
        x += ctx.TextExtents(arrowStr).XAdvance;

        // New size: green when multiplied, yellow when override
        if (HasOverride)
            ctx.SetSourceRGBA(1.0, 0.82, 0.20, 1);   // yellow
        else if (newStack != OriginalStack)
            ctx.SetSourceRGBA(0.25, 0.95, 0.35, 1);  // green
        else
            ctx.SetSourceRGBA(0.75, 0.75, 0.75, 1);  // unchanged: grey

        ctx.MoveTo(x, y2);
        ctx.ShowText(newStr + ovLabel);

        // LoadOrUpdateCairoTexture requires a non-null LoadedTexture instance to
        // write into (it may be empty, but a null ref crashes inside GLImpl).
        var tex = textTex ?? new LoadedTexture(capi);
        capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref tex);
        textTex = tex;
    }

    private void DrawCell(ICoreClientAPI api)
    {
        // Item icon
        api.Render.RenderItemstackToGui(
            new DummySlot(stack),
            Bounds.renderX + IconPad,
            Bounds.renderY + (CellH - IconSize) / 2.0,
            100,           // z – keep in front of background
            IconSize,
            ColorUtil.WhiteArgb);

        // Text texture
        if (textTex?.TextureId > 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(
                textTex.TextureId,
                (int)(Bounds.renderX + TextOffX),
                (int)Bounds.renderY,
                textTex.Width,
                textTex.Height);
        }
    }

    private static string TruncateName(string name, Context ctx, int maxWidth)
    {
        if (ctx.TextExtents(name).XAdvance <= maxWidth) return name;
        while (name.Length > 1 && ctx.TextExtents(name + "…").XAdvance > maxWidth)
            name = name[..^1];
        return name + "…";
    }

    public void Dispose()
    {
        textTex?.Dispose();
        textTex = null;
    }
}
