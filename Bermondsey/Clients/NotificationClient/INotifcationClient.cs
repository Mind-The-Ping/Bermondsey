using Bermondsey.Models;
using CSharpFunctionalExtensions;

namespace Bermondsey.Clients.NotificationClient;
public interface INotifcationClient
{
    public Task<Result> SendAsync(
        Guid userId, 
        PhoneOS phoneOS, 
        string message, 
        CancellationToken cancellationToken = default);
}
