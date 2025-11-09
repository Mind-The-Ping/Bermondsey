using Bermondsey.Models;

namespace Bermondsey.NotificationOrchestrator;
public interface INotificationOrchestrator
{
    Task SendDisruptionNotificationAsync(User user);

    Task SendResolutionNotificationAsync(User user);
}
