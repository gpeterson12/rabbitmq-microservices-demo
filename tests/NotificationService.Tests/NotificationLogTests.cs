using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Tests;

public class NotificationLogTests
{
    private static NotificationRecord Record(Guid orderId, string status = NotificationRecord.StatusReserved) =>
        new()
        {
            NotifiedAt = DateTimeOffset.UtcNow,
            Status = status,
            OrderId = orderId,
            Sku = "SKU-WIDGET",
            Quantity = 1,
        };

    [Fact]
    public void Empty_log_returns_empty_snapshot()
    {
        var log = new NotificationLog();

        Assert.Empty(log.LatestFirst());
    }

    [Fact]
    public void Records_are_returned_newest_first()
    {
        var log = new NotificationLog();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            log.Add(Record(id));
        }

        Assert.Equal(ids.AsEnumerable().Reverse(), log.LatestFirst().Select(record => record.OrderId));
    }

    [Fact]
    public void Oldest_records_are_evicted_once_capacity_is_reached()
    {
        var log = new NotificationLog(capacity: 3);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            log.Add(Record(id));
        }

        var snapshot = log.LatestFirst();

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(ids.AsEnumerable().TakeLast(3).Reverse(), snapshot.Select(record => record.OrderId));
    }

    [Fact]
    public void Snapshot_is_isolated_from_subsequent_writes()
    {
        var log = new NotificationLog();
        log.Add(Record(Guid.NewGuid()));
        var snapshot = log.LatestFirst();

        log.Add(Record(Guid.NewGuid()));

        Assert.Single(snapshot);
    }

    [Fact]
    public void Concurrent_adds_do_not_lose_records_within_capacity()
    {
        var log = new NotificationLog();
        const int count = 1_000;
        var orderIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

        Parallel.For(0, count, i => log.Add(Record(orderIds[i])));

        var snapshot = log.LatestFirst();
        Assert.Equal(count, snapshot.Count);
        Assert.Equal(count, snapshot.Select(record => record.OrderId).Distinct().Count());
    }

    [Fact]
    public void Zero_capacity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationLog(capacity: 0));
    }
}
