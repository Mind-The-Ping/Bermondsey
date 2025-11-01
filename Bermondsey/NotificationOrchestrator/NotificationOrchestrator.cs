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

namespace Bermondsey.NotificationOrchestrator;
public class NotificationOrchestrator : INotificationOrchestrator
{
    private const int BatchSize = 500;
    private const int MaxParallelThreads = 100;

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
        _notificationClient = notificationClient ?? throw new ArgumentNullException(nameof(notificationClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationSender = busClient.CreateSender(serviceBusOptions.Value.Queues.Notifications);
    }

    public async Task SendDisruptionNotificationAsync(Disruption disruption, IEnumerable<User> users)
    {
        var userList = users.ToList();
        int totalBatches = (int)Math.Ceiling(userList.Count / (double)BatchSize);

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            var batchUsers = userList.Skip(batchIndex * BatchSize).Take(BatchSize).ToList();
            int parallelThreads = Math.Min(MaxParallelThreads, batchUsers.Count);

            _logger.LogInformation("Processing batch {BatchIndex}/{TotalBatches} with {UserCount} users using {Threads} threads for " +
                "disruption notification",
                batchIndex + 1, totalBatches, batchUsers.Count, parallelThreads);

            var semaphore = new SemaphoreSlim(parallelThreads);

            var tasks = batchUsers.Select(async user =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var now = TimeOnly.FromDateTime(DateTime.UtcNow);
                    if (new TimeOnly(user.EndTime.Hour, user.EndTime.Minute) <= now) {
                        return;
                    }

                    var message = _messageFormatter.FormatDisruption(
                        user.Line.Name,
                        user.StartStation.Name,
                        user.EndStation.Name,
                        user.Severity,
                        user.AffectedStations);

                    var notificationId = Guid.NewGuid();

                    user.NotificationId = notificationId;

                    var notificationResult = await _notificationClient
                        .SendAsync(user.Id, user.PhoneOS, notificationId, message);

                    NotificationSentBy sentBy;

                    if (notificationResult.IsSuccess) {
                        sentBy = NotificationSentBy.Push;
                    }
                    else
                    {
                        _logger.LogWarning("Push disruption notification failed for {UserId}: {Error}", user.Id, notificationResult.Error);

                        var smsResult = await _smsClient.SendAsync(user.PhoneNumber, message);
                        sentBy = smsResult.IsSuccess ? NotificationSentBy.Sms : NotificationSentBy.Failed;

                        if (smsResult.IsFailure) {
                            _logger.LogWarning("SMS notification failed for {UserId}: {Error}", user.Id, smsResult.Error);
                        }
                    }

                    await PublishNotificationMessageAsync(
                        notificationId,
                        user.Id,
                        user.Line.Id,
                        user.DisruptionId,
                        user.StartStation.Id,
                        user.EndStation.Id,
                        disruption.SeverityId,
                        disruption.Id,
                        sentBy,
                        [.. user.AffectedStations.Select(x => x.Id)]);
                }
                finally {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
    }

    public async Task SendResolutionNotificationAsync(IEnumerable<User> users)
    {
        var userList = users.ToList();
        int totalBatches = (int)Math.Ceiling(userList.Count / (double)BatchSize);

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            var batchUsers = userList.Skip(batchIndex * BatchSize).Take(BatchSize).ToList();
            int parallelThreads = Math.Min(MaxParallelThreads, batchUsers.Count);

            _logger.LogInformation("Processing batch {BatchIndex}/{TotalBatches} with {UserCount} users using {Threads} threads " +
                "for resolved notification",
                batchIndex + 1, totalBatches, batchUsers.Count, parallelThreads);

            var semaphore = new SemaphoreSlim(parallelThreads);

            var tasks = batchUsers.Select(async user =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var now = TimeOnly.FromDateTime(DateTime.UtcNow);
                    if (new TimeOnly(user.EndTime.Hour, user.EndTime.Minute) <= now) {
                        return;
                    }

                    var message = _messageFormatter.FormatResolved(
                        user.Line.Name,
                        user.StartStation.Name,
                        user.EndStation.Name);

                    var notificationResult = await _notificationClient
                       .SendAsync(user.Id, user.PhoneOS, user.NotificationId!.Value, message);

                    if(notificationResult.IsFailure)
                    {
                        _logger.LogWarning("Push resolved notification failed for {UserId}: {Error}", user.Id, notificationResult.Error);

                        var smsResult = await _smsClient.SendAsync(user.PhoneNumber, message);

                        if (smsResult.IsFailure) {
                            _logger.LogWarning("SMS resolved notification failed for {UserId}: {Error}", user.Id, smsResult.Error);
                        }
                    }

                }
                finally {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
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
            id, userId, lineId, disruptionId, startStationId, endStationId,
            severityId, descriptionId, notificationSentBy, DateTime.UtcNow, affectedStationsId);

        try
        {
            await _notificationSender
                .SendMessageAsync(
                new ServiceBusMessage(BinaryData.FromObjectAsJson(dto)));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Could not send notification {id} for {userId} : {ex.Message}");
        }
    }
}
