using NotificationService.Services;

namespace NotificationService.Tests;

public class ProcessedEventTableTests
{
    [Fact]
    public void TryMark_returns_true_once_per_event_id()
    {
        var table = new ProcessedEventTable();
        var eventId = Guid.NewGuid();

        Assert.True(table.TryMark(eventId));
        Assert.False(table.TryMark(eventId));
    }

    [Fact]
    public void IsProcessed_reflects_marked_event_ids()
    {
        var table = new ProcessedEventTable();
        var eventId = Guid.NewGuid();

        Assert.False(table.IsProcessed(eventId));

        table.TryMark(eventId);

        Assert.True(table.IsProcessed(eventId));
        Assert.False(table.IsProcessed(Guid.NewGuid()));
    }

    [Fact]
    public void Oldest_entry_is_evicted_when_capacity_is_reached()
    {
        var table = new ProcessedEventTable(capacity: 2);
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
    public void Concurrent_marks_of_the_same_id_succeed_exactly_once()
    {
        var table = new ProcessedEventTable();
        const int racers = 32;
        var eventId = Guid.NewGuid();
        var successes = 0;

        Parallel.For(0, racers, _ =>
        {
            if (table.TryMark(eventId))
            {
                Interlocked.Increment(ref successes);
            }
        });

        Assert.Equal(1, successes);
    }
}
