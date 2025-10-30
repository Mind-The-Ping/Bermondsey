using Bermondsey.Models;
using CSharpFunctionalExtensions;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Logging;

namespace Bermondsey.Clients.NotificationClient;
public class NotificationClient: INotifcationClient
{
    private readonly ILogger<NotificationClient> _logger;
    private readonly NotificationHubClient _notificationHubClient; 

    public NotificationClient(
        ILogger<NotificationClient> logger,
        NotificationHubClient notificationHubClient)
    {
        _logger = logger ?? 
            throw new ArgumentNullException(nameof(logger));

       _notificationHubClient = notificationHubClient ?? 
            throw new ArgumentNullException(nameof(notificationHubClient));
    }

    public Task<Result> SendAsync(Guid userId, PhoneOS phoneOS, string message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
