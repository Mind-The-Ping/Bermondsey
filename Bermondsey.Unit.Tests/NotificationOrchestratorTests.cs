using Bermondsey.Clients.NotificationClient;
using Bermondsey.Clients.SmsClient;
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
    private readonly INotifcationClient _notificationClient;
    private readonly INotificationSentByRepository _repository;

    private readonly NotificationOrchestrator.NotificationOrchestrator _notificationOrchestrator;

    public NotificationOrchestratorTests()
    {
        _smsClient = Substitute.For<ISmsClient>();
        _notificationClient = Substitute.For<INotifcationClient>();
        _repository = Substitute.For<INotificationSentByRepository>();

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
        var messagerFormatter = new MessageFormatter(iMessageTemplatesOptions.Value);

        var logger = Substitute.For<ILogger<NotificationOrchestrator.NotificationOrchestrator>>();

        _notificationOrchestrator = new NotificationOrchestrator.NotificationOrchestrator(
            _smsClient,
            messagerFormatter,
            _notificationClient,
            _repository,
            logger);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Push_Notification_Successful()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations);

        _notificationClient.SendAsync(
            journey.UserId,
            journey.PhoneOS,
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        NotificationStatus capturedNotification = null!;

        _repository.CreateAsync(Arg.Do<NotificationStatus>(c => capturedNotification = c))
            .Returns(Task.CompletedTask);


        await _notificationOrchestrator.SendDisruptionNotificationAsync(journey);

        var notificationSent = _notificationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var sentStatusSaved = _repository.ReceivedCalls();
        sentStatusSaved.Should().HaveCount(1);

        capturedNotification.Id.Should().Be(journey.NotificationId);
        capturedNotification.NotificationSentBy.Should().Be(NotificationSentBy.Push);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Sms_Text_Successful()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations);

        _notificationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        NotificationStatus capturedNotification = null!;

        _repository.CreateAsync(Arg.Do<NotificationStatus>(c => capturedNotification = c))
            .Returns(Task.CompletedTask);

        await _notificationOrchestrator.SendDisruptionNotificationAsync(journey);

        var notificationSent = _notificationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        capturedNotification.Id.Should().Be(journey.NotificationId);
        capturedNotification.NotificationSentBy.Should().Be(NotificationSentBy.Sms);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Fails()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations);

        _notificationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        _smsClient.SendAsync(
         Arg.Any<string>(),
         Arg.Any<FormattedMessage>())
         .Returns(Result.Failure("This failed."));

        NotificationStatus capturedNotification = null!;

        _repository.CreateAsync(Arg.Do<NotificationStatus>(c => capturedNotification = c))
            .Returns(Task.CompletedTask);

        await _notificationOrchestrator.SendDisruptionNotificationAsync(journey);

        capturedNotification.Id.Should().Be(journey.NotificationId);
        capturedNotification.NotificationSentBy.Should().Be(NotificationSentBy.Failed);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendDisruptionNotificationAsync_Expired_Does_Not_Send_Notification()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-1)),
                affectedStations);

        _notificationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendDisruptionNotificationAsync(journey);

        var notificationSent = _notificationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(0);
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

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations);

        _notificationClient.SendAsync(
            journey.UserId,
            journey.PhoneOS,
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        NotificationStatus capturedNotification = null!;

        _repository.CreateAsync(Arg.Do<NotificationStatus>(c => capturedNotification = c))
            .Returns(Task.CompletedTask);


        await _notificationOrchestrator.SendResolutionNotificationAsync(journey);

        var notificationSent = _notificationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var sentStatusSaved = _repository.ReceivedCalls();
        sentStatusSaved.Should().HaveCount(1);

        capturedNotification.Id.Should().Be(journey.NotificationId);
        capturedNotification.NotificationSentBy.Should().Be(NotificationSentBy.Push);
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

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations);

        _notificationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        NotificationStatus capturedNotification = null!;

        _repository.CreateAsync(Arg.Do<NotificationStatus>(c => capturedNotification = c))
            .Returns(Task.CompletedTask);

        await _notificationOrchestrator.SendResolutionNotificationAsync(journey);

        var notificationSent = _notificationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(1);

        var smsSent = _smsClient.ReceivedCalls();
        smsSent.Should().HaveCount(1);

        capturedNotification.Id.Should().Be(journey.NotificationId);
        capturedNotification.NotificationSentBy.Should().Be(NotificationSentBy.Sms);
    }

    [Fact]
    public async Task NotificationOrchestrator_SendResolutionNotificationAsync_Fails()
    {
        var affectedStations = new List<Station>()
        {
            new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
            new(Guid.Parse("b38c1561-7a46-4a2c-a231-f07385f68cb9"), "Northwood"),
            new(Guid.Parse("cb591632-0af3-4682-a02c-b4c86e7729fa"), "Northwood Hills"),
            new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner")
        };

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5)),
                affectedStations);

        _notificationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Failure("This failed."));

        _smsClient.SendAsync(
         Arg.Any<string>(),
         Arg.Any<FormattedMessage>())
         .Returns(Result.Failure("This failed."));

        NotificationStatus capturedNotification = null!;

        _repository.CreateAsync(Arg.Do<NotificationStatus>(c => capturedNotification = c))
            .Returns(Task.CompletedTask);

        await _notificationOrchestrator.SendResolutionNotificationAsync(journey);

        capturedNotification.Id.Should().Be(journey.NotificationId);
        capturedNotification.NotificationSentBy.Should().Be(NotificationSentBy.Failed);
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

        var journey = new Journey(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Line(Guid.Parse("9e3a7f43-b6c4-4f12-9a72-ffbe2d15b9e6"), "Metropolitan"),
                new(Guid.Parse("6187fce5-a122-4899-832a-1d33c616da94"), "Moor Park"),
                new(Guid.Parse("b1e8fe87-98d5-4d8a-bb64-229aaa23b834"), "Pinner"),
                Severity.Severe,
                "+447400123456",
                PhoneOS.Android,
                TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-1)),
                affectedStations);

        _notificationClient.SendAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhoneOS>(),
            Arg.Any<Guid>(),
            Arg.Any<FormattedMessage>())
            .Returns(Result.Success());

        await _notificationOrchestrator.SendResolutionNotificationAsync(journey);

        var notificationSent = _notificationClient.ReceivedCalls();
        notificationSent.Should().HaveCount(0);
    }
}
