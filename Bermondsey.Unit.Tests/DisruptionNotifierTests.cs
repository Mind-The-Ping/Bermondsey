using Azure.Communication.Sms;
using Azure.Messaging.ServiceBus;
using Bermondsey.Clients;
using Bermondsey.Clients.Stratford;
using Bermondsey.Clients.Waterloo;
using Bermondsey.Messages;
using Bermondsey.Models;
using Bermondsey.Options;
using Bermondsey.Repositories;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bermondsey.Unit.Tests;
public class DisruptionNotifierTests
{
    private readonly ISmsClient _smsClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly MessageFormatter _messagerFormatter;
    private readonly ServiceBusSender _notificationSender;
    private readonly IOptions<ServiceBusOptions> _iServiceBusOptions;

    private readonly Line _line;
    private readonly Station _startStation;
    private readonly Station _endStation;
    private readonly TimeOnly _endTime;

    public DisruptionNotifierTests()
    {
        _smsClient = Substitute.For<ISmsClient>();
        _smsClient.SendAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Success());

        var serviceBusOptions = new ServiceBusOptions()
        {
            Queues = new QueueOptions()
            {
                Notifications = "queue.4",
            }
        };

        _iServiceBusOptions = Microsoft.Extensions.Options.Options.Create(serviceBusOptions);

        _serviceBusClient = Substitute.For<ServiceBusClient>();
        _notificationSender = Substitute.For<ServiceBusSender>();

        _serviceBusClient
           .CreateSender(serviceBusOptions.Queues.Notifications)
           .Returns(_notificationSender);

        var templateOptions = new MessageTemplatesOptions()
        {
            Disruption = "Your journey on the {line} line from {origin} to {destination} has a {severity} issue.",
            Resolved = "Your issue on the {line} from {origin} to {destination} is now resolved."
        };

        var iMessageTemplatesOptions = Microsoft.Extensions.Options.Options.Create(templateOptions);
        _messagerFormatter = new MessageFormatter(iMessageTemplatesOptions);

        _line = new Line(Guid.Parse("8c3a4d59-f2e0-46a8-9f56-ec27eaffded9"), "District");
        _startStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");
        _endStation = new Station(Guid.Parse("968bc258-138c-45cf-83c0-599705285d25"), "West Ham");
        _endTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30));
    }


    [Fact]
    public async Task DisruptionNotifier_NotifyDisruptionAsync_New_Users_Disruption_Notifys_New_Users_Only()
    {
        var notifiedRepository = Substitute.For<IUserNotifiedRepository>();

        notifiedRepository.GetUsersByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Enumerable.Empty<User>());

        notifiedRepository.SaveUsersAsync(Arg.Any<IEnumerable<User>>())
            .Returns(Result.Success());

        notifiedRepository.DeleteByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        var affectedUsers = new List<AffectedUser>
        {
            new(Guid.NewGuid(), _startStation, _endStation, _endTime),
            new(Guid.NewGuid(),  _startStation, _endStation, _endTime)
        };

        var waterlooClient = Substitute.For<IWaterlooClient>();
        waterlooClient.GetAffectedUsersAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Severity>(),
            Arg.Any<TimeOnly>(),
            Arg.Any<DayOfWeek>())
            .Returns(Result.Success<IEnumerable<AffectedUser>>(affectedUsers));

        var userDetails = new List<UserDetails>
        {
            new(affectedUsers.First().Id, "+447123456789", PhoneOS.Android),
            new(affectedUsers.Last().Id, "+447234567890", PhoneOS.IOS)
        };

        var stratfordClient = Substitute.For<IStratfordClient>();
        stratfordClient.GetUserDetailsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(Result.Success<IEnumerable<UserDetails>>(userDetails));

        var disruptionNotifer = new DisruptionNotifier(
            _smsClient,
            _serviceBusClient,
            waterlooClient,
            stratfordClient,
            _messagerFormatter,
            _iServiceBusOptions,
            notifiedRepository);

        var disruption = new Disruption(
            Guid.NewGuid(),
            _line,
            _startStation.Id,
            _endStation.Id,
            Severity.Suspended,
            Guid.NewGuid());

        await disruptionNotifer.NotifyDisruptionAsync(disruption);

        var notificationsSents = _notificationSender.ReceivedCalls();
        notificationsSents.Should().HaveCount(2);

        var message1 = (ServiceBusMessage)notificationsSents.First().GetArguments()[0]!;
        var notification1 = message1.Body.ToObjectFromJson<Notification>();

        notification1!.UserId.Should().Be(affectedUsers.First().Id);
        notification1!.DisruptionId.Should().Be(disruption.Id);
        notification1!.LineId.Should().Be(_line.Id);
        notification1!.StartStationId.Should().Be(_startStation.Id);
        notification1!.EndStationId.Should().Be(_endStation.Id);
        notification1!.NotificationSentBy.Should().Be(NotificationSentBy.Sms);

        var message2 = (ServiceBusMessage)notificationsSents.Last().GetArguments()[0]!;
        var notification2 = message2.Body.ToObjectFromJson<Notification>();

        notification2!.UserId.Should().Be(affectedUsers.Last().Id);
        notification2!.DisruptionId.Should().Be(disruption.Id);
        notification2!.LineId.Should().Be(_line.Id);
        notification2!.StartStationId.Should().Be(_startStation.Id);
        notification2!.EndStationId.Should().Be(_endStation.Id);
        notification2!.NotificationSentBy.Should().Be(NotificationSentBy.Sms);

        var notifyRepoCalled = notifiedRepository.ReceivedCalls();
        notifyRepoCalled.Should().HaveCount(2);

        var usersSaved = (IEnumerable<User>)notifyRepoCalled.Last().GetArguments()[0]!;

        usersSaved.First().Id.Should().Be(affectedUsers.First().Id);
        usersSaved.First().PhoneNumber.Should().Be(userDetails.First().PhoneNumber);
        usersSaved.Last().Id.Should().Be(affectedUsers.Last().Id);
        usersSaved.Last().PhoneNumber.Should().Be(userDetails.Last().PhoneNumber);
    }

    [Fact]
    public async Task DisruptionNotifier_NotifyDisruptionAsync_NewUsers_NotifiedUsers_SameSeverity_Disruption_Notifys_New_Users_Only()
    {
        var severity = Severity.Severe;

        var disruption = new Disruption(
         Guid.NewGuid(),
         _line,
         _startStation.Id,
         _endStation.Id,
         severity,
         Guid.NewGuid());

        var users = new List<User>
        {
            new User(
                Guid.NewGuid(),
                disruption.Id,
                _line,
                _startStation,
                _endStation,
                severity,
                "+447123456789",
                _endTime),
            new User(
                Guid.NewGuid(),
                disruption.Id,
                _line,
                _startStation,
                _endStation,
                severity,
                 "+447234567890",
                _endTime),
        };

        var notifiedRepository = Substitute.For<IUserNotifiedRepository>();

        notifiedRepository.GetUsersByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(users);

        notifiedRepository.SaveUsersAsync(Arg.Any<IEnumerable<User>>())
            .Returns(Result.Success());

        notifiedRepository.DeleteByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        var affectedUsers = new List<AffectedUser>
        {
            new(Guid.NewGuid(), _startStation, _endStation, _endTime),
            new(Guid.NewGuid(), _startStation, _endStation, _endTime)
        };

        var waterlooClient = Substitute.For<IWaterlooClient>();
        waterlooClient.GetAffectedUsersAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Severity>(),
            Arg.Any<TimeOnly>(),
            Arg.Any<DayOfWeek>())
            .Returns(Result.Success<IEnumerable<AffectedUser>>(affectedUsers));

        var userDetails = new List<UserDetails>
        {
            new(affectedUsers.First().Id, "+447345678901", PhoneOS.Android),
            new(affectedUsers.Last().Id, "+447456789012", PhoneOS.IOS)
        };

        var stratfordClient = Substitute.For<IStratfordClient>();
        stratfordClient.GetUserDetailsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(Result.Success<IEnumerable<UserDetails>>(userDetails));

        var disruptionNotifer = new DisruptionNotifier(
           _smsClient,
           _serviceBusClient,
           waterlooClient,
           stratfordClient,
           _messagerFormatter,
           _iServiceBusOptions,
           notifiedRepository);

        await disruptionNotifer.NotifyDisruptionAsync(disruption);

        var notificationsSents = _notificationSender.ReceivedCalls();
        notificationsSents.Should().HaveCount(2);

        var message1 = (ServiceBusMessage)notificationsSents.First().GetArguments()[0]!;
        var notification1 = message1.Body.ToObjectFromJson<Notification>();

        notification1!.UserId.Should().Be(affectedUsers.First().Id);
        notification1!.DisruptionId.Should().Be(disruption.Id);
        notification1!.LineId.Should().Be(_line.Id);
        notification1!.StartStationId.Should().Be(_startStation.Id);
        notification1!.EndStationId.Should().Be(_endStation.Id);
        notification1!.NotificationSentBy.Should().Be(NotificationSentBy.Sms);

        var message2 = (ServiceBusMessage)notificationsSents.Last().GetArguments()[0]!;
        var notification2 = message2.Body.ToObjectFromJson<Notification>();

        notification2!.UserId.Should().Be(affectedUsers.Last().Id);
        notification2!.DisruptionId.Should().Be(disruption.Id);
        notification2!.LineId.Should().Be(_line.Id);
        notification2!.StartStationId.Should().Be(_startStation.Id);
        notification2!.EndStationId.Should().Be(_endStation.Id);
        notification2!.NotificationSentBy.Should().Be(NotificationSentBy.Sms);

        var notifyRepoCalled = notifiedRepository.ReceivedCalls();
        notifyRepoCalled.Should().HaveCount(2);

        var usersSaved = (IEnumerable<User>)notifyRepoCalled.Last().GetArguments()[0]!;

        usersSaved.First().Id.Should().Be(affectedUsers.First().Id);
        usersSaved.First().PhoneNumber.Should().Be(userDetails.First().PhoneNumber);
        usersSaved.Last().Id.Should().Be(affectedUsers.Last().Id);
        usersSaved.Last().PhoneNumber.Should().Be(userDetails.Last().PhoneNumber);
    }

    [Fact]
    public async Task DisruptionNotifier_NotifyDisruptionAsync_NewUsers_NotifiedUsers_DifferentSeverity_Disruption_Notifys_All()
    {
        var disruption = new Disruption(
           Guid.NewGuid(),
           _line,
           _startStation.Id,
           _endStation.Id,
           Severity.Minor,
           Guid.NewGuid());

        var users = new List<User>
        {
            new User(
                Guid.NewGuid(),
                disruption.Id,
                _line,
                _startStation,
                _endStation,
                Severity.Severe,
                "+447123456789",
                _endTime),
            new User(
                Guid.NewGuid(),
                disruption.Id,
                _line,
                _startStation,
                _endStation,
                Severity.Severe,
                 "+447234567890",
                _endTime),
        };

        var notifiedRepository = Substitute.For<IUserNotifiedRepository>();

        notifiedRepository.GetUsersByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(users);

        notifiedRepository.SaveUsersAsync(Arg.Any<IEnumerable<User>>())
            .Returns(Result.Success());

        notifiedRepository.DeleteByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        var affectedUsers = new List<AffectedUser>
        {
            new(Guid.NewGuid(), _startStation, _endStation, _endTime),
            new(Guid.NewGuid(), _startStation, _endStation, _endTime)
        };

        var waterlooClient = Substitute.For<IWaterlooClient>();
        waterlooClient.GetAffectedUsersAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Severity>(),
            Arg.Any<TimeOnly>(),
            Arg.Any<DayOfWeek>())
            .Returns(Result.Success<IEnumerable<AffectedUser>>(affectedUsers));

        var userDetails = new List<UserDetails>
        {
            new(affectedUsers.First().Id, "+447345678901", PhoneOS.Android),
            new(affectedUsers.Last().Id, "+447456789012", PhoneOS.IOS)
        };

        var stratfordClient = Substitute.For<IStratfordClient>();
        stratfordClient.GetUserDetailsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(Result.Success<IEnumerable<UserDetails>>(userDetails));

        var disruptionNotifer = new DisruptionNotifier(
         _smsClient,
         _serviceBusClient,
         waterlooClient,
         stratfordClient,
         _messagerFormatter,
         _iServiceBusOptions,
         notifiedRepository);

        await disruptionNotifer.NotifyDisruptionAsync(disruption);

        var notificationsSent = _notificationSender.ReceivedCalls();
        notificationsSent.Should().HaveCount(4);
    }

    [Fact]
    public async Task DisruptionNotifier_NotifyDisruptionAsync_NewUser_NotifiedUser_Different_Severity_Disruption_Notify_Sends_New()
    {
        var severityId = Guid.NewGuid();

        var disruption = new Disruption(
           Guid.NewGuid(),
           _line,
           _startStation.Id,
           _endStation.Id,
           Severity.Minor,
           severityId);

        var users = new List<User>
        {
            new User(
                Guid.NewGuid(),
                disruption.Id,
                _line,
                _startStation,
                _endStation,
                Severity.Severe,
                "+447123456789",
                _endTime)
        };

        var notifiedRepository = Substitute.For<IUserNotifiedRepository>();

        notifiedRepository.GetUsersByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(users);

        notifiedRepository.SaveUsersAsync(Arg.Any<IEnumerable<User>>())
            .Returns(Result.Success());

        notifiedRepository.DeleteByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        var affectedUsers = new List<AffectedUser>
        {
            new(users.First().Id, _startStation, _endStation, _endTime),
        };

        var waterlooClient = Substitute.For<IWaterlooClient>();
        waterlooClient.GetAffectedUsersAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Severity>(),
            Arg.Any<TimeOnly>(),
            Arg.Any<DayOfWeek>())
            .Returns(Result.Success<IEnumerable<AffectedUser>>(affectedUsers));

        var userDetails = new List<UserDetails>
        {
            new(affectedUsers.First().Id, "+447345678901", PhoneOS.Android),
        };

        var stratfordClient = Substitute.For<IStratfordClient>();
        stratfordClient.GetUserDetailsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(Result.Success<IEnumerable<UserDetails>>(userDetails));

        var disruptionNotifer = new DisruptionNotifier(
         _smsClient,
         _serviceBusClient,
         waterlooClient,
         stratfordClient,
         _messagerFormatter,
         _iServiceBusOptions,
         notifiedRepository);

        await disruptionNotifer.NotifyDisruptionAsync(disruption);

        var notificationsSent = _notificationSender.ReceivedCalls();
        notificationsSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        var sentPhoneNumber = (string)smsSent.First().GetArguments()[0]!;
        sentPhoneNumber.Should().Be(userDetails.First().PhoneNumber);
    }

    [Fact]
    public async Task DisruptionNotifier_NotifyDisruptionAsync_NewUser_NotifiedUser_Fails_Sends_Message_Saves()
    {
        var severityId = Guid.NewGuid();

        var disruption = new Disruption(
           Guid.NewGuid(),
           _line,
           _startStation.Id,
           _endStation.Id,
           Severity.Minor,
           severityId);

        var users = new List<User>
        {
            new User(
                Guid.NewGuid(),
                disruption.Id,
                _line,
                _startStation,
                _endStation,
                Severity.Severe,
                "+447123456789",
                _endTime)
        };

        var notifiedRepository = Substitute.For<IUserNotifiedRepository>();

        notifiedRepository.GetUsersByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(users);

        notifiedRepository.SaveUsersAsync(Arg.Any<IEnumerable<User>>())
            .Returns(Result.Success());

        notifiedRepository.DeleteByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        var affectedUsers = new List<AffectedUser>
        {
            new(users.First().Id, _startStation, _endStation, _endTime),
        };

        var waterlooClient = Substitute.For<IWaterlooClient>();
        waterlooClient.GetAffectedUsersAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Severity>(),
            Arg.Any<TimeOnly>(),
            Arg.Any<DayOfWeek>())
            .Returns(Result.Success<IEnumerable<AffectedUser>>(affectedUsers));

        var userDetails = new List<UserDetails>
        {
            new(affectedUsers.First().Id, "+447345678901", PhoneOS.Android),
        };

        var stratfordClient = Substitute.For<IStratfordClient>();
        stratfordClient.GetUserDetailsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(Result.Success<IEnumerable<UserDetails>>(userDetails));

        var smsClient = Substitute.For<ISmsClient>();
        smsClient.SendAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Result.Failure("It failed dude."));

        var disruptionNotifer = new DisruptionNotifier(
         smsClient,
         _serviceBusClient,
         waterlooClient,
         stratfordClient,
         _messagerFormatter,
         _iServiceBusOptions,
         notifiedRepository);

        await disruptionNotifer.NotifyDisruptionAsync(disruption);

        var notificationsSents = _notificationSender.ReceivedCalls();
        notificationsSents.Should().HaveCount(1);

        var message = (ServiceBusMessage)notificationsSents.First().GetArguments()[0]!;
        var notification = message.Body.ToObjectFromJson<Notification>();

        notification!.UserId.Should().Be(affectedUsers.First().Id);
        notification!.DisruptionId.Should().Be(disruption.Id);
        notification!.LineId.Should().Be(_line.Id);
        notification!.StartStationId.Should().Be(_startStation.Id);
        notification!.EndStationId.Should().Be(_endStation.Id);
        notification!.NotificationSentBy.Should().Be(NotificationSentBy.Failed);
    }

    [Fact]
    public async Task DisruptionNotifier_NotifyDisruptionResolvedAsync_Sends_Notification_Successfully()
    {
        var disruptionEnd = new DisruptionEnd(Guid.NewGuid(), DateTime.UtcNow);
        var users = new List<User>
        {
            new User(
                Guid.NewGuid(),
                disruptionEnd.Id,
                _line,
                _startStation,
                _endStation,
                Severity.Severe,
                "+447123456789",
                _endTime)
        };

        var notifiedRepository = Substitute.For<IUserNotifiedRepository>();

        notifiedRepository.GetUsersByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(users);

        notifiedRepository.SaveUsersAsync(Arg.Any<IEnumerable<User>>())
            .Returns(Result.Success());

        notifiedRepository.DeleteByDisruptionIdAsync(Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        var waterlooClient = Substitute.For<IWaterlooClient>();
        waterlooClient.GetAffectedUsersAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Severity>(),
            Arg.Any<TimeOnly>(),
            Arg.Any<DayOfWeek>())
            .Returns(Result.Success(Enumerable.Empty<AffectedUser>()));

        var stratfordClient = Substitute.For<IStratfordClient>();
        stratfordClient.GetUserDetailsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(Result.Success(Enumerable.Empty<UserDetails>()));

        var disruptionNotifer = new DisruptionNotifier(
             _smsClient,
             _serviceBusClient,
             waterlooClient,
             stratfordClient,
             _messagerFormatter,
             _iServiceBusOptions,
             notifiedRepository);

        await disruptionNotifer.NotifyDisruptionResolvedAsync(disruptionEnd);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        var sentPhoneNumber = (string)smsSent.First().GetArguments()[0]!;
        sentPhoneNumber.Should().Be(users.First().PhoneNumber);

        var disruptionDeleted = notifiedRepository.ReceivedCalls();
        disruptionDeleted.Should().HaveCount(2);
        var disruptionId = (Guid)disruptionDeleted.Last().GetArguments()[0]!;
        disruptionId.Should().Be(disruptionEnd.Id);
    }
}
