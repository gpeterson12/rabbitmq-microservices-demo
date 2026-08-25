using InventoryService.Models;

namespace InventoryService.Services;

public enum ReserveStatus
{
    Reserved,
    UnknownSku,
    InsufficientStock,
}

public sealed record ReserveResult(ReserveStatus Status, int RemainingStock = 0);

public interface IStockTable
{
    ReserveResult Reserve(string sku, int quantity);

    IReadOnlyList<InventoryItem> Snapshot();
}
