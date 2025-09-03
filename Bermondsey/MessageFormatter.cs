using Bermondsey.Models;
using Bermondsey.Options;
using Microsoft.Extensions.Options;


namespace Bermondsey;
public class MessageFormatter
{
    private readonly MessageTemplatesOptions _templates;

    public MessageFormatter(IOptions<MessageTemplatesOptions> options)
    {
        _templates = options.Value;
    }

    public string FormatDisruption(string line, string origin, string destination, Severity severity)
    {
        return _templates.Disruption
            .Replace("{line}", line)
            .Replace("{origin}", origin)
            .Replace("{destination}", destination)
            .Replace("{severity}", severity.ToString());
    }

    public string FormatResolved(string line, string origin, string destination)
    {
        return _templates.Resolved
            .Replace("{line}", line)
            .Replace("{origin}", origin)
            .Replace("{destination}", destination);
    }
}
