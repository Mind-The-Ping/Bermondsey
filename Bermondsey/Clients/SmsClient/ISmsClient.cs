using Azure.Communication.Sms;
using CSharpFunctionalExtensions;

namespace Bermondsey.Clients.SmsClient;
public interface ISmsClient
{
    Task<Result> SendAsync(
        string to,
        string message,
        SmsSendOptions? options = null,
        CancellationToken cancellationToken = default);
}
