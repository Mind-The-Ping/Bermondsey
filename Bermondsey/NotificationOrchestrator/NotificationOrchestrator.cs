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

    public Task SendDisruptionNotificationAsync(Journey journey)
        => SendNotificationAsync(
            journey,
            u => _messageFormatter.FormatDisruption(u.Line.Name, u.StartStation.Name, u.EndStation.Name, u.Severity, u.AffectedStations),
            "disruption");

    public Task SendResolutionNotificationAsync(Journey journey)
        => SendNotificationAsync(
            journey,
            u => _messageFormatter.FormatResolved(u.Line.Name, u.StartStation.Name, u.EndStation.Name),
            "resolved");

    private async Task SendNotificationAsync(Journey journey, Func<Journey, FormattedMessage> formatMessage, string notificationType)
    {
        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        if (new TimeOnly(journey.EndTime.Hour, journey.EndTime.Minute) <= now)
            return;

        var message = formatMessage(journey);

        var notificationResult = await _notificationClient.SendAsync(
            journey.UserId, 
            journey.PhoneOS, 
            journey.NotificationId, 
            journey.UnReadMessageCount,
            notificationType,
            message);

        NotificationSentBy sentBy;

        if (notificationResult.IsSuccess) {
            sentBy = NotificationSentBy.Push;
        }
        else
        {
            _logger.LogWarning("Push {NotificationType} notification failed for {UserId}: {Error}", notificationType, journey.UserId, notificationResult.Error);

            var smsResult = await _smsClient.SendAsync(journey.PhoneNumber, message);
            sentBy = smsResult.IsSuccess ? NotificationSentBy.Sms : NotificationSentBy.Failed;

            if (smsResult.IsFailure)
            {
                _logger.LogWarning("SMS {NotificationType} notification failed for {UserId}: {Error}", notificationType, journey.UserId, smsResult.Error);
            }
        }

        await _repository.CreateAsync(new NotificationStatus(journey.NotificationId, sentBy));
    }
}
