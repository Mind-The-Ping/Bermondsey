using Bermondsey.Models;
using Bermondsey.Options;
using Microsoft.Extensions.Options;


namespace Bermondsey.MessageTemplate;
public class MessageFormatter
{
    private readonly MessageTemplatesOptions _templates;

    public MessageFormatter(IOptions<MessageTemplatesOptions> options)
    {
        _templates = options.Value;
    }

    public FormattedMessage FormatDisruption(
        string line, 
        string origin, 
        string destination, 
        Severity severity, 
        IEnumerable<Station> stations)
    {
        var stationList = string.Join(", ", stations.Select(x => x.Name));

        if(severity == Severity.Minor || 
           severity == Severity.Severe)
        {
            return new FormattedMessage
            {
                Title = _templates.Delay.Title
               .Replace("{line}", line)
               .Replace("{origin}", origin)
               .Replace("{destination}", destination)
               .Replace("{severity}", severity.ToString()),

                Body = _templates.Delay.Body
               .Replace("{line}", line)
               .Replace("{origin}", origin)
               .Replace("{destination}", destination)
               .Replace("{severity}", severity.ToString())
               .Replace("{stations}", stationList)
            };
        }
        else
        {
            return new FormattedMessage
            {
                Title = _templates.Disruption.Title
               .Replace("{line}", line)
               .Replace("{origin}", origin)
               .Replace("{destination}", destination)
               .Replace("{severity}", severity.ToString()),

                Body = _templates.Disruption.Body
               .Replace("{line}", line)
               .Replace("{origin}", origin)
               .Replace("{destination}", destination)
               .Replace("{severity}", severity.ToString())
               .Replace("{stations}", stationList)
            };
        }
    }

    public FormattedMessage FormatResolved(string line, string origin, string destination)
    {
        return new FormattedMessage
        {
            Title = _templates.Resolved.Title
              .Replace("{line}", line)
              .Replace("{origin}", origin)
              .Replace("{destination}", destination),

            Body = _templates.Resolved.Body
              .Replace("{line}", line)
              .Replace("{origin}", origin)
              .Replace("{destination}", destination)
        };
    }
}
