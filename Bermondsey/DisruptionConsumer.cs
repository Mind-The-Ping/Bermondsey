using Azure.Messaging.ServiceBus;
using Bermondsey.Models;
using Bermondsey.NotificationOrchestrator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Bermondsey;

public class DisruptionConsumer
{
    private readonly ILogger<DisruptionConsumer> _logger;
    private readonly INotificationOrchestrator _notificationOrchestrator;

    public DisruptionConsumer(
        ILogger<DisruptionConsumer> logger,
        INotificationOrchestrator notificationOrchestrator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationOrchestrator = notificationOrchestrator ?? 
            throw new ArgumentNullException(nameof(notificationOrchestrator));
    }

    [Function("NotificationConsumer")]
    public async Task NotificationHandler(
        [ServiceBusTrigger("%QueueNotifications%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);


        try
        {
            var json = message.Body.ToArray();
            var messageJson = JsonSerializer.Deserialize<User>(json);
            await _notificationOrchestrator.SendDisruptionNotificationAsync(messageJson!);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Could not deserialize notification.");
        }
        await messageActions.CompleteMessageAsync(message);
    }

    [Function("NotificationsResolvedConsumer")]
    public async Task DisruptionEndsHandler(
        [ServiceBusTrigger("%QueueResolvedNotifications%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

        try
        {
            var json = message.Body.ToArray();
            var messageJson = JsonSerializer.Deserialize<User>(json);
            await _notificationOrchestrator.SendResolutionNotificationAsync(messageJson!);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Could not deserialize notification.");
        }

        await messageActions.CompleteMessageAsync(message);
    }
}