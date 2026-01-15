using Azure.Communication.Sms;
using Bermondsey.Clients.SmsClient.PhoneNumber;
using Bermondsey.MessageTemplate;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using System.Text;

namespace Bermondsey.Clients.SmsClient;

public class RealSmsClient : ISmsClient
{
    private readonly Azure.Communication.Sms.SmsClient _inner;
    private readonly PhoneNumberPool _phoneNumberPool;
    private readonly PerNumberRateLimiter _rateLimiter;

    public RealSmsClient(IOptions<SmsOptions> options)
    {
        var actualOptions = options.Value ?? 
            throw new ArgumentNullException(nameof(options));

        var phoneList = actualOptions.GetPhoneNumberList();

        if (phoneList.Count == 0) {
            throw new ArgumentNullException(
                "At least one phone number is required in configuration");
        }

        _phoneNumberPool = new PhoneNumberPool(phoneList);
        _rateLimiter = new PerNumberRateLimiter(phoneList, actualOptions.MaxPerMinute);

        _inner = new Azure.Communication.Sms.SmsClient(actualOptions.ConnectionString);
    }

    public async Task<Result> SendAsync(
        string to, 
        FormattedMessage message, 
        SmsSendOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        string fromPhoneNumber;
        for (int i = 0; i < _phoneNumberPool.Count; i++)
        {
            fromPhoneNumber = _phoneNumberPool.Next();
            if (_rateLimiter.TryAcquire(fromPhoneNumber))
            {
                try
                {
                    var response = await _inner.SendAsync(
                       fromPhoneNumber,
                       to,
                       FormatSmsText(message),
                       options,
                       cancellationToken);

                    if (response.Value.Successful) {
                        return Result.Success();
                    }

                    var errorMessage = response.Value.ErrorMessage ?? "Unknown error";
                    return Result.Failure($"Failed to send SMS to {to}: {errorMessage}");
                }
                catch (Exception ex) {
                    return Result.Failure($"Exception sending SMS to {to}: {ex.Message}");
                }
            }
        }

        const string throttledMessage = "SMS rate limit exceeded.";
        return Result.Failure(throttledMessage);
    }

    private static string FormatSmsText(FormattedMessage message)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{message.Title.Trim()}");
        sb.AppendLine();
        sb.AppendLine(message.Body.Trim());
        return sb.ToString();
    }
}
