using Bermondsey.Models;
using CSharpFunctionalExtensions;

namespace Bermondsey.Clients.Stratford;
public interface IStratfordClient
{
    public Task<Result<IEnumerable<UserDetails>>> GetUserDetailsAsync(IEnumerable<Guid> ids);
}
