namespace Bermondsey.Models;

public record User(
    Guid Id, 
    Guid DisruptionId, 
    Station StartStation,
    Station EndStation,
    Severity Severity,
    string PhoneNumber);
