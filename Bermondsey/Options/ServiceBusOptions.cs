namespace Bermondsey.Options;
public class ServiceBusOptions
{
    public required string ConnectionString { get; set; }
    public required QueueOptions Queues { get; set; }
    public required TopicOptions Topics { get; set; }
}


public class QueueOptions
{
    public required string Disruptions { get; set; }
    public required string Notifications { get; set; }
}

public class TopicOptions
{
    public required TopicSubscription DisruptionEnds { get; set; }
}

public class TopicSubscription
{
    public required string Name { get; set; }
    public required string Subscription { get; set; }
}