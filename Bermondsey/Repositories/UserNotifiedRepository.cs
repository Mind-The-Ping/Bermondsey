using Bermondsey.Models;
using CSharpFunctionalExtensions;
using StackExchange.Redis;
using System.Text.Json;

namespace Bermondsey.Repositories;

public class UserNotifiedRepository : IUserNotifiedRepository
{
    private readonly IDatabase _database;

    public UserNotifiedRepository(ConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    private static string GetKey(User user) =>
       $"notified:{user.DisruptionId}:{user.Id}";

    public async Task<Result> SaveUsersAsync(IEnumerable<User> users)
    {
        var errors = new List<string>();
        var batch = _database.CreateBatch();

        var tasks = new List<Task>();

        foreach (var user in users)
        {
            var key = GetKey(user);
            var value = JsonSerializer.Serialize(user);

            var now = DateTime.UtcNow;
            var todayEndTime = now.Date.Add(user.EndTime.ToTimeSpan());
            var ttl = (todayEndTime - now);

            if (ttl <= TimeSpan.Zero)
            {
                errors.Add($"TTL already expired for user {user.Id}.");
                continue;
            }

            var stringSetTask = batch.StringSetAsync(key, value, ttl + TimeSpan.FromMinutes(1));
            var setAddTask = batch.SetAddAsync($"notified_index:{user.DisruptionId}", key);

            tasks.Add(stringSetTask.ContinueWith(t =>
            {
                if (!t.Result)
                    errors.Add($"Failed to save user {user.Id} to database.");
            }));

            tasks.Add(setAddTask.ContinueWith(t =>
            {
                if (!t.Result)
                    errors.Add($"Failed to index user {user.Id} for disruption {user.DisruptionId}.");
            }));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        return errors.Count != 0
            ? Result.Failure(string.Join("; ", errors))
            : Result.Success();
    }

    public async Task<IEnumerable<User>> GetUsersByDisruptionIdAsync(Guid disruptionId)
    {
        var results = new List<User>();

        var indexKey = $"notified_index:{disruptionId}";
        var keys = (await _database.SetMembersAsync(indexKey))
           .Select(x => (RedisKey)x.ToString())
           .ToArray();

        foreach (var key in keys)
        {
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

        if (keys.Length > 0)
        {
            var batch = _database.CreateBatch();
            var deleteTasks = new List<Task>();

            foreach (var key in keys) {
                deleteTasks.Add(batch.KeyDeleteAsync(key));
            }

            deleteTasks.Add(batch.KeyDeleteAsync(indexKey));

            batch.Execute();
            await Task.WhenAll(deleteTasks);
        }
        else {
            await _database.KeyDeleteAsync(indexKey);
        }
    }
}
