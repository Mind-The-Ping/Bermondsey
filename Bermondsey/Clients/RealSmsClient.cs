using Azure.Communication.Sms;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;

namespace Bermondsey.Clients;

public interface ISmsClient
{
    Task<Result> SendAsync(
        string to,
        string message,
        SmsSendOptions? options = null,
        CancellationToken cancellationToken = default);
}


public class RealSmsClient : ISmsClient
{
    private readonly SmsClient _inner;
    private readonly SmsOptions _options;

    public RealSmsClient(IOptions<SmsOptions> options)
    {
        _options = options.Value ?? 
            throw new ArgumentNullException(nameof(options));

        _inner = new SmsClient(_options.ConnectionString);
    }

    public async Task<Result> SendAsync(string to, string message, SmsSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _inner.SendAsync(_options.PhoneNumber, to, message, options, cancellationToken);

            if(response.Value.Successful) {
                return Result.Success();
            }

            var errorMessage = response.Value.ErrorMessage ?? "Unknown error";
            return Result.Failure($"Failed to send SMS to {to}: {errorMessage}");
        }
        catch(Exception ex) {
            return Result.Failure($"Exception sending SMS to {to}: {ex.Message}");
        }
    }
}
