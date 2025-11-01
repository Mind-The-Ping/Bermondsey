using Bermondsey.Messages;
using Bermondsey.Models;

namespace Bermondsey.NotificationOrchestrator;
public interface INotificationOrchestrator
{
    Task SendDisruptionNotificationAsync(Disruption disruption, IEnumerable<User> users);

    Task SendResolutionNotificationAsync(IEnumerable<User> users);
}
