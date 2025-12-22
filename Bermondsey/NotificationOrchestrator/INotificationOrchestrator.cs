using Bermondsey.Models;

namespace Bermondsey.NotificationOrchestrator;
public interface INotificationOrchestrator
{
    Task SendDisruptionNotificationAsync(Journey journey);

    Task SendResolutionNotificationAsync(Journey journey);
}
