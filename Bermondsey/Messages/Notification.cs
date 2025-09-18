namespace Bermondsey.Messages;

public enum NotificationSentBy
{
    Sms,
    Push,
    Failed
}


public record Notification(
    Guid Id,
    Guid UserId,
    Guid LineId,
    Guid DisruptionId,
    Guid StartStationId,
    Guid EndStationId,
    Guid SeverityId,
    Guid DescriptionId,
    NotificationSentBy NotificationSentBy,
    DateTime SentTime);
