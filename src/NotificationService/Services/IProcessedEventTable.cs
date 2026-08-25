namespace NotificationService.Services;

public interface IProcessedEventTable
{
    /// <summary>
    /// Atomically records <paramref name="eventId"/> as processed.
    /// Returns false when the event was already seen (duplicate delivery).
    /// </summary>
    bool TryMark(Guid eventId);
}
