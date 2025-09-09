namespace Bermondsey.Options;
public class ServiceBusOptions
{
    public required QueueOptions Queues { get; set; }
}

public class QueueOptions
{
    public required string Notifications { get; set; }
}
