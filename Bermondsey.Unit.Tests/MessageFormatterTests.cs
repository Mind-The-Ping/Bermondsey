using Bermondsey.MessageTemplate;
using Bermondsey.Models;
using Bermondsey.Options;
using FluentAssertions;

namespace Bermondsey.Unit.Tests;

public class MessageFormatterTests
{
    private readonly MessageFormatter _messageFormatter;

    public MessageFormatterTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MessageTemplatesOptions
        {
            Delay = new TemplateSection()
            {
                Title = "Your journey from {origin} to {destination} has a {severity} delay.",
                Body = "Your journey on the {line} line from {origin} to {destination} has {severity} delay." +
                " The following stations are affected: {stations}."
            },
            Disruption = new TemplateSection()
            {
                Title = "Your journey from {origin} to {destination} has {severity} stations.",
                Body = "Your journey on the {line} line from {origin} to {destination} has {severity} stations." +
                " The following stations are affected: {stations}."
            },
            Resolved = new TemplateSection()
            {
                Title = "Your issue from {origin} to {destination} is now resolved.",
                Body = "Your issue on the {line} line from {origin} to {destination} is now resolved. Service is back to normal."
            }
        });

        _messageFormatter = new MessageFormatter(options);
    }

    [Theory]
    [InlineData(Severity.Minor)]
    [InlineData(Severity.Severe)]
    public void MessageFormatter_DelayMesaage_Correct(Severity severity)
    {
        var line = "Central";
        var origin = "White City";
        var destination = "Queensway";
        var stations = new List<Station>
        {
            new(Guid.Parse("dfe2f641-17ea-4ff6-bc45-d8fbc20ef057"), "White City"),
            new(Guid.Parse("4c4529fd-1b39-4a34-afb2-b0c815151012"), "Shepherd's Bush"),
            new(Guid.Parse("cd981628-5257-4fd1-a657-7168613eb50d"), "Notting Hill Gate")
        };

        var stationList = string.Join(", ", stations.Select(x => x.Name));

        var result = _messageFormatter.FormatDisruption(line, origin, destination, severity, stations);
        result.Title.Should().Be($"Your journey from {origin} to {destination} has a {severity} delay.");
        result.Body.Should().Be($"Your journey on the {line} line from {origin} to {destination} has {severity} delay." +
                $" The following stations are affected: {stationList}.");
    }

    [Theory]
    [InlineData(Severity.Closed)]
    [InlineData(Severity.Suspended)]
    public void MessageFormatter_DisruptionMesaage_Correct(Severity severity)
    {
        var line = "Central";
        var origin = "White City";
        var destination = "Queensway";
        var stations = new List<Station>
        {
            new(Guid.Parse("dfe2f641-17ea-4ff6-bc45-d8fbc20ef057"), "White City"),
            new(Guid.Parse("4c4529fd-1b39-4a34-afb2-b0c815151012"), "Shepherd's Bush"),
            new(Guid.Parse("cd981628-5257-4fd1-a657-7168613eb50d"), "Notting Hill Gate")
        };

        var stationList = string.Join(", ", stations.Select(x => x.Name));

        var result = _messageFormatter.FormatDisruption(line, origin, destination, severity, stations);
        result.Title.Should().Be($"Your journey from {origin} to {destination} has {severity} stations.");
        result.Body.Should().Be($"Your journey on the {line} line from {origin} to {destination} has {severity} stations." +
                $" The following stations are affected: {stationList}.");
    }

    [Fact]
    public void MessageFormatter_ResolvedMesaage_Correct()
    {
        var line = "Central";
        var origin = "White City";
        var destination = "Queensway";

        var result = _messageFormatter.FormatResolved(line, origin, destination);
        result.Title.Should().Be($"Your issue from {origin} to {destination} is now resolved.");
        result.Body.Should().Be($"Your issue on the {line} line from {origin} to {destination} is now resolved. Service is back to normal.");
    }
}
