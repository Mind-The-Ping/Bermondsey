using Bermondsey.MessageTemplate;
using Bermondsey.Models;
using CSharpFunctionalExtensions;

namespace Bermondsey.Clients.NotificationClient;
public interface INotifcationClient
{
    public Task<Result> SendAsync(
        Guid userId,
        PhoneOS phoneOS,
        Guid notificationId,
        int unreadCount,
        string notificationType,
        FormattedMessage message, 
        CancellationToken cancellationToken = default);
}
