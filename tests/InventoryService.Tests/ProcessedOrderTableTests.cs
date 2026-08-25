using InventoryService.Services;

namespace InventoryService.Tests;

public class ProcessedOrderTableTests
{
    [Fact]
    public void TryMark_returns_true_once_per_order_id()
    {
        var table = new ProcessedOrderTable();
        var orderId = Guid.NewGuid();

        Assert.True(table.TryMark(orderId));
        Assert.False(table.TryMark(orderId));
    }

    [Fact]
    public void IsProcessed_reflects_marked_order_ids()
    {
        var table = new ProcessedOrderTable();
        var orderId = Guid.NewGuid();

        Assert.False(table.IsProcessed(orderId));

        table.TryMark(orderId);

        Assert.True(table.IsProcessed(orderId));
        Assert.False(table.IsProcessed(Guid.NewGuid()));
    }

    [Fact]
    public void Evicted_entries_are_no_longer_considered_processed()
    {
        var table = new ProcessedOrderTable(capacity: 1);
        var first = Guid.NewGuid();

        table.TryMark(first);
        table.TryMark(Guid.NewGuid());

        Assert.False(table.IsProcessed(first));
    }

    [Fact]
    public void Distinct_order_ids_are_all_accepted()
    {
        var table = new ProcessedOrderTable();

        Assert.True(table.TryMark(Guid.NewGuid()));
        Assert.True(table.TryMark(Guid.NewGuid()));
        Assert.True(table.TryMark(Guid.NewGuid()));
    }

    [Fact]
    public void Oldest_entry_is_evicted_when_capacity_is_reached()
    {
        var table = new ProcessedOrderTable(capacity: 2);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        Assert.True(table.TryMark(first));
        Assert.True(table.TryMark(second));
        Assert.True(table.TryMark(third));

        Assert.False(table.TryMark(second));
        Assert.False(table.TryMark(third));
        Assert.True(table.TryMark(first), "oldest entry should have been evicted");
    }

    [Fact]
    public void Zero_capacity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessedOrderTable(capacity: 0));
    }

    [Fact]
    public void Concurrent_marks_of_distinct_ids_succeed_exactly_once_each()
    {
        var table = new ProcessedOrderTable();
        const int count = 1_000;
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        var successes = 0;

        Parallel.For(0, count, i =>
        {
            if (table.TryMark(ids[i]))
            {
                Interlocked.Increment(ref successes);
            }
        });

        Assert.Equal(count, successes);
    }

    [Fact]
    public void Concurrent_marks_of_the_same_id_succeed_exactly_once()
    {
        var table = new ProcessedOrderTable();
        const int racers = 32;
        var orderId = Guid.NewGuid();
        var successes = 0;

        Parallel.For(0, racers, _ =>
        {
            if (table.TryMark(orderId))
            {
                Interlocked.Increment(ref successes);
            }
        });

        Assert.Equal(1, successes);
    }
}
