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

    public async Task<IEnumerable<User>> GetUsersWithDifferentSeverityAsync(Guid disruptionId, Severity severity)
    {
        var results = new List<User>();

        var indexKey = $"notified_index:{disruptionId}";
        var keys = (await _database.SetMembersAsync(indexKey))
           .Select(x => (RedisKey)x.ToString())
           .ToArray();

        foreach (var key in keys)
        {
            var parts = key.ToString().Split(':');
            if (parts.Length < 4) {
                continue;
            }
       
            if (Enum.TryParse<Severity>(parts[^1], out var storedSeverity))
            {
                if (storedSeverity == severity) {
                    continue;
                }
            }

            var data = await _database.StringGetAsync(key);
            if (!data.IsNullOrEmpty)
            {
                var user = JsonSerializer.Deserialize<User>(data!);
                if (user is not null) {
                    results.Add(user);
                }
            }
        }

        return results;
    }

    public async Task DeleteByDisruptionIdAsync(Guid disruptionId)
    {
        var indexKey = $"notified_index:{disruptionId}";
        var keys = (await _database.SetMembersAsync(indexKey))
            .Select(x => (RedisKey)x.ToString())
            .ToArray();

        if (keys.Length != 0) {
            await _database.KeyDeleteAsync(keys);
        }

        await _database.KeyDeleteAsync(indexKey);
    }
}
