using Bermondsey.Models;
using Bermondsey.Options;
using Bermondsey.Repositories;
using FluentAssertions;
using StackExchange.Redis;
using System.Text.Json;
using Testcontainers.Redis;

namespace Bermondsey.Integration.Tests;

public class UserNotifiedRepositoryTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;

    public UserNotifiedRepositoryTests()
    {
        _redisContainer = new RedisBuilder()
          .WithImage("redis:7.2")
          .WithCleanUp(true)
          .Build();
    }

    public async Task InitializeAsync() =>
        await _redisContainer.StartAsync();


    public async Task DisposeAsync() =>
        await _redisContainer.DisposeAsync();


    [Fact]
    public async Task UserNotifiedRepository_SaveUsersAsync_Successfully()
    {
        var redis = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var database = redis.GetDatabase();

        var userNotifiedRepository = new UserNotifiedRepository(redis);

        var line = new Line(Guid.Parse("c7f7c41a-03d2-4a79-9e8e-b55b1b5a056e"), "Central");
        var startStation = new Station(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane");
        var endStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");

        var user = new User(
            Guid.NewGuid(), 
            Guid.NewGuid(), 
            line,
            startStation, 
            endStation, 
            Severity.Minor, 
            "+441234567890",
            TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30)));

        var users = new List<User>
        {
            user
        };

        var result = await userNotifiedRepository.SaveUsersAsync(users);
        result.IsSuccess.Should().BeTrue();

        var values = await database.SetMembersAsync($"notified_index:{user.DisruptionId}");
        values.Count().Should().Be(1);

        var recordResult = await database.StringGetAsync((RedisKey)values.First().ToString());
        var userRecord = JsonSerializer.Deserialize<User>(recordResult!);

        userRecord.Should().Be(user);
    }

    [Fact]
    public async Task UserNotifiedRepository_GetUsersByDisruptionIdAsync_Successfully()
    {
        var multiplexer = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var userNotifiedRepository = new UserNotifiedRepository(multiplexer);

        var line = new Line(Guid.Parse("c7f7c41a-03d2-4a79-9e8e-b55b1b5a056e"), "Central");
        var startStation = new Station(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane");
        var endStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");

        var disruptionId = Guid.NewGuid();

        var user1 = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890",
             TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30)));


        var user2 = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Severe, 
            "+441244562891",
             TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(45)));

        var users = new List<User>
        {
            user1, user2
        };

        await userNotifiedRepository.SaveUsersAsync(users);

        var result = await userNotifiedRepository.GetUsersByDisruptionIdAsync(disruptionId);

        result.Count().Should().Be(2);
        result.Should().BeEquivalentTo([user1, user2]);
    }

    [Fact]
    public async Task UserNotifiedRepository_DeleteByDisruptionIdAsync_Successfully()
    {
        var multiplexer = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var userNotifiedRepository = new UserNotifiedRepository(multiplexer);

        var disruptionId = Guid.NewGuid();

        var line = new Line(Guid.Parse("c7f7c41a-03d2-4a79-9e8e-b55b1b5a056e"), "Central");
        var startStation = new Station(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane");
        var endStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");

        var user1 = new User(
            Guid.NewGuid(),
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890", 
            new TimeOnly(5, 15));

        var user2 = new User(
            Guid.NewGuid(),
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Severe, 
            "+441244562891", 
            new TimeOnly(5, 15));

        var users = new List<User>
        {
            user1,
            user2
        };

        await userNotifiedRepository.SaveUsersAsync(users);
        await userNotifiedRepository.DeleteByDisruptionIdAsync(disruptionId);

        var result = await userNotifiedRepository.GetUsersByDisruptionIdAsync(disruptionId);
        result.Count().Should().Be(0);
    }

    [Fact]
    public async Task UserNotifiedRepository_Keys_ShouldExpire_AfterTTL()
    {
        var multiplexer = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var repo = new UserNotifiedRepository(multiplexer);

        var disruptionId = Guid.NewGuid();

        var line = new Line(Guid.Parse("c7f7c41a-03d2-4a79-9e8e-b55b1b5a056e"), "Central");
        var startStation = new Station(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane");
        var endStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");

        var user = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890", 
             TimeOnly.FromDateTime(DateTime.UtcNow.AddSeconds(1)));

        var users = new List<User>
        {
            user
        };

        await repo.SaveUsersAsync(users);
        await Task.Delay(70000);

        var result = await repo.GetUsersByDisruptionIdAsync(disruptionId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UserNotifiedRepository_DeleteByDisruptionIdAsync_ShouldNotFail_WhenNoUsers()
    {
        var multiplexer = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var repo = new UserNotifiedRepository(multiplexer);

        var disruptionId = Guid.NewGuid();

        await repo.DeleteByDisruptionIdAsync(disruptionId);

        var result = await repo.GetUsersByDisruptionIdAsync(disruptionId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UserNotifiedRepository_SaveUsersAsync_ShouldOverwrite_OnDuplicate()
    {
        var multiplexer = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var repo = new UserNotifiedRepository(multiplexer);

        var disruptionId = Guid.NewGuid();

        var line = new Line(Guid.Parse("c7f7c41a-03d2-4a79-9e8e-b55b1b5a056e"), "Central");
        var startStation = new Station(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane");
        var endStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");

        var user = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890",
            TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(10)));

        var users1 = new List<User>
        {
            user
        };
        await repo.SaveUsersAsync(users1);


        var updatedUser = user with { Severity = Severity.Severe };
        var users2 = new List<User>
        {
            updatedUser
        };
        await repo.SaveUsersAsync(users2);

        var result = await repo.GetUsersByDisruptionIdAsync(disruptionId);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(updatedUser);
    }
}
