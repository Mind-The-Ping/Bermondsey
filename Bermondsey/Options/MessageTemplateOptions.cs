namespace Bermondsey.Options;
public class MessageTemplatesOptions
{
    public required TemplateSection Delay { get; set; }
    public required TemplateSection Disruption { get; set; }
    public required TemplateSection Resolved { get; set; }
}

public class TemplateSection
{
    public required string Title { get; set; }
    public required string Body { get; set; }
}

