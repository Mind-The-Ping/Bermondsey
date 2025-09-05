using Azure.Messaging.ServiceBus;
using Bermondsey.Clients;
using Bermondsey.Messages;
using CSharpFunctionalExtensions;

namespace Bermondsey;
public class DisruptionNotifier
{
    private readonly ISmsClient _smsClient;
    private readonly IWaterlooClient _waterlooClient;
    private readonly MessageFormatter _messageFormatter;
    private readonly UserNotifiedRepository _userNotifiedRepository;

    public DisruptionNotifier(
        ISmsClient smsClient,
        ServiceBusClient busClient,
        IWaterlooClient waterlooClient,
        MessageFormatter messageFormatter,
        UserNotifiedRepository userNotifiedRepository)
    {
        _smsClient = smsClient;
        _waterlooClient = waterlooClient;
        _messageFormatter = messageFormatter;
        _userNotifiedRepository = userNotifiedRepository;
    }

    public async Task<Result> NotifyDisruptionResolvedAsync(DisruptionEnd disruptionEnd)
    {
        var notifiedUsers = await _userNotifiedRepository
            .GetUsersByDisruptionIdAsync(disruptionEnd.Id);

        var errors = new List<string>();

        foreach (var user in notifiedUsers)
        {
            var message = _messageFormatter.FormatResolved(
                user.Line.Name,
                user.StartStation.Name,
                user.EndStation.Name);

            var messageResult = await _smsClient.SendAsync(user.PhoneNumber, message);
            if(messageResult.IsFailure) 
            {
                errors.Add($"Failed to send notification to {user.PhoneNumber} " +
                    $": {messageResult.Error}.");
            }
        }

        await _userNotifiedRepository.DeleteByDisruptionIdAsync(disruptionEnd.Id);

        return errors.Count != 0
            ? Result.Failure(string.Join("; ", errors))
            : Result.Success();
    }
}
