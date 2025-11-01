using Bermondsey.Clients.Stratford;
using Bermondsey.Clients.Waterloo;
using Bermondsey.Messages;
using Bermondsey.Models;
using Bermondsey.NotificationOrchestrator;
using Bermondsey.Repositories;
using CSharpFunctionalExtensions;

namespace Bermondsey;
public class DisruptionNotifier
{
    private readonly TimeZoneInfo _londonTimeZone;
    private readonly IWaterlooClient _waterlooClient;
    private readonly IStratfordClient _stratfordClient;
    private readonly IUserNotifiedRepository _userNotifiedRepository;
    private readonly INotificationOrchestrator _notificationOrchestrator;

    public DisruptionNotifier(
        IWaterlooClient waterlooClient,
        IStratfordClient stratfordClient,
        IUserNotifiedRepository userNotifiedRepository,
        INotificationOrchestrator notificationOrchestrator)
    {
        _waterlooClient = waterlooClient;
        _stratfordClient = stratfordClient;
        _userNotifiedRepository = userNotifiedRepository;
        _londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        _notificationOrchestrator= notificationOrchestrator;
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

        await _notificationOrchestrator.SendDisruptionNotificationAsync(disruption, finalUsersToNotify);
        await _userNotifiedRepository.SaveUsersAsync(finalUsersToNotify);

        return errors.Count != 0
            ? Result.Failure(string.Join("; ", errors))
            : Result.Success();
    }

    public async Task NotifyDisruptionResolvedAsync(DisruptionEnd disruptionEnd)
    {
        var notifiedUsers = await _userNotifiedRepository
            .GetUsersByDisruptionIdAsync(disruptionEnd.Id);

        await _notificationOrchestrator.SendResolutionNotificationAsync(notifiedUsers);
        await _userNotifiedRepository.DeleteByDisruptionIdAsync(disruptionEnd.Id);
    }
}
