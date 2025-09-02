namespace Bermondsey.Models;

public record User(
    Guid Id, 
    Guid DisruptionId, 
    Severity Severity,
    string PhoneNumber);
