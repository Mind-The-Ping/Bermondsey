using Azure.Messaging.ServiceBus;
using Bermondsey.Clients.NotificationClient;
using Bermondsey.Clients.SmsClient;
using Bermondsey.Messages;
using Bermondsey.MessageTemplate;
using Bermondsey.Models;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Bermondsey.Unit.Tests;
public class NotificationOrchestratorTests
{
    private readonly ISmsClient _smsClient;
    private readonly INotifcationClient _notifcationClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSender _notificationSender;
    private readonly MessageFormatter _messagerFormatter;

    private readonly NotificationOrchestrator.NotificationOrchestrator _notificationOrchestrator;

    public NotificationOrchestratorTests()
    {
        _smsClient = Substitute.For<ISmsClient>();
        _notifcationClient = Substitute.For<INotifcationClient>();
        _serviceBusClient = Substitute.For<ServiceBusClient>();
        _notificationSender = Substitute.For<ServiceBusSender>();

        var serviceBusOptions = new ServiceBusOptions()
        {
            Queues = new QueueOptions()
            {
                Notifications = "queue.4",
            }
        };

        _serviceBusClient
         .CreateSender(serviceBusOptions.Queues.Notifications)
         .Returns(_notificationSender);

        var templateOptions = Microsoft.Extensions.Options.Options.Create(new MessageTemplatesOptions
        {
            Delay = new TemplateSection()
            {
                Title = "Your journey from {origin} to {destination} has a {severity} delays.",
                Body = "Your journey on the {line} line from {origin} to {destination} has {severity} delays." +
                " The following stations are affected: {stations}."
            },
            Disruption = new TemplateSection()
            {
                Title = "Your journey from {origin} to {destination} is {severity}.",
                Body = "Your journey on the {line} line from {origin} to {destination} is {severity}." +
                " The following stations are affected: {stations}."
            },
            Resolved = new TemplateSection()
            {
                Title = "Your issue from {origin} to {destination} is now resolved.",
                Body = "Your issue on the {line} line from {origin} to {destination} is now resolved. Services are back to normal."
            }
        });

        var iMessageTemplatesOptions = Microsoft.Extensions.Options.Options.Create(templateOptions);
        _messagerFormatter = new MessageFormatter(iMessageTemplatesOptions.Value);

        var logger = Substitute.For<ILogger<NotificationOrchestrator.NotificationOrchestrator>>();

        var iServiceBusOptions = Microsoft.Extensions.Options.Options.Create(serviceBusOptions);

        _notificationOrchestrator = new NotificationOrchestrator.NotificationOrchestrator(
            _smsClient,
            _messagerFormatter,
            _serviceBusClient,
            _notifcationClient,
            logger,
            iServiceBusOptions);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Push_Notification_Successful()
    {
        var line = new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan");

        var disruption = new Disruption(
            Guid.NewGuid(),
            line,
            Guid.Parse("b5fd3078-17d7-4e45-8ff6-d7d85025e1b0"),
            Guid.Parse("7d89b35f-9a87-49df-98ff-fd98f1f67235"),
            Severity.Severe,
            Guid.NewGuid(),
            Guid.NewGuid());

        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(), 
                disruption.Id,
                line,
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations)
        };

        _notifcationClient.SendAsync(
            users.First().Id,
            users.First().PhoneOS,
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendDisruptionNotificationAsync(disruption, users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(1);

        var message = (ServiceBusMessage)notificationSenderSent.First().GetArguments()[0]!;
        var notification = message.Body.ToObjectFromJson<Notification>();

        notification!.UserId.Should().Be(users.First().Id);
        notification!.DisruptionId.Should().Be(disruption.Id);
        notification!.LineId.Should().Be(line.Id);
        notification!.StartStationId.Should().Be(users.First().StartStation.Id);
        notification!.EndStationId.Should().Be(users.First().EndStation.Id);
        notification!.NotificationSentBy.Should().Be(NotificationSentBy.Push);
        notification!.AffectedStationIds.Should().BeEquivalentTo(affectedStations.Select(x => x.Id).ToList());
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Sms_Text_Successful()
    {
        var line = new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan");

        var disruption = new Disruption(
            Guid.NewGuid(),
            line,
            Guid.Parse("b5fd3078-17d7-4e45-8ff6-d7d85025e1b0"),
            Guid.Parse("7d89b35f-9a87-49df-98ff-fd98f1f67235"),
            Severity.Severe,
            Guid.NewGuid(),
            Guid.NewGuid());

        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                disruption.Id,
                line,
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations)
        };

        _notifcationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        _smsClient.SendAsync(
            users.First().PhoneNumber,
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendDisruptionNotificationAsync(disruption, users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(1);

        var message = (ServiceBusMessage)notificationSenderSent.First().GetArguments()[0]!;
        var notification = message.Body.ToObjectFromJson<Notification>();

        notification!.UserId.Should().Be(users.First().Id);
        notification!.DisruptionId.Should().Be(disruption.Id);
        notification!.LineId.Should().Be(line.Id);
        notification!.StartStationId.Should().Be(users.First().StartStation.Id);
        notification!.EndStationId.Should().Be(users.First().EndStation.Id);
        notification!.NotificationSentBy.Should().Be(NotificationSentBy.Sms);
        notification!.AffectedStationIds.Should().BeEquivalentTo(affectedStations.Select(x => x.Id).ToList());
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Fails()
    {
        var line = new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan");

        var disruption = new Disruption(
            Guid.NewGuid(),
            line,
            Guid.Parse("b5fd3078-17d7-4e45-8ff6-d7d85025e1b0"),
            Guid.Parse("7d89b35f-9a87-49df-98ff-fd98f1f67235"),
            Severity.Severe,
            Guid.NewGuid(),
            Guid.NewGuid());

        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                disruption.Id,
                line,
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations)
        };

        _notifcationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        _smsClient.SendAsync(
            Arg.Any<string>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        await _notificationOrchestrator.SendDisruptionNotificationAsync(disruption, users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(1);

        var message = (ServiceBusMessage)notificationSenderSent.First().GetArguments()[0]!;
        var notification = message.Body.ToObjectFromJson<Notification>();

        notification!.UserId.Should().Be(users.First().Id);
        notification!.DisruptionId.Should().Be(disruption.Id);
        notification!.LineId.Should().Be(line.Id);
        notification!.StartStationId.Should().Be(users.First().StartStation.Id);
        notification!.EndStationId.Should().Be(users.First().EndStation.Id);
        notification!.NotificationSentBy.Should().Be(NotificationSentBy.Failed);
        notification!.AffectedStationIds.Should().BeEquivalentTo(affectedStations.Select(x => x.Id).ToList());
    }


    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Expired_Does_Not_Send_Notification()
    {
        var line = new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan");

        var disruption = new Disruption(
            Guid.NewGuid(),
            line,
            Guid.Parse("b5fd3078-17d7-4e45-8ff6-d7d85025e1b0"),
            Guid.Parse("7d89b35f-9a87-49df-98ff-fd98f1f67235"),
            Severity.Severe,
            Guid.NewGuid(),
            Guid.NewGuid());

        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                disruption.Id,
                line,
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-1)),
                affectedStations)
        };

        _notifcationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendDisruptionNotificationAsync(disruption, users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(0);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(0);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_1000_Users_Successful()
    {
        var userCount = 1000;

        var disruption = new Disruption(
         Guid.NewGuid(),
         new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
         Guid.Parse("b5fd3078-17d7-4e45-8ff6-d7d85025e1b0"),
         Guid.Parse("7d89b35f-9a87-49df-98ff-fd98f1f67235"),
         Severity.Severe,
         Guid.NewGuid(),
         Guid.NewGuid());

        var users = GenerateRandomUsers(userCount);

        _notifcationClient.SendAsync(
           Arg.Any<Guid>(),
           Arg.Any<PhoneOS>(),
           Arg.Any<Guid>(),
           Arg.Any<FormattedMessage>())
           .Returns(Result.Success());

        await _notificationOrchestrator.SendDisruptionNotificationAsync(disruption, users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(userCount);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(userCount);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendResolutionNotificationAsync_Push_Notification_Successful()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations)
        };

        users.First().NotificationId = Guid.NewGuid();

        _notifcationClient.SendAsync(
            users.First().Id,
            users.First().PhoneOS,
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendResolutionNotificationAsync(users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(0);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(0);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendResolutionNotificationAsync_Sms_Text_Successful()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations)
        };

        users.First().NotificationId = Guid.NewGuid();

        _notifcationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("It failed."));

        _smsClient.SendAsync(
           users.First().PhoneNumber,
           Arg.Any<FormattedMessage>())
           .Returns(Result.Success());

        await _notificationOrchestrator.SendResolutionNotificationAsync(users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(0);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendResolutionNotificationAsync_Failed()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations)
        };

        users.First().NotificationId = Guid.NewGuid();

        _notifcationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("It failed."));

        _smsClient.SendAsync(
           users.First().PhoneNumber,
           Arg.Any<FormattedMessage>())
           .Returns(Result.Failure("This failed too nerd."));

        await _notificationOrchestrator.SendResolutionNotificationAsync(users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(0);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendResolutionNotificationAsync_Expired_Does_Not_Send_Notification()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var users = new List<User>()
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-1)),
                affectedStations)
        };

        users.First().NotificationId = Guid.NewGuid();

        _notifcationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendResolutionNotificationAsync(users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(0);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(0);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendResolutionNotificationAsync_1000_Users_Successful()
    {
        var userCount = 1000;
        var users = GenerateRandomUsers(userCount);

        _notifcationClient.SendAsync(
           Arg.Any<Guid>(),
           Arg.Any<PhoneOS>(),
           Arg.Any<Guid>(),
           Arg.Any<FormattedMessage>())
           .Returns(Result.Success());

        await _notificationOrchestrator.SendResolutionNotificationAsync(users);

        var notificationSent = _notifcationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(userCount);

        var notificationSenderSent = _notificationSender.ReceivedCalls();
        notificationSenderSent.Should().HaveCount(0);
    }

    private List<User> GenerateRandomUsers(int count)
    {
        var random = new Random();

        var stationNames = new[] { "Moor Park", "Northwood", "Pinner", "Uxbridge", "Baker Street", "Watford", "Harrow", "Chorleywood" };
        var lineNames = new[] { "Metropolitan", "Central", "Jubilee", "Piccadilly", "Northern", "District", "Bakerloo" };

        var users = new List<User>();

        for (int i = 0; i < count; i++)
        {
            var line = new Line(Guid.NewGuid(), lineNames[random.Next(lineNames.Length)]);

            var startStation = new Station(Guid.NewGuid(), stationNames[random.Next(stationNames.Length)]);
            Station endStation;
            do
            {
                endStation = new Station(Guid.NewGuid(), stationNames[random.Next(stationNames.Length)]);
            } while (endStation.Name == startStation.Name);


            var affectedStations = new List<Station>();
            int affectedCount = random.Next(1, 5);
            for (int j = 0; j < affectedCount; j++)
            {
                affectedStations.Add(new Station(Guid.NewGuid(), stationNames[random.Next(stationNames.Length)]));
            }

            var severityValues = Enum.GetValues<Severity>();
            var phoneOsValues = Enum.GetValues<PhoneOS>();
            var severity = (Severity)severityValues.GetValue(random.Next(severityValues.Length))!;
            var phoneOs = (PhoneOS)phoneOsValues.GetValue(random.Next(phoneOsValues.Length))!;
            var phoneNumber = $"+44{random.Next(700000000, 799999999)}";
            var time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(1));

            var user = new User(
                Guid.NewGuid(),
                Guid.NewGuid(),
                line,
                startStation,
                endStation,
                severity,
                phoneNumber,
                phoneOs,
                time,
                affectedStations
            );

            user.NotificationId = Guid.NewGuid();

            users.Add(user);
        }

        return users;
    }
}
