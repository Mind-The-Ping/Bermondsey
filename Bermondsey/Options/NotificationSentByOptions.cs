namespace Bermondsey.Options;
public class NotificationSentByOptions
{
    public string ConnectionString { get; set; } = null!;

    public string DatabaseName { get; set; } = null!;

    public string NotificationSentByCollectionName { get; set; } = null!;
}
