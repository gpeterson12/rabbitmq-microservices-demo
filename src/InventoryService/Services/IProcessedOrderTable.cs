namespace InventoryService.Services;

public interface IProcessedOrderTable
{
    /// <summary>
    /// Returns true when <paramref name="orderId"/> has already been marked
    /// as processed (duplicate delivery).
    /// </summary>
    bool IsProcessed(Guid orderId);

    /// <summary>
    /// Atomically records <paramref name="orderId"/> as processed.
    /// Returns false when the order was already seen (duplicate delivery).
    /// Callers should invoke this only AFTER the processing side effects
    /// have succeeded; see OrderCreatedConsumer for the tradeoff.
    /// </summary>
    bool TryMark(Guid orderId);
}
