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
/// Top: multiplier input, a "flat size" toggle + value, a "prevent item loss"
///      safety toggle, a search box and a per-item override editor.
/// Middle: a multi-column, scrollable grid of every item/block showing
///         "original → new" with the new value in green (yellow for overrides).
/// Bottom: an unsaved-changes indicator + Save / Close.
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
    private bool   useFlat;
    private int    flatValue         = 100;
    private bool   preventShrinking   = true;
    private string searchText        = "";
    private long   searchDebounceId  = -1;
    private bool   dirty;
    private bool   confirmingClose;

    private ElementBounds clipBounds = null!;
    private ElementBounds listBounds = null!;

    // Layout (unscaled)
    private const int    Columns   = 3;
    private const double DialogW   = 820;
    private const double DialogH   = 648;
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
        useFlat           = data.UseFlatSize;
        flatValue         = Math.Max(1, data.FlatStackSize);
        preventShrinking  = data.PreventShrinking;

        foreach (var it in allItems) it.Dispose();
        allItems.Clear();
        selected = null;

        foreach (var (code, orig) in data.OriginalItemStacks.OrderBy(k => k.Key))
        {
            var item = capi.World.GetItem(new AssetLocation(code));
            if (item == null) continue;
            var si = new StackableItem(capi, new ItemStack(item), code, false, orig, ComputeBase);
            if (data.ItemOverrides.TryGetValue(code, out int ov)) { si.HasOverride = true; si.OverrideValue = ov; }
            allItems.Add(si);
        }

        foreach (var (code, orig) in data.OriginalBlockStacks.OrderBy(k => k.Key))
        {
            if (orig <= 0) continue;
            var block = capi.World.GetBlock(new AssetLocation(code));
            if (block == null) continue;
            var si = new StackableItem(capi, new ItemStack(block), code, true, orig, ComputeBase);
            if (data.BlockOverrides.TryGetValue(code, out int ov)) { si.HasOverride = true; si.OverrideValue = ov; }
            allItems.Add(si);
        }

        RebuildRows();
    }

    // Mode-aware base stack size (before any per-item override), shared by every
    // preview cell so they update live as the multiplier / flat / safety toggles change.
    private int ComputeBase(int original)
        => StackSizeCalc.Base(original, useFlat, flatValue, currentMultiplier, preventShrinking);

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

        // Row 1: the two possible stack-size values (only one is active at a time).
        var multLabel = ElementBounds.Fixed(Pad,       y, 110, RowH);
        var multInput = ElementBounds.Fixed(Pad + 120, y,  70, RowH);
        var flatLabel = ElementBounds.Fixed(Pad + 210, y,  90, RowH);
        var flatInput = ElementBounds.Fixed(Pad + 305, y,  70, RowH);
        y += RowH + 8;

        // Row 2: mode switch (multiplier vs flat) + the item-loss safety guard.
        var flatSwitch   = ElementBounds.Fixed(Pad,       y, 30, RowH);
        var modeLabel    = ElementBounds.Fixed(Pad + 38,  y, 300, RowH);
        var shrinkSwitch = ElementBounds.Fixed(Pad + 360, y, 30, RowH);
        var shrinkLabel  = ElementBounds.Fixed(Pad + 398, y, 260, RowH);
        y += RowH + 8;

        var searchLabel = ElementBounds.Fixed(Pad, y, 70, RowH);
        var searchInput = ElementBounds.Fixed(Pad + 75, y, ListW - 75, RowH);
        y += RowH + 8;

        // Override editor row
        var selLabel  = ElementBounds.Fixed(Pad, y, 300, RowH);
        var ovLabel   = ElementBounds.Fixed(Pad + 310, y, 60, RowH);
        var ovInput   = ElementBounds.Fixed(Pad + 372, y, 70, RowH);
        var setBtn    = ElementBounds.Fixed(Pad + 448, y, 70, RowH);
        var clearBtn  = ElementBounds.Fixed(Pad + 522, y, 120, RowH);
        y += RowH + 10;

        clipBounds = ElementBounds.Fixed(Pad, y, ListW, ListH);
        listBounds = ElementBounds.Fixed(0, 0, ListW, rowCells.Count * StackableRowCell.CellH)
            .WithParent(clipBounds);
        var scrollBounds = ElementStdBounds.VerticalScrollbar(clipBounds);
        y += ListH + 12;

        var statusLabel = ElementBounds.Fixed(Pad, y + 6, DialogW - Pad * 3 - 200, RowH);
        var saveBtn     = ElementBounds.Fixed(DialogW - Pad - 200, y, 90, 32);
        var closeBtn    = ElementBounds.Fixed(DialogW - Pad - 100, y, 90, 32);

        var font     = CairoFont.WhiteSmallText();
        var detail   = CairoFont.WhiteDetailText();

        // Recomposing (e.g. on a server-pushed refresh) replaces SingleComposer;
        // dispose the previous one first so its GUI resources aren't leaked.
        SingleComposer?.Dispose();

        SingleComposer = capi.Gui
            .CreateCompo("picostackables", dialogBounds)
            .AddShadedDialogBG(bgBounds, withTitleBar: true)
            .AddDialogTitleBar("PicoStackables – Stack Sizes", () => RequestClose())
            .BeginChildElements(bgBounds)

                // Row 1 – the two possible values
                .AddStaticText("Multiplier ×", font, multLabel)
                .AddHoverText("Multiplies every item/block's vanilla stack size. Ignored while Flat size mode is on.", font, 260, multLabel)
                .AddNumberInput(multInput, OnMultiplierChanged, detail, "multInput")
                .AddStaticText("Flat size =", font, flatLabel)
                .AddHoverText("When Flat size mode is on, every managed item/block is set to exactly this number.", font, 260, flatLabel)
                .AddNumberInput(flatInput, OnFlatChanged, detail, "flatInput")

                // Row 2 – mode switch + safety guard
                .AddSwitch(OnFlatToggled, flatSwitch, "flatSwitch")
                .AddDynamicText("", font, modeLabel, "modeLabel")
                .AddHoverText("Off = scale stacks by the multiplier.  On = set every stack to the flat size.", font, 260, modeLabel)
                .AddSwitch(OnShrinkToggled, shrinkSwitch, "shrinkSwitch")
                .AddStaticText("Prevent item loss", font, shrinkLabel)
                .AddHoverText("Never sets a stack size below its vanilla value, so a lower multiplier or a small flat size can't delete items from existing oversized stacks. Recommended.", font, 280, shrinkLabel)

                .AddStaticText("Search:", font, searchLabel)
                .AddTextInput(searchInput, OnSearchChanged, detail, "searchInput")

                .AddDynamicText("Left-click an item to edit it  ·  right-click to quick-toggle an override", font, selLabel, "selLabel")
                .AddStaticText("Set to:", font, ovLabel)
                .AddNumberInput(ovInput, _ => { }, detail, "ovInput")
                .AddSmallButton("Apply", OnApplyOverride, setBtn)
                .AddSmallButton("Clear override", OnClearOverride, clearBtn)

                .BeginClip(clipBounds)
                    .AddCellList(listBounds, RequireCell, rowCells, "cellList")
                .EndClip()
                .AddVerticalScrollbar(OnScroll, scrollBounds, "scrollbar")

                .AddDynamicText("", font, statusLabel, "statusLabel")
                .AddSmallButton("Save",  OnSave,        saveBtn)
                .AddSmallButton("Close", RequestClose,  closeBtn)

            .EndChildElements()
            .Compose();

        SingleComposer.GetNumberInput("multInput").SetValue(currentMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("flatInput").SetValue(flatValue.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetSwitch("flatSwitch").SetValue(useFlat);
        SingleComposer.GetSwitch("shrinkSwitch").SetValue(preventShrinking);
        SingleComposer.GetTextInput("searchInput").SetPlaceHolderText("item code or name…");
        if (!string.IsNullOrEmpty(searchText))
            SingleComposer.GetTextInput("searchInput").SetValue(searchText);

        // We just loaded values straight from the server, so nothing is unsaved yet.
        // (SetValue above may fire change handlers; clear the flag afterwards.)
        dirty = false;
        confirmingClose = false;
        UpdateStatusLabel();
        UpdateModeLabel();

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
        UpdateModeLabel();
        MarkDirty();
        // Visible cells re-bake automatically via their ComputeBase closure.
    }

    private void OnFlatToggled(bool on)
    {
        useFlat = on;
        UpdateModeLabel();
        MarkDirty();
    }

    private void OnFlatChanged(string val)
    {
        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0)
            flatValue = v;
        UpdateModeLabel();
        MarkDirty();
    }

    private void OnShrinkToggled(bool on)
    {
        preventShrinking = on;
        MarkDirty();
    }

    // Shows which mode is currently in effect (and its value), so it's clear that
    // the other input is being ignored — friendlier than silently having two live fields.
    private void UpdateModeLabel()
    {
        SingleComposer?.GetDynamicText("modeLabel")?.SetNewText(
            useFlat
                ? $"» Flat size active — every stack = {flatValue}"
                : $"» Multiplier active — ×{currentMultiplier.ToString("0.##", CultureInfo.InvariantCulture)}");
    }

    // Guards against losing edits: the first Close with unsaved changes arms a
    // confirmation instead of closing; a second Close (or Save first) goes through.
    private bool RequestClose()
    {
        if (dirty && !confirmingClose)
        {
            confirmingClose = true;
            SingleComposer?.GetDynamicText("statusLabel")?.SetNewText(
                "● Unsaved changes — click Close again to discard, or Save to keep them.");
            return false;
        }
        TryClose();
        return true;
    }

    private void MarkDirty()
    {
        dirty = true;
        confirmingClose = false;   // any fresh edit cancels a pending close-confirm
        UpdateStatusLabel();
    }

    private void UpdateStatusLabel()
    {
        SingleComposer?.GetDynamicText("statusLabel")?.SetNewText(
            dirty
                ? "● Unsaved changes – click Save to apply them in-world"
                : "✓ Settings are saved and applied");
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
            else { item.HasOverride = true; item.OverrideValue = item.ComputeStack(); }
            MarkDirty();
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
        int val = selected.ComputeStack();
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
            MarkDirty();
        }
        return true;
    }

    private bool OnClearOverride()
    {
        if (selected == null) return true;
        selected.HasOverride = false;
        MarkDirty();
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
            UseFlatSize      = useFlat,
            FlatStackSize    = flatValue,
            PreventShrinking = preventShrinking,
        });

        dirty = false;
        confirmingClose = false;
        UpdateStatusLabel();
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
