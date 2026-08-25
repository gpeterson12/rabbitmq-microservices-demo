namespace InventoryService.Services;

public interface IProcessedOrderTable
{
    /// <summary>
    /// Atomically records <paramref name="orderId"/> as processed.
    /// Returns false when the order was already seen (duplicate delivery).
    /// </summary>
    bool TryMark(Guid orderId);
}
