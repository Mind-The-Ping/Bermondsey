using Bermondsey.Models;
using CSharpFunctionalExtensions;

namespace Bermondsey.Repositories;
public interface IUserNotifiedRepository
{
    Task<Result> SaveUsersAsync(IEnumerable<User> users);
    Task<IEnumerable<User>> GetUsersByDisruptionIdAsync(Guid disruptionId);
    Task DeleteByDisruptionIdAsync(Guid disruptionId);
}
