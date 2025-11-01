using Azure.Communication.Sms;
using Bermondsey.MessageTemplate;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using System.Text;

namespace Bermondsey.Clients.SmsClient;

public class RealSmsClient : ISmsClient
{
    private readonly Azure.Communication.Sms.SmsClient _inner;
    private readonly SmsOptions _options;

    public RealSmsClient(IOptions<SmsOptions> options)
    {
        _options = options.Value ?? 
            throw new ArgumentNullException(nameof(options));

        _inner = new Azure.Communication.Sms.SmsClient(_options.ConnectionString);
    }

    public async Task<Result> SendAsync(
        string to, 
        FormattedMessage message, 
        SmsSendOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _inner.SendAsync(
                _options.PhoneNumber, 
                to, 
                FormatSmsText(message), 
                options, 
                cancellationToken);

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

    private static string FormatSmsText(FormattedMessage message)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{message.Title.Trim()}");
        sb.AppendLine();
        sb.AppendLine(message.Body.Trim());
        return sb.ToString();
    }
}
