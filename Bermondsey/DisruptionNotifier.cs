using Azure.Messaging.ServiceBus;
using Bermondsey.Clients;
using Bermondsey.Clients.Stratford;
using Bermondsey.Clients.Waterloo;
using Bermondsey.Messages;
using Bermondsey.Models;
using Bermondsey.Options;
using Bermondsey.Repositories;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;

namespace Bermondsey;
public class DisruptionNotifier
{
    private readonly ISmsClient _smsClient;
    private readonly TimeZoneInfo _londonTimeZone;
    private readonly IWaterlooClient _waterlooClient;
    private readonly IStratfordClient _stratfordClient;
    private readonly MessageFormatter _messageFormatter;
    private readonly ServiceBusSender _notificationSender;
    private readonly IUserNotifiedRepository _userNotifiedRepository;

    public DisruptionNotifier(
        ISmsClient smsClient,
        ServiceBusClient busClient,
        IWaterlooClient waterlooClient,
        IStratfordClient stratfordClient,
        MessageFormatter messageFormatter,
        IOptions<ServiceBusOptions> serviceBusOptions,
        IUserNotifiedRepository userNotifiedRepository)
    {
        _smsClient = smsClient;
        _waterlooClient = waterlooClient;
        _stratfordClient = stratfordClient;
        _messageFormatter = messageFormatter;
        _userNotifiedRepository = userNotifiedRepository;
        _londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        _notificationSender = busClient.CreateSender(serviceBusOptions.Value.Queues.Notifications);
    }

    public async Task<Result> NotifyDisruptionAsync(Disruption disruption)
    {
        var notifiedUsers = await _userNotifiedRepository
        .GetUsersByDisruptionIdAsync(disruption.Id);

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _londonTimeZone);

        var affectedUsers = await _waterlooClient.GetAffectedUsersAsync(
           disruption.Line.Id,
           disruption.StartStationId,
           disruption.EndStationId,
           disruption.Severity,
           TimeOnly.FromDateTime(DateTime.UtcNow),
           localTime.DayOfWeek);

        if (affectedUsers.IsFailure) {
            return Result.Failure($"Failed to get affected users : {affectedUsers.Error}");
        }

        var userDetails = await _stratfordClient.GetUserDetailsAsync(
            affectedUsers.Value.Select(x => x.Id));

        if (userDetails.IsFailure) {
            return Result.Failure($"Failed to get users details : {userDetails.Error}");
        }

        var phoneLookup = userDetails.Value?
        .ToDictionary(u => u.Id, u => u.PhoneNumber)
        ?? new Dictionary<Guid, string>();

        var usersToNotify = new Dictionary<Guid, User>();
        var errors = new List<string>();

        foreach (var affectedUser in affectedUsers.Value)
        {
            if (!phoneLookup.TryGetValue(affectedUser.Id, out var phoneNumber))
            {
                errors.Add($"Failed to find phone number for {affectedUser.Id}");
                continue;
            }

            var user = new User(
                affectedUser.Id,
                disruption.Id,
                disruption.Line,
                affectedUser.StartStation,
                affectedUser.EndStation,
                disruption.Severity,
                phoneNumber,
                affectedUser.EndTime);

            usersToNotify[user.Id] = user;
        }

        foreach (var oldUser in notifiedUsers)
        {
            if (!usersToNotify.ContainsKey(oldUser.Id) &&
                 oldUser.Severity != disruption.Severity)
            {
                usersToNotify[oldUser.Id] = oldUser;
            }
        }

        var finalUsersToNotify = usersToNotify.Values.ToList();

        foreach (var user in finalUsersToNotify)
        {
            var message = _messageFormatter.FormatDisruption(
                user.Line.Name,
                user.StartStation.Name,
                user.EndStation.Name,
                user.Severity);

            var messageResult = await _smsClient.SendAsync(user.PhoneNumber, message);

            if (messageResult.IsFailure)
            {
                errors.Add($"Failed to send notification to {user.PhoneNumber}: {messageResult.Error}.");

                await PublishNotificationMessageAsync(
                    Guid.NewGuid(),
                    user.Id,
                    user.Line.Id,
                    user.DisruptionId,
                    user.StartStation.Id,
                    user.EndStation.Id,
                    disruption.SeverityId,
                    NotificationSentBy.Failed);

                continue;
            }

            await PublishNotificationMessageAsync(
                Guid.NewGuid(),
                user.Id,
                user.Line.Id,
                user.DisruptionId,
                user.StartStation.Id,
                user.EndStation.Id,
                disruption.SeverityId,
                NotificationSentBy.Sms);
        }

        await _userNotifiedRepository.SaveUsersAsync(finalUsersToNotify);

        return errors.Count != 0
            ? Result.Failure(string.Join("; ", errors))
            : Result.Success();
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

    private async Task<Result> PublishNotificationMessageAsync(
        Guid id,
        Guid userId,
        Guid lineId,
        Guid disruptionId,
        Guid startStationId,
        Guid endStationId,
        Guid severityId,
        NotificationSentBy notificationSentBy)
    {
        var dto = new Notification(
            id,
            userId,
            lineId,
            disruptionId,
            startStationId,
            endStationId,
            severityId,
            notificationSentBy,
            DateTime.UtcNow);

        var message = BinaryData.FromObjectAsJson(dto);

        try {
            await _notificationSender.SendMessageAsync(new ServiceBusMessage(message));
        }
        catch (Exception ex) {
            return Result.Failure($"Could not send notification {id} for {userId} : {ex.Message}");
        }

        return Result.Success();
    }
}
