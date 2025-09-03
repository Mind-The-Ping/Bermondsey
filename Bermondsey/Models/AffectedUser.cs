namespace Bermondsey.Models;
public record AffectedUser(
    Guid Id,
    Station StartStation,
    Station EndStation,
    TimeOnly EndTime);