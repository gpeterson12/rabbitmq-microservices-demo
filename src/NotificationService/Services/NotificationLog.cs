using System.Collections.Concurrent;
using NotificationService.Models;

namespace NotificationService.Services;

public sealed class NotificationLog : INotificationLog
{
    private readonly ConcurrentBag<NotificationRecord> _records = [];

    public void Add(NotificationRecord record) => _records.Add(record);

    public IReadOnlyList<NotificationRecord> LatestFirst() =>
        [.. _records.OrderByDescending(record => record.NotifiedAt)];
}
