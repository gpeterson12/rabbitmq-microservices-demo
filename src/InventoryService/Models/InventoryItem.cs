namespace InventoryService.Models;

public sealed class InventoryItem(string sku, int initialQuantity)
{
    private readonly object _lock = new();

    public string Sku { get; } = sku;

    public int Quantity { get; private set; } = initialQuantity;

    public (bool Reserved, int RemainingStock) TryReserve(int quantity)
    {
        lock (_lock)
        {
            if (quantity <= 0 || Quantity < quantity)
            {
                return (false, Quantity);
            }

            Quantity -= quantity;
            return (true, Quantity);
        }
    }
}
