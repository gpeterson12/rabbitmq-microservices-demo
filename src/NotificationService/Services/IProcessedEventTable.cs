namespace NotificationService.Services;

public interface IProcessedEventTable
{
    /// <summary>
    /// Returns true when <paramref name="eventId"/> has already been marked
    /// as processed (duplicate delivery).
    /// </summary>
    bool IsProcessed(Guid eventId);

    /// <summary>
    /// Atomically records <paramref name="eventId"/> as processed.
    /// Returns false when the event was already seen (duplicate delivery).
    /// Callers should invoke this only AFTER the processing side effects
    /// have succeeded; see RabbitMqConsumerBase subclasses for the tradeoff.
    /// </summary>
    bool TryMark(Guid eventId);
}
