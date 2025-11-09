using Bermondsey.Clients.NotificationClient;
using Bermondsey.Clients.SmsClient;
using Bermondsey.MessageTemplate;
using Bermondsey.Models;
using Microsoft.Extensions.Logging;

namespace Bermondsey.NotificationOrchestrator;
public class NotificationOrchestrator : INotificationOrchestrator
{
    private readonly ISmsClient _smsClient;
    private readonly MessageFormatter _messageFormatter;
    private readonly INotifcationClient _notificationClient;
    private readonly INotificationSentByRepository _repository;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(
        ISmsClient smsClient,
        MessageFormatter messageFormatter,
        INotifcationClient notificationClient,
        INotificationSentByRepository repository,
        ILogger<NotificationOrchestrator> logger)
    {
        _smsClient = smsClient ?? throw new ArgumentNullException(nameof(smsClient));
        _messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
        _notificationClient = notificationClient ?? throw new ArgumentNullException(nameof(notificationClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendDisruptionNotificationAsync(User user)
        => SendNotificationAsync(
            user,
            u => _messageFormatter.FormatDisruption(u.Line.Name, u.StartStation.Name, u.EndStation.Name, u.Severity, u.AffectedStations),
            "disruption");

    public Task SendResolutionNotificationAsync(User user)
        => SendNotificationAsync(
            user,
            u => _messageFormatter.FormatResolved(u.Line.Name, u.StartStation.Name, u.EndStation.Name),
            "resolved");

    private async Task SendNotificationAsync(User user, Func<User, FormattedMessage> formatMessage, string notificationType)
    {
        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        if (new TimeOnly(user.EndTime.Hour, user.EndTime.Minute) <= now)
            return;

        var message = formatMessage(user);

        var notificationResult = await _notificationClient.SendAsync(user.Id, user.PhoneOS, user.NotificationId, message);
        NotificationSentBy sentBy;

        if (notificationResult.IsSuccess) {
            sentBy = NotificationSentBy.Push;
        }
        else
        {
            _logger.LogWarning("Push {NotificationType} notification failed for {UserId}: {Error}", notificationType, user.Id, notificationResult.Error);

            var smsResult = await _smsClient.SendAsync(user.PhoneNumber, message);
            sentBy = smsResult.IsSuccess ? NotificationSentBy.Sms : NotificationSentBy.Failed;

            if (smsResult.IsFailure)
            {
                _logger.LogWarning("SMS {NotificationType} notification failed for {UserId}: {Error}", notificationType, user.Id, smsResult.Error);
            }
        }

        await _repository.CreateAsync(new NotificationStatus(user.NotificationId, sentBy));
    }
}
