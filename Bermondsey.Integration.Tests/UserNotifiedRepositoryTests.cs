using Bermondsey.Models;
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
        var affectedStations = new List<Station>()
        {
           new(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane"),
           new(Guid.Parse("299580df-c896-486f-898d-c51f4a0bd0d2"), "St. Pauls"),
           new(Guid.Parse("aaedc653-e766-4d6b-87e2-4c87322971ef"), "Bank"),
           new(Guid.Parse("db101bcd-350e-4485-b875-7ac2c8c1b6cc"), "Liverpool Street"),
           new(Guid.Parse("b7a5ae67-882b-4509-8df9-4bae2ef1dd2a"), "Bethnal Green"),
           new(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End")
        };

        var user = new User(
            Guid.NewGuid(), 
            Guid.NewGuid(), 
            line,
            startStation, 
            endStation, 
            Severity.Minor, 
            "+441234567890",
            TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30)),
            affectedStations);

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

        userRecord.Id.Should().Be(user.Id);
        userRecord.DisruptionId.Should().Be(user.DisruptionId);
        userRecord.Line.Should().Be(user.Line);
        userRecord.StartStation.Should().Be(user.StartStation);
        userRecord.EndStation.Should().Be(user.EndStation);
        userRecord.Severity.Should().Be(user.Severity);
        userRecord.PhoneNumber.Should().Be(user.PhoneNumber);
        userRecord.EndTime.Should().Be(user.EndTime);
        userRecord.AffectedStations.Should().BeEquivalentTo(user.AffectedStations);
    }

    [Fact]
    public async Task UserNotifiedRepository_GetUsersByDisruptionIdAsync_Successfully()
    {
        var multiplexer = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
        var userNotifiedRepository = new UserNotifiedRepository(multiplexer);

        var line = new Line(Guid.Parse("c7f7c41a-03d2-4a79-9e8e-b55b1b5a056e"), "Central");
        var startStation = new Station(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane");
        var endStation = new Station(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End");
        var affectedStations = new List<Station>()
        {
           new(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane"),
           new(Guid.Parse("299580df-c896-486f-898d-c51f4a0bd0d2"), "St. Pauls"),
           new(Guid.Parse("aaedc653-e766-4d6b-87e2-4c87322971ef"), "Bank"),
           new(Guid.Parse("db101bcd-350e-4485-b875-7ac2c8c1b6cc"), "Liverpool Street"),
           new(Guid.Parse("b7a5ae67-882b-4509-8df9-4bae2ef1dd2a"), "Bethnal Green"),
           new(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End")
        };

        var disruptionId = Guid.NewGuid();

        var user1 = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890",
             TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(30)),
             affectedStations);


        var user2 = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Severe, 
            "+441244562891",
             TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(45)),
             affectedStations);

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
        var affectedStations = new List<Station>()
        {
           new(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane"),
           new(Guid.Parse("299580df-c896-486f-898d-c51f4a0bd0d2"), "St. Pauls"),
           new(Guid.Parse("aaedc653-e766-4d6b-87e2-4c87322971ef"), "Bank"),
           new(Guid.Parse("db101bcd-350e-4485-b875-7ac2c8c1b6cc"), "Liverpool Street"),
           new(Guid.Parse("b7a5ae67-882b-4509-8df9-4bae2ef1dd2a"), "Bethnal Green"),
           new(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End")
        };

        var user1 = new User(
            Guid.NewGuid(),
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890", 
            new TimeOnly(5, 15),
            affectedStations);

        var user2 = new User(
            Guid.NewGuid(),
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Severe, 
            "+441244562891", 
            new TimeOnly(5, 15),
            affectedStations);

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
        var affectedStations = new List<Station>()
        {
           new(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane"),
           new(Guid.Parse("299580df-c896-486f-898d-c51f4a0bd0d2"), "St. Pauls"),
           new(Guid.Parse("aaedc653-e766-4d6b-87e2-4c87322971ef"), "Bank"),
           new(Guid.Parse("db101bcd-350e-4485-b875-7ac2c8c1b6cc"), "Liverpool Street"),
           new(Guid.Parse("b7a5ae67-882b-4509-8df9-4bae2ef1dd2a"), "Bethnal Green"),
           new(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End")
        };

        var user = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890", 
             TimeOnly.FromDateTime(DateTime.UtcNow.AddSeconds(1)), 
             affectedStations);

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
        var affectedStations = new List<Station>()
        {
           new(Guid.Parse("44e87f5b-015d-42f8-a250-232e226de45b"), "Chancery Lane"),
           new(Guid.Parse("299580df-c896-486f-898d-c51f4a0bd0d2"), "St. Pauls"),
           new(Guid.Parse("aaedc653-e766-4d6b-87e2-4c87322971ef"), "Bank"),
           new(Guid.Parse("db101bcd-350e-4485-b875-7ac2c8c1b6cc"), "Liverpool Street"),
           new(Guid.Parse("b7a5ae67-882b-4509-8df9-4bae2ef1dd2a"), "Bethnal Green"),
           new(Guid.Parse("73bce1de-143f-4903-928a-c34ceb3db42e"), "Mile End")
        };

        var user = new User(
            Guid.NewGuid(), 
            disruptionId,
            line,
            startStation,
            endStation,
            Severity.Minor, 
            "+441234567890",
            TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(10)), 
            affectedStations);

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
