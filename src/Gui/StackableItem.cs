using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PicoStackables.Gui;

/// <summary>
/// One item or block tracked by the dialog. Owns its icon stack, a lazily-baked
/// Cairo text texture ("16 → 32") and its override state. Several of these are
/// rendered side-by-side per row to form a multi-column grid.
/// </summary>
public class StackableItem
{
    public readonly string Code;
    public readonly bool   IsBlock;
    public readonly int    OriginalStack;
    public readonly string DisplayName;
    public readonly string SearchKey;   // precomputed lowercase "code name" for fast filtering

    public bool HasOverride;
    public int  OverrideValue;
    public bool Selected;

    private readonly ICoreClientAPI capi;
    private readonly ItemStack      stack;
    private readonly Func<float>    getMultiplier;

    private LoadedTexture? textTex;
    private float lastMult     = float.MinValue;
    private bool  lastHasOvr;
    private int   lastOvrVal;
    private bool  lastSelected;
    private int   lastTexW;

    public const int IconSize = 38;
    public const int IconPad  = 6;
    public const int TextOffX = IconPad + IconSize + 6;

    public StackableItem(
        ICoreClientAPI capi,
        ItemStack      stack,
        string         code,
        bool           isBlock,
        int            originalStack,
        Func<float>    getMultiplier)
    {
        this.capi          = capi;
        this.stack         = stack;
        this.Code          = code;
        this.IsBlock       = isBlock;
        this.OriginalStack = originalStack;
        this.getMultiplier = getMultiplier;

        DisplayName = stack.GetName() ?? code;
        SearchKey   = (code + " " + DisplayName).ToLowerInvariant();
    }

    public int ComputeStack(float mult) => HasOverride
        ? Math.Max(1, OverrideValue)
        : Math.Max(1, (int)Math.Round(OriginalStack * mult));

    public void Render(ICoreClientAPI api, double x, double y, double colWidth, double rowHeight)
    {
        // Icon (no stack-size number drawn – we show that as text)
        api.Render.RenderItemstackToGui(
            new DummySlot(stack),
            x + IconPad + IconSize / 2.0,
            y + rowHeight / 2.0,
            100,
            IconSize,
            ColorUtil.WhiteArgb,
            shading: true,
            rotate: false,
            showStackSize: false);

        int texW = Math.Max(1, (int)(colWidth - TextOffX - IconPad));
        int texH = Math.Max(1, (int)rowHeight);

        float mult = getMultiplier();
        if (NeedsRebake(mult, texW))
            Bake(mult, texW, texH);

        if (textTex != null && textTex.TextureId > 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(
                textTex.TextureId,
                (int)(x + TextOffX),
                (int)y,
                textTex.Width,
                textTex.Height);
        }
    }

    private bool NeedsRebake(float mult, int texW)
    {
        if (textTex == null) return true;
        if (texW != lastTexW) return true;
        if (lastSelected != Selected) return true;
        if (lastHasOvr != HasOverride) return true;
        if (HasOverride && lastOvrVal != OverrideValue) return true;
        if (!HasOverride && Math.Abs(mult - lastMult) > 0.0001f) return true;
        return false;
    }

    private void Bake(float mult, int texW, int texH)
    {
        lastMult     = mult;
        lastHasOvr   = HasOverride;
        lastOvrVal   = OverrideValue;
        lastSelected = Selected;
        lastTexW     = texW;

        int newStack = ComputeStack(mult);

        using var surface = new ImageSurface(Format.Argb32, texW, texH);
        using var ctx     = new Context(surface);

        // Selection highlight behind the text
        if (Selected)
        {
            ctx.SetSourceRGBA(0.30, 0.55, 0.95, 0.30);
            RoundedRect(ctx, 0, 4, texW - 2, texH - 8, 4);
            ctx.Fill();
        }

        double scale = RuntimeEnv.GUIScale;

        // Name (white)
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(13 * scale);
        ctx.SetSourceRGBA(1, 1, 1, 0.96);
        ctx.MoveTo(4, 17 * scale);
        ctx.ShowText(Truncate(DisplayName, ctx, texW - 6));

        // Stack line: "16 → 32"
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(12 * scale);

        double y2 = 36 * scale;
        double x  = 4;

        ctx.SetSourceRGBA(0.75, 0.75, 0.75, 1);
        ctx.MoveTo(x, y2);
        ctx.ShowText($"{OriginalStack}");
        x += ctx.TextExtents($"{OriginalStack}").XAdvance;

        ctx.SetSourceRGBA(0.55, 0.55, 0.55, 1);
        ctx.MoveTo(x, y2);
        ctx.ShowText(" → ");
        x += ctx.TextExtents(" → ").XAdvance;

        if (HasOverride)
            ctx.SetSourceRGBA(1.0, 0.82, 0.20, 1);    // yellow override
        else if (newStack != OriginalStack)
            ctx.SetSourceRGBA(0.25, 0.95, 0.35, 1);   // green multiplied
        else
            ctx.SetSourceRGBA(0.75, 0.75, 0.75, 1);   // unchanged

        ctx.MoveTo(x, y2);
        ctx.ShowText($"{newStack}");

        var tex = textTex ?? new LoadedTexture(capi);
        capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref tex);
        textTex = tex;
    }

    private static void RoundedRect(Context ctx, double x, double y, double w, double h, double r)
    {
        ctx.NewPath();
        ctx.Arc(x + w - r, y + r,     r, -Math.PI / 2, 0);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.Arc(x + r,     y + h - r, r, Math.PI / 2, Math.PI);
        ctx.Arc(x + r,     y + r,     r, Math.PI, Math.PI * 1.5);
        ctx.ClosePath();
    }

    private static string Truncate(string name, Context ctx, int maxWidth)
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
