namespace Bermondsey.Models;

public class Journey(
    Guid journeyId,
    Guid userId,
    Guid disruptionId,
    Guid notificationId,
    Line line,
    Station startStation,
    Station endStation,
    Severity severity,
    string phoneNumber,
    PhoneOS phoneOS,
    TimeOnly endTime,
    IEnumerable<Station> affectedStations,
    int unReadMessageCount)
{
    public Guid JourneyId { get; init; } = journeyId;
    public Guid UserId { get; init; } = userId;
    public Guid DisruptionId { get; init; } = disruptionId;
    public Guid NotificationId { get; set; } = notificationId;
    public Line Line { get; init; } = line;
    public Station StartStation { get; init; } = startStation;
    public Station EndStation { get; init; } = endStation;
    public Severity Severity { get; init; } = severity;
    public string PhoneNumber { get; init; } = phoneNumber;
    public PhoneOS PhoneOS { get; init; } = phoneOS;
    public TimeOnly EndTime { get; init; } = endTime;
    public IEnumerable<Station> AffectedStations { get; init; } = affectedStations;
    public int UnReadMessageCount { get; set; } = unReadMessageCount;
}