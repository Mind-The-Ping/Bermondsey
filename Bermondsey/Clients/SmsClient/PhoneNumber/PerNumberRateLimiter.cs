namespace Bermondsey.Clients.SmsClient.PhoneNumber;

public class PerNumberRateLimiter
{
    private readonly int _maxPerMinute;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, Queue<DateTime>> _sendTimes = new();
    private readonly object _lock = new();

    public PerNumberRateLimiter(IEnumerable<string> numbers, int maxPerMinute)
    {
        _maxPerMinute = maxPerMinute;

        foreach (var number in numbers)
        {
            _sendTimes[number] = new Queue<DateTime>();
        }
    }

    public bool TryAcquire(string number)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var queue = _sendTimes[number];

            while (queue.Count > 0 && now - queue.Peek() > _window) {
                queue.Dequeue();
            }

            if (queue.Count >= _maxPerMinute) {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
