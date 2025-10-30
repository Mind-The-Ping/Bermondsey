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

        var newUsers = affectedUsers.Value.ToList();
        var usersToNotify = new Dictionary<Guid, User>();

        foreach (var notifiedUser in notifiedUsers)
        {
            if (notifiedUser.Severity == disruption.Severity)
            {
                var newUser = newUsers.SingleOrDefault(x => x.Id == notifiedUser.Id);
                if (newUser is not null) {
                    newUsers.Remove(newUser);
                }
                continue;
            }

            usersToNotify[notifiedUser.Id] = notifiedUser;
        }


        var userDetails = await _stratfordClient.GetUserDetailsAsync(
            newUsers.Select(x => x.Id));

        if (userDetails.IsFailure) {
            return Result.Failure($"Failed to get users details : {userDetails.Error}");
        }

        var phoneLookup = userDetails.Value?
          .ToDictionary(
              u => u.Id, 
              u => new { u.PhoneNumber, u.PhoneOS })
          ?? [];

        var errors = new List<string>();

        foreach (var newUser in newUsers)
        {
            if (!phoneLookup.TryGetValue(newUser.Id, out var phoneDetails))
            {
                errors.Add($"Failed to find phone number for {newUser.Id}");
                continue;
            }

            var user = new User(
                newUser.Id,
                disruption.Id,
                disruption.Line,
                newUser.StartStation,
                newUser.EndStation,
                disruption.Severity,
                phoneDetails.PhoneNumber,
                phoneDetails.PhoneOS,
                newUser.EndTime,
                newUser.AffectedStations);

            usersToNotify[user.Id] = user;
        }

        var finalUsersToNotify = usersToNotify.Values.ToList();

        foreach (var user in finalUsersToNotify)
        {
            var now = TimeOnly.FromDateTime(DateTime.UtcNow);
            now = new TimeOnly(now.Hour, now.Minute);

            var end = new TimeOnly(user.EndTime.Hour, user.EndTime.Minute);

            if (end <= now) {
                continue;
            }

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
                    disruption.DescriptionId,
                    NotificationSentBy.Failed,
                    user.AffectedStations.Select(x => x.Id).ToList());

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
                disruption.DescriptionId,
                NotificationSentBy.Sms,
                user.AffectedStations.Select(x => x.Id).ToList());
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
        Guid descriptionId,
        NotificationSentBy notificationSentBy,
        IList<Guid> affectedStationsId)
    {
        var dto = new Notification(
            id,
            userId,
            lineId,
            disruptionId,
            startStationId,
            endStationId,
            severityId,
            descriptionId,
            notificationSentBy,
            DateTime.UtcNow,
            affectedStationsId);

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
