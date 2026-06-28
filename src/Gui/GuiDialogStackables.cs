using System;
using System.Collections.Generic;
using System.Linq;
using PicoStackables.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PicoStackables.Gui;

/// <summary>
/// In-game config dialog for PicoStackables.
///
/// Layout (scaled units):
///   ┌─ PicoStackables ────────────────────────────────[×]─┐
///   │  Stack Multiplier: [2.0      ]                      │
///   │  Search:           [_______________________]        │
///   ├──────────────────────────────────────────────────   │
///   │  [icon] Stick                              [scroll] │
///   │         16 → 32                                     │
///   │  [icon] Stone-Granite                               │
///   │         64 → 128                                    │
///   │  ...                                                │
///   ├──────────────────────────────────────────────────   │
///   │                    [  Save  ]  [  Close  ]          │
///   └─────────────────────────────────────────────────────┘
/// </summary>
public class GuiDialogStackables : GuiDialog
{
    public override string ToggleKeyCombinationCode => null!; // opened via command only

    private readonly PicoStackablesClientSystem clientSys;

    // All cells, pre-built from server data; search filters into filteredCells
    private List<StackableItemCell> allCells      = new();
    private List<StackableItemCell> filteredCells = new();

    private float  currentMultiplier = 2f;
    private string searchText        = "";

    // Bounds that need to stay in scope for scroll callbacks
    private ElementBounds clipBounds  = null!;
    private ElementBounds listBounds  = null!;

    // Dialog dimensions (unscaled units)
    private const double DialogW    = 520;
    private const double DialogH    = 580;
    private const double Pad        = 12;
    private const double ListH      = 380;  // visible clip height
    private const double CellH      = 54;
    private const double CellW      = 460;  // list inner width (leaves room for scrollbar)
    private const double ScrollbarW = 20;

    public GuiDialogStackables(ICoreClientAPI capi, PicoStackablesClientSystem clientSys)
        : base(capi)
    {
        this.clientSys = clientSys;
        if (clientSys.ServerData != null)
            BuildCells(clientSys.ServerData);
        ComposeDialog();
    }

    /// <summary>Called by the client system when fresh data arrives from the server.</summary>
    public void RefreshFromData(StackablesInitPacket data)
    {
        currentMultiplier = data.GlobalMultiplier;
        BuildCells(data);
        ComposeDialog();
    }

    // -------------------------------------------------------------------------
    // Cell construction
    // -------------------------------------------------------------------------

    private void BuildCells(StackablesInitPacket data)
    {
        currentMultiplier = data.GlobalMultiplier;

        allCells.Clear();

        // Items
        foreach (var (code, origStack) in data.OriginalItemStacks.OrderBy(k => k.Key))
        {
            var item = capi.World.GetItem(new AssetLocation(code));
            if (item == null) continue;

            var stack  = new ItemStack(item);
            var bounds = ElementBounds.Fixed(0, 0, CellW, CellH); // y set by GetItemY

            var cell = new StackableItemCell(
                capi, stack, code, isBlock: false,
                originalStack: origStack, bounds,
                getMultiplier: () => currentMultiplier);

            if (data.ItemOverrides.TryGetValue(code, out int ov))
            {
                cell.HasOverride  = true;
                cell.OverrideValue = ov;
            }

            allCells.Add(cell);
        }

        // Blocks (only those that can be stacked in inventory, i.e. origStack > 1 typically)
        foreach (var (code, origStack) in data.OriginalBlockStacks.OrderBy(k => k.Key))
        {
            if (origStack <= 0) continue;
            var block = capi.World.GetBlock(new AssetLocation(code));
            if (block == null) continue;

            var stack  = new ItemStack(block);
            var bounds = ElementBounds.Fixed(0, 0, CellW, CellH);

            var cell = new StackableItemCell(
                capi, stack, code, isBlock: true,
                originalStack: origStack, bounds,
                getMultiplier: () => currentMultiplier);

            if (data.BlockOverrides.TryGetValue(code, out int ov))
            {
                cell.HasOverride  = true;
                cell.OverrideValue = ov;
            }

            allCells.Add(cell);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string q = searchText.ToLowerInvariant();
        filteredCells = string.IsNullOrEmpty(q)
            ? allCells
            : allCells.Where(c => c.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                  c.stack.GetName().Contains(q, StringComparison.OrdinalIgnoreCase))
                      .ToList();

        // Re-position cell bounds vertically
        for (int i = 0; i < filteredCells.Count; i++)
            filteredCells[i].Bounds.fixedY = i * CellH;
    }

    // -------------------------------------------------------------------------
    // Dialog composition
    // -------------------------------------------------------------------------

    private void ComposeDialog()
    {
        double headerH  = 30; // title bar (added automatically)
        double innerH   = DialogH - headerH;

        // Full dialog area
        var dialogBounds = ElementBounds.Fixed(0, 0, DialogW, DialogH)
            .WithAlignment(EnumDialogArea.CenterMiddle);

        // Background fills dialog
        var bgBounds = ElementBounds.Fill.WithFixedPadding(0);

        // ── Rows inside the dialog ──────────────────────────────────────────

        double rowY = Pad;

        // Multiplier label + input
        var multLabelBounds = ElementBounds.Fixed(Pad, rowY, 160, 28);
        var multInputBounds = ElementBounds.Fixed(Pad + 165, rowY, 100, 28);
        rowY += 36;

        // Search label + input
        var searchLabelBounds = ElementBounds.Fixed(Pad, rowY, 60, 28);
        var searchInputBounds = ElementBounds.Fixed(Pad + 65, rowY, CellW - 65, 28);
        rowY += 40;

        // Cell list clip + scrollbar
        clipBounds = ElementBounds.Fixed(Pad, rowY, CellW, ListH);
        listBounds = ElementBounds.Fixed(0, 0, CellW, filteredCells.Count * CellH)
            .WithParent(clipBounds);
        var scrollbarBounds = ElementStdBounds.VerticalScrollbar(clipBounds);
        rowY += ListH + Pad;

        // Buttons
        var saveBounds  = ElementBounds.Fixed(DialogW - Pad - 200, rowY, 90, 30);
        var closeBounds = ElementBounds.Fixed(DialogW - Pad - 100, rowY, 90, 30);

        SingleComposer = capi.Gui
            .CreateCompo("picostackables", dialogBounds)
            .AddShadedDialogBG(bgBounds, withTitleBar: true)
            .AddDialogTitleBar("PicoStackables – Stack Sizes", OnTitleClose)
            .BeginChildElements(bgBounds)

                // Multiplier row
                .AddStaticText("Stack Multiplier:", CairoFont.WhiteSmallText(), multLabelBounds)
                .AddTextInput(multInputBounds, OnMultiplierChanged, CairoFont.WhiteDetailText(), "multInput")

                // Search row
                .AddStaticText("Search:", CairoFont.WhiteSmallText(), searchLabelBounds)
                .AddTextInput(searchInputBounds, OnSearchChanged, CairoFont.WhiteDetailText(), "searchInput")

                // Scrollable item list
                .BeginClip(clipBounds)
                    .AddCellList(listBounds, OnCellClick, filteredCells, "cellList")
                .EndClip()
                .AddVerticalScrollbar(OnScroll, scrollbarBounds, "scrollbar")

                // Buttons
                .AddSmallButton("Save",  OnSave,     saveBounds)
                .AddSmallButton("Close", OnCloseBtnClick, closeBounds)

            .EndChildElements()
            .Compose();

        // Seed text inputs with current values
        SingleComposer.GetTextInput("multInput").SetValue(currentMultiplier.ToString("0.##"));
        SingleComposer.GetTextInput("searchInput").SetPlaceHolderText("item code or name…");

        SyncScrollbar();
    }

    private void SyncScrollbar()
    {
        if (SingleComposer == null) return;
        var list      = SingleComposer.GetCellList<StackableItemCell>("cellList");
        var scrollbar = SingleComposer.GetScrollbar("scrollbar");
        if (list == null || scrollbar == null) return;

        double totalH = filteredCells.Count * CellH;
        scrollbar.SetHeights((float)ListH, (float)totalH);
    }

    // -------------------------------------------------------------------------
    // Callbacks
    // -------------------------------------------------------------------------

    private void OnMultiplierChanged(string val)
    {
        if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float f))
        {
            currentMultiplier = Math.Max(0.01f, f);
            // Cells read currentMultiplier via their Func<float> and re-bake automatically
        }
    }

    private void OnSearchChanged(string val)
    {
        searchText = val;
        ApplyFilter();

        var list = SingleComposer?.GetCellList<StackableItemCell>("cellList");
        if (list == null) return;

        list.ReloadCells(filteredCells);
        SingleComposer!.GetScrollbar("scrollbar")?.ScrollToY(0);
        SyncScrollbar();
    }

    private void OnScroll(float val)
    {
        var list = SingleComposer?.GetCellList<StackableItemCell>("cellList");
        if (list == null) return;

        list.insideBounds.fixedY = -val;
        list.insideBounds.CalcWorldBounds();
    }

    private void OnCellClick(StackableItemCell cell, bool rightClick)
    {
        // Right-click: toggle / clear override. Left-click: no-op (future: open inline editor).
        if (!rightClick) return;

        if (cell.HasOverride)
        {
            cell.HasOverride = false;
        }
        else
        {
            // Default the override to current computed size so the user can adjust from there
            float mult    = currentMultiplier;
            int computed  = Math.Max(1, (int)Math.Round(cell.OriginalStack * mult));
            cell.HasOverride  = true;
            cell.OverrideValue = computed;
        }
    }

    private bool OnSave()
    {
        var itemOvr  = new System.Collections.Generic.Dictionary<string, int>();
        var blockOvr = new System.Collections.Generic.Dictionary<string, int>();

        foreach (var cell in allCells)
        {
            if (!cell.HasOverride) continue;
            if (cell.IsBlock) blockOvr[cell.Code] = cell.OverrideValue;
            else              itemOvr[cell.Code]  = cell.OverrideValue;
        }

        clientSys.SendSave(new StackablesSavePacket
        {
            GlobalMultiplier = currentMultiplier,
            ItemOverrides    = itemOvr,
            BlockOverrides   = blockOvr,
        });

        return true;
    }

    private bool OnCloseBtnClick()
    {
        TryClose();
        return true;
    }

    private void OnTitleClose() => TryClose();

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        SingleComposer?.Dispose();
    }
}
