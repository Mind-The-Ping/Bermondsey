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
            Disruption = "Your journey on the {line} line from {origin} to {destination} has a {severity} issue.",
            Resolved = "Your issue on the {line} from {origin} to {destination} is now resolved."
        });

        _messageFormatter = new MessageFormatter(options);
    }

    [Fact]
    public void MessageFormatter_DisruptionMesaage_Correct()
    {
        var line = "Central";
        var origin = "White City";
        var destination = "Queensway";
        var severity = Severity.Minor;

        var result = _messageFormatter.FormatDisruption(line, origin, destination, severity);
        result.Should().Be($"Your journey on the {line} line from {origin} to {destination} has a {severity} issue.");
    }

    [Fact]
    public void MessageFormatter_ResolvedMesaage_Correct()
    {
        var line = "Central";
        var origin = "White City";
        var destination = "Queensway";

        var result = _messageFormatter.FormatResolved(line, origin, destination);
        result.Should().Be($"Your issue on the {line} from {origin} to {destination} is now resolved.");
    }
}
