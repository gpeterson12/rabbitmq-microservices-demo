using System.Collections.Concurrent;
using InventoryService.Models;

namespace InventoryService.Services;

public sealed class StockTable : IStockTable
{
    private readonly ConcurrentDictionary<string, InventoryItem> _items = new(
        new Dictionary<string, InventoryItem>
        {
            ["SKU-WIDGET"] = new("SKU-WIDGET", 50),
            ["SKU-GADGET"] = new("SKU-GADGET", 10),
            ["SKU-GIZMO"] = new("SKU-GIZMO", 0),
        });

    public ReserveResult Reserve(string sku, int quantity)
    {
        if (!_items.TryGetValue(sku, out var item))
        {
            return new ReserveResult(ReserveStatus.UnknownSku);
        }

        var (reserved, remainingStock) = item.TryReserve(quantity);

        return reserved
            ? new ReserveResult(ReserveStatus.Reserved, remainingStock)
            : new ReserveResult(ReserveStatus.InsufficientStock);
    }

    public IReadOnlyList<InventoryItem> Snapshot() =>
        [.. _items.Values.OrderBy(item => item.Sku)];
}
