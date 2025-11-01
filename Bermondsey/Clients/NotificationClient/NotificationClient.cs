using Bermondsey.MessageTemplate;
using Bermondsey.Models;
using CSharpFunctionalExtensions;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Logging;

namespace Bermondsey.Clients.NotificationClient;

public class NotificationClient : INotifcationClient
{
    private readonly ILogger<NotificationClient> _logger;
    private readonly NotificationHubClient _notificationHubClient;

    public NotificationClient(
        ILogger<NotificationClient> logger,
        NotificationHubClient notificationHubClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationHubClient = notificationHubClient ?? throw new ArgumentNullException(nameof(notificationHubClient));
    }

    public async Task<Result> SendAsync(
    Guid userId,
    PhoneOS phoneOS,
    Guid notificationId,
    FormattedMessage message,
    CancellationToken cancellationToken = default)
    {
        try
        {
            string platform = phoneOS.ToString().ToLower();
            var payload = NotificationTemplateLoader.BuildPayload(
                platform, 
                message.Title, 
                message.Body, 
                notificationId);

            if (payload.IsFailure) {
                return Result.Failure(payload.Error);
            }

            NotificationOutcome? outcome = phoneOS switch
            {
                PhoneOS.Android => await _notificationHubClient
                    .SendFcmV1NativeNotificationAsync(payload.Value, $"user:{userId}", cancellationToken),

                PhoneOS.IOS => await _notificationHubClient
                    .SendAppleNativeNotificationAsync(payload.Value, $"user:{userId}", cancellationToken),

                _ => null
            };

            if (outcome == null) {
                return Result.Failure($"Unsupported PhoneOS type: {phoneOS}");
            }

            if (outcome.State is NotificationOutcomeState.Abandoned or NotificationOutcomeState.Unknown)
            {
                _logger.LogWarning(
                    "Notification to {UserId} ({PhoneOS}) failed. State: {State}, TrackingId: {TrackingId}",
                    userId, phoneOS, outcome.State, outcome.TrackingId);

                return Result.Failure($"Notification send failed for user {userId}. State: {outcome.State}");
            }

            _logger.LogInformation(
                "Notification sent to {UserId} ({PhoneOS}). State: {State}, TrackingId: {TrackingId}",
                userId, phoneOS, outcome.State, outcome.TrackingId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for user {UserId}", userId);
            return Result.Failure($"Failed to send push notification for {notificationId} on {phoneOS}: {ex.Message}");
        }
    }
}
