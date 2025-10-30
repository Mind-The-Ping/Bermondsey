namespace Bermondsey.Models;

public record User(
    Guid Id, 
    Guid DisruptionId,
    Line Line,
    Station StartStation,
    Station EndStation,
    Severity Severity,
    string PhoneNumber,
    PhoneOS PhoneOS,
    TimeOnly EndTime,
    IEnumerable<Station> AffectedStations);
