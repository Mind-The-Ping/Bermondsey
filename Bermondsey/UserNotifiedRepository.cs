using Bermondsey.Models;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace Bermondsey;

public class UserNotifiedRepository
{
    private readonly IDatabase _database;

    public UserNotifiedRepository(IOptions<RedisOptions> options)
    {
        var redisOptions = options.Value ??
            throw new ArgumentNullException(nameof(options));

        var redis = ConnectionMultiplexer.Connect(redisOptions.Connection);
        _database = redis.GetDatabase();
    }

    private static string GetKey(User user) =>
       $"notified:{user.DisruptionId}:{user.Id}:{user.Severity}";

    public async Task<Result> SaveUsersAsync(IEnumerable<(User user, TimeOnly endTime)> users)
    {
        foreach (var (user, endTime) in users)
        {
            var key = GetKey(user);
            var value = JsonSerializer.Serialize(user);

            var now = DateTime.UtcNow;
            var todayEndTime = now.Date.Add(endTime.ToTimeSpan());
            var ttl = todayEndTime - now;

            var result = await _database.StringSetAsync(key, value, ttl);

            if(!result) {
                Result.Failure($"Failed to save user {user.Id} to database.");
            }

            result = await _database.SetAddAsync($"notified_index:{user.DisruptionId}", key);

            if (!result) {
                Result.Failure($"Failed to save user {user.Id} to database.");
            }
        }

        return Result.Success();
    }
}
