using Azure.Communication.Sms;
using Bermondsey.MessageTemplate;
using CSharpFunctionalExtensions;

namespace Bermondsey.Clients.SmsClient;
public interface ISmsClient
{
    Task<Result> SendAsync(
        string to,
        FormattedMessage message,
        SmsSendOptions? options = null,
        CancellationToken cancellationToken = default);
}
