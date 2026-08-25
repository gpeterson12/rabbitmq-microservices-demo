using NotificationService.Models;

namespace NotificationService.Services;

public interface INotificationLog
{
    void Add(NotificationRecord record);

    IReadOnlyList<NotificationRecord> LatestFirst();
}
