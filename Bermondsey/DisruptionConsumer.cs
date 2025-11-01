using Azure.Messaging.ServiceBus;
using Bermondsey.Messages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Bermondsey;

public class DisruptionConsumer
{
    private readonly DisruptionNotifier _notifier;
    private readonly ILogger<DisruptionConsumer> _logger;

    public DisruptionConsumer(
        DisruptionNotifier notifier,
        ILogger<DisruptionConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    [Function("DisruptionsConsumer")]
    public async Task DisruptionHandler(
        [ServiceBusTrigger("%QueueDisruptions%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);


        try
        {
            var json = message.Body.ToArray();
            var messageJson = JsonSerializer.Deserialize<Disruption>(json);
            var result = await _notifier.NotifyDisruptionAsync(messageJson!);

            if(result.IsFailure) {
                _logger.LogError(result.Error);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Could not deserialize disruption.");
        }
        await messageActions.CompleteMessageAsync(message);
    }

    [Function("DisruptionsEndConsumer")]
    public async Task DisruptionEndsHandler(
        [ServiceBusTrigger("%TopicsDisruptionEndsName%", "%TopicsDisruptionEndsSubscription%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

        try
        {
            var json = message.Body.ToArray();
            var messageJson = JsonSerializer.Deserialize<DisruptionEnd>(json);
            await _notifier.NotifyDisruptionResolvedAsync(messageJson!);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Could not deserialize disruption.");
        }

        await messageActions.CompleteMessageAsync(message);
    }
}