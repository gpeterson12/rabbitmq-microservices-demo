using InventoryService.Models;
using InventoryService.Services;

namespace InventoryService.Tests;

public class StockTableTests
{
    [Fact]
    public void Seeded_stock_contains_expected_skus_and_quantities()
    {
        var table = new StockTable();

        var snapshot = table.Snapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Contains(snapshot, item => item.Sku == "SKU-WIDGET" && item.Quantity == 50);
        Assert.Contains(snapshot, item => item.Sku == "SKU-GADGET" && item.Quantity == 10);
        Assert.Contains(snapshot, item => item.Sku == "SKU-GIZMO" && item.Quantity == 0);
    }

    [Fact]
    public void Reserve_decrements_stock_and_returns_remaining()
    {
        var table = new StockTable();

        var result = table.Reserve("SKU-WIDGET", 3);

        Assert.Equal(ReserveStatus.Reserved, result.Status);
        Assert.Equal(47, result.RemainingStock);
        Assert.Equal(47, table.Snapshot().Single(item => item.Sku == "SKU-WIDGET").Quantity);
    }

    [Fact]
    public void Reserving_entire_available_quantity_leaves_zero_remaining()
    {
        var table = new StockTable();

        var result = table.Reserve("SKU-GADGET", 10);

        Assert.Equal(ReserveStatus.Reserved, result.Status);
        Assert.Equal(0, result.RemainingStock);
    }

    [Fact]
    public void Reserving_more_than_available_is_insufficient_and_leaves_stock_untouched()
    {
        var table = new StockTable();

        var result = table.Reserve("SKU-GADGET", 11);

        Assert.Equal(ReserveStatus.InsufficientStock, result.Status);
        Assert.Equal(10, table.Snapshot().Single(item => item.Sku == "SKU-GADGET").Quantity);
    }

    [Fact]
    public void Zero_stock_sku_cannot_reserve_anything()
    {
        var table = new StockTable();

        var result = table.Reserve("SKU-GIZMO", 1);

        Assert.Equal(ReserveStatus.InsufficientStock, result.Status);
        Assert.Equal(0, table.Snapshot().Single(item => item.Sku == "SKU-GIZMO").Quantity);
    }

    [Theory]
    [InlineData("SKU-UNKNOWN")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unrecognized_sku_is_unknown(string sku)
    {
        var table = new StockTable();

        var result = table.Reserve(sku, 1);

        Assert.Equal(ReserveStatus.UnknownSku, result.Status);
    }

    [Fact]
    public void Concurrent_reservations_do_not_oversell()
    {
        var table = new StockTable();
        const int attempts = 25;
        var reservedCount = 0;

        Parallel.For(0, attempts, _ =>
        {
            if (table.Reserve("SKU-GADGET", 1).Status == ReserveStatus.Reserved)
            {
                Interlocked.Increment(ref reservedCount);
            }
        });

        Assert.Equal(10, reservedCount);
        Assert.Equal(0, table.Snapshot().Single(item => item.Sku == "SKU-GADGET").Quantity);
    }
}
