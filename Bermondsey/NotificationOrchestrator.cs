using Azure.Messaging.ServiceBus;
using Bermondsey.Clients.NotificationClient;
using Bermondsey.Clients.SmsClient;
using Bermondsey.Messages;
using Bermondsey.MessageTemplate;
using Bermondsey.Models;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification = Bermondsey.Messages.Notification;

namespace Bermondsey;
public class NotificationOrchestrator
{
    private const int _parallelThreads = 100;

    private readonly ISmsClient _smsClient;
    private readonly MessageFormatter _messageFormatter;
    private readonly ServiceBusSender _notificationSender;
    private readonly INotifcationClient _notificationClient;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(
        ISmsClient smsClient,
        MessageFormatter messageFormatter,
        ServiceBusClient busClient,
        INotifcationClient notificationClient, 
        ILogger<NotificationOrchestrator> logger,
        IOptions<ServiceBusOptions> serviceBusOptions)
    {
        _smsClient = smsClient ?? throw new ArgumentNullException(nameof(smsClient));
        _messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
        _notificationClient= notificationClient ?? throw new ArgumentNullException(nameof(notificationClient));
        _logger= logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationSender = busClient.CreateSender(serviceBusOptions.Value.Queues.Notifications);
    }

    public async Task SendDisruptionNotificationAsync(
        Disruption disruption,
        IEnumerable<User> users)
    {
        await Parallel.ForEachAsync(users, new ParallelOptions
        {
            MaxDegreeOfParallelism = _parallelThreads
        }, async (user, token) =>
        {
            var now = TimeOnly.FromDateTime(DateTime.UtcNow);
            now = new TimeOnly(now.Hour, now.Minute);

            var end = new TimeOnly(user.EndTime.Hour, user.EndTime.Minute);

            if (end <= now) {
                return;
            }

            var message = _messageFormatter.FormatDisruption(
                user.Line.Name,
                user.StartStation.Name,
                user.EndStation.Name,
                user.Severity,
                user.AffectedStations);

            var notificationId = Guid.NewGuid();
            var notificationResult = await _notificationClient
                .SendAsync(
                user.Id, 
                user.PhoneOS, 
                notificationId, 
                message, 
                token);

            if(notificationResult.IsSuccess)
            {
                await PublishNotificationMessageAsync(
                  notificationId,
                  user.Id,
                  user.Line.Id,
                  user.DisruptionId,
                  user.StartStation.Id,
                  user.EndStation.Id,
                  disruption.SeverityId,
                  disruption.Id,
                  NotificationSentBy.Push,
                  [.. user.AffectedStations.Select(x => x.Id)]);
            }
            else if(notificationResult.IsFailure)
            {
                _logger.LogWarning("Could not send notification out as " +
                    "push notification for {userId} for error : {error}", user.Id, notificationResult.Error);

                var smsResult = await _smsClient.SendAsync(
                    user.PhoneNumber, 
                    message, 
                    cancellationToken: token);

                if (smsResult.IsSuccess)
                {
                    await PublishNotificationMessageAsync(
                      notificationId,
                      user.Id,
                      user.Line.Id,
                      user.DisruptionId,
                      user.StartStation.Id,
                      user.EndStation.Id,
                      disruption.SeverityId,
                      disruption.Id,
                      NotificationSentBy.Sms,
                      [.. user.AffectedStations.Select(x => x.Id)]);
                }
                else if (smsResult.IsFailure) 
                {
                    _logger.LogWarning("Could not send notification out as " +
                    "sms text for {userId} for error : {error}", user.Id, notificationResult.Error);

                    await PublishNotificationMessageAsync(
                       notificationId,
                       user.Id,
                       user.Line.Id,
                       user.DisruptionId,
                       user.StartStation.Id,
                       user.EndStation.Id,
                       disruption.SeverityId,
                       disruption.Id,
                       NotificationSentBy.Failed,
                       [.. user.AffectedStations.Select(x => x.Id)]);
                }
            }
        });
    }

    public async Task SendResolvedNotificationAsync(IEnumerable<User> users)
    {
        await Parallel.ForEachAsync(users, new ParallelOptions
        {
            MaxDegreeOfParallelism = _parallelThreads
        }, async (user, token) =>
        {
            var message = _messageFormatter.FormatResolved(
               user.Line.Name,
               user.StartStation.Name,
               user.EndStation.Name);

            var notificationResult = await _notificationClient
                .SendAsync(
                user.Id,
                user.PhoneOS,
                Guid.NewGuid(),
                message,
                token);

            if(notificationResult.IsFailure)
            {
                _logger.LogWarning("Could not send notification out as " +
                    "push notification for {userId} for error : {error}", user.Id, notificationResult.Error);

                var smsResult = await _smsClient
                .SendAsync(
                    user.PhoneNumber,
                    message,
                    cancellationToken: token);

                if (smsResult.IsFailure) {
                    _logger.LogWarning("Could not send notification out as " +
                   "sms text for {userId} for error : {error}", user.Id, notificationResult.Error);
                }
            }
        });
    }

    private async Task<Result> PublishNotificationMessageAsync(
       Guid id,
       Guid userId,
       Guid lineId,
       Guid disruptionId,
       Guid startStationId,
       Guid endStationId,
       Guid severityId,
       Guid descriptionId,
       NotificationSentBy notificationSentBy,
       IList<Guid> affectedStationsId)
    {
        var dto = new Notification(
            id,
            userId,
            lineId,
            disruptionId,
            startStationId,
            endStationId,
            severityId,
            descriptionId,
            notificationSentBy,
            DateTime.UtcNow,
            affectedStationsId);

        var message = BinaryData.FromObjectAsJson(dto);

        try {
            await _notificationSender.SendMessageAsync(new ServiceBusMessage(message));
        }
        catch (Exception ex) {
            return Result.Failure($"Could not send notification {id} for {userId} : {ex.Message}");
        }

        return Result.Success();
    }
}
