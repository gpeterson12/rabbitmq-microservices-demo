namespace InventoryService.Services;

/// <summary>
/// Bounded, thread-safe set of recently processed order ids used to make
/// redeliveries idempotent. Evicts the oldest entry once the capacity is
/// reached; consistent with the project's in-memory-only state design.
/// </summary>
public sealed class ProcessedOrderTable : IProcessedOrderTable
{
    public const int DefaultCapacity = 100_000;

    private readonly object _lock = new();
    private readonly Queue<Guid> _insertionOrder;
    private readonly HashSet<Guid> _keys;
    private readonly int _capacity;

    public ProcessedOrderTable(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _insertionOrder = new Queue<Guid>(capacity);
        _keys = new HashSet<Guid>(capacity);
    }

    public bool IsProcessed(Guid orderId)
    {
        lock (_lock)
        {
            return _keys.Contains(orderId);
        }
    }

    public bool TryMark(Guid orderId)
    {
        lock (_lock)
        {
            if (!_keys.Add(orderId))
            {
                return false;
            }

            while (_keys.Count > _capacity)
            {
                _keys.Remove(_insertionOrder.Dequeue());
            }

            _insertionOrder.Enqueue(orderId);
            return true;
        }
    }
}
