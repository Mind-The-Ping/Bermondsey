using Bermondsey.Options;
using FluentAssertions;
using MongoDB.Driver;

namespace Bermondsey.Integration.Tests;
public class NotificationSentByRepositoryTests
{
    [Fact(Skip = "Skipping this test for now")]
    public async Task NotificationSentByRepository_CreateAsync_Successful()
    {
        var notificationOptions = new NotificationSentByOptions()
        {
            ConnectionString = "mongodb://testuser:testpassword@localhost:27017",
            DatabaseName = "testdb",
            NotificationSentByCollectionName = "Notifications"
        };

        var options = Microsoft.Extensions.Options.Options.Create(notificationOptions);
        var repository = new NotificationSentByRepository(options);

        var notificationStatus = new NotificationStatus(Guid.NewGuid(), Models.NotificationSentBy.Push);
        await repository.CreateAsync(notificationStatus);

        var mongoClient = new MongoClient(
            notificationOptions.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
           notificationOptions.DatabaseName);

        var notificationCollection = mongoDatabase.GetCollection<NotificationStatus>(
            notificationOptions.NotificationSentByCollectionName);

        var result = await notificationCollection.Find(x => x.Id == notificationStatus.Id).FirstAsync();

        result.Should().NotBeNull();
        result.Id.Should().Be(notificationStatus.Id);
        result.NotificationSentBy.Should().Be(notificationStatus.NotificationSentBy);
    }
}
