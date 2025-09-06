using Bermondsey.Models;

namespace Bermondsey.Messages;
public record Disruption(
    Guid Id,
    Line Line,
    Guid StartStationId,
    Guid EndStationId,
    Severity Severity,
    Guid SeverityId);
