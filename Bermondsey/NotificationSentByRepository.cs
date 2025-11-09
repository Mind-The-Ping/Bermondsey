using Bermondsey.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Bermondsey;
public class NotificationSentByRepository : INotificationSentByRepository
{
    private readonly IMongoCollection<NotificationStatus> _notificationCollection;

    public NotificationSentByRepository(IOptions<NotificationSentByOptions> options) 
    {
        var notificationOptions = options.Value ?? 
            throw new ArgumentNullException(nameof(options));

        var mongoClient = new MongoClient(
            notificationOptions.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
           notificationOptions.DatabaseName);

        _notificationCollection = mongoDatabase.GetCollection<NotificationStatus>(
            notificationOptions.NotificationSentByCollectionName);
    }

    public async Task CreateAsync(NotificationStatus status) =>
       await _notificationCollection.InsertOneAsync(status);
}
