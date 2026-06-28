using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace PicoStackables.Gui;

/// <summary>
/// One row in the cell list. Renders up to <see cref="Columns"/> StackableItems
/// side-by-side to build a multi-column grid while still using the (virtualized,
/// scrollable, clipped) GuiElementCellList under the hood.
/// </summary>
public class StackableRowCell : IGuiElementCell
{
    public const int CellH = 50;

    private readonly List<StackableItem> items;
    private readonly int columns;
    private readonly Action<StackableItem, bool> onItemClick;

    public StackableRowCell(
        List<StackableItem> items,
        int columns,
        Action<StackableItem, bool> onItemClick)
    {
        this.items       = items;
        this.columns     = columns;
        this.onItemClick = onItemClick;
    }

    public ElementBounds Bounds { get; set; } = null!;
    public ElementBounds InsideClipBounds { get; set; } = null!;
    public string MouseOverCursor => null!;

    public void Compose() { }
    public void UpdateCellEdit(ICoreClientAPI api, bool editing, int cellIndex) { }

    public void UpdateCellHeight()
    {
        Bounds.fixedHeight = CellH;
        Bounds.CalcWorldBounds();
    }

    public void OnRenderInteractiveElements(ICoreClientAPI api, float dt)
    {
        double colWidth = Bounds.InnerWidth / columns;
        for (int i = 0; i < items.Count; i++)
        {
            double x = Bounds.renderX + i * colWidth;
            items[i].Render(api, x, Bounds.renderY, colWidth, Bounds.InnerHeight);
        }
    }

    public void OnMouseDownOnElement(MouseEvent e, int elementIndex) { }

    public void OnMouseUpOnElement(MouseEvent e, int elementIndex)
    {
        double colWidth = Bounds.InnerWidth / columns;
        double relX     = e.X - Bounds.renderX;
        int    col      = (int)(relX / colWidth);
        if (col >= 0 && col < items.Count)
            onItemClick(items[col], e.Button == EnumMouseButton.Right);
    }

    public void OnMouseMoveOnElement(MouseEvent e, int elementIndex) { }

    public void Dispose() { }
}
