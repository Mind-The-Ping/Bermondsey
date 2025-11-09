namespace Bermondsey;
public interface INotificationSentByRepository
{
    Task CreateAsync(NotificationStatus status);
}
