namespace Bermondsey.Models;

public class User(
    Guid id,
    Guid disruptionId,
    Guid notificationId,
    Line line,
    Station startStation,
    Station endStation,
    Severity severity,
    string phoneNumber,
    PhoneOS phoneOS,
    TimeOnly endTime,
    IEnumerable<Station> affectedStations)
{
    public Guid Id { get; init; } = id;
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
}