using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PicoStackables.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PicoStackables.Gui;

/// <summary>
/// In-game config dialog for PicoStackables.
///
/// Top: global multiplier input + search box + a per-item override editor.
/// Middle: a multi-column, scrollable grid of every item/block showing
///         "original → new" with the new value in green (yellow for overrides).
/// Bottom: Save / Close.
/// </summary>
public class GuiDialogStackables : GuiDialog
{
    public override string ToggleKeyCombinationCode => null!; // opened via command only

    private readonly PicoStackablesClientSystem clientSys;

    private readonly List<StackableItem> allItems      = new();
    private List<StackableItem>          filteredItems = new();
    private List<StackableRowCell>       rowCells      = new();

    private StackableItem? selected;

    private float  currentMultiplier = 2f;
    private string searchText        = "";
    private long   searchDebounceId  = -1;

    private ElementBounds clipBounds = null!;
    private ElementBounds listBounds = null!;

    // Layout (unscaled)
    private const int    Columns   = 3;
    private const double DialogW   = 760;
    private const double DialogH   = 600;
    private const double Pad       = 16;
    private const double TopOffset = 32;   // clears the title bar
    private const double RowH      = 30;
    private const double ListH     = 392;
    private const double ScrollW   = 20;

    private double ListW => DialogW - Pad * 2 - ScrollW;

    public GuiDialogStackables(ICoreClientAPI capi, PicoStackablesClientSystem clientSys)
        : base(capi)
    {
        this.clientSys = clientSys;
        if (clientSys.ServerData != null)
            BuildItems(clientSys.ServerData);
        ComposeDialog();
    }

    public void RefreshFromData(StackablesInitPacket data)
    {
        BuildItems(data);
        ComposeDialog();
    }

    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    private void BuildItems(StackablesInitPacket data)
    {
        currentMultiplier = data.GlobalMultiplier;

        foreach (var it in allItems) it.Dispose();
        allItems.Clear();
        selected = null;

        foreach (var (code, orig) in data.OriginalItemStacks.OrderBy(k => k.Key))
        {
            var item = capi.World.GetItem(new AssetLocation(code));
            if (item == null) continue;
            var si = new StackableItem(capi, new ItemStack(item), code, false, orig,
                                       () => currentMultiplier);
            if (data.ItemOverrides.TryGetValue(code, out int ov)) { si.HasOverride = true; si.OverrideValue = ov; }
            allItems.Add(si);
        }

        foreach (var (code, orig) in data.OriginalBlockStacks.OrderBy(k => k.Key))
        {
            if (orig <= 0) continue;
            var block = capi.World.GetBlock(new AssetLocation(code));
            if (block == null) continue;
            var si = new StackableItem(capi, new ItemStack(block), code, true, orig,
                                       () => currentMultiplier);
            if (data.BlockOverrides.TryGetValue(code, out int ov)) { si.HasOverride = true; si.OverrideValue = ov; }
            allItems.Add(si);
        }

        RebuildRows();
    }

    private void RebuildRows()
    {
        string q = searchText.Trim().ToLowerInvariant();
        filteredItems = string.IsNullOrEmpty(q)
            ? allItems
            : allItems.Where(i => i.SearchKey.Contains(q)).ToList();

        rowCells = new List<StackableRowCell>((filteredItems.Count + Columns - 1) / Columns);
        for (int i = 0; i < filteredItems.Count; i += Columns)
        {
            int count = Math.Min(Columns, filteredItems.Count - i);
            rowCells.Add(new StackableRowCell(
                filteredItems.GetRange(i, count), Columns, OnItemClicked));
        }
    }

    // -------------------------------------------------------------------------
    // Composition
    // -------------------------------------------------------------------------

    private void ComposeDialog()
    {
        var dialogBounds = ElementBounds.Fixed(0, 0, DialogW, DialogH)
            .WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(0);

        double y = TopOffset + Pad;

        var multLabel = ElementBounds.Fixed(Pad, y, 210, RowH);
        var multInput = ElementBounds.Fixed(Pad + 215, y, 90, RowH);
        y += RowH + 8;

        var searchLabel = ElementBounds.Fixed(Pad, y, 70, RowH);
        var searchInput = ElementBounds.Fixed(Pad + 75, y, ListW - 75, RowH);
        y += RowH + 8;

        // Override editor row
        var selLabel  = ElementBounds.Fixed(Pad, y, 320, RowH);
        var ovLabel   = ElementBounds.Fixed(Pad + 330, y, 90, RowH);
        var ovInput   = ElementBounds.Fixed(Pad + 420, y, 70, RowH);
        var setBtn    = ElementBounds.Fixed(Pad + 495, y, 80, RowH);
        var clearBtn  = ElementBounds.Fixed(Pad + 580, y, 110, RowH);
        y += RowH + 10;

        clipBounds = ElementBounds.Fixed(Pad, y, ListW, ListH);
        listBounds = ElementBounds.Fixed(0, 0, ListW, rowCells.Count * StackableRowCell.CellH)
            .WithParent(clipBounds);
        var scrollBounds = ElementStdBounds.VerticalScrollbar(clipBounds);
        y += ListH + 12;

        var saveBtn  = ElementBounds.Fixed(DialogW - Pad - 200, y, 90, 32);
        var closeBtn = ElementBounds.Fixed(DialogW - Pad - 100, y, 90, 32);

        var font     = CairoFont.WhiteSmallText();
        var detail   = CairoFont.WhiteDetailText();

        SingleComposer = capi.Gui
            .CreateCompo("picostackables", dialogBounds)
            .AddShadedDialogBG(bgBounds, withTitleBar: true)
            .AddDialogTitleBar("PicoStackables – Stack Sizes", () => TryClose())
            .BeginChildElements(bgBounds)

                .AddStaticText("Global Stack Multiplier:", font, multLabel)
                .AddNumberInput(multInput, OnMultiplierChanged, detail, "multInput")

                .AddStaticText("Search:", font, searchLabel)
                .AddTextInput(searchInput, OnSearchChanged, detail, "searchInput")

                .AddDynamicText("Click an item to edit its stack size", font, selLabel, "selLabel")
                .AddStaticText("Set to:", font, ovLabel)
                .AddNumberInput(ovInput, _ => { }, detail, "ovInput")
                .AddSmallButton("Apply", OnApplyOverride, setBtn)
                .AddSmallButton("Use multiplier", OnClearOverride, clearBtn)

                .BeginClip(clipBounds)
                    .AddCellList(listBounds, RequireCell, rowCells, "cellList")
                .EndClip()
                .AddVerticalScrollbar(OnScroll, scrollBounds, "scrollbar")

                .AddSmallButton("Save",  OnSave,    saveBtn)
                .AddSmallButton("Close", () => { TryClose(); return true; }, closeBtn)

            .EndChildElements()
            .Compose();

        SingleComposer.GetNumberInput("multInput").SetValue(currentMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
        SingleComposer.GetTextInput("searchInput").SetPlaceHolderText("item code or name…");
        if (!string.IsNullOrEmpty(searchText))
            SingleComposer.GetTextInput("searchInput").SetValue(searchText);

        SyncScrollbar();
    }

    private IGuiElementCell RequireCell(StackableRowCell cell, ElementBounds bounds)
    {
        bounds.fixedHeight    = StackableRowCell.CellH;
        cell.Bounds           = bounds;
        cell.InsideClipBounds = clipBounds;
        return cell;
    }

    private void SyncScrollbar()
    {
        var scrollbar = SingleComposer?.GetScrollbar("scrollbar");
        if (scrollbar == null) return;
        scrollbar.SetHeights((float)ListH, (float)(rowCells.Count * StackableRowCell.CellH));
    }

    // -------------------------------------------------------------------------
    // Callbacks
    // -------------------------------------------------------------------------

    private void OnMultiplierChanged(string val)
    {
        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            currentMultiplier = Math.Max(0.01f, f);
        // Visible cells re-bake automatically via their getMultiplier closure.
    }

    private void OnSearchChanged(string val)
    {
        searchText = val;

        // Debounce: only refilter ~250ms after the last keystroke to avoid stutter.
        if (searchDebounceId >= 0) capi.Event.UnregisterCallback(searchDebounceId);
        searchDebounceId = capi.Event.RegisterCallback(_ => ApplySearch(), 250);
    }

    private void ApplySearch()
    {
        searchDebounceId = -1;
        RebuildRows();

        var list = SingleComposer?.GetCellList<StackableRowCell>("cellList");
        if (list == null) return;

        list.ReloadCells(rowCells);
        list.Bounds.fixedY = 0;
        list.Bounds.CalcWorldBounds();
        SyncScrollbar();
    }

    private void OnScroll(float val)
    {
        var list = SingleComposer?.GetCellList<StackableRowCell>("cellList");
        if (list == null) return;
        list.Bounds.fixedY = -val;
        list.Bounds.CalcWorldBounds();
    }

    private void OnItemClicked(StackableItem item, bool rightClick)
    {
        if (rightClick)
        {
            // Quick toggle override on/off
            if (item.HasOverride) item.HasOverride = false;
            else { item.HasOverride = true; item.OverrideValue = item.ComputeStack(currentMultiplier); }
            if (item == selected) LoadSelectionIntoEditor();
            return;
        }

        if (selected != null) selected.Selected = false;
        selected = item;
        item.Selected = true;
        LoadSelectionIntoEditor();
    }

    private void LoadSelectionIntoEditor()
    {
        if (selected == null) return;
        SingleComposer.GetDynamicText("selLabel")
            .SetNewText($"{selected.DisplayName}  ({selected.Code})");
        int val = selected.HasOverride ? selected.OverrideValue : selected.ComputeStack(currentMultiplier);
        SingleComposer.GetNumberInput("ovInput").SetValue(val.ToString());
    }

    private bool OnApplyOverride()
    {
        if (selected == null) return true;
        string raw = SingleComposer.GetNumberInput("ovInput").GetText();
        if (int.TryParse(raw, out int v) && v > 0)
        {
            selected.HasOverride  = true;
            selected.OverrideValue = v;
        }
        return true;
    }

    private bool OnClearOverride()
    {
        if (selected == null) return true;
        selected.HasOverride = false;
        LoadSelectionIntoEditor();
        return true;
    }

    private bool OnSave()
    {
        var itemOvr  = new Dictionary<string, int>();
        var blockOvr = new Dictionary<string, int>();
        foreach (var it in allItems)
        {
            if (!it.HasOverride) continue;
            if (it.IsBlock) blockOvr[it.Code] = it.OverrideValue;
            else            itemOvr[it.Code]  = it.OverrideValue;
        }

        clientSys.SendSave(new StackablesSavePacket
        {
            GlobalMultiplier = currentMultiplier,
            ItemOverrides    = itemOvr,
            BlockOverrides   = blockOvr,
        });
        return true;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        if (searchDebounceId >= 0) capi.Event.UnregisterCallback(searchDebounceId);
        SingleComposer?.Dispose();
    }
}
