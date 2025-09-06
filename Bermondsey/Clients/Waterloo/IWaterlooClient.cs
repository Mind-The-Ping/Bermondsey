using Bermondsey.Models;
using CSharpFunctionalExtensions;

namespace Bermondsey.Clients.Waterloo;
public interface IWaterlooClient
{
    public Task<Result<IEnumerable<AffectedUser>>> GetAffectedUsersAsync(
        Guid line,
        Guid startStation,
        Guid endStation,
        Severity severity,
        TimeOnly time,
        DayOfWeek queryDay,
        CancellationToken cancellationToken = default);
}
